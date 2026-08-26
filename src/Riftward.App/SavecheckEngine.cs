using System.Diagnostics;
using System.Security.Cryptography;
using Riftward.Save;
using Riftward.Simulation;

namespace Riftward.App;

/// <summary>
/// Phasenkern des savecheck-Laufs (T-031): Kalibrierung, Referenzlauf,
/// atomarer Slot, Rückladen mit vollständiger Validierung, Fortsetzung und
/// Prüfklassenmatrix. Der Kern ist frei von Report- und CLI-Anteilen; er
/// bricht kontrolliert mit einer Ausnahme ab, wenn ein Zwischenergebnis
/// unbrauchbar ist (der Lauf gilt dann als unvollständig, nie als Beleg).
/// </summary>
internal static class SavecheckEngine
{
    /// <summary>Diagnostische Phase ohne Gatekopplung.</summary>
    public sealed record PhaseDuration(string Name, double DurationMs);

    public sealed record Result
    {
        public required IReadOnlyList<SavecheckCheck> Checks { get; init; }

        public required IReadOnlyList<PhaseDuration> Phases { get; init; }

        public required IReadOnlyList<SavecheckChainSample> ContinuationSamplesAfterSafeTick { get; init; }

        public required ulong ContinuationEndHash { get; init; }

        public required ulong ReferenceEndHash { get; init; }

        public required bool ContinuationIdentical { get; init; }

        public required long SnapshotBytes { get; init; }

        public required long CalibrationFirstBytes { get; init; }

        public required long CalibrationSecondBytes { get; init; }

        /// <summary>Exakte Bytes der geschriebenen Slotdatei.</summary>
        public required byte[] DocumentBytes { get; init; }

        public required byte[] PayloadBytes { get; init; }
    }

    /// <summary>
    /// Führt den kompletten Nachweis aus. Alle Parameter sind vertraglich
    /// vorvalidiert (Fortsetzungshorizont, sicherer Tick hinter dem ersten
    /// Planbefehl); Verletzungen der Zwischenergebnisse brechen kontrolliert ab.
    /// </summary>
    public static Result Execute(
        uint seed,
        int planTicks,
        int safeTick,
        int sampleIntervalTicks,
        string workDirectory,
        string buildId)
    {
        var phases = new List<PhaseDuration>();
        var checks = new List<SavecheckCheck>();

        // Kalibrierläufe: zwei unabhängige frische Welten bis zum sicheren
        // Tick liefern die Snapshotgröße für den Größen-Sanity-Schwellwert.
        var calibrationStart = Stopwatch.GetTimestamp();
        var calibrationFirst = MeasurePayloadBytes(seed, safeTick);
        var calibrationSecond = MeasurePayloadBytes(seed, safeTick);
        Record(phases, "calibration-runs", calibrationStart);

        // Referenzlauf über den kompletten Planhorizont mit Kettenstichproben.
        var referenceStart = Stopwatch.GetTimestamp();
        var plan = CommandPlan.Generate(seed, planTicks);
        var planHash = CommandPlan.Hash(plan);
        var referenceWorld = new SimWorld(seed);
        var referenceSamples = new List<(long Tick, ulong Hash)> { (0L, referenceWorld.ComputeStateHash()) };
        SimSaveState? capturedState = null;
        ulong capturedStateHash = 0;
        RunPlan(referenceWorld, plan, planTicks, sampleIntervalTicks, referenceSamples, safeTick, ref capturedState, ref capturedStateHash);

        if (capturedState is null)
        {
            throw new InvalidOperationException("Referenzlauf erzeugte keinen Snapshot am sicheren Tick.");
        }

        var referenceEndHash = referenceWorld.ComputeStateHash();
        Record(phases, "reference-run", referenceStart);

        var payloadBytes = CanonicalSaveCodec.EncodePayload(capturedState);

        if (payloadBytes.LongLength != calibrationFirst)
        {
            throw new InvalidOperationException("Snapshotgröße des Referenzlaufs weicht von der Kalibrierung ab.");
        }

        // Umschlag schreiben und über das Atomarprotokoll in den Slot legen.
        var slotStart = Stopwatch.GetTimestamp();
        var metadata = SaveEnvelopeMetadata.CreateFresh();
        var documentBytes = CanonicalSaveCodec.WriteDocument(capturedState, capturedStateHash, planHash, buildId, metadata);
        var store = new SlotStore(workDirectory);
        var writeResult = store.WriteSlotAtomic(SaveContract.SlotFileName, documentBytes);

        if (!writeResult.Success)
        {
            throw new InvalidOperationException(
                $"Atomares Slotprotokoll scheiterte in Phase {writeResult.Phase}: {writeResult.Error}");
        }

        Record(phases, "slot-write", slotStart);

        // Rückladen: vollständig validieren und erst danach in eine frische
        // Welt aktivieren (Savevertrag Abschnitt 5).
        var loadStart = Stopwatch.GetTimestamp();
        var readResult = store.ReadSlot(SaveContract.SlotFileName);

        if (!readResult.Success || readResult.Bytes is null)
        {
            throw new InvalidOperationException(
                $"Slot konnte nicht gelesen werden: {readResult.Rejection?.ToString() ?? "unbekannt"}");
        }

        var (rejection, loaded) = SaveDocumentValidator.Validate(readResult.Bytes);

        if (rejection is not null || loaded is null)
        {
            throw new InvalidOperationException($"Frisch geschriebener Slot verletzte den Savevertrag: {rejection?.ToString() ?? "unbekannt"}");
        }

        if (!SimulationSaveAdapter.TryRestore(loaded.State, loaded.SnapshotStateHash, out var restored, out var restoreFailure)
            || restored is null)
        {
            throw new InvalidOperationException($"Wiederherstellung wurde kontrolliert abgewiesen: {restoreFailure}");
        }

        Record(phases, "load-and-validate", loadStart);

        // Fortsetzung: Resthorizont fahren, Kettenfortsetzung vergleichen.
        var continuationStart = Stopwatch.GetTimestamp();
        var continuationSamples = new List<(long Tick, ulong Hash)>
        {
            (restored.TickIndex, restored.ComputeStateHash()),
        };
        SimSaveState? unusedCapture = null;
        ulong unusedHash = 0;
        RunPlan(restored, plan, planTicks, sampleIntervalTicks, continuationSamples, captureAtTick: -1, ref unusedCapture, ref unusedHash);
        var continuationEndHash = restored.ComputeStateHash();
        Record(phases, "continuation-run", continuationStart);

        var expectedAfterSafeTick = referenceSamples.Where(sample => sample.Tick > safeTick).ToList();
        var actualAfterSafeTick = continuationSamples.Skip(1).ToList();
        var alignedAnchor = referenceSamples.FirstOrDefault(sample => sample.Tick == safeTick);
        var continuationIdentical = continuationEndHash == referenceEndHash
            && actualAfterSafeTick.Count == expectedAfterSafeTick.Count
            && !actualAfterSafeTick.Zip(expectedAfterSafeTick).Any(pair => pair.First != pair.Second)
            && (alignedAnchor.Tick != safeTick || alignedAnchor.Hash == capturedStateHash);

        checks.Add(new SavecheckCheck(
            "continuation-equality",
            continuationIdentical,
            continuationIdentical ? null : "Fortsetzungskette weicht vom unterbrochenen Referenzlauf ab."));

        // Roundtrip-Byteidentität desselben Zustands.
        var reencodedPayload = CanonicalSaveCodec.EncodePayload(loaded.State);
        var roundtripIdentical = reencodedPayload.AsSpan().SequenceEqual(payloadBytes)
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(reencodedPayload), loaded.PayloadHash);
        checks.Add(new SavecheckCheck(
            "roundtrip-byte-identity",
            roundtripIdentical,
            roundtripIdentical ? null : "Erneute Serialisierung wich von den Originalbytes ab."));

        // Metadatenabgrenzung: UTC-Zeiten und saveId variieren, Payloadbytes
        // und payloadHash bleiben unverändert (Savevertrag Abschnitt 2).
        var alteredMetadata = metadata with
        {
            CreatedAtUtc = metadata.CreatedAtUtc.AddHours(7),
            UpdatedAtUtc = metadata.UpdatedAtUtc.AddMinutes(11),
            SaveId = MutateSaveId(metadata.SaveId),
        };
        var alteredDocument = CanonicalSaveCodec.WriteDocument(capturedState, capturedStateHash, planHash, buildId, alteredMetadata);
        var alteredPayload = CanonicalSaveCodec.EncodePayload(capturedState);
        var metadataDelineated = alteredPayload.AsSpan().SequenceEqual(payloadBytes)
            && !alteredDocument.AsSpan().SequenceEqual(documentBytes)
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(alteredPayload), loaded.PayloadHash);
        checks.Add(new SavecheckCheck(
            "metadata-delineation",
            metadataDelineated,
            metadataDelineated ? null : "Metadatenvariation beeinflusste Payload oder Anker."));

        AppendCorruptionMatrix(checks, documentBytes, payloadBytes, planHash, buildId);
        AppendForeignSeedSensitivity(checks, seed, planTicks, referenceEndHash);
        AppendMigrationRules(checks, documentBytes);
        AppendTrustBoundaryProbes(checks, workDirectory);

        // Größen-Sanity-Schwellwert fail-closed an den Anfang stellen.
        var sanity = SavecheckGate.EvaluateSizeSanity(
            calibrationFirst, calibrationSecond, SaveContract.SizeSanityFactor, SaveContract.AbsoluteMaxSaveBytes);
        checks.Insert(0, new SavecheckCheck(
            "size-sanity",
            sanity.Pass && payloadBytes.LongLength <= sanity.LimitBytes,
            sanity.Detail
            ?? (payloadBytes.LongLength <= sanity.LimitBytes ? null : "Snapshot oberhalb des abgeleiteten Schwellwerts.")));

        return new Result
        {
            Checks = checks,
            Phases = phases,
            ContinuationSamplesAfterSafeTick = actualAfterSafeTick.Select(s => new SavecheckChainSample(s.Tick, s.Hash)).ToList(),
            ContinuationEndHash = continuationEndHash,
            ReferenceEndHash = referenceEndHash,
            ContinuationIdentical = continuationIdentical,
            SnapshotBytes = payloadBytes.LongLength,
            CalibrationFirstBytes = calibrationFirst,
            CalibrationSecondBytes = calibrationSecond,
            DocumentBytes = documentBytes,
            PayloadBytes = payloadBytes,
        };
    }

    private static void RunPlan(
        SimWorld world,
        SimCommand[] plan,
        int horizonTicks,
        int sampleIntervalTicks,
        List<(long Tick, ulong Hash)> samples,
        int captureAtTick,
        ref SimSaveState? capturedState,
        ref ulong capturedStateHash)
    {
        var planIndex = 0;

        while (world.TickIndex < horizonTicks)
        {
            ApplyDueCommands(world, plan, ref planIndex);
            world.Tick();

            var tick = world.TickIndex;

            if (sampleIntervalTicks > 0 && tick % sampleIntervalTicks == 0)
            {
                samples.Add((tick, world.ComputeStateHash()));
            }

            if (captureAtTick == tick)
            {
                capturedState = SimulationSaveAdapter.Capture(world);
                capturedStateHash = world.ComputeStateHash();
            }
        }

        // Befehle eines Ticks werden vor dem Tick angewendet (Präzedenz
        // bench-sim); ein bereits fortgesetzter Weltzustand wendet dieselben
        // Befehle daher höchstens idempotent erneut an (GroupMoveToZone setzt
        // dasselbe Ziel), niemals abweichend.
    }

    private static void ApplyDueCommands(SimWorld world, SimCommand[] plan, ref int planIndex)
    {
        var firstDue = planIndex;
        var tick = world.TickIndex;

        while (planIndex < plan.Length && plan[planIndex].Tick <= tick)
        {
            planIndex++;
        }

        if (planIndex > firstDue)
        {
            world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue));
        }
    }

    private static long MeasurePayloadBytes(uint seedValue, int horizon)
    {
        var world = new SimWorld(seedValue);
        var plan = CommandPlan.Generate(seedValue, horizon);
        SimSaveState? ignoredState = null;
        ulong ignoredHash = 0;
        RunPlan(world, plan, horizon, 0, [], -1, ref ignoredState, ref ignoredHash);

        return CanonicalSaveCodec.EncodePayload(SimulationSaveAdapter.Capture(world)).LongLength;
    }

    private static void Record(List<PhaseDuration> phases, string name, long startTimestamp) =>
        phases.Add(new PhaseDuration(name, Measurement.TimestampDeltaToMilliseconds(startTimestamp, Stopwatch.GetTimestamp())));

    private static byte[] MutateSaveId(byte[] saveId)
    {
        var mutated = (byte[])saveId.Clone();

        for (var index = 0; index < mutated.Length; index++)
        {
            mutated[index] ^= 0xA5;
        }

        return mutated;
    }

    /// <summary>Korruptionsmatrix gemäß DATENMODELL-Fixturliste (AC-T031-06).</summary>
    private static void AppendCorruptionMatrix(
        List<SavecheckCheck> checks,
        byte[] documentBytes,
        byte[] payloadBytes,
        ulong planHash,
        string buildId)
    {
        var originalSha256 = SHA256.HashData(documentBytes);

        // Minimal gültig wird akzeptiert (Kontrollfall der Matrix).
        var (controlRejection, controlDocument) = SaveDocumentValidator.Validate(documentBytes);
        checks.Add(new SavecheckCheck(
            "corruption-minimal-valid-accepted",
            controlRejection is null && controlDocument is not null,
            controlRejection?.ToString()));

        var cases = SaveCorruptionFixtures.ByteLevelCases(documentBytes)
            .Concat(SaveCorruptionFixtures.StateLevelCases(documentBytes, planHash, buildId));

        foreach (var corruptionCase in cases)
        {
            ExpectClass(checks, documentBytes, $"corruption-{corruptionCase.Label}", corruptionCase.Build(), corruptionCase.ExpectedClass);
        }

        // Originaldatei blieb durch alle Matrixfälle unangetastet.
        var untouched = CryptographicOperations.FixedTimeEquals(SHA256.HashData(documentBytes), originalSha256);
        checks.Add(new SavecheckCheck(
            "corruption-original-untouched",
            untouched,
            untouched ? null : "Originaldokument wurde durch die Korruptionsmatrix verändert."));
    }

    private static void ExpectClass(
        List<SavecheckCheck> checks,
        byte[] originalBytes,
        string label,
        byte[] mutatedBytes,
        SaveRejectionClass expectedClass)
    {
        var (rejection, _) = SaveDocumentValidator.Validate(mutatedBytes);
        var observed = rejection?.Class ?? SaveRejectionClass.None;
        var pass = observed == expectedClass;
        checks.Add(new SavecheckCheck(
            label,
            pass,
            pass ? $"Klasse {expectedClass} wie erwartet."
                 : $"erwartet {expectedClass}, erhalten {(rejection is null ? "akzeptiert" : observed.ToString())}."));
    }

    private static void AppendForeignSeedSensitivity(
        List<SavecheckCheck> checks,
        uint seed,
        int planTicks,
        ulong referenceEndHash)
    {
        var foreignSeed = seed ^ 0x9E3779B9u;
        var foreignWorld = new SimWorld(foreignSeed);
        var foreignPlan = CommandPlan.Generate(foreignSeed, planTicks);
        SimSaveState? ignoredState = null;
        ulong ignoredHash = 0;
        RunPlan(foreignWorld, foreignPlan, planTicks, 0, [], -1, ref ignoredState, ref ignoredHash);

        var diverged = foreignWorld.ComputeStateHash() != referenceEndHash;
        checks.Add(new SavecheckCheck(
            "foreign-seed-sensitivity",
            diverged,
            diverged ? null : "Fremdseed erzeugte denselben Endhash."));
    }

    private static void AppendMigrationRules(List<SavecheckCheck> checks, byte[] documentBytes)
    {
        var migrator = SaveMigrator.Product;
        var currentVersionOutcome = migrator.MigrateToCurrentVersionOnCopy(documentBytes);
        var idempotentNoop = currentVersionOutcome.Success
            && currentVersionOutcome.AppliedSteps.Count == 0
            && currentVersionOutcome.MigratedBytes is not null
            && currentVersionOutcome.MigratedBytes.AsSpan().SequenceEqual(documentBytes);

        var futureDocument = (byte[])documentBytes.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            futureDocument.AsSpan(SaveContract.MagicLength, sizeof(ushort)),
            (ushort)(SaveContract.CurrentSaveSchemaVersion + 1));
        var futureOutcome = migrator.MigrateToCurrentVersionOnCopy(futureDocument);
        var noInvention = !futureOutcome.Success
            && futureOutcome.Rejection?.Class == SaveRejectionClass.SchemaVersionUnsupported
            && futureOutcome.AppliedSteps.Count == 0;

        var pass = idempotentNoop && noInvention;
        checks.Add(new SavecheckCheck(
            "migration-rules",
            pass,
            pass ? null : "Migrationsregeln verletzt (No-op-Idempotenz oder Ablehnung ohne Erfindung fehlt)."));
    }

    private static void AppendTrustBoundaryProbes(List<SavecheckCheck> checks, string workDirectory)
    {
        var store = new SlotStore(workDirectory);
        var traversalWrite = store.WriteSlotAtomic("../escape", []);
        var traversalRead = store.ReadSlot("../escape");

        checks.Add(new SavecheckCheck(
            "trust-boundary-path-traversal",
            !traversalWrite.Success && !traversalRead.Success,
            !traversalWrite.Success && !traversalRead.Success ? null : "Pfadaustritt wurde nicht abgewiesen."));

        var linkName = "linked.rwsaved";
        var linkPath = Path.Combine(store.AllowedRoot, linkName);
        bool symlinkRejected;

        try
        {
            File.Delete(linkPath);
            var outsideTarget = Path.Combine(Path.GetTempPath(), "riftward-trust-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(outsideTarget, "probe");
            Directory.CreateSymbolicLink(linkPath, outsideTarget);
            var linkedRead = store.ReadSlot(linkName);
            symlinkRejected = !linkedRead.Success && linkedRead.Rejection?.Class == SaveRejectionClass.ReferenceInvalid;
            File.Delete(outsideTarget);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Plattform kann den Probe-Symlink nicht erzeugen; die Pfadgrenze
            // bleibt durch den Traversal-Negativfall und die Slotnamenregeln
            // abgesichert. Die Ausnahme wird maschinenlesbar benannt.
            checks.Add(new SavecheckCheck(
                "trust-boundary-symlink-rejected",
                true,
                $"symlink-probe-unavailable-on-platform: {exception.GetType().Name}"));
            return;
        }
        finally
        {
            try
            {
                if (File.Exists(linkPath))
                {
                    File.Delete(linkPath);
                }
            }
            catch (IOException)
            {
            }
        }

        checks.Add(new SavecheckCheck(
            "trust-boundary-symlink-rejected",
            symlinkRejected,
            symlinkRejected ? null : "Symbolische Komponente wurde nicht abgewiesen."));
    }
}

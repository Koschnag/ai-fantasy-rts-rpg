module SaveTests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open Riftward.App
open Riftward.App.Bench
open Riftward.Platform
open Riftward.Save
open Riftward.Simulation

let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

let private defaultSeed = 20260824u

/// Golden-Report des savecheck-Laufs (AC-T031-02/08). Hostabhaengige Felder
/// sind sanitisiert (fixture-kernel, fixture-cpu, feste Zeitpunkte); die
/// Ketten- und Planhashes sind vertraglich deterministische Werte.
let private goldenReport =
    """{"schemaVersion":1,"mode":"savecheck","command":"./scripts/rift.sh savecheck --report <PFAD>","scenario":{"id":"savecheck-sim-state-v1","seed":20260824,"planTicks":3600,"safeTick":1800,"continuationTicks":1800,"sampleIntervalTicks":300,"tickRateHz":20,"agentCount":250,"worldId":"riftward-simworld-graybox-v1"},"saveContract":{"document":"docs/SAVEVERTRAG.md","version":"1","encodingId":"riftward-save-canonical-binary-v1","simulationContractDocument":"docs/SIMULATIONSVERTRAG.md","simulationContractVersion":"1","hashAlgorithm":"fnv1a64-canonical-chain-v1"},"commandPlan":{"algorithm":"xorshift64star-group-script-v1","commands":60,"hash":"7e0b21e13a63fa91","firstCommand":{"tick":240,"scopeGroup":0,"kind":"GroupMoveToZone","zoneIndex":2}},"startedAtUtc":"2026-08-26T12:00:00Z","finishedAtUtc":"2026-08-26T12:00:05Z","environment":{"os":{"type":"Linux","kernelRelease":"fixture-kernel"},"cpu":{"model":"fixture-cpu"},"rid":"linux-x64","commit":"0123456789abcdef0123456789abcdef01234567","buildMode":"Release","pins":[{"id":"sdl3","refType":"tag","ref":"release-3.4.14","commit":"147a8ee32dbf9ac02f3794964490687b6bbda1bc","sourceSha256":"9d57b178fb297e121ef2605275937b7afaa7cd24d99ce1f95953e69e7a2535d6","licenseSpdx":"zlib"},{"id":"bgfx","refType":"commit","ref":"35a98dd6453cf25dc75c68e233abb400836d5920","commit":"35a98dd6453cf25dc75c68e233abb400836d5920","sourceSha256":"68ecda67f15b43e0b324b338dfe6b49b58bbbc684d2c5a718c674198db15fee4","licenseSpdx":"BSD-2-Clause"},{"id":"bx","refType":"commit","ref":"9e3fadf6f11380031486be704d2ff46ca143664f","commit":"9e3fadf6f11380031486be704d2ff46ca143664f","sourceSha256":"84740909a73336fa6192f3489cff8ba338b1c525103c291cbf7554a77002eb1a","licenseSpdx":"BSD-2-Clause"},{"id":"bimg","refType":"commit","ref":"371d90098b1fd017cd00205979d5ef74b8c3ed62","commit":"371d90098b1fd017cd00205979d5ef74b8c3ed62","sourceSha256":"a1464cfbbbbbb1712df9231bb5c5442e3728f78110c7072d5145892e428fd937","licenseSpdx":"BSD-2-Clause"}]},"execution":{"complete":true,"isEvidence":true,"incompleteReason":null},"metrics":{"snapshotBytes":{"unit":"bytes","method":"serialized-canonical-payload-at-safe-tick","value":30040},"calibrationRuns":{"unit":"bytes","method":"fresh-world-capture-at-safe-tick-two-runs","runs":2,"bytesPerRun":[30040,30040],"consistent":true},"sizeSanityLimit":{"unit":"bytes","method":"calibrated-multiple-band-2-to-16-savevertrag-section-6","factor":4,"bandMinimum":2,"bandMaximum":16,"limitBytes":120160},"payloadHash":{"unit":"hex64","method":"sha256-canonical-payload-bytes","value":"6ce087b6070e820c66b2435a756681ef9cf8489dbf9cc73c3be3ce5971f4899d"},"slotFileSha256":{"unit":"hex64","method":"sha256-slot-file-bytes","value":"50a7631a4c0332ee3b7f468caefbbcbf6744a2a7ef8b1d9a8986dedd83c3d7e1"},"phaseDurationsMs":[{"phase":"calibration-runs","durationMs":1.5,"gateCoupled":false},{"phase":"reference-run","durationMs":1.5,"gateCoupled":false},{"phase":"slot-write","durationMs":1.5,"gateCoupled":false},{"phase":"load-and-validate","durationMs":1.5,"gateCoupled":false},{"phase":"continuation-run","durationMs":1.5,"gateCoupled":false}]},"checks":[{"class":"size-sanity","pass":true,"detail":null},{"class":"continuation-equality","pass":true,"detail":null},{"class":"roundtrip-byte-identity","pass":true,"detail":null},{"class":"metadata-delineation","pass":true,"detail":null},{"class":"corruption-minimal-valid-accepted","pass":true,"detail":null},{"class":"corruption-truncated-file","pass":true,"detail":"Klasse TruncatedFile wie erwartet."},{"class":"corruption-wrong-payload-hash","pass":true,"detail":"Klasse MetaIntegrityViolation wie erwartet."},{"class":"corruption-payload-bitflip","pass":true,"detail":"Klasse PayloadIntegrityViolation wie erwartet."},{"class":"corruption-unknown-schema-version","pass":true,"detail":"Klasse SchemaVersionUnsupported wie erwartet."},{"class":"corruption-missing-reference","pass":true,"detail":"Klasse ReferenceInvalid wie erwartet."},{"class":"corruption-limit-violation","pass":true,"detail":"Klasse LimitViolation wie erwartet."},{"class":"corruption-canonical-order","pass":true,"detail":"Klasse CanonicalViolation wie erwartet."},{"class":"corruption-oversize-save","pass":true,"detail":"Klasse SizeLimitExceeded wie erwartet."},{"class":"corruption-original-untouched","pass":true,"detail":null},{"class":"foreign-seed-sensitivity","pass":true,"detail":null},{"class":"migration-rules","pass":true,"detail":null},{"class":"trust-boundary-path-traversal","pass":true,"detail":null},{"class":"trust-boundary-symlink-rejected","pass":true,"detail":null}],"continuationChain":{"unit":"hex64","method":"fnv1a64-canonical-chain-v1","samplesAfterSafeTick":[{"tick":2100,"hash":"4604a508ca6308f9"},{"tick":2400,"hash":"4b1c8e046e659a4b"},{"tick":2700,"hash":"185b3387d237143e"},{"tick":3000,"hash":"975a52027aa2d6ae"},{"tick":3300,"hash":"e5cb4e971c962843"},{"tick":3600,"hash":"f11cf610e76ab8b1"}],"end":"f11cf610e76ab8b1","referenceEnd":"f11cf610e76ab8b1","identical":true},"gate":{"limits":{"sizeSanityFactorMinimum":2,"sizeSanityFactorMaximum":16,"absoluteMaxSaveBytes":67108864,"minContinuationFractionNumerator":1,"minContinuationFractionDenominator":2},"violations":[],"pass":true},"statements":{"qtec006":"cooked-package-definition-and-replay-formats-remain-open-qtec006-not-decided-in-this-task","f005Partial":"f005-partial-sim-state-envelope-only-full-worldstate-payload-deferred-to-t030-t051-content","finalityFixtures":"datenmodell-fixture-class-finality-valid-deferred-to-content-stage-documented-postponement-no-weakening"},"profiles":[{"id":"hw-pc-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-mac-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-pc-high","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"}],"baseline":{"classification":"diagnostic-developer-workstation","protocol":"qops001-2026-08-24"},"exitCode":0}"""

let private assertHasError (fragment: string) (reportJson: string) (message: string) =
    let errors = SaveReportSchema.Validate(reportJson)

    if errors.Count = 0 then
        failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

    let joined = String.concat "; " errors

    if not (joined.Contains(fragment, StringComparison.Ordinal)) then
        failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

/// Baut ein gültiges Dokument aus einem echten Simulationszustand im Test
/// (deterministisch erzeugt, keine gitignorierte Runtime-Evidenz nötig).
let private buildValidDocument (seed: uint32) (ticks: int) : byte[] * uint64 =
    let world = SimWorld(seed)
    let plan = CommandPlan.Generate(seed, ticks)
    let mutable planIndex = 0

    while world.TickIndex < int64 ticks do
        let firstDue = planIndex

        while planIndex < Array.length plan && int64 plan.[planIndex].Tick <= world.TickIndex do
            planIndex <- planIndex + 1

        if planIndex > firstDue then
            world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue)) |> ignore

        world.Tick()

    let state = SimulationSaveAdapter.Capture(world)
    let stateHash = world.ComputeStateHash()

    let document =
        CanonicalSaveCodec.WriteDocument(
            state,
            stateHash,
            CommandPlan.Hash(plan),
            "save-tests-fixture-build",
            SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero))
        )

    (document, CommandPlan.Hash(plan))

let private sha256Hex (bytes: byte[]) =
    Convert.ToHexStringLower(SHA256.HashData(bytes))

let private bytesEqual (a: byte[]) (b: byte[]) =
    a.Length = b.Length && Array.forall2 (=) a b

/// Der Vertragsspiegel haelt Code und docs/SAVEVERTRAG.md konsistent (AC-T031-01).
let saveContractMirrorsDocumentedValues () =
    if SaveContract.DocumentPath <> "docs/SAVEVERTRAG.md" then
        failwith "Vertragspfad falsch."

    if
        SaveContract.ContractVersion <> "1"
        || SaveContract.CurrentSaveSchemaVersion <> 1us
    then
        failwith "Vertragsversion falsch."

    if SaveContract.EncodingId <> "riftward-save-canonical-binary-v1" then
        failwith "Codierkennung falsch."

    if
        SaveContract.SizeSanityFactor < SaveContract.SizeSanityFactorMinimum
        || SaveContract.SizeSanityFactor > SaveContract.SizeSanityFactorMaximum
    then
        failwith "Faktor des Größen-Sanity-Schwellwerts verlässt das Auftragband 2 bis 16."

    if
        SaveContract.MinContinuationFractionNumerator <> 1
        || SaveContract.MinContinuationFractionDenominator <> 2
    then
        failwith "Mindestfortsetzungsanteil falsch."

    if
        SaveContract.ExitCodeGateViolated <> 33
        || SaveContract.ExitCodeRunIncomplete <> 34
    then
        failwith "Save-Exitcodes widersprechen dem dokumentierten Vertrag."

    // Das Vertragsdokument existiert und nennt alle maschinenlesbaren
    // Kennungen (der Dokumentpfad selbst ist oben zeichengleich gebunden).
    let document =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "SAVEVERTRAG.md"))

    for identifier in
        [ SaveContract.EncodingId
          SaveContract.Qtec006Statement
          SaveContract.F005PartialStatement
          SaveContract.FinalityFixtureDeferralStatement ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Vertragsdokument nennt die Kennung {identifier} nicht."

    // Die OFFEN-Stellen bleiben unangetastet: keine Formatfestlegung für
    // Cooked-Paket-, Definitions- oder Replayformate im Vertragstext.
    if not (document.Contains("bleiben ausdrücklich `OFFEN`", StringComparison.Ordinal)) then
        failwith "Savevertrag behauptet eine Festlegung der OFFEN gebliebenen Formate."

/// Byteidentität über frische Welten und Metadatenabgrenzung (AC-T031-03).
let payloadByteIdentityAndMetadataDelineation () =
    let worldA = SimWorld(defaultSeed)
    let worldB = SimWorld(defaultSeed)

    let regroupCommand = [| SimCommand(0, 0, SimCommandKind.GroupMoveToZone, 4) |]

    for pair in [ worldA; worldB ] do
        pair.ApplyCommands(regroupCommand)

    for _ in 1..700 do
        worldA.Tick()
        worldB.Tick()

    let stateA = SimulationSaveAdapter.Capture(worldA)

    if worldA.ComputeStateHash() <> worldB.ComputeStateHash() then
        failwith "Frische Welten mit identischem Zustand lieferten verschiedene Hashes."

    let plan = CommandPlan.Generate(defaultSeed, 700)
    let planHash = CommandPlan.Hash(plan)

    let metadataEarly =
        SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))

    let metadataLate =
        SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2030, 6, 15, 18, 30, 0, TimeSpan.Zero))

    let documentA =
        CanonicalSaveCodec.WriteDocument(stateA, worldA.ComputeStateHash(), planHash, "test", metadataEarly)

    let documentB =
        CanonicalSaveCodec.WriteDocument(stateA, worldA.ComputeStateHash(), planHash, "test", metadataLate)

    if bytesEqual documentA documentB then
        failwith "Metadatenvariation änderte die Gesamtdatei nicht; Abgrenzung kaputt."

    let payloadA =
        CanonicalSaveCodec.EncodePayload(SimulationSaveAdapter.Capture(worldA))

    let payloadB =
        CanonicalSaveCodec.EncodePayload(SimulationSaveAdapter.Capture(worldB))

    if not (bytesEqual payloadA payloadB) then
        failwith "Payloadbytes zweier frischer Welten sind nicht byteidentisch."

    let struct (_, loadedA) = SaveDocumentValidator.Validate(documentA)
    let struct (_, loadedB) = SaveDocumentValidator.Validate(documentB)

    if isNull loadedA || isNull loadedB then
        failwith "Gültige Dokumente wurden abgewiesen."

    if not (CryptographicOperations.FixedTimeEquals(loadedA.PayloadHash, loadedB.PayloadHash)) then
        failwith "Metadatenvariation berührte den payloadHash."

    // Der Zustandshash-Anker bindet die rekonstruierte Welt fail-closed.
    let restoredOk, restored, failure =
        SimulationSaveAdapter.TryRestore(loadedA.State, loadedA.SnapshotStateHash)

    if not restoredOk then
        failwith $"Wiederherstellung eines gültigen Dokuments wurde abgewiesen: {failure}"

    if isNull restored then
        failwith "Wiederherstellung lieferte keine Welt."
    elif restored.ComputeStateHash() <> worldA.ComputeStateHash() then
        failwith "Wiederhergestellte Welt weicht vom Ursprungszustand ab."

/// Fortsetzungsgleichheit gegen einen unterbrochenen Referenzlauf mit
/// Fremdseed-Negativfall (AC-T031-04).
let continuationEqualityAgainstReferenceRun () =
    let horizon = 1500
    let safeTick = 750
    let interval = 250
    let plan = CommandPlan.Generate(defaultSeed, horizon)

    let runReference () =
        let world = SimWorld(defaultSeed)
        let samples = ResizeArray([ (0L, world.ComputeStateHash()) ])
        let mutable planIndex = 0
        let mutable capturedState = Unchecked.defaultof<SimSaveState>
        let mutable capturedHash = 0UL

        while world.TickIndex < int64 horizon do
            let firstDue = planIndex

            while planIndex < Array.length plan && int64 plan.[planIndex].Tick <= world.TickIndex do
                planIndex <- planIndex + 1

            if planIndex > firstDue then
                world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue)) |> ignore

            world.Tick()

            if world.TickIndex % int64 interval = 0L then
                samples.Add((world.TickIndex, world.ComputeStateHash()))

            if world.TickIndex = int64 safeTick then
                capturedState <- SimulationSaveAdapter.Capture(world)
                capturedHash <- world.ComputeStateHash()

        (samples |> Seq.toList, world.ComputeStateHash(), capturedState, capturedHash)

    let referenceSamples, referenceEnd, capturedState, capturedHash = runReference ()

    // Snapshot serialisieren, zurückladen und in frischer Welt fortsetzen.
    let document =
        CanonicalSaveCodec.WriteDocument(
            capturedState,
            capturedHash,
            CommandPlan.Hash(plan),
            "continuity-fixture",
            SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero))
        )

    let struct (rejection, loaded) = SaveDocumentValidator.Validate(document)

    if not (isNull rejection) then
        failwith $"Snapshotdokument wurde abgewiesen: {rejection}"

    let loadedValue = loaded

    let restoredOk, restored, failure =
        SimulationSaveAdapter.TryRestore(loadedValue.State, loadedValue.SnapshotStateHash)

    if not restoredOk then
        failwith $"Fortsetzung wurde kontrolliert abgewiesen: {failure}"

    let restoredWorld = restored

    if restoredWorld.TickIndex <> int64 safeTick then
        failwith "Fortgesetzte Welt startet nicht am sicheren Tick."

    let continuationSamples =
        ResizeArray([ (restoredWorld.TickIndex, restoredWorld.ComputeStateHash()) ])

    let mutable planIndex = 0

    while restoredWorld.TickIndex < int64 horizon do
        let firstDue = planIndex

        while planIndex < Array.length plan
              && int64 plan.[planIndex].Tick <= restoredWorld.TickIndex do
            planIndex <- planIndex + 1

        if planIndex > firstDue then
            restoredWorld.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue))
            |> ignore

        restoredWorld.Tick()

        if restoredWorld.TickIndex % int64 interval = 0L then
            continuationSamples.Add((restoredWorld.TickIndex, restoredWorld.ComputeStateHash()))

    let expected =
        referenceSamples |> List.filter (fun (tick, _) -> tick > int64 safeTick)

    let actual = continuationSamples |> Seq.toList |> List.tail

    if actual <> expected then
        failwith "Fortsetzungskette weicht von der unterbrochenen Referenzkette ab."

    if restoredWorld.ComputeStateHash() <> referenceEnd then
        failwith "Kettenende nach Fortsetzung weicht vom Referenzlauf ab."

    // Fremdseed ändert die Fortsetzung nachweislich.
    let foreignWorld = SimWorld(defaultSeed ^^^ 0x9E3779B9u)
    let foreignPlan = CommandPlan.Generate(defaultSeed ^^^ 0x9E3779B9u, horizon)
    let mutable foreignPlanIndex = 0

    while foreignWorld.TickIndex < int64 horizon do
        let firstDue = foreignPlanIndex

        while foreignPlanIndex < Array.length foreignPlan
              && int64 foreignPlan.[foreignPlanIndex].Tick <= foreignWorld.TickIndex do
            foreignPlanIndex <- foreignPlanIndex + 1

        if foreignPlanIndex > firstDue then
            foreignWorld.ApplyCommands(foreignPlan.AsSpan(firstDue, foreignPlanIndex - firstDue))
            |> ignore

        foreignWorld.Tick()

    if foreignWorld.ComputeStateHash() = referenceEnd then
        failwith "Fremdseed ergab denselben Endhash."

/// Korruptionsmatrix je Klasse unterscheidbar, ohne das Original zu berühren (AC-T031-06).
let corruptionMatrixRejectsEveryClassDistinctly () =
    let document, planHash = buildValidDocument defaultSeed 900
    let originalSha256 = sha256Hex document

    // Minimal gültig wird akzeptiert.
    let struct (controlRejection, controlDocument) =
        SaveDocumentValidator.Validate(document)

    if not (isNull controlRejection) || isNull controlDocument then
        failwith "Minimal gültiger Save wurde abgewiesen."

    let cases =
        Seq.append
            (SaveCorruptionFixtures.ByteLevelCases(document) |> Seq.cast<obj>)
            (SaveCorruptionFixtures.StateLevelCases(document, planHash, "save-tests-fixture-build")
             |> Seq.cast<obj>)
        |> Seq.map (fun item -> item :?> SaveCorruptionCase)

    let mutable observedClasses = Set.empty

    for case in cases do
        let mutated = case.Build.Invoke()
        let struct (rejection, _) = SaveDocumentValidator.Validate(mutated)

        let observed =
            if isNull rejection then
                SaveRejectionClass.None
            else
                rejection.Class

        if observed <> case.ExpectedClass then
            failwith $"Fall {case.Label}: erwartet {case.ExpectedClass}, erhalten {observed}."

        observedClasses <- observedClasses.Add(observed.ToString())

    if sha256Hex document <> originalSha256 then
        failwith "Originaldokument wurde durch die Matrix verändert."

    // Jede DATENMODELL-Klasse ist unterscheidbar vertreten (ohne die der
    // Contentstufe vorbehaltene Klasse „finalitätsnah gültig“).
    let required =
        [ "TruncatedFile"
          "MetaIntegrityViolation"
          "PayloadIntegrityViolation"
          "SchemaVersionUnsupported"
          "MagicInvalid"
          "CanonicalViolation"
          "SizeLimitExceeded"
          "ReferenceInvalid"
          "LimitViolation" ]

    for className in required do
        if not (observedClasses.Contains(className)) then
            failwith $"Korruptionsklasse {className} fehlt in der unterscheidbaren Matrix."

/// Anzeigemetadaten bleiben ohne vollständigen Payload lesbar (AC-T031-01/06).
let displayMetadataRemainsReadableWithoutPayload () =
    let document, _ = buildValidDocument defaultSeed 800
    let headerLength = int (SaveDocumentValidator.GetHeaderLength(document))

    let cutAfterHeader =
        SaveDocumentValidator.PreambleBytes
        + headerLength
        + SaveDocumentValidator.MetaHashBytes

    let truncated = document.AsSpan(0, cutAfterHeader).ToArray()

    let struct (fullRejection, _) = SaveDocumentValidator.Validate(truncated)

    if
        not (isNull fullRejection)
        && fullRejection.Class <> SaveRejectionClass.TruncatedFile
    then
        failwith "Abgeschnittener Save wurde beim Vollvalidieren nicht als abgeschnitten erkannt."
    elif isNull fullRejection then
        failwith "Abgeschnittener Save wurde beim Vollvalidieren akzeptiert."

    let struct (displayRejection, display) =
        SaveDocumentValidator.ReadDisplayMetadata(truncated)

    if not (isNull displayRejection) then
        failwith $"Anzeigemetadaten waren trotz Kopf nicht lesbar: {displayRejection}"

    if isNull display then
        failwith "Anzeigemetadaten fehlen."
    elif display.DisplayPlaytimeTicks <> 800L then
        failwith "Spielzeit der Anzeigemetadaten falsch."
    elif display.DisplayPlaceKey.Length <> 0 || display.DisplayPreviewAvailable then
        failwith "Unverfügbare Anzeigefelder wurden nicht als leer/unavailable geführt."

// Interne Fault-Injection-Nahtstelle des Atomarprotokolls.
type internal SaveFaultPort() =
    let inner = SystemIoSaveFilePort()
    let mutable armedPhase: string option = None

    member _.Arm(phase: string) = armedPhase <- Some phase
    member _.Disarm() = armedPhase <- None

    interface ISaveFilePort with
        member _.DirectoryExists(p) = inner.DirectoryExists(p)
        member _.CreateDirectory(p) = inner.CreateDirectory(p)
        member _.ResolveFullPath(p) = inner.ResolveFullPath(p)
        member _.EntryExists(p) = inner.EntryExists(p)
        member _.IsReparsePoint(p) = inner.IsReparsePoint p

        member _.WriteAllBytesSynced(_p, _b) =
            if armedPhase = Some "temp-write" then
                raise (IOException("fault-injection: temp-write"))

            inner.WriteAllBytesSynced(_p, _b)

        member _.ReadAllBytes(p) =
            if armedPhase = Some "validation-read" then
                raise (IOException("fault-injection: validation-read"))

            let bytes = inner.ReadAllBytes p

            if armedPhase = Some "validation" then
                bytes.[0] <- bytes.[0] ^^^ 0xFFuy

            bytes

        member _.AtomicReplace(sourcePath, targetPath) =
            if armedPhase = Some "replace" then
                raise (IOException("fault-injection: replace"))

            inner.AtomicReplace(sourcePath, targetPath)

        member _.DeleteQuiet(p) = inner.DeleteQuiet(p)

/// Fault-Injection je Schreibphase lässt den letzten gültigen Stand intakt (AC-T031-05).
let slotProtocolFaultInjectionPreservesLastValidState () =
    let workRoot =
        Path.Combine(Path.GetTempPath(), "rift-t031-" + Guid.NewGuid().ToString("N"))

    let port = new SaveFaultPort()
    let store = new SlotStore(workRoot, port)

    try
        let document, _ = buildValidDocument defaultSeed 700

        // Gültiger Grundstand im Slot.
        let baseline = store.WriteSlotAtomic(SaveContract.SlotFileName, document)

        if not baseline.Success then
            failwith $"Grundstand konnte nicht geschrieben werden: {baseline.Error}"

        // Ein grünes Ergebnis trägt niemals eine Warnung: Es behauptet
        // genau die ausgeführten Protokollschritte und nichts zusätzlich;
        // ein teilweise ausgeführter Schritt dürfte sich nicht als Erfolg
        // maskieren.
        if not (isNull baseline.Warning) then
            failwith $"Atomarer Grundstand trug eine Warnung: {baseline.Warning}"

        // Je Phase ein kontrollierter Ausfall; danach ist der Grundstand
        // stets unverändert ladbar und keine Tempdatei bleibt liegen.
        // Die Portphasen und ihre Slotprotokoll-Gegenstücke.
        for portPhase, storePhase in
            [ "temp-write", "temp-write"
              "validation-read", "validation-read"
              "validation", "validation"
              "replace", "atomic-replace" ] do
            port.Arm(portPhase)
            let result = store.WriteSlotAtomic(SaveContract.SlotFileName, document)
            port.Disarm()

            if result.Success then
                failwith $"Fault-Injection {portPhase} führte zu einem scheinbaren Erfolg."

            if not (isNull result.Phase) && result.Phase <> storePhase then
                failwith $"Fault-Injection meldete Phase {result.Phase} statt {storePhase}."

            let read = store.ReadSlot(SaveContract.SlotFileName)

            if not read.Success || isNull read.Bytes || not (bytesEqual read.Bytes document) then
                failwith $"Nach Ausfall in Phase {portPhase} war der letzte gültige Stand nicht intakt."

            let leftovers =
                Directory.GetFiles(workRoot)
                |> Array.filter (fun file -> Path.GetFileName(file) <> SaveContract.SlotFileName)

            if not (Array.isEmpty leftovers) then
                failwith $"Nach Ausfall in Phase {portPhase} blieben Tempdateien liegen."
    finally
        if Directory.Exists(workRoot) then
            Directory.Delete(workRoot, true)

/// Gate-Evaluator entscheidet fail-closed über Kalibrierung, Band und Horizont (AC-T031-08/02).
let savecheckGateCoversEveryClassFailClosed () =
    let absolute = SaveContract.AbsoluteMaxSaveBytes

    let struct (passSanity, passDetail, passLimit) =
        SavecheckGate.EvaluateSizeSanity(1000L, 1000L, 4, absolute)

    if not passSanity || not (isNull passDetail) || passLimit <> 4000L then
        failwith "Bestehensklasse des Größen-Sanity-Gates falsch."


    let sanityPasses (a: int64) (b: int64) (factor: int) =
        let struct (pass, _, _) = SavecheckGate.EvaluateSizeSanity(a, b, factor, absolute)
        pass

    if sanityPasses 1000L 999L 4 then
        failwith "Abweichende Kalibrierläufe wurden akzeptiert."

    if sanityPasses 0L 0L 4 then
        failwith "Nichtpositive Kalibrierläufe wurden akzeptiert."

    if sanityPasses 1000L 1000L (SaveContract.SizeSanityFactorMinimum - 1) then
        failwith "Faktor unterhalb des Bands wurde akzeptiert."

    if sanityPasses 1000L 1000L (SaveContract.SizeSanityFactorMaximum + 1) then
        failwith "Faktor oberhalb des Bands wurde akzeptiert."

    let emptyVerdict = SavecheckGate.Evaluate([ SavecheckCheck("demo", true, null) ])

    if not (Seq.isEmpty emptyVerdict.Violations) then
        failwith "Leere Verletzungsmenge wurde als Verletzung gemeldet."

    let failingVerdict = SavecheckGate.Evaluate([])

    if failingVerdict.Pass then
        failwith "Leere Klassenliste wurde nicht fail-closed abgewiesen."

    let violationVerdict =
        SavecheckGate.Evaluate([ SavecheckCheck("klasse", false, "detail") ])

    if violationVerdict.Pass || Seq.length violationVerdict.Violations <> 1 then
        failwith "Fehlgeschlagene Prüfklassen falteten das Gate nicht."

    let okCont, contTicks = SavecheckGate.ContinuationMeetsContractMinimum(3600L, 1800L)

    if not okCont || contTicks <> 1800L then
        failwith "Vertragsmindestanteil der Fortsetzung bei exakter Hälfte verletzt."

    let belowMinimumOk, _ = SavecheckGate.ContinuationMeetsContractMinimum(3600L, 1801L)

    if belowMinimumOk then
        failwith "Fortsetzungshorizont unterhalb des Mindestanteils wurde akzeptiert."

    let emptyPlanOk, _ = SavecheckGate.ContinuationMeetsContractMinimum(0L, 0L)

    if emptyPlanOk then
        failwith "Leerer Planhorizont wurde akzeptiert."

/// Migrationsregeln: strikt monoton, idempotent auf Kopie, Original bleibt erhalten (AC-T031-07).
let migrationRulesAreStrictlyMonotonicAndIdempotentOnCopy () =
    let document, _ = buildValidDocument defaultSeed 600

    // Produktmigrator: No-op für die aktuelle Version ist byteidentisch.
    let productOutcome = SaveMigrator.Product.MigrateToCurrentVersionOnCopy(document)

    if
        not productOutcome.Success
        || not (isNull productOutcome.Rejection)
        || productOutcome.AppliedSteps.Count <> 0
        || isNull productOutcome.MigratedBytes
        || not (bytesEqual productOutcome.MigratedBytes document)
    then
        failwith "No-op-Migration der aktuellen Version ist nicht byteidentisch/idempotent."

    // Zukünftige Version wird ohne erfundene Migration abgelehnt.
    let futureDocument = Array.copy document
    let futureVersion = SaveContract.CurrentSaveSchemaVersion + 1us
    futureDocument.[SaveContract.MagicLength] <- byte futureVersion
    futureDocument.[SaveContract.MagicLength + 1] <- byte (futureVersion >>> 8)

    let futureOutcome =
        SaveMigrator.Product.MigrateToCurrentVersionOnCopy(futureDocument)

    if
        futureOutcome.Success
        || isNull futureOutcome.Rejection
        || futureOutcome.Rejection.Class <> SaveRejectionClass.SchemaVersionUnsupported
        || futureOutcome.AppliedSteps.Count <> 0
    then
        failwith "Zukünftige Schemaversion wurde nicht ohne Migrationserfindung abgewiesen."

    // Synthetisches Zwei-Version-Fixturepaar (reine interne
    // Testinfrastruktur). Das V0-Fixture trägt Magic und Schemaversion 0,
    // damit der Migrator die Schritt­suche erreicht; sein Rest ist Marker.
    let markerBytes = Encoding.UTF8.GetBytes "RIFTWARD-SYNTHETIC-V0-NO-PRODUCT-PROMISE"

    let syntheticV0Fixture =
        [| yield!
               [| SaveContract.Magic0
                  SaveContract.Magic1
                  SaveContract.Magic2
                  SaveContract.Magic3 |]
           yield 0uy
           yield 0uy
           yield! markerBytes |]

    let migrator = new SaveMigrator()
    let mutable stepCalls = 0

    migrator.RegisterStepForTests(
        0,
        1,
        fun original ->
            stepCalls <- stepCalls + 1

            if original <> syntheticV0Fixture then
                failwith "Synthetischer Schritt erhielt unerwartete Eingabe."

            Array.copy document
    )

    let firstRun = migrator.MigrateToCurrentVersionOnCopy(syntheticV0Fixture)

    if
        not firstRun.Success
        || firstRun.AppliedSteps.Count <> 1
        || isNull firstRun.MigratedBytes
        || not (bytesEqual firstRun.MigratedBytes document)
    then
        failwith "Synthetische 0→1-Migration lieferte kein gültiges Dokument."

    let secondRun = migrator.MigrateToCurrentVersionOnCopy(syntheticV0Fixture)

    if not (bytesEqual secondRun.MigratedBytes firstRun.MigratedBytes) then
        failwith "Wiederholte Migration lieferte ein anderes Ergebnis (Idempotenz verletzt)."

    if
        not (
            bytesEqual
                syntheticV0Fixture
                ([| yield!
                        [| SaveContract.Magic0
                           SaveContract.Magic1
                           SaveContract.Magic2
                           SaveContract.Magic3 |]
                    yield 0uy
                    yield 0uy
                    yield! markerBytes |])
        )
    then
        failwith "Originalstand wurde durch die Migration auf Kopie verändert."

    if stepCalls <> 2 then
        failwith "Schrittzahl der Migration weicht von zwei Läufen ab."

    // Ein fehlgeschlagener Schritt erhält den Originalstand.
    let failingMigrator = new SaveMigrator()
    failingMigrator.RegisterStepForTests(0, 1, (fun _ -> failwith "kontrollierter Fixture-Ausfall"))

    let failureOutcome =
        failingMigrator.MigrateToCurrentVersionOnCopy(syntheticV0Fixture)

    if failureOutcome.Success || isNull failureOutcome.Rejection then
        failwith "Fehlgeschlagener Migrationsschritt wurde als Erfolg gemeldet."

/// Reportvertrag akzeptiert das Goldendokument und lehnt Fälschungen ab (AC-T031-08).
let reportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    if not (SaveReportSchema.Validate(goldenReport).Count = 0) then
        failwith (
            "Goldenreport wurde abgelehnt: "
            + String.concat "; " (SaveReportSchema.Validate(goldenReport))
        )

    // Kennzahl ohne Methodenkennung.
    assertHasError
        "snapshotBytes.method"
        (goldenReport.Replace("\"method\":\"serialized-canonical-payload-at-safe-tick\",", ""))
        "Kennzahl ohne Methodenkennung wurde akzeptiert"

    // Dauerfeld mit behaupteter Gatekopplung.
    assertHasError
        "erwarteter Wert false"
        (goldenReport.Replace("\"gateCoupled\":false", "\"gateCoupled\":true"))
        "Gategekoppelte Dauer wurde akzeptiert"

    // Großbuchstaben-Anker verletzt die Kanonform des SHA-256-Felds.
    assertHasError
        "Kleinbuchstaben"
        (goldenReport.Replace(
            "\"value\":\"6ce087b6070e820c66b2435a756681ef9cf8489dbf9cc73c3be3ce5971f4899d\"",
            "\"value\":\"6CE087B6070E820C66B2435A756681EF9CF8489DBF9CC73C3BE3CE5971F4899D\""
        ))
        "Grossbuchstaben-Anker wurde akzeptiert"

    // Unvollständiger Lauf darf sich nicht als vollständige Evidenz ausgeben.
    assertHasError
        "erwarteter Wert true"
        (goldenReport.Replace("\"complete\":true", "\"complete\":false"))
        "Unvollständiger Lauf wurde als vollständig akzeptiert"

    // Fremde Schemaversion.
    assertHasError
        "schemaVersion"
        (goldenReport.Replace("\"schemaVersion\":1,", "\"schemaVersion\":2,"))
        "Fremde Schemaversion wurde akzeptiert"

    // Erfundener Faktor außerhalb des Auftragbands.
    assertHasError
        "factor"
        (goldenReport.Replace(
            "\"factor\":4,\"bandMinimum\":2,\"bandMaximum\":16",
            "\"factor\":17,\"bandMinimum\":2,\"bandMaximum\":16"
        ))
        "Bandverletzender Faktor wurde akzeptiert"

    // Unbekanntes Feld.
    assertHasError
        "unbekanntes Feld"
        (goldenReport.Replace("\"schemaVersion\":1,", "\"schemaVersion\":1,\"extraField\":1,"))
        "Unbekanntes Feld wurde akzeptiert"

    // Beschädigtes Zwischenartefakt.
    assertHasError "gueltiges JSON" "{beschädigt" "Beschädigtes Dokument wurde akzeptiert"

/// Exitcodebedeutungen bleiben stabil; savecheck ergänzt 33/34 ohne Änderung (AC-T031-10).
let exitCodeMappingStaysStableIncludingSaveCodes () =
    let expectations =
        [ PlatformErrorCode.Internal, 1
          PlatformErrorCode.BenchScenarioUnavailable, 25
          PlatformErrorCode.BenchBudgetViolated, 26
          PlatformErrorCode.TelemetryInvalid, 27
          PlatformErrorCode.ReportNotWritable, 28
          PlatformErrorCode.SoakGateViolated, 30
          PlatformErrorCode.SoakRunIncomplete, 31
          PlatformErrorCode.SoakScenarioUnavailable, 32
          PlatformErrorCode.SaveGateViolated, 33
          PlatformErrorCode.SaveRunIncomplete, 34 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Exitcode fuer {code} ist {ExitCodes.Map(code)}, dokumentiert ist {expected}."

    if ExitCodes.Ok <> 0 || ExitCodes.Usage <> 2 then
        failwith "Basis-Exitcodes wurden veraendert."

/// Architekturtest: Savekern bleibt rein (BCL-only, C#, keine Plattformtypen,
/// kein Timing im Kern) und die UnsafeAccessor-Bindungen verweisen auf echte
/// Felder des unveränderten Simulationskerns (AC-T031-09/10).
let architectureKeepsSaveProjectPureAndBindsSimStateAccess () =
    let saveDirectory = Path.Combine(repositoryRoot, "src", "Riftward.Save")
    let csproj = File.ReadAllText(Path.Combine(saveDirectory, "Riftward.Save.csproj"))

    if not (csproj.Contains("../Riftward.Simulation/Riftward.Simulation.csproj", StringComparison.Ordinal)) then
        failwith "Savekern referenziert den Simulationskern nicht."

    let projectReferences =
        Regex.Matches(csproj, "<ProjectReference Include=\"(?<path>[^\"]+)\"")
        |> Seq.map (fun matchResult -> matchResult.Groups.["path"].Value)
        |> Seq.toList

    if projectReferences <> [ "../Riftward.Simulation/Riftward.Simulation.csproj" ] then
        failwith $"Savekern hat unerlaubte Projektreferenzen: {projectReferences}."

    if Regex.IsMatch(csproj, "<PackageReference") then
        failwith "Savekern besitzt NuGet-Abhängigkeiten; BCL-only verletzt."

    if not (Array.isEmpty (Directory.GetFiles(saveDirectory, "*.fs"))) then
        failwith "F#-Quellen im Runtime-Speicher-/Ladepfad gefunden."

    let forbidden =
        [ "SDL"
          "bgfx"
          "Riftward.Platform"
          "Riftward.App"
          "Stopwatch"
          "System.Reflection"
          "Activator"
          "Emit" ]

    for file in Directory.GetFiles(saveDirectory, "*.cs") do
        let sourceLines = File.ReadAllLines(file)

        // Zeilenkommentare entfernen, damit verbietende Erklaerungen nicht
        // selbst als Verstoss gelten.
        let codeOnly =
            sourceLines
            |> Array.map (fun line ->
                let index = line.IndexOf("//", StringComparison.Ordinal)
                if index >= 0 then line.Substring(0, index) else line)

        let source = String.Concat(codeOnly)

        for token in forbidden do
            if source.Contains(token, StringComparison.Ordinal) then
                failwith $"Verbotener Bezeichner '{token}' in {Path.GetFileName(file)}."

    // Die App bindet den Savekern zwischen Simulations- und Hostschicht.
    let appCsproj =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Riftward.App.csproj"))

    if not (appCsproj.Contains("../Riftward.Save/", StringComparison.Ordinal)) then
        failwith "App referenziert das Saveprojekt nicht."

    // UnsafeAccessor-Bindungen müssen auf reale private Felder des
    // byteidentischen Simulationskerns zeigen; jede Drift wird hier und
    // spätestens beim Kopfanker fail-closed sichtbar.
    let adapterSource =
        File.ReadAllText(Path.Combine(saveDirectory, "SimulationSaveAdapter.cs"))

    let simWorldSource =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.Simulation", "SimWorld.cs"))

    let names =
        Regex.Matches(adapterSource, "Name = \"(?<name>_[A-Za-z0-9]+)\"")
        |> Seq.map (fun matchResult -> matchResult.Groups.["name"].Value)
        |> Seq.distinct
        |> Seq.toList

    if names.Length < 12 then
        failwith $"Zu wenige Zustandsbindungen gefunden ({names.Length}); Spiegel unvollständig."

    for name in names do
        if not (simWorldSource.Contains(name + ";", StringComparison.Ordinal)) then
            failwith $"Zustandsbindung {name} existiert nicht im unveränderten Simulationskern."

/// rift.sh savecheck behält den App-Buildguard (AC-T031-02).
let riftScriptSavecheckContractKeepsAppBuildGuard () =
    let script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "rift.sh"))

    if not (script.Contains("savecheck)", StringComparison.Ordinal)) then
        failwith "rift.sh führt savecheck nicht aus."

    let caseStart = script.IndexOf("savecheck)", StringComparison.Ordinal)
    let caseSlice = script.Substring(caseStart, min 400 (script.Length - caseStart))

    if not (caseSlice.Contains("rift_need_app_output", StringComparison.Ordinal)) then
        failwith "savecheck umgeht den App-Buildguard."

    if not (caseSlice.Contains("savecheck \"$@\"", StringComparison.Ordinal)) then
        failwith "savecheck reicht Argumente nicht an den Host durch."

/// Frischer-Prozess-Hilfslauf über den öffentlichen Host.
let private runAppHost (arguments: string[]) =
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    startInfo.ArgumentList.Add(
        Path.Combine(repositoryRoot, "src", "Riftward.App", "bin", "Release", "net10.0", "Riftward.App.dll")
    )

    for argument in arguments do
        startInfo.ArgumentList.Add(argument)

    use processHandle = Process.Start(startInfo)
    let stdout = processHandle.StandardOutput.ReadToEnd()
    let stderr = processHandle.StandardError.ReadToEnd()
    processHandle.WaitForExit()
    (processHandle.ExitCode, stdout.TrimEnd(), stderr.TrimEnd())

let private payloadHashOf (path: string) =
    use document = JsonDocument.Parse(File.ReadAllText(path))

    document.RootElement.GetProperty("metrics").GetProperty("payloadHash").GetProperty("value").GetString()

let private chainEndOf (path: string) =
    use document = JsonDocument.Parse(File.ReadAllText(path))
    document.RootElement.GetProperty("continuationChain").GetProperty("end").GetString()

/// CLI-Vertrag: zwei Fresh-Prozessläufe mit identischem Payloadanker sowie
/// kontrollierte Fehlerfälle ohne Reportvortäuschung (AC-T031-02/03/04).
let cliContractRunsSavecheckWithReportsAndControlledFailures () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t031-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let argumentsFor reportPath =
            [| "savecheck"
               "--report"
               reportPath
               "--work"
               (Path.Combine(temporary, "work"))
               "--plan-ticks"
               "2400"
               "--safe-tick"
               "1200"
               "--sample-interval-ticks"
               "300" |]

        let reportOne = Path.Combine(temporary, "save-run1.json")
        let reportTwo = Path.Combine(temporary, "save-run2.json")

        let exitOne, _, _ = runAppHost (argumentsFor reportOne)

        if exitOne <> 0 then
            failwith $"savecheck-Lauf ergab Exitcode {exitOne}."

        let exitTwo, _, _ = runAppHost (argumentsFor reportTwo)

        if exitTwo <> 0 then
            failwith $"Zweiter savecheck-Lauf ergab Exitcode {exitTwo}."

        for path in [ reportOne; reportTwo ] do
            let json = File.ReadAllText(path)

            if not (SaveReportSchema.Validate(json).Count = 0) then
                failwith $"Echter Report verletzte den Schemavertrag: {path}"

            use document = JsonDocument.Parse(json)
            let root = document.RootElement

            if root.GetProperty("execution").GetProperty("complete").GetBoolean() <> true then
                failwith "Vollständiger Lauf ist nicht als vollständig markiert."

            if root.GetProperty("execution").GetProperty("isEvidence").GetBoolean() <> true then
                failwith "Vollständiger Lauf ist nicht als Evidenz markiert."

            if root.GetProperty("gate").GetProperty("pass").GetBoolean() <> true then
                failwith "Erfolgreicher Lauf markierte das Gate nicht als bestanden."

            let checksPass =
                root.GetProperty("checks").EnumerateArray()
                |> Seq.forall (fun check -> check.GetProperty("pass").GetBoolean())

            if not checksPass then
                failwith "Erfolgreicher Lauf enthielt fehlgeschlagene Prüfklassen."

            let corruptionChecks =
                root.GetProperty("checks").EnumerateArray()
                |> Seq.filter (fun check ->
                    (check.GetProperty("class").GetString()).StartsWith("corruption-", StringComparison.Ordinal))
                |> Seq.length

            if corruptionChecks < 10 then
                failwith "Korruptionsmatrix im echten Lauf unvollständig vertreten."

            let profilesNotMeasured =
                root.GetProperty("profiles").EnumerateArray()
                |> Seq.forall (fun profile -> profile.GetProperty("status").GetString() = "NOT-MEASURED")

            if not profilesNotMeasured then
                failwith "Pflichtprofile sind im echten Lauf nicht NOT-MEASURED."

            let gateCoupledDurations =
                root.GetProperty("metrics").GetProperty("phaseDurationsMs").EnumerateArray()
                |> Seq.forall (fun phase -> phase.GetProperty("gateCoupled").GetBoolean() = false)

            if not gateCoupledDurations then
                failwith "Dauerfelder sind nicht ausnahmslos diagnostisch."

        // Byteidentität der Payloads über unabhängige Fresh-Prozesse.
        if payloadHashOf reportOne <> payloadHashOf reportTwo then
            failwith "Fresh-Prozessläufe lieferten verschiedene payloadHashes."

        if chainEndOf reportOne <> chainEndOf reportTwo then
            failwith "Fresh-Prozessläufe lieferten verschiedene Kettenenden."

        // Fehlender Reportpfad: Usagefehler.
        let exitUsage, _, _ = runAppHost [| "savecheck" |]

        if exitUsage <> ExitCodes.Usage then
            failwith "Fehlender Reportpfad ergab keinen Usagefehler."

        // Fortsetzungshorizont unterhalb des Vertragsmindestanteils: Usagefehler.
        let shortReport = Path.Combine(temporary, "short.json")

        let exitShort, _, stderrShort =
            runAppHost
                [| "savecheck"
                   "--report"
                   shortReport
                   "--plan-ticks"
                   "600"
                   "--safe-tick"
                   "500" |]

        if exitShort <> ExitCodes.Usage then
            failwith $"Horizontverstoß ergab {exitShort} statt Usagefehler."

        if stderrShort.Length = 0 then
            failwith "Horizontverstoß blieb ohne verständliche Meldung."

        if File.Exists(shortReport) then
            failwith "Abgebrochener Aufruf schrieb einen Report."

        // Nicht schreibbarer Reportpfad: definierter Code 28 ohne Absturz.
        let blockedReport = Path.Combine(temporary, "missing-dir", "nested", "save.json")

        let exitBlocked, _, _ = runAppHost [| "savecheck"; "--report"; blockedReport |]

        if exitBlocked <> ExitCodes.Map(PlatformErrorCode.ReportNotWritable) then
            failwith $"Nicht schreibbarer Reportpfad ergab {exitBlocked}."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

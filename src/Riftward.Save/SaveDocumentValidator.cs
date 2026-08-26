using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Riftward.Simulation;

namespace Riftward.Save;

/// <summary>
/// Unterscheidbare Verletzungsklassen des Loaders (Savevertrag Abschnitt 11).
/// Jeder Save wird höchstens einer Klasse zugeordnet; die Prüfreihenfolge
/// aus Savevertrag Abschnitt 2 macht die Zuordnung deterministisch.
/// </summary>
public enum SaveRejectionClass
{
    None = 0,

    /// <summary>Datei beginnt nicht mit dem Vertragsmagic.</summary>
    MagicInvalid,

    /// <summary>Schemaversion ist unbekannt oder zukünftig.</summary>
    SchemaVersionUnsupported,

    /// <summary>Datei ist kürzer als die deklarierte Rahmenstruktur.</summary>
    TruncatedFile,

    /// <summary>Deklarierte oder tatsächliche Größe oberhalb der Limits.</summary>
    SizeLimitExceeded,

    /// <summary>Kopfbytes passen nicht zum metaHash-Anker.</summary>
    MetaIntegrityViolation,

    /// <summary>Payloadbytes passen nicht zum payloadHash-Anker.</summary>
    PayloadIntegrityViolation,

    /// <summary>Verletzte feste Ordnung oder Framing (Überhang, Re-Encoding-Ungleichheit).</summary>
    CanonicalViolation,

    /// <summary>Grenzwertverletzung eines Zustandsfelds.</summary>
    LimitViolation,

    /// <summary>Fehlende oder beschädigte Weltreferenz (begehbare Kacheln, Zonen).</summary>
    ReferenceInvalid,
}

/// <summary>Eine kontrollierte Ablehnung mit Klasse und verständlichem Detail ohne interne Pfade.</summary>
public sealed record SaveRejection(SaveRejectionClass Class, string Detail)
{
    public override string ToString() => $"{Class}: {Detail}";
}

/// <summary>Geladenes Save-Dokument: Umschlagmetadaten plus geprüfter Zustand.</summary>
public sealed record LoadedSaveDocument
{
    public required ushort SaveSchemaVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required byte[] SaveId { get; init; }

    public required string BuildId { get; init; }

    public required int ContentPackageCount { get; init; }

    public required long DisplayPlaytimeTicks { get; init; }

    public required string DisplayPlaceKey { get; init; }

    public required bool DisplayPreviewAvailable { get; init; }

    public required uint ScenarioSeed { get; init; }

    public required string WorldId { get; init; }

    public required string SimulationContractVersion { get; init; }

    public required string EncodingId { get; init; }

    public required ulong CommandPlanHash { get; init; }

    public required ulong SnapshotStateHash { get; init; }

    public required byte[] PayloadHash { get; init; }

    public required SimSaveState State { get; init; }
}

/// <summary>Ergebnis des Kopfliesens ohne Payloadprüfung (Anzeigemetadaten-Pfad).</summary>
public sealed record SaveDisplayMetadata
{
    public required ushort SaveSchemaVersion { get; init; }

    public required long DisplayPlaytimeTicks { get; init; }

    public required string DisplayPlaceKey { get; init; }

    public required bool DisplayPreviewAvailable { get; init; }
}

/// <summary>
/// Strikter Einzelpass-Validator des Saveformats V1. Reihenfolge gemäß
/// Savevertrag Abschnitt 2: Framing und Größenlimits → metaHash →
/// payloadHash → kanonische Dekodierung mit Re-Encoding-Gleichheit →
/// Grenzwerte → Referenzen. Alle Längen werden vor Zuweisungen gegen
/// Vertragsgrenzen geprüft; jeder Save gilt als untrusted.
/// </summary>
public static class SaveDocumentValidator
{
    private const int StringLengthCap = 512;

    /// <summary>Prüft ein vollständiges Dokument und liefert Ablehnung oder Dokument.</summary>
    public static (SaveRejection? Rejection, LoadedSaveDocument? Document) Validate(ReadOnlySpan<byte> file)
    {
        // 1) Framing: Magic.
        if (file.Length < PreambleMinimumBytes
            || file[0] != SaveContract.Magic0
            || file[1] != SaveContract.Magic1
            || file[2] != SaveContract.Magic2
            || file[3] != SaveContract.Magic3)
        {
            return Reject(SaveRejectionClass.MagicInvalid, "Datei trägt nicht das Vertragsmagic RWSD.");
        }

        // 2) Framing: Schemaversion zuerst lesbar.
        var schemaVersion = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(SaveContract.MagicLength, sizeof(ushort)));

        if (schemaVersion != SaveContract.CurrentSaveSchemaVersion)
        {
            return Reject(
                SaveRejectionClass.SchemaVersionUnsupported,
                $"Schemaversion {schemaVersion} wird ohne erfundene Migration nicht unterstützt.");
        }

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(
            file.Slice(PreambleBytes - sizeof(uint), sizeof(uint)));

        // 3) Framing: Kopfgrenzen vor jeder Zuweisung.
        if (headerLength < MinimumHeaderByteCount || headerLength > SaveContract.MaxHeaderBytes)
        {
            return Reject(SaveRejectionClass.SizeLimitExceeded, "Kopflänge außerhalb der Vertragsframinggrenzen.");
        }

        var preamblePlusHeader = PreambleBytes + (int)headerLength;

        if (file.Length < preamblePlusHeader + MetaHashBytes)
        {
            return Reject(SaveRejectionClass.TruncatedFile, "Datei endet vor dem metaHash-Anker.");
        }

        // 4) metaHash über Magic, Schemaversion, Kopflänge und Kopf.
        var expectedMetaHash = SHA256.HashData(file.Slice(0, preamblePlusHeader));

        if (!CryptographicOperations.FixedTimeEquals(expectedMetaHash, file.Slice(preamblePlusHeader, MetaHashBytes)))
        {
            return Reject(
                SaveRejectionClass.MetaIntegrityViolation,
                "Kopfbytes einschließlich payloadHash-Feld widersprechen dem metaHash-Anker.");
        }

        var header = file.Slice(PreambleBytes, (int)headerLength);
        var payloadLength = (long)ReadHeaderU64(header, HeaderOffsetPayloadLength(header));

        // 5) Absolutes Größenlimit vor Zuweisung.
        if (payloadLength < 0 || payloadLength > SaveContract.AbsoluteMaxSaveBytes)
        {
            return Reject(SaveRejectionClass.SizeLimitExceeded, "Deklarierte Payloadgröße oberhalb des absoluten Limits.");
        }

        // 6) Rahmenkonsistenz: Abschneidung oder Überhang.
        var expectedTotal = (long)preamblePlusHeader + MetaHashBytes + payloadLength;

        if (file.Length < expectedTotal)
        {
            return Reject(SaveRejectionClass.TruncatedFile, "Datei ist gegenüber der deklarierten Rahmenstruktur abgeschnitten.");
        }

        if (file.Length > expectedTotal)
        {
            return Reject(SaveRejectionClass.CanonicalViolation, "Datei besitzt Überhangbytes jenseits der deklarierten Struktur.");
        }

        // 7) payloadHash über die Payloadbytes.
        var payloadStart = (int)(expectedTotal - payloadLength);
        var payload = file.Slice(payloadStart, (int)payloadLength);
        var computedPayloadHash = SHA256.HashData(payload);

        if (!CryptographicOperations.FixedTimeEquals(computedPayloadHash, ReadHeaderPayloadHash(header)))
        {
            return Reject(
                SaveRejectionClass.PayloadIntegrityViolation,
                "Payloadbytes widersprechen dem aufgezeichneten payloadHash.");
        }

        // 8) Kanonische Dekodierung mit exaktem Verbrauch.
        var decode = DecodePayload(payload);

        if (decode.Rejection is not null)
        {
            return (decode.Rejection, null);
        }

        var state = decode.State!;

        // 9) Kanonform: Re-Encoding muss byteidentisch sein.
        var reencoded = CanonicalSaveCodec.EncodePayload(state);

        if (!reencoded.AsSpan().SequenceEqual(payload))
        {
            return Reject(
                SaveRejectionClass.CanonicalViolation,
                "Eingabe verstößt gegen die feste Feld-/Bytelordnung (Re-Encoding-Ungleichheit).");
        }

        // 10) Grenzwertprüfung.
        var limitFailure = EvaluateLimits(state);

        if (limitFailure is not null)
        {
            return (limitFailure, null);
        }

        // 11) Referenzprüfung gegen die feste Weltgeometrie.
        var referenceFailure = EvaluateReferences(state);

        if (referenceFailure is not null)
        {
            return (referenceFailure, null);
        }

        var document = ReadHeaderFields(header, schemaVersion, state);
        return (null, document);
    }

    /// <summary>
    /// Liest ausschließlich Kopf und Anzeigemetadaten; der Payload wird
    /// weder geprüft noch geladen (Savevertrag Abschnitt 4:
    /// displayMetadata bleibt ohne vollständigen Payload lesbar).
    /// </summary>
    public static (SaveRejection? Rejection, SaveDisplayMetadata? Metadata) ReadDisplayMetadata(ReadOnlySpan<byte> file)
    {
        if (file.Length < PreambleMinimumBytes
            || file[0] != SaveContract.Magic0
            || file[1] != SaveContract.Magic1
            || file[2] != SaveContract.Magic2
            || file[3] != SaveContract.Magic3)
        {
            return (new SaveRejection(SaveRejectionClass.MagicInvalid, "Datei trägt nicht das Vertragsmagic RWSD."), null);
        }

        var schemaVersion = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(SaveContract.MagicLength, sizeof(ushort)));

        if (schemaVersion != SaveContract.CurrentSaveSchemaVersion)
        {
            return (
                new SaveRejection(
                    SaveRejectionClass.SchemaVersionUnsupported,
                    $"Schemaversion {schemaVersion} wird ohne erfundene Migration nicht unterstützt."),
                null);
        }

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(PreambleBytes - sizeof(uint), sizeof(uint)));

        if (headerLength < MinimumHeaderByteCount || headerLength > SaveContract.MaxHeaderBytes)
        {
            return (
                new SaveRejection(SaveRejectionClass.SizeLimitExceeded, "Kopflänge außerhalb der Vertragsframinggrenzen."),
                null);
        }

        var preamblePlusHeader = PreambleBytes + (int)headerLength;

        if (file.Length < preamblePlusHeader + MetaHashBytes)
        {
            return (new SaveRejection(SaveRejectionClass.TruncatedFile, "Datei endet vor dem metaHash-Anker."), null);
        }

        var expectedMetaHash = SHA256.HashData(file.Slice(0, preamblePlusHeader));

        if (!CryptographicOperations.FixedTimeEquals(expectedMetaHash, file.Slice(preamblePlusHeader, MetaHashBytes).ToArray()))
        {
            return (
                new SaveRejection(SaveRejectionClass.MetaIntegrityViolation, "Kopfbytes widersprechen dem metaHash-Anker."),
                null);
        }

        var header = file.Slice(PreambleBytes, (int)headerLength);

        try
        {
            _ = ReadHeaderString(header, HeaderOffsetBuildId(header));
            var placeKey = ReadHeaderString(header, HeaderOffsetPlaceKey(header));
            var playtime = (long)ReadHeaderU64(header, HeaderOffsetPlaytime(header));
            var preview = header[HeaderOffsetPreview(header)] != 0;

            return (
                null,
                new SaveDisplayMetadata
                {
                    SaveSchemaVersion = schemaVersion,
                    DisplayPlaytimeTicks = playtime,
                    DisplayPlaceKey = placeKey,
                    DisplayPreviewAvailable = preview,
                });
        }
        catch (InvalidOperationException exception)
        {
            return (new SaveRejection(SaveRejectionClass.CanonicalViolation, exception.Message), null);
        }
    }

    internal const int PreambleBytes = CanonicalSaveCodec.PreambleBytes;

    internal const int MetaHashBytes = CanonicalSaveCodec.MetaHashBytes;

    internal const int PreambleMinimumBytes = PreambleBytes + sizeof(uint);

    internal static int MinimumHeaderByteCount => CanonicalSaveCodec.MinimumHeaderBytes;

    /// <summary>Liest die Kopflänge aus dem Rahmen (Magic muss bereits geprüft sein).</summary>
    internal static uint GetHeaderLength(ReadOnlySpan<byte> file) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(PreambleBytes - sizeof(uint), sizeof(uint)));

    // Kopfmaße in fester Ordnung: created/updated/saveId/buildId/packages/
    // playtime/placeKey/preview/seed/worldId/contractVersion/encodingId/
    // planHash/stateHash/payloadLength/payloadHash.
    private static int HeaderOffsetBuildId(ReadOnlySpan<byte> header) =>
        (2 * sizeof(long)) + SaveContract.SaveIdLength;

    private static int HeaderOffsetPackages(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetBuildId(header);
        offset += sizeof(ushort) + ReadHeaderString(header, offset).Length;
        return offset;
    }

    private static int HeaderOffsetPlaytime(ReadOnlySpan<byte> header) => HeaderOffsetPackages(header) + sizeof(ushort);

    private static int HeaderOffsetPlaceKey(ReadOnlySpan<byte> header) => HeaderOffsetPlaytime(header) + sizeof(ulong);

    private static int HeaderOffsetPreview(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetPlaceKey(header);
        offset += sizeof(ushort) + ReadHeaderString(header, offset).Length;
        return offset;
    }

    private static int HeaderOffsetAfterPreview(ReadOnlySpan<byte> header) => HeaderOffsetPreview(header) + sizeof(byte);

    private static int HeaderOffsetWorldId(ReadOnlySpan<byte> header) => HeaderOffsetAfterPreview(header) + sizeof(uint);

    private static int HeaderOffsetContractVersion(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetWorldId(header);
        offset += sizeof(ushort) + ReadHeaderString(header, offset).Length;
        return offset;
    }

    private static int HeaderOffsetEncodingId(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetContractVersion(header);
        offset += sizeof(ushort) + ReadHeaderString(header, offset).Length;
        return offset;
    }

    private static int HeaderOffsetPlanHash(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetEncodingId(header);
        offset += sizeof(ushort) + ReadHeaderString(header, offset).Length;
        return offset;
    }

    private static int HeaderOffsetStateHash(ReadOnlySpan<byte> header) => HeaderOffsetPlanHash(header) + sizeof(ulong);

    /// <summary>Kopfoffset des payloadLength-Felds (intern für Korruptions-Fixtures).</summary>
    internal static int HeaderOffsetPayloadLength(ReadOnlySpan<byte> header) => HeaderOffsetStateHash(header) + sizeof(ulong);

    private static int HeaderOffsetPayloadHash(ReadOnlySpan<byte> header) => HeaderOffsetPayloadLength(header) + sizeof(ulong);

    private static LoadedSaveDocument ReadHeaderFields(ReadOnlySpan<byte> header, ushort schemaVersion, SimSaveState state)
    {
        var created = ReadHeaderI64(header, 0);
        var updated = ReadHeaderI64(header, sizeof(long));
        var saveId = header.Slice((2 * sizeof(long)), SaveContract.SaveIdLength).ToArray();
        var buildId = ReadHeaderString(header, HeaderOffsetBuildId(header));
        var packages = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(HeaderOffsetPackages(header), sizeof(ushort)));
        var playtime = (long)ReadHeaderU64(header, HeaderOffsetPlaytime(header));
        var placeKey = ReadHeaderString(header, HeaderOffsetPlaceKey(header));
        var preview = header[HeaderOffsetPreview(header)] != 0;
        var seed = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(HeaderOffsetAfterPreview(header), sizeof(uint)));
        var worldId = ReadHeaderString(header, HeaderOffsetWorldId(header));
        var contractVersion = ReadHeaderString(header, HeaderOffsetContractVersion(header));
        var encodingId = ReadHeaderString(header, HeaderOffsetEncodingId(header));
        var planHash = ReadHeaderU64(header, HeaderOffsetPlanHash(header));
        var stateHash = ReadHeaderU64(header, HeaderOffsetStateHash(header));
        var payloadHash = ReadHeaderPayloadHash(header);

        return new LoadedSaveDocument
        {
            SaveSchemaVersion = schemaVersion,
            CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(created),
            UpdatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(updated),
            SaveId = saveId,
            BuildId = buildId,
            ContentPackageCount = packages,
            DisplayPlaytimeTicks = playtime,
            DisplayPlaceKey = placeKey,
            DisplayPreviewAvailable = preview,
            ScenarioSeed = seed,
            WorldId = worldId,
            SimulationContractVersion = contractVersion,
            EncodingId = encodingId,
            CommandPlanHash = planHash,
            SnapshotStateHash = stateHash,
            PayloadHash = payloadHash,
            State = state,
        };
    }

    private static long ReadHeaderI64(ReadOnlySpan<byte> header, int offset) =>
        BinaryPrimitives.ReadInt64LittleEndian(header.Slice(offset, sizeof(long)));

    private static ulong ReadHeaderU64(ReadOnlySpan<byte> header, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(offset, sizeof(ulong)));

    private static byte[] ReadHeaderPayloadHash(ReadOnlySpan<byte> header)
    {
        var offset = HeaderOffsetPayloadHash(header);

        if (offset + SaveContract.HashLength > header.Length)
        {
            throw new InvalidOperationException("Kopf endet innerhalb des payloadHash-Felds.");
        }

        return header.Slice(offset, SaveContract.HashLength).ToArray();
    }

    private static string ReadHeaderString(ReadOnlySpan<byte> header, int offset)
    {
        if (offset + sizeof(ushort) > header.Length)
        {
            throw new InvalidOperationException("Zeichenfolgenlängenpräfix ragt über den Kopf.");
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(offset, sizeof(ushort)));

        if (length > StringLengthCap || offset + sizeof(ushort) + length > header.Length)
        {
            throw new InvalidOperationException("Zeichenfolge ragt über den Kopf.");
        }

        try
        {
            return Encoding.UTF8.GetString(header.Slice(offset + sizeof(ushort), length));
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidOperationException("Kopfzeichenfolge ist kein gültiges UTF-8.");
        }
    }

    private sealed record DecodeResult(SaveRejection? Rejection, SimSaveState? State);

    private static DecodeResult DecodePayload(ReadOnlySpan<byte> payload)
    {
        var agents = SimulationContract.AgentCount;
        var minimum = SaveContract.PayloadFixedPrefixBytes + ((long)agents * SaveContract.AgentStrideBytes);

        if (payload.Length < minimum)
        {
            return new DecodeResult(
                new SaveRejection(
                    SaveRejectionClass.CanonicalViolation,
                    "Payload unterschreitet das feste Maß des Relevantzustands (Agentenanzahl ist vertraglich fixiert)."),
                null);
        }

        try
        {
            var offset = 0;
            var tickIndex = ReadI64(payload, ref offset);
            var seed = ReadU32(payload, ref offset);

            var targetZones = new int[SimulationContract.GroupCount];

            for (var group = 0; group < SimulationContract.GroupCount; group++)
            {
                targetZones[group] = ReadI32(payload, ref offset);
            }

            var positionX = new long[agents];
            var positionY = new long[agents];
            var velocityX = new long[agents];
            var velocityY = new long[agents];
            var goalTile = new int[agents];
            var groupByAgent = new byte[agents];
            var pathState = new byte[agents];
            var plannedZone = new short[agents];
            var waypointCursor = new int[agents];
            var waypointCount = new int[agents];
            var pendingWaypoints = new int[agents][];

            for (var agent = 0; agent < agents; agent++)
            {
                positionX[agent] = ReadI64(payload, ref offset);
                positionY[agent] = ReadI64(payload, ref offset);
                velocityX[agent] = ReadI64(payload, ref offset);
                velocityY[agent] = ReadI64(payload, ref offset);
                goalTile[agent] = ReadI32(payload, ref offset);
                groupByAgent[agent] = ReadU8(payload, ref offset);
                pathState[agent] = ReadU8(payload, ref offset);
                plannedZone[agent] = ReadI16(payload, ref offset);
                waypointCursor[agent] = ReadI32(payload, ref offset);
                waypointCount[agent] = ReadI32(payload, ref offset);

                var rawPending = waypointCount[agent] - waypointCursor[agent];

                if (waypointCount[agent] < 0
                    || waypointCount[agent] > NavWorld.MaxWaypointsPerAgent
                    || waypointCursor[agent] < 0)
                {
                    return new DecodeResult(
                        new SaveRejection(SaveRejectionClass.LimitViolation, "Wegpunktgrenzen des Agenten sind verletzt."),
                        null);
                }

                // Ein transientes Cursor>Anzahl-Paar ist bytegetreuer
                // Relevantzustand; sein Schwanz ist kanonisch leer.
                var pending = Math.Max(0, rawPending);
                var slice = new int[pending];

                for (var index = 0; index < pending; index++)
                {
                    slice[index] = ReadI32(payload, ref offset);
                }

                pendingWaypoints[agent] = slice;
            }

            if (offset != payload.Length)
            {
                return new DecodeResult(
                    new SaveRejection(SaveRejectionClass.CanonicalViolation, "Payload besitzt Restbytes jenseits des festen Zustandsmaßes."),
                    null);
            }

            return new DecodeResult(null, new SimSaveState
            {
                TickIndex = tickIndex,
                Seed = seed,
                TargetZoneByGroup = targetZones,
                PositionXQ16 = positionX,
                PositionYQ16 = positionY,
                VelocityXQ16 = velocityX,
                VelocityYQ16 = velocityY,
                GoalTile = goalTile,
                Group = groupByAgent,
                PathState = pathState,
                PlannedZone = plannedZone,
                WaypointCursor = waypointCursor,
                WaypointCount = waypointCount,
                PendingWaypoints = pendingWaypoints,
            });
        }
        catch (PayloadBoundsException)
        {
            return new DecodeResult(
                new SaveRejection(
                    SaveRejectionClass.CanonicalViolation,
                    "Payload endet innerhalb eines Zustandsfelds (Framing verletzt das feste Maß)."),
                null);
        }
    }

    /// <summary>Internes Signal für Reads jenseits der Payloadgrenze.</summary>
    private sealed class PayloadBoundsException : Exception
    {
    }

    private static long ReadI64(ReadOnlySpan<byte> source, ref int offset)
    {
        EnsureBounds(source, offset, sizeof(long));
        var value = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset, sizeof(long)));
        offset += sizeof(long);
        return value;
    }

    private static int ReadI32(ReadOnlySpan<byte> source, ref int offset)
    {
        EnsureBounds(source, offset, sizeof(int));
        var value = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        return value;
    }

    private static short ReadI16(ReadOnlySpan<byte> source, ref int offset)
    {
        EnsureBounds(source, offset, sizeof(short));
        var value = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, sizeof(short)));
        offset += sizeof(short);
        return value;
    }

    private static uint ReadU32(ReadOnlySpan<byte> source, ref int offset)
    {
        EnsureBounds(source, offset, sizeof(uint));
        var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    private static byte ReadU8(ReadOnlySpan<byte> source, ref int offset)
    {
        EnsureBounds(source, offset, sizeof(byte));
        return source[offset++];
    }

    private static void EnsureBounds(ReadOnlySpan<byte> source, int offset, int length)
    {
        if (offset + length > source.Length)
        {
            throw new PayloadBoundsException();
        }
    }

    private static SaveRejection? EvaluateLimits(SimSaveState state)
    {
        if (state.TickIndex < 0)
        {
            return new SaveRejection(SaveRejectionClass.LimitViolation, "Tickindex ist negativ.");
        }

        var maxXQ16 = NavWorld.TilesX * NavWorld.TileSizeQ16;
        var maxYQ16 = NavWorld.TilesY * NavWorld.TileSizeQ16;

        for (var group = 0; group < SimulationContract.GroupCount; group++)
        {
            if (state.TargetZoneByGroup[group] < 0 || state.TargetZoneByGroup[group] >= NavWorld.ZoneCount)
            {
                return new SaveRejection(
                    SaveRejectionClass.LimitViolation,
                    $"Gruppenziel {group} zeigt auf eine unbekannte Zone.");
            }
        }

        var agents = state.Group.Length;

        for (var agent = 0; agent < agents; agent++)
        {
            if (state.PositionXQ16[agent] < 0 || state.PositionXQ16[agent] >= maxXQ16
                || state.PositionYQ16[agent] < 0 || state.PositionYQ16[agent] >= maxYQ16)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Position von Agent {agent} liegt außerhalb der Welt.");
            }

            if (Math.Abs(state.VelocityXQ16[agent]) >= maxXQ16 || Math.Abs(state.VelocityYQ16[agent]) >= maxYQ16)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Geschwindigkeit von Agent {agent} liegt außerhalb der Weltgrenzen.");
            }

            if (state.GoalTile[agent] < 0 || state.GoalTile[agent] >= NavWorld.TileCount)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Zielkachel von Agent {agent} liegt außerhalb der Welt.");
            }

            if (state.Group[agent] >= SimulationContract.GroupCount)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Gruppenindex von Agent {agent} ist unbekannt.");
            }

            if (state.PathState[agent] > (byte)SimAgentPathState.Unreachable)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Pfadstatus von Agent {agent} ist unbekannt.");
            }

            if (state.PlannedZone[agent] < 0 || state.PlannedZone[agent] >= NavWorld.ZoneCount)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Geplante Zone von Agent {agent} ist unbekannt.");
            }

            if (state.WaypointCursor[agent] < 0
                || state.WaypointCursor[agent] > NavWorld.MaxWaypointsPerAgent
                || state.WaypointCount[agent] < 0
                || state.WaypointCount[agent] > NavWorld.MaxWaypointsPerAgent)
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Wegpunktgrenzen von Agent {agent} sind verletzt.");
            }

            if (state.PendingWaypoints[agent].Length != Math.Max(0, state.WaypointCount[agent] - state.WaypointCursor[agent]))
            {
                return new SaveRejection(SaveRejectionClass.LimitViolation, $"Ausstehende Wegpunktanzahl von Agent {agent} passt nicht zu Cursor/Anzahl.");
            }
        }

        return null;
    }

    private static SaveRejection? EvaluateReferences(SimSaveState state)
    {
        var agents = state.Group.Length;

        for (var agent = 0; agent < agents; agent++)
        {
            // Die Position eines Agenten liegt immer auf begehbarer Geometrie
            // (achsenweise Kollisionsauflösung des Kerns); eine Abweichung ist
            // eine beschädigte Weltreferenz. Die Zielkachel dagegen darf
            // vertraglich die Mitte eines unpassierbaren Zellbereichs zeigen,
            // wenn die Grobsuche das Ziel als unerreichbar meldet; sie wird
            // daher nur grenzwertig, nicht referentiell geprüft.
            var positionTile =
                (NavWorld.TileIndexOfPosition(state.PositionYQ16[agent]) * NavWorld.TilesX)
                + NavWorld.TileIndexOfPosition(state.PositionXQ16[agent]);

            if (!NavWorld.IsWalkableIndex(positionTile))
            {
                return new SaveRejection(
                    SaveRejectionClass.ReferenceInvalid,
                    $"Position von Agent {agent} referenziert keine existierende begehbare Kachel.");
            }

            var pending = state.PendingWaypoints[agent];

            for (var index = 0; index < pending.Length; index++)
            {
                var tile = pending[index];

                if (tile < 0 || tile >= NavWorld.TileCount || !NavWorld.IsWalkableIndex(tile))
                {
                    return new SaveRejection(
                        SaveRejectionClass.ReferenceInvalid,
                        $"Wegpunkt {index} von Agent {agent} referenziert keine existierende begehbare Kachel.");
                }
            }
        }

        return null;
    }

    private static (SaveRejection? Rejection, LoadedSaveDocument? Document) Reject(SaveRejectionClass @class, string detail) =>
        (new SaveRejection(@class, detail), null);
}

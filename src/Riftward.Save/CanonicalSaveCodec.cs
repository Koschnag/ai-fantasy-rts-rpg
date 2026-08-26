using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Riftward.Simulation;

namespace Riftward.Save;

/// <summary>
/// Umschlagmetadaten eines Schreibvorgangs. Sie sind laut Savevertrag
/// Abschnitt 2 ausdrücklich nicht Teil der Determinismusbehauptung: Zwei
/// Prozesse dürfen für denselben Zustand verschiedene Metadaten tragen,
/// ohne dass sich Payloadbytes oder payloadHash unterscheiden.
/// </summary>
public sealed record SaveEnvelopeMetadata
{
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>16 opake Bytes (Zufallswert).</summary>
    public required byte[] SaveId { get; init; }

    public static SaveEnvelopeMetadata CreateFresh(DateTimeOffset? moment = null)
    {
        var instant = moment ?? DateTimeOffset.UtcNow;
        var saveId = new byte[SaveContract.SaveIdLength];
        RandomNumberGenerator.Fill(saveId);

        return new SaveEnvelopeMetadata { CreatedAtUtc = instant, UpdatedAtUtc = instant, SaveId = saveId };
    }
}

/// <summary>
/// Kanonische Binärcodierung des Saveformats V1
/// (<c>riftward-save-canonical-binary-v1</c>, Savevertrag Abschnitte 1 bis 4):
/// feste Feldordnung, Little-Endian-Festbreiten-Ganzzahlen, UTF-8-Zeichenfolgen
/// mit vorangestellter u16-Länge, keine Auffüllbytes und keine Feldkennungen.
///
/// Rahmenstruktur: Magic „RWSD“, Schemaversion (u16, zuerst lesbar),
/// Kopflänge (u32), Kopf, metaHash (SHA-256 über alle vorangehenden Bytes,
/// einschließlich des payloadHash-Felds im Kopf), Payload. Die Gesamtgröße
/// ist vollständig aus dem Kopf ableitbar; Überhangbytes sind vertragswidrig.
/// </summary>
public static class CanonicalSaveCodec
{
    /// <summary>Länge des festen Rahmens vor dem Kopf.</summary>
    public const int PreambleBytes =
        SaveContract.MagicLength + sizeof(ushort) + sizeof(uint);

    /// <summary>Länge des metaHash-Ankers.</summary>
    public const int MetaHashBytes = SaveContract.HashLength;

    /// <summary>Kopflänge bei allen Zeichenfolgen der Vertragswerte.</summary>
    public static int MinimumHeaderBytes => MeasureHeader("Riftward", "Riftward", SaveContract.EncodingId, string.Empty);

    /// <summary>Kodiert eine UTF-8-Zeichenfolge mit u16-Längenpräfix.</summary>
    private static void WriteString(Span<byte> target, ref int offset, string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);

        if (byteCount > ushort.MaxValue)
        {
            throw new InvalidOperationException("Vertragszeichenfolge überschreitet die Kodiergrenze.");
        }

        BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(offset, 2), (ushort)byteCount);
        offset += 2;
        offset += Encoding.UTF8.GetBytes(value, target.Slice(offset));
    }

    private static int MeasureHeader(
        string worldId,
        string simulationContractVersion,
        string encodingId,
        string buildId) =>
        (2 * sizeof(long))
        + SaveContract.SaveIdLength
        + (sizeof(ushort) + Encoding.UTF8.GetByteCount(buildId))
        + sizeof(ushort)
        + sizeof(long)
        + (sizeof(ushort) + 0)
        + sizeof(byte)
        + sizeof(uint)
        + (sizeof(ushort) + Encoding.UTF8.GetByteCount(worldId))
        + (sizeof(ushort) + Encoding.UTF8.GetByteCount(simulationContractVersion))
        + (sizeof(ushort) + Encoding.UTF8.GetByteCount(encodingId))
        + (2 * sizeof(ulong))
        + sizeof(ulong)
        + SaveContract.HashLength;

    /// <summary>
    /// Schreibt ein vollständig gültiges Dokument V1: kanonischer Payload aus
    /// dem Zustand, SHA-256-Anker über den Payload, Kopfangaben und metaHash.
    /// </summary>
    public static byte[] WriteDocument(
        SimSaveState state,
        ulong snapshotStateHash,
        ulong commandPlanHash,
        string buildId,
        SaveEnvelopeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.SaveId.Length != SaveContract.SaveIdLength)
        {
            throw new ArgumentException("saveId besitzt nicht die vertragliche Länge.", nameof(metadata));
        }

        var payload = EncodePayload(state);
        var payloadHash = SHA256.HashData(payload);
        var headerLength = MeasureHeader(
            SimulationContract.WorldId,
            SimulationContract.ContractVersion,
            SaveContract.EncodingId,
            buildId);

        if (headerLength > SaveContract.MaxHeaderBytes)
        {
            throw new InvalidOperationException("Kopf überschreitet die Framinggrenze.");
        }

        var totalLength = PreambleBytes + headerLength + MetaHashBytes + payload.Length;
        var document = new byte[totalLength];

        document[0] = SaveContract.Magic0;
        document[1] = SaveContract.Magic1;
        document[2] = SaveContract.Magic2;
        document[3] = SaveContract.Magic3;
        BinaryPrimitives.WriteUInt16LittleEndian(
            document.AsSpan(SaveContract.MagicLength, sizeof(ushort)),
            SaveContract.CurrentSaveSchemaVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(
            document.AsSpan(PreambleBytes - sizeof(uint), sizeof(uint)),
            (uint)headerLength);

        var header = document.AsSpan(PreambleBytes, headerLength);
        var offset = 0;
        WriteLong(header, ref offset, metadata.CreatedAtUtc.ToUnixTimeMilliseconds());
        WriteLong(header, ref offset, metadata.UpdatedAtUtc.ToUnixTimeMilliseconds());
        metadata.SaveId.CopyTo(header.Slice(offset));
        offset += SaveContract.SaveIdLength;
        WriteString(header, ref offset, buildId);
        WriteUShort(header, ref offset, 0);
        WriteUnsigned(header, ref offset, (ulong)state.TickIndex);
        WriteString(header, ref offset, string.Empty);
        header[offset] = 0;
        offset += sizeof(byte);
        WriteU32(header, ref offset, state.Seed);
        WriteString(header, ref offset, SimulationContract.WorldId);
        WriteString(header, ref offset, SimulationContract.ContractVersion);
        WriteString(header, ref offset, SaveContract.EncodingId);
        WriteUnsigned(header, ref offset, commandPlanHash);
        WriteUnsigned(header, ref offset, snapshotStateHash);
        WriteUnsigned(header, ref offset, (ulong)payload.Length);
        payloadHash.CopyTo(header.Slice(offset));

        if (offset + SaveContract.HashLength != headerLength)
        {
            throw new InvalidOperationException("Kopfmaß und Kodierung weichen ab.");
        }

        SHA256.HashData(document.AsSpan(0, PreambleBytes + headerLength))
            .CopyTo(document.AsSpan(PreambleBytes + headerLength, MetaHashBytes));
        payload.CopyTo(document.AsSpan(PreambleBytes + headerLength + MetaHashBytes));

        return document;
    }

    /// <summary>
    /// Kanonischer Payload nach Savevertrag Abschnitt 3: fester Kopf, je
    /// Agent fester Strang plus ausstehender Wegpunktschwanz ab Cursor.
    /// </summary>
    public static byte[] EncodePayload(SimSaveState state)
    {
        var agents = state.Group.Length;
        long tailBytes = 0;

        for (var agent = 0; agent < agents; agent++)
        {
            // Ein transientes Cursor>Anzahl-Paar bleibt bytegetreu erhalten;
            // sein Schwanz ist kanonisch leer (Savevertrag Abschnitt 3).
            tailBytes += 4L * Math.Max(0, state.WaypointCount[agent] - state.WaypointCursor[agent]);
        }

        var total = SaveContract.PayloadFixedPrefixBytes
            + ((long)agents * SaveContract.AgentStrideBytes)
            + tailBytes;

        if (total > int.MaxValue)
        {
            throw new InvalidOperationException("Payload überschreitet die Kodiergrenze.");
        }

        var payload = new byte[total];
        var offset = 0;

        WriteLong(payload, ref offset, state.TickIndex);
        WriteU32(payload, ref offset, state.Seed);

        for (var group = 0; group < SimulationContract.GroupCount; group++)
        {
            WriteSigned(payload, ref offset, state.TargetZoneByGroup[group]);
        }

        for (var agent = 0; agent < agents; agent++)
        {
            WriteLong(payload, ref offset, state.PositionXQ16[agent]);
            WriteLong(payload, ref offset, state.PositionYQ16[agent]);
            WriteLong(payload, ref offset, state.VelocityXQ16[agent]);
            WriteLong(payload, ref offset, state.VelocityYQ16[agent]);
            WriteSigned(payload, ref offset, state.GoalTile[agent]);
            payload[offset] = state.Group[agent];
            offset += sizeof(byte);
            payload[offset] = state.PathState[agent];
            offset += sizeof(byte);
            WriteShort(payload, ref offset, state.PlannedZone[agent]);
            WriteSigned(payload, ref offset, state.WaypointCursor[agent]);
            WriteSigned(payload, ref offset, state.WaypointCount[agent]);

            var pending = state.PendingWaypoints[agent];

            for (var index = 0; index < pending.Length; index++)
            {
                WriteSigned(payload, ref offset, pending[index]);
            }
        }

        if (offset != total)
        {
            throw new InvalidOperationException("Payloadmaß und Kodierung weichen ab.");
        }

        return payload;
    }

    private static void WriteLong(Span<byte> target, ref int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(target.Slice(offset, sizeof(long)), value);
        offset += sizeof(long);
    }

    private static void WriteUnsigned(Span<byte> target, ref int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(target.Slice(offset, sizeof(ulong)), value);
        offset += sizeof(ulong);
    }

    private static void WriteSigned(Span<byte> target, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(target.Slice(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    private static void WriteU32(Span<byte> target, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(target.Slice(offset, sizeof(uint)), value);
        offset += sizeof(uint);
    }

    private static void WriteShort(Span<byte> target, ref int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(target.Slice(offset, sizeof(short)), value);
        offset += sizeof(short);
    }

    private static void WriteUShort(Span<byte> target, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }
}

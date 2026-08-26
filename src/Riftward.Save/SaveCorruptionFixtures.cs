using System.Security.Cryptography;
using Riftward.Simulation;

namespace Riftward.Save;

/// <summary>
/// Eine Korruptionsklasse der DATENMODELL-Fixturliste mit ihrem Erzeuger und
/// ihrer vertraglich erwarteten Verletzungsklasse. Der Erzeuger schließt das
/// gültige Basisdokument ein und liefert die mutierten Bytes.
/// </summary>
internal sealed record SaveCorruptionCase(string Label, SaveRejectionClass ExpectedClass, Func<byte[]> Build);

/// <summary>
/// Interne Prüfinfrastruktur des Savevertrags (AC-T031-06): erzeugt die
/// unterscheidbaren Korruptionsfixtures je Klasse aus einem gültigen
/// Basisdokument. Sie wird vom savecheck-Lauf und den regulären Tests
/// gemeinsam verwendet, damit Klasse und Erwartung nur an einer Stelle
/// definiert sind; sie begründet keine Produktmigrations- oder
/// Altdatenzusagen. Die Klasse „finalitätsnah gültig“ bleibt gemäß
/// SAVEVERTRAG Abschnitt 11 ausdrücklich der Contentstufe vorbehalten.
/// </summary>
internal static class SaveCorruptionFixtures
{
    /// <summary>Byteebene-Mutationen ohne Zustandsrekonstruktion.</summary>
    public static IReadOnlyList<SaveCorruptionCase> ByteLevelCases(byte[] validDocument)
    {
        var headerLength = (int)SaveDocumentValidator.GetHeaderLength(validDocument);

        var truncated = () => validDocument.AsSpan(0, validDocument.Length - 8).ToArray();

        var wrongPayloadHash = () =>
        {
            var mutated = (byte[])validDocument.Clone();
            // Letztes Kopfbyte liegt im payloadHash-Feld; der metaHash deckt
            // das Feld ab und erkennt die Manipulation getrennt vom Payload.
            mutated[SaveDocumentValidator.PreambleBytes + headerLength - 1] ^= 0x01;
            return mutated;
        };

        var payloadBitFlip = () =>
        {
            var mutated = (byte[])validDocument.Clone();
            mutated[^1] ^= 0x02;
            return mutated;
        };

        var unknownSchemaVersion = () =>
        {
            var mutated = (byte[])validDocument.Clone();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                mutated.AsSpan(SaveContract.MagicLength, sizeof(ushort)),
                (ushort)(SaveContract.CurrentSaveSchemaVersion + 1));
            return mutated;
        };

        var magicInvalid = () =>
        {
            var mutated = (byte[])validDocument.Clone();
            mutated[2] = (byte)'X';
            return mutated;
        };

        var trailingByte = () =>
        {
            var mutated = new byte[validDocument.Length + 1];
            validDocument.CopyTo(mutated, 0);
            mutated[^1] = 0x00;
            return mutated;
        };

        var oversized = () =>
        {
            var mutated = (byte[])validDocument.Clone();
            var payloadLengthFileOffset =
                SaveDocumentValidator.PreambleBytes
                + SaveDocumentValidator.HeaderOffsetPayloadLength(
                    mutated.AsSpan(SaveDocumentValidator.PreambleBytes, headerLength));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
                mutated.AsSpan(payloadLengthFileOffset, sizeof(ulong)),
                (ulong)(SaveContract.AbsoluteMaxSaveBytes + 1));
            FixMetaHash(mutated, headerLength);
            return mutated;
        };

        return
        [
            new SaveCorruptionCase("truncated-file", SaveRejectionClass.TruncatedFile, truncated),
            new SaveCorruptionCase("wrong-payload-hash", SaveRejectionClass.MetaIntegrityViolation, wrongPayloadHash),
            new SaveCorruptionCase("payload-bitflip", SaveRejectionClass.PayloadIntegrityViolation, payloadBitFlip),
            new SaveCorruptionCase("unknown-schema-version", SaveRejectionClass.SchemaVersionUnsupported, unknownSchemaVersion),
            new SaveCorruptionCase("magic-invalid", SaveRejectionClass.MagicInvalid, magicInvalid),
            new SaveCorruptionCase("canonical-order", SaveRejectionClass.CanonicalViolation, trailingByte),
            new SaveCorruptionCase("oversize-save", SaveRejectionClass.SizeLimitExceeded, oversized),
        ];
    }

    /// <summary>
    /// Zustandsebene-Mutationen: sie dekodieren das Basisdokument, ändern den
    /// Relevantzustand kontrolliert und bauen ein neu gehashtes Dokument,
    /// damit die Zielklasse (Referenz beziehungsweise Grenzwert) isoliert
    /// geprüft wird statt zufällig bereits durch einen Hashfehler.
    /// </summary>
    public static IReadOnlyList<SaveCorruptionCase> StateLevelCases(
        byte[] validDocument,
        ulong commandPlanHash,
        string buildId)
    {
        var (_, loaded) = SaveDocumentValidator.Validate(validDocument);

        if (loaded is null)
        {
            throw new InvalidOperationException("Korruptionsfixtures benötigen ein gültiges Basisdokument.");
        }

        var unwalkableTile = FindUnwalkableTile();

        // Fehlende Referenz: Agentenposition wird auf eine unpassierbare
        // Kachel versetzt (Positionen sind stets begehbar; Wegpunkte ebenso).
        var missingReference = () =>
        {
            var state = CloneWith(loaded.State);
            state.PositionXQ16[7] =
                ((unwalkableTile % NavWorld.TilesX) * NavWorld.TileSizeQ16)
                + (NavWorld.TileSizeQ16 >> 1);
            state.PositionYQ16[7] =
                ((unwalkableTile / NavWorld.TilesX) * NavWorld.TileSizeQ16)
                + (NavWorld.TileSizeQ16 >> 1);

            return CanonicalSaveCodec.WriteDocument(
                state, loaded.SnapshotStateHash, commandPlanHash, buildId, SaveEnvelopeMetadata.CreateFresh());
        };

        var limitViolation = () =>
        {
            var state = CloneWith(loaded.State);
            state.PositionXQ16[3] = -1234567L;

            return CanonicalSaveCodec.WriteDocument(
                state, loaded.SnapshotStateHash, commandPlanHash, buildId, SaveEnvelopeMetadata.CreateFresh());
        };

        return
        [
            new SaveCorruptionCase("missing-reference", SaveRejectionClass.ReferenceInvalid, missingReference),
            new SaveCorruptionCase("limit-violation", SaveRejectionClass.LimitViolation, limitViolation),
        ];
    }

    /// <summary>Berechnet den metaHash eines Dokuments nach Kopfänderung neu.</summary>
    public static void FixMetaHash(byte[] document, int headerLength)
    {
        var metaHash = SHA256.HashData(document.AsSpan(0, SaveDocumentValidator.PreambleBytes + headerLength));
        metaHash.CopyTo(document, SaveDocumentValidator.PreambleBytes + headerLength);
    }

    private static int FindUnwalkableTile()
    {
        for (var tile = 0; tile < NavWorld.TileCount; tile++)
        {
            if (!NavWorld.IsWalkableIndex(tile))
            {
                return tile;
            }
        }

        throw new InvalidOperationException("Welt besitzt keine unpassierbare Kachel für die Korruptionsmatrix.");
    }

    private static SimSaveState CloneWith(SimSaveState state)
    {
        return state with
        {
            TargetZoneByGroup = state.TargetZoneByGroup.ToArray(),
            PositionXQ16 = state.PositionXQ16.ToArray(),
            PositionYQ16 = state.PositionYQ16.ToArray(),
            VelocityXQ16 = state.VelocityXQ16.ToArray(),
            VelocityYQ16 = state.VelocityYQ16.ToArray(),
            GoalTile = state.GoalTile.ToArray(),
            Group = state.Group.ToArray(),
            PathState = state.PathState.ToArray(),
            PlannedZone = state.PlannedZone.ToArray(),
            WaypointCursor = state.WaypointCursor.ToArray(),
            WaypointCount = state.WaypointCount.ToArray(),
            PendingWaypoints = state.PendingWaypoints.Select(tail => tail.ToArray()).ToArray(),
        };
    }
}

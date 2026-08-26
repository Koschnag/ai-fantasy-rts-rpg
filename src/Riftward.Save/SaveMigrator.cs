namespace Riftward.Save;

/// <summary>Ergebnis einer Migration auf Kopie.</summary>
public sealed record MigrationOutcome
{
    public required bool Success { get; init; }

    public SaveRejection? Rejection { get; init; }

    /// <summary>Ergebnisbytes auf der Kopie; bei Fehlschlag null. Der Originalstand bleibt stets unberührt.</summary>
    public byte[]? MigratedBytes { get; init; }

    /// <summary>Ausgeführte Schritte in Reihenfolge (Diagnose, Format „von→nach“).</summary>
    public required IReadOnlyList<string> AppliedSteps { get; init; }
}

/// <summary>
/// Migrationsregel des Savevertrags Abschnitt 8: <c>saveSchemaVersion</c> ist
/// streng monoton; unbekannte frühere und zukünftige Versionen werden
/// kontrolliert abgewiesen, ohne eine Migration zu erfinden oder still zu
/// verwerfen. Echte Schritte laufen ausschließlich auf Kopien, schrittweise,
/// validieren nach jedem Schritt und sind idempotent; ein Fehler erhält den
/// Originalstand.
///
/// Das Produkt registriert derzeit keinen Schritt (genau Version 1). Die für
/// AC-T031-07 erforderlichen synthetischen Zwei-Version-Fixtures sind reine
/// interne Testinfrastruktur (<c>RegisterStepForTests</c>) und begründen
/// keinerlei Produktmigrations- oder Altdatenzusagen.
/// </summary>
public sealed class SaveMigrator
{
    private readonly Dictionary<int, (int ToVersion, Func<byte[], byte[]> StepOnCopy)> _steps = new();

    /// <summary>Produktmigrator ohne erfundenen Migrationsschritt.</summary>
    public static SaveMigrator Product { get; } = new();

    /// <summary>Interne Testinfrastruktur: registriert einen synthetischen Migrationsschritt von einer zur anderen Version.</summary>
    internal void RegisterStepForTests(int fromVersion, int toVersion, Func<byte[], byte[]> stepOnCopy) =>
        _steps[fromVersion] = (toVersion, stepOnCopy);

    /// <summary>Liest die Schemaversion allein aus dem Framing (Magic plus Versionsfeld).</summary>
    public static bool TryReadSchemaVersion(ReadOnlySpan<byte> file, out ushort version)
    {
        version = default;

        if (file.Length < CanonicalSaveCodec.PreambleBytes - sizeof(uint)
            || file[0] != SaveContract.Magic0
            || file[1] != SaveContract.Magic1
            || file[2] != SaveContract.Magic2
            || file[3] != SaveContract.Magic3)
        {
            return false;
        }

        version = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            file.Slice(SaveContract.MagicLength, sizeof(ushort)));
        return true;
    }

    /// <summary>
    /// Migriert eine Kopie des Originalstands zur aktuellen Version. Für ein
    /// Dokument der aktuellen Version ist das Ergebnis byteidentisch zum
    /// Original (Idempotenz); fehlende Schritte werden nie erfunden.
    /// </summary>
    public MigrationOutcome MigrateToCurrentVersionOnCopy(byte[] originalBytes)
    {
        ArgumentNullException.ThrowIfNull(originalBytes);

        if (!TryReadSchemaVersion(originalBytes, out var version))
        {
            return new MigrationOutcome
            {
                Success = false,
                Rejection = new SaveRejection(SaveRejectionClass.MagicInvalid, "Dokument trägt nicht das Vertragsmagic."),
                AppliedSteps = Array.Empty<string>(),
            };
        }

        if (version == SaveContract.CurrentSaveSchemaVersion)
        {
            return new MigrationOutcome
            {
                Success = true,
                MigratedBytes = originalBytes,
                AppliedSteps = Array.Empty<string>(),
            };
        }

        var working = originalBytes.ToArray();
        var applied = new List<string>();

        while (version != SaveContract.CurrentSaveSchemaVersion)
        {
            if (!_steps.TryGetValue(version, out var step))
            {
                return new MigrationOutcome
                {
                    Success = false,
                    Rejection = new SaveRejection(
                        SaveRejectionClass.SchemaVersionUnsupported,
                        $"Schemaversion {version} besitzt keinen registrierten Migrationsschritt; es wird keine Migration erfunden."),
                    AppliedSteps = applied,
                };
            }

            try
            {
                working = step.StepOnCopy(working);
            }
            catch (Exception exception)
            {
                return new MigrationOutcome
                {
                    Success = false,
                    Rejection = new SaveRejection(
                        SaveRejectionClass.CanonicalViolation,
                        $"Migrationsschritt {version}→{step.ToVersion} schlug kontrolliert fehl: {exception.Message}"),
                    AppliedSteps = applied,
                };
            }

            applied.Add($"{version}→{step.ToVersion}");
            version = (ushort)step.ToVersion;

            // Nach jedem Schritt wird die Kopie vollständig validiert;
            // ein ungültiges Zwischenergebnis erhält den Originalstand.
            if (version == SaveContract.CurrentSaveSchemaVersion)
            {
                var (rejection, _) = SaveDocumentValidator.Validate(working);

                if (rejection is not null)
                {
                    return new MigrationOutcome
                    {
                        Success = false,
                        Rejection = new SaveRejection(
                            rejection.Class,
                            $"Migrationsergebnis verletzt den Savevertrag ({rejection.Detail})"),
                        AppliedSteps = applied,
                    };
                }
            }
        }

        return new MigrationOutcome { Success = true, MigratedBytes = working, AppliedSteps = applied };
    }
}

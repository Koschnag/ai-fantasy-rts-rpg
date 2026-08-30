namespace Riftward.App.Package;

/// <summary>
/// Versionierte Vertragskonstanten des Paketvertrags
/// <c>docs/PAKETVERTRAG.md</c> V1 (T-038, genau linux-x64). Alle Werte sind
/// vertraglich gebunden; Änderungen sind eine neue Vertragsversion.
/// </summary>
public static class PackageContract
{
    /// <summary>Schemaversion des Paketmanifests.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Vertragskennung; wird im Manifest gebunden und vom Prüfreport erwartet.</summary>
    public const string ContractId = "riftward-paketvertrag-v1";

    /// <summary>Paketkennung der internen Alpha.</summary>
    public const string PackageId = "riftward-alpha";

    /// <summary>Einziger unterstützter RID dieses Slices.</summary>
    public const string SupportedRid = "linux-x64";

    /// <summary>Ehrliche Alpha-Kennzeichnung mit Aussagegrenze Graybox.</summary>
    public const string AlphaMarker = "internal-alpha-graybox-v1";

    /// <summary>Gebundene Runtimeform: selbstenthalten, ohne AOT und Trimming.</summary>
    public const string RuntimeForm = "self-contained-coreclr-no-aot-no-trimming-v1";

    /// <summary>
    /// Fixiertes SOURCE_DATE_EPOCH = Lockfile-<c>generatedAtUtc</c>
    /// (identisch zum Native-Build); deterministische Tar-Einträge.
    /// </summary>
    public const long SourceDateEpoch = 1786623387L;

    /// <summary>Basis der Version <c>0.1.0-alpha.&lt;tree8&gt;</c>.</summary>
    public const string VersionBase = "0.1.0-alpha.";

    /// <summary>Präfix des Archivnamens und des Wurzelverzeichnisses.</summary>
    public const string ArchiveRootPrefix = "riftward-";

    /// <summary>Archivsuffix ohne RID.</summary>
    public const string ArchiveRootSuffix = "-linux-x64";

    /// <summary>Vertragspfad des gebündelten Native-Artefaktmanifests.</summary>
    public const string NativeManifestTargetPath = "native/artifact-hashes.json";

    /// <summary>Vertragsverzeichnis der gebündelten Bestandsfixtures.</summary>
    public const string FixtureTargetDir = "fixtures/command";

    /// <summary>Dateiname des Paketmanifests.</summary>
    public const string ManifestFileName = "package-manifest.json";

    /// <summary>Dateiname des Paketankers (sha256sum-Form über das Manifest).</summary>
    public const string AnchorFileName = "package-manifest.sha256";

    /// <summary>Vertragspfad der Release Notes im Paket.</summary>
    public const string ReleaseNotesTargetPath = "docs/RELEASE_NOTES.md";

    /// <summary>Vertragspfad des Lizenz-/Attributionsmanifests im Paket.</summary>
    public const string LicensesTargetPath = "docs/LIZENZEN.md";

    /// <summary>Bestandscodes der Host-Artefaktprüfung, die der Schutzabschnitt bindet.</summary>
    public static readonly IReadOnlyList<int> ProtectionExitCodes = new[] { 14, 15, 16, 17 };

    /// <summary>Quellrelativer Pfad des Native-Artefaktmanifests (gitignoriert).</summary>
    public static readonly string NativeManifestSourcePath =
        Path.Combine(".ai", "runtime", "cache", "native", "artifact-hashes.json");

    /// <summary>Quellrelativer Pfad des Native-Dist (gitignoriert).</summary>
    public static readonly string NativeDistSourceDir =
        Path.Combine(".ai", "runtime", "cache", "native", "dist");

    /// <summary>Präfix der Manifestschlüssel im Native-Dist; wird deterministisch umgeschrieben.</summary>
    public const string NativeManifestSourcePrefix = ".ai/runtime/cache/native/dist/";

    /// <summary>Quellverzeichnis der gebündelten Bestandsfixtures.</summary>
    public static readonly string FixtureSourceDir = Path.Combine("tests", "fixtures", "command");

    /// <summary>Kennung des gebundenen Schutzmechanismus (bestehende Host-Prüfung).</summary>
    public const string ProtectionKind = "host-artifact-manifest-check-v1";

    /// <summary>Unix-Modus ausführbarer Dateien im Archiv (verbindlich fixiert).</summary>
    public const string UnixModeExecutable = "0755";

    /// <summary>Unix-Modus regulärer Dateien im Archiv (verbindlich fixiert).</summary>
    public const string UnixModeRegular = "0644";

    /// <summary>Unix-Modus Verzeichnisse im Archiv (verbindlich fixiert).</summary>
    public const string UnixModeDirectory = "0755";

    /// <summary>Unix-Modus Symlinks im Archiv (verbindlich fixiert).</summary>
    public const string UnixModeSymlink = "0777";
}

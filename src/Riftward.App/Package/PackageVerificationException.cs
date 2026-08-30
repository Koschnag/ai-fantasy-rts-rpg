namespace Riftward.App.Package;

/// <summary>Unterscheidbare Verletzungsklassen der Paketverifikation.</summary>
public enum PackageViolationClass
{
    ManifestMissing,
    ManifestMalformed,
    ManifestHashInvalid,
    AnchorMissing,
    AnchorMismatch,
    SideCarMissing,
    SideCarMismatch,
    EntryMissing,
    EntryIncomplete,
    EntryHashMismatch,
    EntrySymlinkMismatch,
    UnmanifestedFile,
}

/// <summary>Vertragskennung einer Verletzungsklasse im maschinenlesbaren Report.</summary>
public static class PackageViolationClassNames
{
    /// <summary>Liefert die vertragliche, maschinenlesbare Kennung der Klasse.</summary>
    public static string Of(PackageViolationClass violationClass) => violationClass switch
    {
        PackageViolationClass.ManifestMissing => "MANIFEST_MISSING",
        PackageViolationClass.ManifestMalformed => "MANIFEST_MALFORMED",
        PackageViolationClass.ManifestHashInvalid => "MANIFEST_HASH_INVALID",
        PackageViolationClass.AnchorMissing => "ANCHOR_MISSING",
        PackageViolationClass.AnchorMismatch => "ANCHOR_MISMATCH",
        PackageViolationClass.SideCarMissing => "SIDE_CAR_MISSING",
        PackageViolationClass.SideCarMismatch => "SIDE_CAR_MISMATCH",
        PackageViolationClass.EntryMissing => "ENTRY_MISSING",
        PackageViolationClass.EntryIncomplete => "ENTRY_INCOMPLETE",
        PackageViolationClass.EntryHashMismatch => "ENTRY_HASH_MISMATCH",
        PackageViolationClass.EntrySymlinkMismatch => "ENTRY_SYMLINK_MISMATCH",
        PackageViolationClass.UnmanifestedFile => "UNMANIFESTED_FILE",
        _ => throw new ArgumentOutOfRangeException(nameof(violationClass)),
    };
}

/// <summary>Kontrollierter Verifikationsfehler mit unterscheidbarer Klasse.</summary>
public sealed class PackageVerificationException(string violationClass, string path, string detail)
    : Exception($"{violationClass}: {path}: {detail}")
{
    /// <summary>Maschinenlesbare Klassenkennung.</summary>
    public string ViolationClass { get; } = violationClass;

    /// <summary>Betroffener Pfad (oder Manifestname).</summary>
    public string Path { get; } = path;

    /// <summary>Verstaendlicher Grund ohne Geheimnisse.</summary>
    public string Detail { get; } = detail;

    /// <summary>Erzeugt die Ausnahme aus dem Enum.</summary>
    public static PackageVerificationException Of(
        PackageViolationClass violationClass,
        string path,
        string detail) =>
        new(PackageViolationClassNames.Of(violationClass), path, detail);
}

namespace Riftward.App.Bench;

/// <summary>Pflicht-Hardwareprofile gemaeß docs/PERFORMANCE_BUDGET.md.</summary>
public static class HardwareProfiles
{
    public const string PcMinimum = "hw-pc-min";
    public const string MacMinimum = "hw-mac-min";
    public const string PcHigh = "hw-pc-high";

    public static readonly IReadOnlyList<string> Mandatory = [PcMinimum, MacMinimum, PcHigh];
}

/// <summary>Synthetisch beschreibbare Hardwareklasse fuer Bindungsentscheidungen.</summary>
public sealed record HardwareDescriptor(
    string GpuName,
    string CpuModel,
    bool IsDeveloperWorkstation);

/// <summary>Statuskennung eines Profils im Benchmarkreport.</summary>
public sealed record ProfileStatus(
    string ProfileId,
    string Status,
    string? BoundReferenceClass,
    string Reason)
{
    public const string NotMeasured = "NOT-MEASURED";
    public const string Pass = "PASS";
}

/// <summary>
/// Ehrlichkeitsregel (AC-T020-05, Q-OPS-001-Klaerungsprotokoll): Ein
/// Profilbestehen entsteht nur durch deklarierte Bindung an die zugehoerige
/// Referenzklasse UND benannte Referenzrechner. Entwickler-PC-Laeufe bleiben
/// stets diagnostische Baseline; fehlende Referenzhardware bleibt
/// NOT-MEASURED mit Eskalation statt Ersatz.
/// </summary>
public static class ProfileBinding
{
    public const string ReferenceMachinesUnnamedReason = "reference-hardware-unnamed-qops001";
    public const string DeveloperWorkstationDiagnosticReason = "developer-workstation-diagnostic-baseline";
    public const string BindingMismatchReason = "claimed-class-does-not-match-profile";
    public const string ReferenceHardwareUnavailableReason = "mandatory-profile-not-measured-no-reference-hardware";

    /// <summary>
    /// Entscheidet eine behauptete Bindung. <paramref name="referenceMachinesNamed"/>
    /// ist erst true, wenn die Projektleitung konkrete Referenzrechner
    /// benannt hat (Q-OPS-001 bleibt bis dahin OFFEN).
    /// </summary>
    public static ProfileStatus EvaluateClaim(
        string profileId,
        HardwareDescriptor hardware,
        string claimedReferenceClass,
        bool referenceMachinesNamed)
    {
        if (!MatchesClass(profileId, claimedReferenceClass))
        {
            return new ProfileStatus(profileId, ProfileStatus.NotMeasured, null, BindingMismatchReason);
        }

        if (hardware.IsDeveloperWorkstation)
        {
            return new ProfileStatus(profileId, ProfileStatus.NotMeasured, null, DeveloperWorkstationDiagnosticReason);
        }

        if (!referenceMachinesNamed)
        {
            return new ProfileStatus(profileId, ProfileStatus.NotMeasured, null, ReferenceMachinesUnnamedReason);
        }

        return new ProfileStatus(profileId, ProfileStatus.Pass, claimedReferenceClass, "declared-binding");
    }

    /// <summary>Profilstati eines Reports ohne gueltige Referenzbindung.</summary>
    public static IReadOnlyList<ProfileStatus> MandatoryWithoutReferenceHardware() =>
        HardwareProfiles.Mandatory
            .Select(profile => new ProfileStatus(profile, ProfileStatus.NotMeasured, null, ReferenceHardwareUnavailableReason))
            .ToArray();

    /// <summary>Klassen-Signaturpruefung: GPU-Bezeichnung gegen die Referenzklasse des Profils.</summary>
    public static bool MatchesClass(string profileId, string gpuClassClaim)
    {
        var claim = gpuClassClaim.Trim();
        return profileId switch
        {
            HardwareProfiles.PcMinimum => ClassMatches(claim, "gtx 660", "gtx660"),
            HardwareProfiles.MacMinimum => ClassMatches(claim, "apple m1", "m1"),
            HardwareProfiles.PcHigh => ClassMatches(claim, "rx 570", "rx 580", "rx570", "rx580"),
            _ => false,
        };
    }

    private static bool ClassMatches(string claim, params string[] tokens) =>
        tokens.Any(token => claim.Contains(token, StringComparison.Ordinal));
}

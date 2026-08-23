namespace Riftward.Platform;

/// <summary>
/// Kontrollierte Fehlerobjekte an der Prozessgrenze des Plattform-Layers.
/// Kein nativer Typ, kein Absturz: Jeder definierte Fehler besitzt einen
/// stabilen Code und einen verstaendlichen Meldungstext ohne Geheimnisse.
/// </summary>
public enum PlatformErrorCode
{
    None = 0,

    /// <summary>Unspezifischer interner Fehler ohne definierte Fehlerklasse.</summary>
    Internal = 1,

    /// <summary>Erwartete native Artefaktdatei fehlt im Artefaktverzeichnis.</summary>
    ArtifactMissing = 16,

    /// <summary>Artefakt unvollstaendig: Dateigroesse weicht vom Manifest ab.</summary>
    ArtifactIncomplete = 15,

    /// <summary>Artefakthash weicht vom aufgezeichneten Manifest ab.</summary>
    ArtifactHashMismatch = 17,

    /// <summary>Artefaktmanifest fehlt oder ist nicht lesbar.</summary>
    ArtifactManifestInvalid = 14,

    /// <summary>Natives Backend/GPU-Kontext konnte nicht initialisiert werden.</summary>
    BackendInitFailed = 18,

    /// <summary>Fenster oder Video-Subsystem konnte nicht initialisiert werden.</summary>
    WindowFailed = 19,

    /// <summary>Ressourcen-Freigabe in falscher Reihenfolge (z. B. Shutdown vor Ressourcen).</summary>
    WrongShutdownOrder = 20,

    /// <summary>Ungueltiges bzw. bereits freigegebenes Handle verwendet.</summary>
    InvalidHandle = 21,

    /// <summary>Plattform wird von diesem Build nicht unterstuetzt.</summary>
    UnsupportedPlatform = 22,

    /// <summary>Smoke endete, ohne dass ein fehlerfreier Frame gerendert wurde.</summary>
    SmokeNoFrame = 23,

    /// <summary>Effizienzbudget verletzt; Report wurde dennoch geschrieben.</summary>
    EfficiencyBudgetViolated = 24,
}

/// <summary>Ein einzelner kontrollierter Fehler mit Code, Meldung und Detailpfad.</summary>
public sealed record PlatformError(PlatformErrorCode Code, string Message, string? Detail = null)
{
    public override string ToString() =>
        Detail is null ? $"{Code}: {Message}" : $"{Code}: {Message} ({Detail})";
}

/// <summary>Ausnahmeform eines <see cref="PlatformError"/> fuer Aufrufer, die mit Exceptions arbeiten.</summary>
public sealed class PlatformException : Exception
{
    public PlatformError Error { get; }

    public PlatformException(PlatformError error)
        : base(error.ToString())
    {
        Error = error;
    }
}

/// <summary>
/// Stabile Abbildung kontrollierter Fehler auf Prozess-Exitcodes.
/// Dokumentiert in docs/NATIVE_UNTERBAU.md; Codes sind Teil des oeffentlichen
/// Befehlsvertrags und duerfen nur per dokumentierter Entscheidung aendern.
/// </summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int Usage = 2;
    public const int Internal = 1;

    public static int Map(PlatformErrorCode code) => (int)code;
}

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

    /// <summary>Benchmark-Szenario unbekannt oder noch nicht implementiert; kein Report.</summary>
    BenchScenarioUnavailable = 25,

    /// <summary>Bench-Budget verletzt; Report wurde dennoch geschrieben.</summary>
    BenchBudgetViolated = 26,

    /// <summary>Zwischenmetriken oder Report widersprechen dem Schemavertrag; kein gefälschter Report.</summary>
    TelemetryInvalid = 27,

    /// <summary>Reportpfad ist nicht schreibbar.</summary>
    ReportNotWritable = 28,

    /// <summary>Opt-in Frame-Evidenzartefakt fehlgeschlagen (T-023); Report wurde dennoch geschrieben.</summary>
    FrameArtifactFailed = 29,

    /// <summary>Soak-Zuverlaessigkeitsgate verletzt (T-022); Report wurde dennoch geschrieben und klar als nicht bestanden markiert.</summary>
    SoakGateViolated = 30,

    /// <summary>Soaklauf unvollstaendig oder vorzeitig beendet (T-022); der Teilreport gilt ausdruecklich nicht als Evidenz.</summary>
    SoakRunIncomplete = 31,

    /// <summary>Soak-Szenario unbekannt oder noch nicht implementiert (T-022); kein Report.</summary>
    SoakScenarioUnavailable = 32,

    /// <summary>Save-Gate verletzt (T-031); Report wurde dennoch geschrieben und klar als nicht bestanden markiert.</summary>
    SaveGateViolated = 33,

    /// <summary>Savecheck unvollstaendig oder vorzeitig beendet (T-031); der Teilreport gilt ausdruecklich nicht als Evidenz.</summary>
    SaveRunIncomplete = 34,

    /// <summary>Kommandoschleifen-Gate verletzt (T-032); Report wurde dennoch geschrieben und klar als nicht bestanden markiert.</summary>
    CommandGateViolated = 35,

    /// <summary>Kommandoschleifenlauf unvollstaendig oder vorzeitig beendet (T-032); der Teilreport gilt ausdruecklich nicht als Evidenz.</summary>
    CommandRunIncomplete = 36,

    /// <summary>Kommandoschleifen-Szenario unbekannt oder Eingabeskript unlesbar/malformiert (T-032); kein Report.</summary>
    CommandScenarioUnavailable = 37,

    /// <summary>Opt-in Einzelabgriff der Kommandoschleife fehlgeschlagen (T-032); Report wurde dennoch geschrieben mit captured=false und Grund.</summary>
    CommandCaptureFailed = 38,
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

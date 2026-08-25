using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Abschnitt 0 des Auftrags T-023: fixierte, codegebundene Szenariospezifikation
/// von <c>bench-representative</c>. Alle Ziele je Lastklasse sind ausschliesslich
/// aus der Szenebudgettabelle in docs/PERFORMANCE_BUDGET.md und ADR 006
/// abgeleitet; jede Abweichung nach unten waere eine Verschwaechung und
/// eskaliert, jede nach oben bleibt unzulassig (Auftrag mayEscalate). Die
/// Werte sind die maschinenlesbare Spiegelung der Dokumentgrenzen und werden
/// von einem Test gegen das Dokument gehalten.
/// </summary>
public static class RepresentativeScenario
{
    public const string ScenarioId = BenchScenarios.Representative;

    /// <summary>
    /// Kennung der Graybox-Ladeverteilung: keine Spielinhalte, keine Namen,
    /// keine Fremdbezuege (Clean-Room gemaess docs/CLEAN_ROOM.md).
    /// </summary>
    public const string ContentId = "synthetic-graybox-load-composition";

    // ------------------------------------------------------------------ Lastklassen

    /// <summary>
    /// Gleichzeitig sichtbare, instanzierte, geskinnte Einheiten
    /// (PERFORMANCE_BUDGET.md-Szenebudget: mindestens 350).
    /// </summary>
    public const int VisibleUnitsTarget = 350;

    /// <summary>
    /// Davon genau die vollstaendig simulierten mobilen Agenten aus
    /// Riftward.Simulation (Vertrag: genau 250, unveraendert wiederverwendet).
    /// </summary>
    public const int SimulatedAgents = SimulationContract.AgentCount;

    /// <summary>
    /// Deterministisch skriptgesteuerte Hintergrundakteure ohne Simulation;
    /// Rest auf die Sichtbarkeitsvorgabe (350 - 250).
    /// </summary>
    public const int BackgroundActors = VisibleUnitsTarget - SimulatedAgents;

    /// <summary>
    /// Knochen je normaler sichtbarer Einheit (Szenebudgettabelle: 48);
    /// jede normale Einheit durchlaeuft diesen repraesentativen Animationspfad.
    /// </summary>
    public const int BonesPerNormalUnit = 48;

    /// <summary>Sonne als gerichtetes Licht (Szenebudgettabelle: hoechstens 1).</summary>
    public const int SunLights = 1;

    /// <summary>
    /// Lokale Schattenlichter mit aktiven Schattenpaessen
    /// (Szenebudgettabelle: genau 4 lokale Lichter, selektiv mit Schatten).
    /// </summary>
    public const int LocalShadowLights = 4;

    /// <summary>
    /// Nicht-degenerative Partikelspitze: gleichzeitig transparente Partikel
    /// am Peak (Szenebudgettabelle: bis hoechstens 5000); das Szenario faehrt
    /// den Peak bewusst an die dokumentierte Obergrenze.
    /// </summary>
    public const int ParticlePeakTarget = 5000;

    /// <summary>Kantennaenge einer Schattenkarte je lokalem Licht (Graybox-Wahl).</summary>
    public const int ShadowMapSizePixels = 512;

    // ------------------------------------------------------------------ Taktung

    /// <summary>
    /// Deterministische Tick-Zuordnung: alle so viele Frames wird genau ein
    /// fester 20-Hz-Simulationstick ausgefuehrt (Renderentkopplung gemaeß
    /// SIMULATIONSVERTRAG V1). Der Fortschritt haengt an der Framezahl, nie
    /// an der Uhr; identische Framezahl bedeutet identische Tickfolge und
    /// damit Hashkettenklasse K2 ueber Fresh-Prozesslaeufe.
    /// </summary>
    public const int FramesPerSimTick = 2;

    public const int DefaultWarmupFrames = 240;
    public const int DefaultSampleFrames = 1200;

    /// <summary>RSS-Stichprobenintervall je Frames (T-020-Praezedenz).</summary>
    public const int RssSampleIntervalFrames = 30;

    /// <summary>Hashketten-Stichprobenintervall je Simulationsticks.</summary>
    public const int HashSampleIntervalTicks = 30;

    /// <summary>
    /// Fester Captureframeindex strikt nach dem Messfenster (Warm-up plus
    /// Messframes plus definiertem Vorlauf); ohne opt-in Flag entsteht kein Bild.
    /// </summary>
    public const int CaptureLeadFrames = 30;

    public static int TotalFrames(int warmupFrames, int sampleFrames) => warmupFrames + sampleFrames;

    public static int WarmupTicks(int warmupFrames) =>
        (warmupFrames + FramesPerSimTick - 1) / FramesPerSimTick;

    public static int SampleTicks(int warmupFrames, int sampleFrames)
    {
        var totalTicks = TotalTicks(warmupFrames, sampleFrames);
        return totalTicks - WarmupTicks(warmupFrames);
    }

    public static int TotalTicks(int warmupFrames, int sampleFrames) =>
        TotalFrames(warmupFrames, sampleFrames) / FramesPerSimTick;

    public static int CaptureFrameIndex(int warmupFrames, int sampleFrames) =>
        TotalFrames(warmupFrames, sampleFrames) + CaptureLeadFrames;

    // ------------------------------------------------------------------ Budgetgate

        /// <summary>
    /// Dokumentierte Grenzwerte des integrierten Budgetgates. Quellen:
    /// PERFORMANCE_BUDGET.md (33,3 ms Minimumprofilframe, GPU 14 ms Ziel/
    /// 30 ms hart, Tick 8 ms Ziel/16 ms hart, 1200 Draws, 2 Mio. sichtbare
    /// Dreiecke Low ohne Schattenwiederholung, 5000 Partikel, 1+4 Lichter),
    /// AC-T010-07/T-020-Praezedenz (Allokationen hoechstens 1 KiB je warmem
    /// Frame) sowie die Prozess-Arbeitssatzzeile des Dokuments. Keine
    /// Veraenderung dieser Werte ohne dokumentierte Entscheidung.
    /// </summary>
    public sealed record BudgetLimits(
        double P99FrameTimeLimitMs = 33.3,
        double P99GpuTimeHardLimitMs = 30.0,
        double P99GpuTimeTargetMs = 14.0,
        double P99TickTimeHardLimitMs = 16.0,
        double P99TickTimeTargetMs = 8.0,
        double ManagedAllocationsPerWarmFrameLimitBytes = 1024.0,
        long DrawSubmitCallsPerFrameLimit = 1200,
        long VisibleTrianglesMainViewLimit = 2_000_000,
        long ConcurrentParticlesLimit = ParticlePeakTarget,
        long SunLightsMax = SunLights,
        long LocalShadowLightsMax = LocalShadowLights,
        bool RuntimeShaderCompilationAllowed = false,
        long WorkingSetTargetMiB = BudgetDocumentation.WorkingSetTargetMiB,
        long WorkingSetHardLimitMiB = BudgetDocumentation.WorkingSetHardLimitMiB)
    {
        public static BudgetLimits Documented { get; } = new();
    }

    /// <summary>Referenzkonstanten der Dokumentgrenzen (Spiegel fuer Tests).</summary>
    public static class BudgetDocumentation
    {
        /// <summary>Zielzeile „Prozess-Arbeitssatz PC 3,0–3,5 GB“ (T-010-/T-020-MB≈MiB-Praezedenz).</summary>
        public const long WorkingSetTargetMiB = 3500;

        /// <summary>Harte Zeile „4,5 GB Ladepeak“.</summary>
        public const long WorkingSetHardLimitMiB = 4500;
    }
}

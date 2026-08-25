module SoakTests

open System
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text.Json
open System.Text.Json.Nodes
open Riftward.App
open Riftward.App.Bench
open Riftward.App.Soak
open Riftward.Platform
open Riftward.Simulation

let private defaultSeed = 20260824u

let private ThirdPercentileOf (series: SoakWindowSeries) thirdIndex =
    SoakWindowSeries.ThirdPercentile(series.WindowP50Ms, thirdIndex, 0.99)

/// Injizierbare Sofort-Taktquelle: Pacingentscheidungen werden sofort
/// erfuellt, ohne echte Wanduhrzeit zu verbrauchen (kein 8-h-Warten im Test).
type internal InstantPacingClock() =
    let mutable elapsed = 0.0
    member _.Elapsed = elapsed

    interface ITickPacingClock with
        member _.Start() = elapsed <- 0.0
        member _.ElapsedSeconds = elapsed

        member _.WaitUntil(target) =
            if target > elapsed then
                elapsed <- target

let private engineOptions paced horizon watchdogWindow =
    SoakEngineOptions(
        Seed = defaultSeed,
        TotalTicks = horizon,
        Paced = paced,
        WarmupTicks = SoakPlan.WarmupTicks,
        WindowSeconds = 1,
        WatchdogWindowSeconds = watchdogWindow,
        HashSampleIntervalTicks = 300L,
        StrictAllocationVerificationTicks = 200
    )

let private chainOf (result: SoakExecutionResult) =
    [ for index in 0 .. result.ChainSampleCount - 1 ->
          (result.ChainSampleTicks.[index], result.ChainSampleHashes.[index]) ]

/// Planmathematik: Horizont, Fenster und Kettenkapazitaet sind reine Funktionen (AC-T022-03/07).
let soakPlanMathIsPureAndContractBound () =
    if SoakPlan.RequiredWallSeconds <> 28800.0 then
        failwith "Autoritative Mindestdauer ist nicht exakt 8 h."

    if SoakPlan.AuthoritativeTickCount <> 576000L then
        failwith "Autoritativer Tickhorizont entspricht nicht 8 h bei 20 Hz."

    if SoakPlan.WindowTickStride(30) <> 600L then
        failwith "Fensterstride bei 30 s falsch."

    if SoakPlan.WindowCount(576000L, 30) <> 960 then
        failwith "Fensteranzahl des autoritativen Laufs falsch."

    if SoakPlan.WindowCount(1L, 1) <> 1 || SoakPlan.WindowCount(21L, 1) <> 2 then
        failwith "Partielle Fenster werden nicht aufgerundet."

    if SoakPlan.ChainSampleCount(576000L, 36000L) < 18 then
        failwith "Kettenkapazitaet zu klein fuer Start- und Endstichprobe."

    try
        SoakPlan.WindowCount(0L, 1) |> ignore
        failwith "Leerer Horizont wurde akzeptiert."
    with :? ArgumentOutOfRangeException ->
        ()

/// Watchdog erkennt Stalls nur jenseits des Fensters und misst Gap-Maxima (AC-T022-05).
let progressWatchdogDetectsStallsWithInjectedClock () =
    let watchdog = ProgressWatchdog(10.0, 64)
    watchdog.Reset(0.0, 0L)

    // Regulaerer Fortschritt: keine Stallmeldung innerhalb des Fensters.
    for second in 1..10 do
        watchdog.Observe(float second, int64 (second / 2))

        if watchdog.IsStalled(float second) then
            failwith $"Fortschrittslauf wurde als Stall gemeldet (t={second})."

    // Stillstand: exakt am Limit noch kein Stall, danach fail-closed.
    watchdog.Observe(20.0, 5L)

    if watchdog.IsStalled(20.0) then
        failwith "Exakt am Fensterlimit wurde bereits Stall gemeldet."

    if not (watchdog.IsStalled(20.001)) then
        failwith "Stillstand jenseits der Fensterbreite wurde nicht erkannt."

    if watchdog.SecondsWithoutProgress(25.0) < 5.0 then
        failwith "Stalldauer wurde nicht nachgefuehrt."

    // Groesster Fortschrittssprung-Abstand wird beobachtet.
    watchdog.Observe(26.0, 6L)

    if watchdog.MaxObservedProgressGapSeconds < 6.0 then
        failwith "Maximale Fortschrittsluecke wurde unterschritten."

    // Erste Beobachtung ohne Reset startet sauber.
    let fresh = ProgressWatchdog(5.0, 8)
    fresh.Observe(100.0, 7L)

    if fresh.IsStalled(104.999) || fresh.IsStalled(105.0) then
        failwith "Erste Beobachtung darf keinen Stall melden."

    if not (fresh.IsStalled(105.001)) then
        failwith "Watchdog nach Erstbeobachtung armiert nicht."

    try
        ProgressWatchdog(0.0, 8) |> ignore
        failwith "Nichtpositives Fenster wurde akzeptiert."
    with :? ArgumentOutOfRangeException ->
        ()

/// Rauschanalyse liefert Trend, Swing und Residuenkennzahlen deterministisch (Abschnitt 0).
let memoryAnalysisSeparatesTrendFromNoise () =
    let flat = [| for _ in 1..60 -> 48000L |]
    let flatNoise = SoakMemoryAnalysis.Analyse(flat)

    if flatNoise.SwingKiB <> 0L || abs flatNoise.SlopeKiBPerWindow > 1e-9 then
        failwith "Konstante Serie wurde als Wachstum interpretiert."

    let leaky = [| for index in 0..59 -> 48000L + int64 (2 * index) |]
    let leakNoise = SoakMemoryAnalysis.Analyse(leaky)

    if abs (leakNoise.SlopeKiBPerWindow - 2.0) > 1e-6 then
        failwith $"Linearer Leak wurde nicht als Steigung 2 erkannt: {leakNoise.SlopeKiBPerWindow}."

    if leakNoise.MaxAbsResidualKiB > 1e-6 then
        failwith "Perfekt lineare Serie erzeugte Residuen."

    let noisy = [| for index in 0..59 -> 48000L + int64 ((index * 7919) % 13) - 6L |]
    let noisyAnalysis = SoakMemoryAnalysis.Analyse(noisy)

    if abs noisyAnalysis.SlopeKiBPerWindow > 0.5 then
        failwith "Reines Rauschen wurde als Trend fehlinterpretiert."

    if
        noisyAnalysis.MedianAbsResidualKiB <= 0.0
        || noisyAnalysis.MaxAbsResidualKiB < noisyAnalysis.MedianAbsResidualKiB
    then
        failwith "Residuenkennzahlen sind inkonsistent."

    // Drittelsteigungen: erste und letzte Stunde getrennt auswertbar.
    let rising = [| for index in 0..89 -> 10000L + int64 index |]

    if
        SoakMemoryAnalysis.ThirdSlope(rising, 0) <> 1.0
        || SoakMemoryAnalysis.ThirdSlope(rising, 2) <> 1.0
    then
        failwith "Drittelsteigung linearer Serie falsch."

    try
        SoakMemoryAnalysis.Analyse([| 1L |]) |> ignore
        failwith "Ein-Fenster-Serie wurde akzeptiert."
    with :? ArgumentException ->
        ()

/// Fensterserie: Percentile stimmen mit der Telemetriemathematik ueberein, Reihenfolge wird erzwungen.
let windowSeriesMatchesTelemetryMathAndEnforcesOrder () =
    let series = SoakWindowSeries(4, 10)

    let randomValues windowIndex =
        [| for tick in 1..10 -> float ((windowIndex * 100 + tick * 37) % 23) * 0.125 |]

    for window in 0..3 do
        let values = randomValues window

        series.CloseWindow(window, 50000L + int64 window, 0L, ReadOnlySpan<double>(values))

    if series.Count <> 4 then
        failwith "Fensterzahl falsch."

    for window in 0..3 do
        let values = List.ofArray (randomValues window)

        let expected = TelemetryMath.Percentile(values, 0.99)
        let actual = series.WindowP99Ms.[window]

        if Math.Round(expected, 12) <> Math.Round(actual, 12) then
            failwith $"p99 weicht von TelemetryMath ab (Fenster {window})."

    // Drittelaggregation entspricht dem sortierten Referenzwert
    // (Formel wie im Produktionscode: Drittelgrenzen per ganzzahliger Skalierung).
    let total = series.Count
    let start = int (int64 total * 1L / 3L)
    let finish = int (int64 total * 2L / 3L)

    let expectedMiddleThird =
        [| for index in start .. max start (finish - 1) -> series.WindowP50Ms.[index] |]
        |> Array.sort
        |> Array.head

    let actualMiddleThird = ThirdPercentileOf series 1

    if Math.Round(expectedMiddleThird, 12) <> Math.Round(actualMiddleThird, 12) then
        failwith "Drittelaggregation weicht vom sortierten Referenzwert ab."

    try
        series.CloseWindow(2, 0L, 0L, ReadOnlySpan<double>([| 1.0 |]))
        failwith "Fenster ausserhalb der Reihenfolge wurde akzeptiert."
    with :? InvalidOperationException ->
        ()

/// Engine: getakteter Lauf (injizierte Sofortuhr) und beschleunigter Lauf erzeugen identische Praefixketten (AC-T022-07).
let pacingIndependenceIdenticalChainsBetweenModes () =
    let horizon = 700L

    let instantRun =
        SoakEngine.Run(engineOptions true horizon 0.5, InstantPacingClock())

    let acceleratedRun =
        SoakEngine.Run(engineOptions false horizon 0.5, InstantPacingClock())

    if chainOf instantRun <> chainOf acceleratedRun then
        failwith "Getakteter und beschleunigter Kurzlauf liefern unterschiedliche Ketten."

    if instantRun.EndStateHash <> acceleratedRun.EndStateHash then
        failwith "Endhash haengt an der Taktung."

    if
        instantRun.MeasuredTicksExecuted <> horizon
        || acceleratedRun.MeasuredTicksExecuted <> horizon
    then
        failwith "Horizont wurde nicht vollstaendig ausgefuehrt."

    // Kettenstichproben liegen an den vereinbarten Ticks inklusive Ende.
    let lastTick, lastHash = List.last (chainOf instantRun)

    if lastTick <> int64 SoakPlan.WarmupTicks + horizon then
        failwith "Endstichprobe liegt nicht am Endtick."

    if lastHash <> instantRun.EndStateHash then
        failwith "Endstichprobe stimmt nicht mit dem Endhash ueberein."

/// Engine: fremder Seed aendert Ketten und Endhash nachweislich (AC-T022-07).
let foreignSeedChangesEngineOutcome () =
    let baseline = SoakEngine.Run(engineOptions false 700L 0.5, InstantPacingClock())

    let foreignOptions =
        SoakEngineOptions(
            Seed = defaultSeed + 1u,
            TotalTicks = 700L,
            Paced = false,
            WarmupTicks = SoakPlan.WarmupTicks,
            WindowSeconds = 1,
            WatchdogWindowSeconds = 0.5,
            HashSampleIntervalTicks = 300L,
            StrictAllocationVerificationTicks = 200
        )

    let foreign = SoakEngine.Run(foreignOptions, InstantPacingClock())

    if chainOf baseline = chainOf foreign then
        failwith "Fremder Seed ergab identische Kettenstichproben."

    if baseline.StartStateHash = foreign.StartStateHash then
        failwith "Startzustand haengt nicht am Seed."

/// Engine: genau 250 Agenten, Vertragsbindung, Warm-up hinter dem ersten Planbefehl (AC-T022-03).
let engineBindsSimulationContract () =
    let result = SoakEngine.Run(engineOptions false 700L 0.5, InstantPacingClock())

    if result.World.AgentCount <> SimulationContract.AgentCount then
        failwith "Engine simuliert nicht die vertragliche Agentenzahl."

    if result.World.TickIndex <> int64 SoakPlan.WarmupTicks + 700L then
        failwith "Tickindex endet nicht am Warm-up plus Horizont."

    if
        result.CommandCount
        <> (CommandPlan.Generate(defaultSeed, SoakPlan.WarmupTicks + 700)).Length
    then
        failwith "Planumfang weicht vom Referenzplan ab."

    try
        SoakEngine.Run(
            SoakEngineOptions(
                Seed = defaultSeed,
                TotalTicks = 700L,
                Paced = false,
                WarmupTicks = CommandPlan.FirstCommandTick,
                WindowSeconds = 1,
                WatchdogWindowSeconds = 0.5,
                HashSampleIntervalTicks = 300L,
                StrictAllocationVerificationTicks = 200
            ),
            InstantPacingClock()
        )
        |> ignore

        failwith "Warm-up auf dem ersten Planbefehl wurde akzeptiert."
    with :? ArgumentOutOfRangeException ->
        ()

/// Szenarioregistry des soak-Befehls klassifiziert explizit (AC-T022-02).
let soakScenarioRegistryClassifiesExplicitly () =
    if
        SoakScenarios.Classify(SoakScenarios.Replay)
        <> SoakScenarios.Support.Implemented
    then
        failwith "soak-replay ist nicht implementiert klassifiziert."

    if
        SoakScenarios.Classify(SoakScenarios.Calibration)
        <> SoakScenarios.Support.Implemented
    then
        failwith "soak-calibration ist nicht implementiert klassifiziert."

    if SoakScenarios.Classify("soak-nope") <> SoakScenarios.Support.Unknown then
        failwith "Fremde Szenario-ID wurde nicht abgewiesen."

    if SoakScenarios.Classify(null) <> SoakScenarios.Support.Unknown then
        failwith "Fehlende Szenario-ID wurde nicht als unbekannt behandelt."

/// Exitcodevertrag bleibt stabil; neue soak-spezifische Codes sind dokumentiert (AC-T022-02/10).
let exitCodeMappingIncludesSoakCodes () =
    let expectations =
        [ PlatformErrorCode.SoakGateViolated, 30
          PlatformErrorCode.SoakRunIncomplete, 31
          PlatformErrorCode.SoakScenarioUnavailable, 32 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Soak-Exitcode {expected} ist nicht stabil dokumentiert."

    // Bestehende Bedeutungen bleiben unveraendert.
    if ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable) <> 25 then
        failwith "Bestehender Exitcode 25 wurde veraendert."

    if
        ExitCodes.Map(PlatformErrorCode.TelemetryInvalid) <> 27
        || ExitCodes.Map(PlatformErrorCode.ReportNotWritable) <> 28
    then
        failwith "Bestehende Reportcodes wurden veraendert."

/// rift.sh besitzt den soak-Zweig mit Build-Guard (AC-T022-02).
let riftScriptSoakContractKeepsAppBuildGuard () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let script =
        File.ReadAllText(Path.Combine(findRoot (Environment.CurrentDirectory), "scripts", "rift.sh"))

    let soakIndex = script.IndexOf("  soak)", StringComparison.Ordinal)

    if soakIndex < 0 then
        failwith "rift.sh besitzt keinen soak-Zweig."

    let rec nextTopLevel index =
        let found = script.IndexOf("\n  ", index, StringComparison.Ordinal)

        if found < 0 then
            -1
        elif
            found + 2 < script.Length
            && script[found + 2] <> ' '
            && script[found + 2] <> '\n'
        then
            found
        else
            nextTopLevel (found + 1)

    let nextBranch = nextTopLevel (soakIndex + 1)

    let branchLength =
        (if nextBranch < 0 then script.Length else nextBranch + 1) - soakIndex

    let branch = script.Substring(soakIndex, branchLength)

    if not (branch.Contains("rift_need_app_output", StringComparison.Ordinal)) then
        failwith "soak-Zweig prueft den App-Build nicht (fehlender Build muss kontrolliert scheitern)."

    if not (branch.Contains("Riftward.App.dll\" soak", StringComparison.Ordinal)) then
        failwith "soak-Zweig ruft den Host nicht mit dem soak-Modus auf."

    if not (script.Contains("Exitcode 32", StringComparison.Ordinal)) then
        failwith "Hilfetext nennt den Soak-Szenario-Exitcode 32 nicht."

/// Architekturtest: Uhr- und Schlafprimitiven bleiben in PacingClock.cs, Zustand bleibt uhrfrei (AC-T022-11).
let architectureKeepsSoakClockFreeAndLayered () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let root = findRoot (Environment.CurrentDirectory)
    let soakDirectory = Path.Combine(root, "src", "Riftward.App", "Soak")

    let sourceFiles = Directory.GetFiles(soakDirectory, "*.cs")

    if Array.isEmpty sourceFiles then
        failwith "Soakquellen fehlen."

    for file in sourceFiles do
        let fileName = Path.GetFileName(file)
        let sourceLines = File.ReadAllLines(file)

        let codeOnly =
            sourceLines
            |> Array.map (fun line ->
                let index = line.IndexOf("//", StringComparison.Ordinal)
                if index >= 0 then line.Substring(0, index) else line)

        let source = String.Concat(codeOnly)

        // Keine SDL3-/bgfx-/Interoptypen im Soaklaeufer (AC-T022-11);
        // Exitcode-/Toolchainabbildung folgt dem akzeptierten T-021-Praezedenz.
        for forbidden in [ "SDL"; "bgfx"; "NativeApi"; "SdlSession"; "BgfxDevice"; "BgfxShim" ] do
            if source.Contains(forbidden, StringComparison.Ordinal) then
                failwith $"Verbotener Plattformtyp '{forbidden}' in {fileName}."

        // F# bleibt vom Hotpath fern; der Soaklaeufer ist C#.
        if fileName.EndsWith(".fs", StringComparison.Ordinal) then
            failwith "F#-Quelle im Soakverzeichnis."

        // Kernzahlabhaengigkeit, Umgebungslesezugriffe und Zufall bleiben draussen.
        for forbidden in
            [ "DateTime.Now"
              "ProcessorCount"
              "GetEnvironmentVariable"
              "new Random("
              "Random.Shared"
              "Parallel."
              "Task.Run"
              "TimeZoneInfo" ] do
            if source.Contains(forbidden, StringComparison.Ordinal) then
                failwith $"Verbotener Zustands-/Umgebungszugriff '{forbidden}' in {fileName}."

        // Uhr-/Schlafprimitive ausschliesslich in PacingClock.cs.
        let isClockFile =
            String.Equals(fileName, "PacingClock.cs", StringComparison.Ordinal)

        for token in [ "Stopwatch"; "Thread.Sleep"; "Thread.SpinWait" ] do
            if (not isClockFile) && source.Contains(token, StringComparison.Ordinal) then
                failwith $"Uhrprimitiv '{token}' ausserhalb von PacingClock.cs gefunden ({fileName})."

    // TickTiming wird von der Engine benutzt; die Welt selbst kennt den Soak nicht.
    let simulationDirectory = Path.Combine(root, "src", "Riftward.Simulation")

    for file in Directory.GetFiles(simulationDirectory, "*.cs") do
        let source = File.ReadAllText(file)

        if source.Contains("Soak", StringComparison.Ordinal) then
            failwith "Simulationskern referenziert Soakdetails."

/// Golden-Report des soak-Schemavertrags (Schema-/Contentkennung im Dokument).
let private goldenSoakReport =
    "{\"schemaVersion\":2,\"mode\":\"soak\",\"command\":\"./scripts/rift.sh soak --scenario soak-replay --repo"
    + "rt <PFAD>\",\"scenario\":{\"id\":\"soak-replay\",\"seed\":20260824,\"tickRateHz\":20,\"agentCount\":250,\"worl"
    + "dId\":\"riftward-simworld-graybox-v1\",\"content\":\"synthetic-graybox-movement-world\",\"executionModeI"
    + "d\":\"accelerated-diagnostic-v1\"},\"reliabilityContract\":{\"document\":\"docs/SOAKVERTRAG.md\",\"version"
    + "\":\"2\",\"simulationContractDocument\":\"docs/SIMULATIONSVERTRAG.md\",\"simulationContractVersion\":\"1\","
    + "\"hashAlgorithm\":\"fnv1a64-canonical-chain-v1\",\"commandPlanAlgorithm\":\"xorshift64star-group-script"
    + "-v1\",\"evidenceUnitId\":\"deterministic-full-plan-repetition-v2\",\"minimumEvidenceRepetitions\":3,\"all"
    + "ocationLimitBytesPerWarmTick\":0,\"absoluteGrowthLimitMiB\":16,\"trendLimitKiBPerHour\":1024"
    + ",\"watchdogWindowSeconds\":120,\"windowSeconds\":30,\"calibrationReference\":\"calibration-run-a+calibr"
    + "ation-run-b@1800s-each-2026-08-25\"},\"commandPlan\":{\"algorithm\":\"xorshift64star-group-script-v1\","
    + "\"commands\":40,\"hash\":\"320408b724667ea5\",\"firstCommand\":{\"tick\":240,\"scopeGroup\":0,\"kind\":\"GroupM"
    + "oveToZone\",\"zoneIndex\":2}},\"startedAtUtc\":\"2026-08-25T12:00:00Z\",\"finishedAtUtc\":\"2026-08-25T12:"
    + "00:02Z\",\"environment\":{\"os\":{\"type\":\"Linux\",\"kernelRelease\":\"7.0.0-30-generic\"},\"cpu\":{\"model\":\""
    + "fixture-cpu\"},\"rid\":\"linux-x64\",\"commit\":\"0123456789abcdef0123456789abcdef01234567\",\"buildMode\":"
    + "\"Release\",\"pins\":[{\"id\":\"sdl3\",\"refType\":\"tag\",\"ref\":\"release-3.4.14\",\"commit\":\"147a8ee32dbf9ac0"
    + "2f3794964490687b6bbda1bc\",\"sourceSha256\":\"9d57b178fb297e121ef2605275937b7afaa7cd24d99ce1f95953e6"
    + "9e7a2535d6\",\"licenseSpdx\":\"zlib\"},{\"id\":\"bgfx\",\"refType\":\"commit\",\"ref\":\"35a98dd6453cf25dc75c68e"
    + "233abb400836d5920\",\"commit\":\"35a98dd6453cf25dc75c68e233abb400836d5920\",\"sourceSha256\":\"68ecda67f"
    + "15b43e0b324b338dfe6b49b58bbbc684d2c5a718c674198db15fee4\",\"licenseSpdx\":\"BSD-2-Clause\"},{\"id\":\"bx"
    + "\",\"refType\":\"commit\",\"ref\":\"9e3fadf6f11380031486be704d2ff46ca143664f\",\"commit\":\"9e3fadf6f1138003"
    + "1486be704d2ff46ca143664f\",\"sourceSha256\":\"84740909a73336fa6192f3489cff8ba338b1c525103c291cbf7554"
    + "a77002eb1a\",\"licenseSpdx\":\"BSD-2-Clause\"},{\"id\":\"bimg\",\"refType\":\"commit\",\"ref\":\"371d90098b1fd01"
    + "7cd00205979d5ef74b8c3ed62\",\"commit\":\"371d90098b1fd017cd00205979d5ef74b8c3ed62\",\"sourceSha256\":\"a"
    + "1464cfbbbbbb1712df9231bb5c5442e3728f78110c7072d5145892e428fd937\",\"licenseSpdx\":\"BSD-2-Clause\"}]}"
    + ",\"execution\":{\"evidenceUnit\":false,\"evidenceReason\":\"horizon-shortened-diagnostic\",\"paced\":"
    + "false,\"wallDurationSeconds\":0.731,\"requiredWallSeconds\":28800,\"ticksExecuted\":2480,\"requiredTick"
    + "s\":2480,\"warmupTicks\":480,\"complete\":true,\"isEvidence\":false,\"incompleteReason\":null},\"metrics\":"
    + "{\"workingSetKiB\":{\"measured\":true,\"unit\":\"KiB\",\"method\":\"proc-self-status-vmrss-window-samples\","
    + "\"first\":57188,\"min\":57188,\"max\":57364,\"end\":57364,\"windowMeans\":[57188,57352,57364,57364]},\"mana"
    + "gedAllocationsPerWarmTick\":{\"unit\":\"bytes\",\"method\":\"gc-total-allocated-bytes-precise-delta-per-"
    + "tick-sum\",\"perWarmTick\":0,\"verificationTicks\":1200,\"bursts\":1},\"managedAllocationWindowDeltasDia"
    + "gnostic\":{\"unit\":\"bytes\",\"method\":\"gc-total-allocated-bytes-precise-window-delta-sum-over-warm-t"
    + "icks\",\"perWarmTick\":0,\"gateCoupled\":false,\"windowDeltaBytes\":[0,0,0,0]},\"gcPauseSumMs\":{\"unit\":\""
    + "ms\",\"method\":\"gc-get-total-pause-duration-delta\",\"value\":0},\"gcPauseCount\":{\"unit\":\"count\",\"meth"
    + "od\":\"gc-collection-count-gen0-to2-delta\",\"value\":0},\"activeAgents\":{\"unit\":\"count\",\"method\":\"soa"
    + "-agent-count-fixed\",\"value\":250},\"stateHashChain\":{\"unit\":\"hex64\",\"method\":\"fnv1a64-canonical-ch"
    + "ain-v1\",\"start\":\"10e13faf142094db\",\"intervalSampleTicks\":[],\"intervalHashes\":[],\"end\":\"c8a3717a0"
    + "4cc47a4\"},\"goldenFixture\":{\"emitted\":false,\"path\":\"/home/cong/ki-projekt/src/Riftward.App/Soak/s"
    + "oak-replay-chain-v1.json\",\"sha256\":\"5e1a6a20cb7e46adf2a1f37679a15ab66766decedb3a555480783a3767a4"
    + "dd76\",\"schemaId\":\"riftward-soak-chain-fixture-v1\",\"sampleCount\":18,\"samplesMatched\":1,\"sampleMis"
    + "matches\":0,\"sampleSkipped\":1,\"matched\":true},\"watchdog\":{\"unit\":\"seconds\",\"method\":\"progress-wat"
    + "chdog-tick-index-window\",\"windowSeconds\":120,\"checks\":5,\"maxObservedProgressGapSeconds\":0.273,\"s"
    + "talled\":false},\"tickTimeDriftDiagnostic\":{\"unit\":\"ms\",\"method\":\"stopwatch-tick-delta-per-window-"
    + "percentile-aggregate\",\"gateCoupled\":false,\"beginP50Ms\":0.256,\"beginP95Ms\":0.383,\"beginP99Ms\":0.4"
    + "9,\"middleP50Ms\":0.315,\"middleP95Ms\":0.425,\"middleP99Ms\":0.474,\"endP50Ms\":0.441,\"endP95Ms\":0.53,\""
    + "endP99Ms\":0.567},\"drawSubmitCallsPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-"
    + "renderer\"},\"visibleTrianglesPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-rende"
    + "rer\"},\"gpuTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}},\"qtec010\":{\"s"
    + "tatement\":\"tolerated-benchmark-variance-qtec010-remains-open-not-defined-not-consumed-in-this-ta"
    + "sk\"},\"gate\":{\"limits\":{\"absoluteGrowthLimitMiB\":16,\"trendLimitKiBPerHour\":1024,\"allocationsPerWa"
    + "rmTickLimitBytes\":0,\"watchdogWindowSeconds\":120,\"requiredWallSeconds\":28800,\"requiredTicks\":2000"
    + "},\"violations\":[],\"pass\":true,\"complete\":true},\"profiles\":[{\"id\":\"hw-pc-min\","
    + "\"status\":\"NOT-MEASURED\",\"boundReferenceClass\":null,\"reason\":\"mandatory-profile-not-measured-no-r"
    + "eference-hardware\"},{\"id\":\"hw-mac-min\",\"status\":\"NOT-MEASURED\",\"boundReferenceClass\":null,\"reaso"
    + "n\":\"mandatory-profile-not-measured-no-reference-hardware\"},{\"id\":\"hw-pc-high\",\"status\":\"NOT-MEAS"
    + "URED\",\"boundReferenceClass\":null,\"reason\":\"mandatory-profile-not-measured-no-reference-hardware\""
    + "}],\"baseline\":{\"classification\":\"diagnostic-developer-workstation\",\"protocol\":\"qops001-2026-08-2"
    + "4\"},\"exitCode\":0}"

let private assertHasError (fragment: string) (reportJson: string) (message: string) =
    let errors = SoakReportSchema.Validate(reportJson)

    if errors.Count = 0 then
        failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

    let joined = String.concat "; " errors

    if not (joined.Contains(fragment, StringComparison.Ordinal)) then
        failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

let private repositoryRootSoak =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

/// Soakvertrag: Code-Spiegel und Dokument bleiben konsistent, Kapselgrenzen und Konsistenzbedingung halten (AC-T022-01).
let soakContractMirrorsDocumentedValues () =
    if SoakContract.DocumentPath <> "docs/SOAKVERTRAG.md" then
        failwith "Vertragspfad falsch."

    if SoakContract.ContractVersion <> "2" then
        failwith "Vertragsversion falsch (V2 der Projektleitungsentscheidung 2026-08-25 erwartet)."

    if SoakContract.ReplayScenarioId <> "soak-replay" then
        failwith "Szenariokennung falsch."

    if SoakContract.MinimumEvidenceRepetitions < 3 then
        failwith "Evidenzbuendel benoetigt mindestens drei Wiederholungslaeufe."

    if
        SoakContract.AbsoluteGrowthLimitMiB < 4.0
        || SoakContract.AbsoluteGrowthLimitMiB > 64.0
    then
        failwith "Absoluter Schwellwert verletzt die Auftragkapsel 4 bis 64 MiB."

    if
        SoakContract.WatchdogWindowSeconds < 30.0
        || SoakContract.WatchdogWindowSeconds > 300.0
    then
        failwith "Watchdogfenster verletzt das Auftragband 30 bis 300 Sekunden."

    if not SoakContract.TrendConsistencyHolds then
        failwith "Konsistenzbedingung Trendschwelle mal 8 h kleiner gleich absoluter Schwelle verletzt."

    if
        SoakContract.AllocationLimitBytesPerWarmTick
        <> SimulationContract.AllocationLimitBytesPerWarmTick
    then
        failwith "Allokationsgrenze ist nicht unveraenderlich an Simulationsvertrag V1 gebunden."

    if SoakContract.DiagnosticDriftGateCoupled then
        failwith "Driftfelder muessen gatefrei sein."

    // Das Vertragsdokument existiert und nennt alle maschinenlesbaren Kennungen.
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let document =
        File.ReadAllText(Path.Combine(findRoot (Environment.CurrentDirectory), "docs", "SOAKVERTRAG.md"))

    for identifier in
        [ "16 MiB"
          "1024 KiB/h"
          "120 Sekunden"
          "576000"
          SoakContract.EvidenceUnitId
          SoakContract.Qtec010Statement.Substring(0, 44) ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Vertragsdokument nennt die Kennung {identifier} nicht."

    if not (document.Contains("weder definiert", StringComparison.Ordinal)) then
        failwith "Vertragsdokument bestaetigt die Q-TEC-010-Enthaltsamkeit nicht."

    if document.Contains("tolerierte Benchmarkstreuung von", StringComparison.Ordinal) then
        () // bewusst keine Aussage; Streuung bleibt offen.

/// Gate-Evaluator je Bestehens- und Verletzungsklasse fail-closed inklusive Driftentkopplung (AC-T022-05/06/08).
let soakGateCoversEveryClassFailClosed () =
    let limits = SoakBudgetLimits.Documented

    if
        limits.AbsoluteGrowthLimitMiB <> 16.0
        || limits.TrendLimitKiBPerHour <> 1024.0
        || limits.WatchdogWindowSeconds <> 120.0
    then
        failwith "Gate-Grenzwerte spiegeln den Soakvertrag nicht."

    let makeInputs rssMeasured growth trend strict chain stall paced wall ticks =
        SoakGateInputs(
            RssMeasured = rssMeasured,
            AbsoluteGrowthMiB = growth,
            TrendDeltaKiBPerHour = trend,
            StrictAllocationsPerTickBytes = strict,
            ChainMatched = chain,
            StallDetected = stall,
            Paced = paced,
            WallSeconds = wall,
            TicksExecuted = ticks,
            RequiredTicks = 576000L,
            RequiredWallSeconds = SoakPlan.RequiredWallSeconds
        )

    let passCase =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 true false true 28800.5 576000L)

    if not passCase.Pass || passCase.Violations.Count <> 0 then
        failwith $"Bestehensklasse des Soakgates schlug fehl: {passCase.Violations}"

    let diagnosticRun =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 true false false 2.0 2000L)

    if not diagnosticRun.Pass then
        failwith "Beschleunigter Diagnosemodus darf nicht an der Wanduhrdauer scheitern."

    let growthViolation =
        SoakGate.Evaluate(limits, makeInputs true 16.5 10.0 0.0 true false true 28800.5 576000L)

    if
        growthViolation.Pass
        || not (
            growthViolation.Violations
            |> Seq.exists (fun v -> v.Contains("absolute-growth-mib", StringComparison.Ordinal))
        )
    then
        failwith "Absolutes Wachstum jenseits der Schwelle wurde nicht erkannt."

    let trendViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 1024.5 0.0 true false true 28800.5 576000L)

    if
        trendViolation.Pass
        || not (
            trendViolation.Violations
            |> Seq.exists (fun v -> v.Contains("trend-ki-b-per-hour", StringComparison.Ordinal))
        )
    then
        failwith "Trendverletzung wurde nicht erkannt."

    let allocViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.001 true false true 28800.5 576000L)

    if
        allocViolation.Pass
        || not (
            allocViolation.Violations
            |> Seq.exists (fun v -> v.Contains("managed-allocations-per-warm-tick-bytes", StringComparison.Ordinal))
        )
    then
        failwith "Warm-tick-Allokation ueber der Simulationsvertragsgrenze wurde nicht erkannt."

    let chainViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 false false true 28800.5 576000L)

    if
        chainViolation.Pass
        || not (
            chainViolation.Violations
            |> Seq.exists (fun v -> v.Contains("state-hash-chain-mismatch", StringComparison.Ordinal))
        )
    then
        failwith "Kettenabweichung wurde nicht erkannt."

    let stallViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 true true true 28800.5 576000L)

    if
        stallViolation.Pass
        || not (
            stallViolation.Violations
            |> Seq.exists (fun v -> v.Contains("watchdog-progress-stall", StringComparison.Ordinal))
        )
    then
        failwith "Watchdog-Stall wurde nicht erkannt."

    let durationViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 true false true 28799.0 576000L)

    if
        durationViolation.Pass
        || not (
            durationViolation.Violations
            |> Seq.exists (fun v -> v.Contains("wall-duration-seconds", StringComparison.Ordinal))
        )
    then
        failwith "Unterschrittene Wanduhrdauer wurde nicht erkannt."

    let tickViolation =
        SoakGate.Evaluate(limits, makeInputs true 1.0 10.0 0.0 true false true 28800.5 575999L)

    if
        tickViolation.Pass
        || not (
            tickViolation.Violations
            |> Seq.exists (fun v -> v.Contains("tick-count", StringComparison.Ordinal))
        )
    then
        failwith "Unvollstaendiger Tickhorizont wurde nicht erkannt."

    // Fail-closed bei nicht messbaren Werten.
    for broken in
        [ makeInputs false 1.0 10.0 0.0 true false true 28800.5 576000L
          makeInputs true Double.NaN 10.0 0.0 true false true 28800.5 576000L
          makeInputs true 1.0 Double.NaN 0.0 true false true 28800.5 576000L
          makeInputs true 1.0 10.0 Double.NaN true false true 28800.5 576000L ] do
        if SoakGate.Evaluate(limits, broken).Pass then
            failwith "Nicht messbarer Wert wurde als Bestehen gewertet."

/// Reportvertrag akzeptiert das Golddokument und lehnt Faelschungen ab (AC-T022-02/08).
let soakReportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    let goldenErrors = SoakReportSchema.Validate(goldenSoakReport)

    if goldenErrors.Count > 0 then
        let joined = String.Join("; ", goldenErrors)
        failwith $"Goldenreport wurde abgelehnt: {joined}"

    // Kennzahl ohne Methodenkennung.
    assertHasError
        "workingSetKiB.method"
        (goldenSoakReport.Replace("\"method\":\"proc-self-status-vmrss-window-samples\",", ""))
        "Kennzahl ohne Methodenkennung wurde akzeptiert"

    // Typenfremder Messwert.
    assertHasError
        "ganzzahliger"
        (goldenSoakReport.Replace("\"agentCount\":250", "\"agentCount\":\"250\""))
        "Typenfremder Messwert wurde akzeptiert"

    // Erfundener GPU-Messwert ohne Messquelle.
    assertHasError
        "headless Szenario kann diesen Wert nicht messen"
        (goldenSoakReport.Replace(
            "\"gpuTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
            "\"gpuTimeMs\":{\"measured\":true,\"unit\":\"ms\",\"method\":\"bgfx-stats-gpu-timer-p99\",\"p99\":1.2}"
        ))
        "GPU-Wert ohne Messquelle wurde akzeptiert"

    // Fehlende Diagnosekennzeichnung der Driftfelder.
    assertHasError
        "gateCoupled"
        (goldenSoakReport.Replace("\"gateCoupled\":false,\"beginP50Ms\"", "\"beginP50Ms\""))
        "Fehlende Gatekopplungskennzeichnung wurde akzeptiert"

    // Fehlende Q-TEC-010-Enthaltsamkeit.
    assertHasError
        "qtec010"
        (goldenSoakReport.Replace(
            "\"qtec010\":{\"statement\":\"tolerated-benchmark-variance-qtec010-remains-open-not-defined-not-consumed-in-this-task\"},",
            ""
        ))
        "Fehlende Q-TEC-010-Aussage wurde akzeptiert"

    // Unbekanntes Feld.
    assertHasError
        "unbekanntes Feld"
        (goldenSoakReport.Replace("\"schemaVersion\":2,", "\"schemaVersion\":2,\"extraField\":1,"))
        "Unbekanntes Feld wurde akzeptiert"

    // Fremde Schemaversion.
    assertHasError
        "schemaVersion"
        (goldenSoakReport.Replace("\"schemaVersion\":2", "\"schemaVersion\":3"))
        "Fremde Schemaversion wurde akzeptiert"

    // Grossbuchstaben-Hash verletzt die Kanonform (Wert aus dem Golden gelesen).
    let mutable upperHashCase = ""

    do
        use document = JsonDocument.Parse(goldenSoakReport)

        let startHash: string =
            document.RootElement.GetProperty("metrics").GetProperty("stateHashChain").GetProperty("start").GetString()

        upperHashCase <- startHash.ToUpperInvariant()

        assertHasError
            "Hexwert"
            (goldenSoakReport.Replace("\"start\":\"" + startHash + "\"", "\"start\":\"" + upperHashCase + "\""))
            "Grossbuchstaben-Hash wurde akzeptiert"

    // Beschaedigtes Zwischenartefakt.
    assertHasError "gueltiges JSON" "{beschädigt" "Beschaedigtes Dokument wurde akzeptiert"

    // Eine Referenzemission besitzt eine eigene Diagnoseidentitaet und darf
    // sich weder direkt noch durch manipulierte Reportflags selbst bestaetigen.
    let emission = JsonNode.Parse(goldenSoakReport).AsObject()
    let emissionScenario = emission["scenario"].AsObject()
    let emissionExecution = emission["execution"].AsObject()
    let emissionMetrics = emission["metrics"].AsObject()
    let emissionFixture = JsonObject()

    emissionScenario["executionModeId"] <- JsonValue.Create(SoakContract.ReferenceEmissionDiagnosticModeId)

    emissionExecution["evidenceReason"] <- JsonValue.Create(SoakEvidenceUnit.ReferenceEmissionDiagnosticReason)

    emissionFixture["emitted"] <- JsonValue.Create(true)
    emissionFixture["path"] <- JsonValue.Create("fixture-out.json")
    emissionFixture["sha256"] <- JsonValue.Create(String('a', 64))
    emissionFixture["schemaId"] <- JsonValue.Create(SoakChainFixture.Kind)
    emissionFixture["sampleCount"] <- JsonValue.Create(18)
    emissionFixture["note"] <- JsonValue.Create("emission-mode-reference-run-diagnostic-only")
    emissionMetrics["goldenFixture"] <- emissionFixture

    let validEmission = emission.ToJsonString()
    let validEmissionErrors = SoakReportSchema.Validate(validEmission)

    if validEmissionErrors.Count > 0 then
        let joined = String.Join("; ", validEmissionErrors)
        failwith $"Gueltige diagnostische Referenzemission wurde abgelehnt: {joined}"

    emissionExecution["evidenceUnit"] <- JsonValue.Create(true)
    emissionExecution["isEvidence"] <- JsonValue.Create(true)
    emissionExecution["evidenceReason"] <- null

    assertHasError
        "Referenzemission ist niemals Evidenz"
        (emission.ToJsonString())
        "Selbstbestaetigende Referenzemission wurde akzeptiert"

    // Ein realer, akzeptierter Vollhorizontreport bleibt gueltig; seine
    // Abdeckungs-, Ketten- und Planbehauptungen sind aber nicht einzeln
    // faelschbar.
    let evidenceReport =
        File.ReadAllText(Path.Combine(repositoryRootSoak, "artifacts", "t022", "bundle", "repetition-1.json"))

    let evidenceErrors = SoakReportSchema.Validate(evidenceReport)

    if evidenceErrors.Count > 0 then
        let joined = String.Join("; ", evidenceErrors)
        failwith $"Akzeptierter Vollhorizontreport wurde abgelehnt: {joined}"

    let sparseComparison = JsonNode.Parse(evidenceReport).AsObject()
    let sparseMetrics = sparseComparison["metrics"].AsObject()
    let sparseFixture = sparseMetrics["goldenFixture"].AsObject()
    sparseFixture["samplesMatched"] <- JsonValue.Create(1)
    sparseFixture["sampleSkipped"] <- JsonValue.Create(17)

    assertHasError
        "vollstaendige kanonische Stichprobenabdeckung"
        (sparseComparison.ToJsonString())
        "Ausgeduennter Fixturevergleich wurde als Evidenz akzeptiert"

    for counter in [ "sampleCount"; "samplesMatched"; "sampleMismatches"; "sampleSkipped" ] do
        let oversizedComparison = JsonNode.Parse(evidenceReport).AsObject()
        let oversizedMetrics = oversizedComparison["metrics"].AsObject()
        let oversizedFixture = oversizedMetrics["goldenFixture"].AsObject()
        oversizedFixture[counter] <- JsonValue.Create(int64 Int32.MaxValue + 1L)

        assertHasError
            "vollstaendige kanonische Stichprobenabdeckung"
            (oversizedComparison.ToJsonString())
            $"Uebergrosser Fixturezaehler {counter} loeste keine kontrollierte Ablehnung aus"

    let sparseChain = JsonNode.Parse(evidenceReport).AsObject()
    let chainMetrics = sparseChain["metrics"].AsObject()
    let stateHashChain = chainMetrics["stateHashChain"].AsObject()
    stateHashChain["intervalSampleTicks"].AsArray().RemoveAt(0)
    stateHashChain["intervalHashes"].AsArray().RemoveAt(0)

    assertHasError
        "vollstaendige kanonische Intervallkette"
        (sparseChain.ToJsonString())
        "Ausgeduennte Reportkette wurde als Evidenz akzeptiert"

    let foreignPlan = JsonNode.Parse(evidenceReport).AsObject()
    foreignPlan["commandPlan"].AsObject()["hash"] <- JsonValue.Create("0000000000000000")

    assertHasError
        "Vertragssseed und kanonischen Befehlsplan"
        (foreignPlan.ToJsonString())
        "Fremder Befehlsplan wurde als Evidenz akzeptiert"

/// Golden-Fixture: Schema-/Contentbindung gegen die versionierte Datei (AC-T022-07).
let soakChainFixtureIsBoundToContractAndPlan () =
    let loaded = SoakChainFixture.Load(null)

    if loaded.Seed <> SoakContract.DefaultSeed then
        failwith "Fixture-Seed weicht vom Vertragssseed ab."

    if
        int64 loaded.Samples.Count
        <> (SoakPlan.TotalSimulationTick / SoakPlan.HashSampleIntervalTicks) + 2L
    then
        failwith $"Fixture-Stichprobenzahl unerwartet: {loaded.Samples.Count}"

    if loaded.Samples.[0].Tick <> 0L then
        failwith "Fixture beginnt nicht bei Tick 0."

    if loaded.Samples.[loaded.Samples.Count - 1].Tick <> SoakPlan.TotalSimulationTick then
        failwith "Fixture endet nicht am Simulationstick des autoritativen Plans."

    // Starthash sofort unabhaengig reproduzierbar.
    let world = SimWorld(SoakContract.DefaultSeed)

    if world.ComputeStateHash() <> loaded.Samples.[0].Hash then
        failwith "Starthash der Fixture widerspricht der frischen Welt."

    // Planhash bindet den identischen skriptierten Plan.
    let plan =
        CommandPlan.Generate(SoakContract.DefaultSeed, SoakPlan.WarmupTicks + int SoakPlan.AuthoritativeTickCount)

    if
        CommandPlan.Hash(plan).ToString("x16", CultureInfo.InvariantCulture)
        <> loaded.PlanHashHex
    then
        failwith "Planhash der Fixture widerspricht dem Referenzplan."

    if loaded.Sha256.Length <> 64 then
        failwith "Fixture-SHA256-Bindung fehlt."

    // Der Produktionsloader selbst lehnt sparse Fixtures ab; nicht erst die
    // spaetere Reportvalidierung.
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t022-fixture-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let expectFixtureFormatRejection (name: string) (payload: string) (fragment: string) =
            let path = Path.Combine(temporary, name + ".json")
            File.WriteAllText(path, payload)

            let mutable rejected = false

            try
                SoakChainFixture.Load(path) |> ignore
            with :? FormatException as caught ->
                rejected <- caught.Message.Contains(fragment, StringComparison.Ordinal)

            if not rejected then
                failwith $"Ungueltige Golden-Fixture '{name}' wurde nicht kontrolliert mit '{fragment}' abgelehnt."

        let fixtureNode = JsonNode.Parse(File.ReadAllText(loaded.FilePath)).AsObject()
        let samples = fixtureNode["samples"].AsArray()
        let sparse = JsonArray()
        sparse.Add(samples[0].DeepClone())
        sparse.Add(samples[samples.Count - 1].DeepClone())
        fixtureNode["samples"] <- sparse

        expectFixtureFormatRejection "sparse" (fixtureNode.ToJsonString()) "vollstaendigen kanonischen Stichprobenplan"

        let wrongSamples = JsonNode.Parse(File.ReadAllText(loaded.FilePath)).AsObject()
        wrongSamples["samples"] <- JsonObject()
        expectFixtureFormatRejection "samples-object" (wrongSamples.ToJsonString()) "erwartet Array"

        let wrongKind = JsonNode.Parse(File.ReadAllText(loaded.FilePath)).AsObject()
        wrongKind["kind"] <- JsonValue.Create(1)
        expectFixtureFormatRejection "kind-number" (wrongKind.ToJsonString()) "erwartet String"

        let unknownField = JsonNode.Parse(File.ReadAllText(loaded.FilePath)).AsObject()
        unknownField["unexpected"] <- JsonValue.Create(true)
        expectFixtureFormatRejection "unknown-field" (unknownField.ToJsonString()) "unbekanntes Feld"

        let fixtureText = File.ReadAllText(loaded.FilePath)
        let kindLine = $"\"kind\": \"{SoakChainFixture.Kind}\","

        let duplicateKind =
            fixtureText.Replace(kindLine, kindLine + Environment.NewLine + "  " + kindLine)

        if String.Equals(duplicateKind, fixtureText, StringComparison.Ordinal) then
            failwith "Duplicate-Fixture fuer den Regressionstest konnte nicht erzeugt werden."

        expectFixtureFormatRejection "duplicate-kind" duplicateKind "doppeltes Feld"
    finally
        Directory.Delete(temporary, true)

/// Frischer-Prozess-Hilfslauf ueber den oeffentlichen Host (AC-T022-02/10).
let private runSoakHost (arguments: string[]) =
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    startInfo.ArgumentList.Add(
        Path.Combine(repositoryRootSoak, "src", "Riftward.App", "bin", "Release", "net10.0", "Riftward.App.dll")
    )

    for argument in arguments do
        startInfo.ArgumentList.Add(argument)

    use processHandle = Process.Start(startInfo)
    let stdout = processHandle.StandardOutput.ReadToEnd()
    let stderr = processHandle.StandardError.ReadToEnd()
    processHandle.WaitForExit()
    (processHandle.ExitCode, stdout.TrimEnd(), stderr.TrimEnd())

/// CLI-Vertrag: Positiv-, Negativ- und Usagefaeile bleiben kontrolliert (AC-T022-02/04/10).
let cliContractRunsDiagnosticSoakWithReports () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t022-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        // Positivfall: beschleunigter Diagnoselauf mit Schema- und Ehrlichkeitspruefung.
        let diagnosticReport = Path.Combine(temporary, "diag.json")

        let exitOk, _, _ =
            runSoakHost
                [| "soak"
                   "--scenario"
                   SoakScenarios.Replay
                   "--report"
                   diagnosticReport
                   "--diagnostic-accelerated"
                   "--horizon-ticks"
                   "2000" |]

        if exitOk <> 0 then
            failwith $"Diagnoselauf ergab Exitcode {exitOk}."

        let json = File.ReadAllText(diagnosticReport)

        if not (SoakReportSchema.Validate(json).Count = 0) then
            failwith "Diagnose-Report verletzte den Schemavertrag."

        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        if root.GetProperty("execution").GetProperty("evidenceUnit").GetBoolean() then
            failwith "Verkuerzter Diagnoselauf darf keine Evidenzeinheit sein."

        if
            root.GetProperty("execution").GetProperty("evidenceReason").GetString()
            <> "horizon-shortened-diagnostic"
        then
            failwith "Verkuerzter Diagnoselauf ist nicht maschinenlesbar als verkuerzt markiert."

        if
            not (
                root.GetProperty("profiles").EnumerateArray()
                |> Seq.forall (fun profile -> profile.GetProperty("status").GetString() = ProfileStatus.NotMeasured)
            )
        then
            failwith "Pflichtprofile sind im echten Lauf nicht NOT-MEASURED."

        // Referenzemission: echter Hostlauf bleibt auch bei erfolgreichem Gate
        // explizit diagnostisch und bindet eine frisch geschriebene Fixture.
        let emissionReport = Path.Combine(temporary, "emission.json")
        let emittedFixture = Path.Combine(temporary, "emitted-fixture.json")

        let exitEmission, _, _ =
            runSoakHost
                [| "soak"
                   "--scenario"
                   SoakScenarios.Replay
                   "--report"
                   emissionReport
                   "--diagnostic-accelerated"
                   "--horizon-ticks"
                   "2000"
                   "--reference-out"
                   emittedFixture |]

        if exitEmission <> 0 then
            failwith $"Diagnostische Referenzemission ergab Exitcode {exitEmission}."

        if not (File.Exists(emittedFixture)) then
            failwith "Referenzemission schrieb keine Fixture."

        let emissionJson = File.ReadAllText(emissionReport)
        let emissionErrors = SoakReportSchema.Validate(emissionJson)

        if emissionErrors.Count > 0 then
            let joined = String.Join("; ", emissionErrors)
            failwith $"Emissionsreport verletzte den Schemavertrag: {joined}"

        use emissionDocument = JsonDocument.Parse(emissionJson)
        let emissionRoot = emissionDocument.RootElement
        let emissionExecution = emissionRoot.GetProperty("execution")

        if
            emissionExecution.GetProperty("evidenceUnit").GetBoolean()
            || emissionExecution.GetProperty("isEvidence").GetBoolean()
        then
            failwith "Referenzemission duerfte sich nicht selbst als Evidenz bestaetigen."

        if
            emissionExecution.GetProperty("evidenceReason").GetString()
            <> SoakEvidenceUnit.ReferenceEmissionDiagnosticReason
        then
            failwith "Referenzemission traegt nicht den bindenden Diagnosegrund."

        if
            emissionRoot.GetProperty("scenario").GetProperty("executionModeId").GetString()
            <> SoakContract.ReferenceEmissionDiagnosticModeId
        then
            failwith "Referenzemission traegt nicht den eigenen Diagnosemodus."

        if
            not (emissionRoot.GetProperty("metrics").GetProperty("goldenFixture").GetProperty("emitted").GetBoolean())
        then
            failwith "Emissionsreport kennzeichnet die geschriebene Fixture nicht."

        // Fremdseed: Exitcode 30 mit Report und Kettenverletzung.
        let foreignReport = Path.Combine(temporary, "foreign.json")

        let exitForeign, _, _ =
            runSoakHost
                [| "soak"
                   "--scenario"
                   SoakScenarios.Replay
                   "--report"
                   foreignReport
                   "--diagnostic-accelerated"
                   "--horizon-ticks"
                   "2000"
                   "--seed"
                   "42" |]

        if exitForeign <> ExitCodes.Map(PlatformErrorCode.SoakGateViolated) then
            failwith $"Fremdseed ergab nicht Exitcode 30, sondern {exitForeign}."

        let foreignJson = File.ReadAllText(foreignReport)
        use foreignDocument = JsonDocument.Parse(foreignJson)

        if
            foreignDocument.RootElement
                .GetProperty("metrics")
                .GetProperty("goldenFixture")
                .GetProperty("sampleMismatches")
                .GetInt32() < 1
        then
            failwith "Fremdseed-Report belegt die Kettenabweichung nicht."

        // Unbekanntes Szenario: Exitcode 32 ohne Report.
        let unknownPath = Path.Combine(temporary, "unknown.json")

        let exitUnknown, _, stderrUnknown =
            runSoakHost [| "soak"; "--scenario"; "soak-nope"; "--report"; unknownPath |]

        if exitUnknown <> ExitCodes.Map(PlatformErrorCode.SoakScenarioUnavailable) then
            failwith $"Unbekanntes Szenario ergab nicht Exitcode 32."

        if stderrUnknown.Length = 0 then
            failwith "Abbruch ohne verstaendliche Meldung."

        if File.Exists(unknownPath) then
            failwith "Abgebrochener Lauf schrieb einen Report."

        // Horizont ohne Diagnosemodus: Usagefehler ohne Report.
        let usagePath = Path.Combine(temporary, "usage.json")

        let exitUsage, _, _ =
            runSoakHost
                [| "soak"
                   "--scenario"
                   SoakScenarios.Replay
                   "--report"
                   usagePath
                   "--horizon-ticks"
                   "2000" |]

        if exitUsage <> ExitCodes.Usage then
            failwith $"Autoritative Verkuerzung ergab keinen Usagefehler, sondern {exitUsage}."

        if File.Exists(usagePath) then
            failwith "Usageabbruch duerfte keinen Report schreiben."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

/// Evidenzeinheiten-Klassifikation des Soakvertrags V2 ist fail-closed je Verletzungsklasse (AC-T022-03).
let soakEvidenceUnitClassificationIsFailClosed () =
    let unitCase =
        SoakEvidenceUnit.Decide(SoakContract.AcceleratedEvidenceModeId, true, true, true, true, true, true, null)

    if not unitCase.IsUnit || unitCase.Reason <> null then
        failwith $"Vollstaendiger Lauf wurde nicht als Evidenzeinheit erkannt: {unitCase.Reason}."

    let rejections =
        [ SoakEvidenceUnit.Decide(SoakContract.AcceleratedEvidenceModeId, false, true, true, true, true, true, null),
          "horizon-shortened-diagnostic"
          SoakEvidenceUnit.Decide(
              SoakContract.AcceleratedEvidenceModeId,
              true,
              false,
              true,
              true,
              true,
              true,
              "ticks-incomplete"
          ),
          "incomplete:ticks-incomplete"
          SoakEvidenceUnit.Decide(
              SoakContract.AcceleratedEvidenceModeId,
              true,
              false,
              true,
              true,
              true,
              true,
              "watchdog-stall"
          ),
          "incomplete:watchdog-stall"
          SoakEvidenceUnit.Decide(SoakContract.AcceleratedEvidenceModeId, true, true, false, true, true, true, null),
          "non-release-build"
          SoakEvidenceUnit.Decide(SoakContract.RealtimeAuthoritativeModeId, true, true, true, true, true, true, null),
          "execution-mode-diagnostic"
          SoakEvidenceUnit.Decide(SoakContract.AcceleratedEvidenceModeId, true, true, true, true, false, false, null),
          "state-hash-chain-mismatch"
          SoakEvidenceUnit.Decide(SoakContract.AcceleratedEvidenceModeId, true, true, true, true, true, false, null),
          "gate-violated"
          SoakEvidenceUnit.Decide(
              SoakContract.ReferenceEmissionDiagnosticModeId,
              true,
              true,
              true,
              false,
              true,
              true,
              null
          ),
          SoakEvidenceUnit.ReferenceEmissionDiagnosticReason ]

    for decision, expectedReason in rejections do
        if decision.IsUnit then
            failwith $"Verletzungsklasse wurde als Evidenzeinheit akzeptiert ({expectedReason})."

        if decision.Reason <> expectedReason then
            failwith $"Ablehnungsgrund {decision.Reason} entspricht nicht {expectedReason}."

/// Reine Exitcode-Abbildung: Stall/Gateverletzung -> 30, Abbruch -> 31, Bestehen -> 0 (AC-T022-04/05/10).
let soakExitMappingIsStableAndDocumented () =
    if
        SoakExitMapping.Map(true, false, false)
        <> ExitCodes.Map(PlatformErrorCode.SoakGateViolated)
    then
        failwith "Watchdog-Stall ergab nicht Exitcode 30."

    if
        SoakExitMapping.Map(false, true, false)
        <> ExitCodes.Map(PlatformErrorCode.SoakGateViolated)
    then
        failwith "Gateverletzung bei vollstaendigem Lauf ergab nicht Exitcode 30."

    if
        SoakExitMapping.Map(false, false, true)
        <> ExitCodes.Map(PlatformErrorCode.SoakRunIncomplete)
    then
        failwith "Vorzeitiger Abbruch ergab nicht Exitcode 31."

    if
        SoakExitMapping.Map(true, true, false)
        <> ExitCodes.Map(PlatformErrorCode.SoakGateViolated)
    then
        failwith "Stall bei vollstaendigem Lauf ergab nicht Exitcode 30."

    if SoakExitMapping.Map(false, true, true) <> ExitCodes.Ok then
        failwith "Bestandener Lauf ergab nicht Exitcode 0."

/// Watchdog-Stall mit eingefrorenem Fortschritt bei laufender Zeit: IsStalled greift strikt jenseits des Fensters.
let watchdogStallWithFrozenProgressAndRunningClock () =
    let watchdog = ProgressWatchdog(5.0, 16)
    watchdog.Reset(100.0, 1000L)

    for t in [ 101.0; 104.9; 105.0 ] do
        watchdog.Observe(t, 1000L)

        if watchdog.IsStalled(t) then
            failwith $"Stall zu frueh gemeldet (t={t})."

    if not (watchdog.IsStalled(105.0001)) then
        failwith "Stillstand jenseits des Fensters wurde nicht erkannt."

    watchdog.Observe(106.0, 2000L)

    if watchdog.IsStalled(111.0) || not (watchdog.IsStalled(111.001)) then
        failwith "Wiederarmierung nach Fortschritt falsch."

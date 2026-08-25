module BenchSimTests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Riftward.App
open Riftward.App.Bench
open Riftward.Platform
open Riftward.Simulation

// Golden-Fixture fuer den bench-sim-Report (AC-T021-04). Die Fixture traegt
// Schema- und Contentkennung im Dokument selbst: schemaVersion 2, scenario.id
// bench-sim, simulationContract mit Vertragsversion und Weltkennung.
let private goldenReport =
    """{"schemaVersion":2,"mode":"bench","command":"./scripts/rift.sh bench --scenario bench-sim --report <PFAD>","scenario":{"id":"bench-sim","seed":20260824,"tickRateHz":20,"agentCount":250,"worldId":"riftward-simworld-graybox-v1","content":"synthetic-graybox-movement-world"},"simulationContract":{"document":"docs/SIMULATIONSVERTRAG.md","version":"1","numericModel":"q16-16-fixed-point-intonly-v1","hashAlgorithm":"fnv1a64-canonical-chain-v1","commandPlanAlgorithm":"xorshift64star-group-script-v1","allocationLimitBytesPerWarmTick":0},"commandPlan":{"algorithm":"xorshift64star-group-script-v1","commands":25,"hash":"d1b5230adc39da38","firstCommand":{"tick":240,"scopeGroup":0,"kind":"GroupMoveToZone","zoneIndex":2}},"startedAtUtc":"2026-08-24T12:00:00Z","finishedAtUtc":"2026-08-24T12:00:04Z","environment":{"os":{"type":"Linux","kernelRelease":"6.8.0-fixture"},"cpu":{"model":"fixture-cpu"},"rid":"linux-x64","commit":"0123456789abcdef0123456789abcdef01234567","buildMode":"Release","pins":[{"id":"sdl3","refType":"tag","ref":"release-3.4.14","commit":"a1","sourceSha256":"h1","licenseSpdx":"zlib"},{"id":"bgfx","refType":"commit","ref":"b2","commit":"b2","sourceSha256":"h2","licenseSpdx":"BSD-2-Clause"},{"id":"bx","refType":"commit","ref":"c3","commit":"c3","sourceSha256":"h3","licenseSpdx":"BSD-2-Clause"},{"id":"bimg","refType":"commit","ref":"d4","commit":"d4","sourceSha256":"h4","licenseSpdx":"BSD-2-Clause"}]},"measurement":{"warmupTicks":480,"sampleTicks":1200,"ticksExecuted":1680,"rssSampleIntervalTicks":60,"hashSampleIntervalTicks":60},"metrics":{"tickTimeMs":{"unit":"ms","method":"stopwatch-tick-delta","p50":0.27,"p95":0.38,"p99":0.45},"managedAllocationsBytes":{"unit":"bytes","method":"gc-total-allocated-bytes-precise-delta-per-tick-sum","perWarmTick":0.0},"gcPauseSumMs":{"unit":"ms","method":"gc-get-total-pause-duration-delta","value":0.0},"gcPauseCount":{"unit":"count","method":"gc-collection-count-gen0-to2-delta","value":0},"activeAgents":{"unit":"count","method":"soa-agent-count-fixed","value":250},"stateHashChain":{"unit":"hex64","method":"fnv1a64-canonical-chain-v1","start":"10e13faf142094db","intervalSampleTicks":[540,1140],"intervalHashes":["aa00000000000001","bb00000000000002"],"end":"de43976087a5f6a2"},"workingSetKiB":{"measured":true,"unit":"KiB","method":"proc-self-status-vmrss-samples","min":48000,"max":50000,"end":49000},"drawSubmitCallsPerFrame":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"visibleTrianglesPerFrame":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"gpuTimeMs":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"}},"gate":{"limits":{"p99TickTimeHardLimitMs":16.0,"p99TickTimeTargetMs":8.0,"allocationsPerWarmTickBytesMax":0},"pass":true,"p99TargetMet":true,"violations":[]},"profiles":[{"id":"hw-pc-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-mac-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-pc-high","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"}],"baseline":{"classification":"diagnostic-developer-workstation","protocol":"qops001-2026-08-24"},"exitCode":0}"""

let private assertHasError (fragment: string) (reportJson: string) (message: string) =
    let errors = SimReportSchema.Validate(reportJson)

    if errors.Count = 0 then
        failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

    let joined = String.concat "; " errors

    if not (joined.Contains(fragment, StringComparison.Ordinal)) then
        failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

/// Fester Ablauf eines Planhorizonts; liefert Start-, Intervall- und Endhash.
let private runPlan (seed: uint32) (totalTicks: int) (sampleEvery: int) (plan: SimCommand[]) =
    let world = SimWorld(seed)
    let start = world.ComputeStateHash()
    let chain = ResizeArray<uint64>([ start ])
    let chainTicks = ResizeArray<int64>([ 0L ])

    let mutable planIndex = 0
    let mutable tick = 0L

    while tick < int64 totalTicks do
        let firstDue = planIndex

        while planIndex < Array.length plan && int64 plan.[planIndex].Tick <= tick do
            planIndex <- planIndex + 1

        if planIndex > firstDue then
            world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue)) |> ignore

        world.Tick()
        tick <- tick + 1L

        if sampleEvery > 0 && tick % int64 sampleEvery = 0L then
            chain.Add(world.ComputeStateHash())
            chainTicks.Add(tick)

    (start, chain |> Seq.toList, chainTicks |> Seq.toList, world.ComputeStateHash(), world)

let private defaultSeed = 20260824u

/// Der Vertragsspiegel haelt Code und docs/SIMULATIONSVERTRAG.md konsistent (AC-T021-01).
let simulationContractMirrorsDocumentedValues () =
    if SimulationContract.AgentCount <> 250 then
        failwith "Agentenzahl des Vertrags ist nicht 250."

    if SimulationContract.TickRateHz <> 20 then
        failwith "Tickrate des Vertrags ist nicht 20 Hz."

    if SimulationContract.GroupCount <> 5 then
        failwith "Gruppenzahl des Vertrags ist nicht 5."

    if SimulationContract.DocumentPath <> "docs/SIMULATIONSVERTRAG.md" then
        failwith "Vertragspfad falsch."

    if SimulationContract.NumericModelId <> "q16-16-fixed-point-intonly-v1" then
        failwith "Numerikkennung falsch."

    if SimulationContract.HashAlgorithmId <> "fnv1a64-canonical-chain-v1" then
        failwith "Hashkennung falsch."

    if SimulationContract.WorldId <> "riftward-simworld-graybox-v1" then
        failwith "Weltkennung falsch."

    if
        SimulationContract.AllocationLimitBytesPerWarmTick < 0L
        || SimulationContract.AllocationLimitBytesPerWarmTick > 1024L
    then
        failwith "Allokationsgrenze verletzt die Auftragsobergrenze von 1 KiB."

    if SimulationContract.AllocationLimitBytesPerWarmTick <> 0L then
        failwith "Abschnitt 0 hat die Grenze auf 0 verschaeft; Spiegel weicht ab."

    if
        SimulationContract.P99TickTimeTargetMs <> 8.0
        || SimulationContract.P99TickTimeHardLimitMs <> 16.0
    then
        failwith "Tickzeitgrenzen entsprechen nicht PERFORMANCE_BUDGET.md."

    if
        SimulationContract.PathExpansionBudgetPerAgentTick <= 0
        || SimulationContract.PathGlobalExpansionBudgetPerTick < SimulationContract.PathExpansionBudgetPerAgentTick
    then
        failwith "Pfadhaushalt verletzt die Vertragsstruktur."

    // Das Vertragsdokument existiert und nennt alle maschinenlesbaren Kennungen.
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let document =
        File.ReadAllText(Path.Combine(findRoot (Environment.CurrentDirectory), "docs", "SIMULATIONSVERTRAG.md"))

    for identifier in
        [ SimulationContract.WorldId
          SimulationContract.NumericModelId
          SimulationContract.HashAlgorithmId
          SimulationContract.CommandPlanAlgorithmId ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Vertragsdokument nennt die Kennung {identifier} nicht."

/// Genau 250 vollstaendig simulierte Agenten in den Startzonen gebunden (AC-T021-03).
let worldSimulatesExactly250FullySimulatedAgentsInSpawnZones () =
    let world = SimWorld(defaultSeed)
    let snapshot = world.CreateSnapshot()

    if snapshot.AgentCount <> 250 then
        failwith "Snapshot enthaelt nicht genau 250 Agenten."

    if snapshot.PositionXQ16.Length <> 250 || snapshot.PathState.Length <> 250 then
        failwith "SoA-Laengen falsch."

    for agent in 0..249 do
        let tileX = NavWorld.TileIndexOfPosition(snapshot.PositionXQ16.[agent])
        let tileY = NavWorld.TileIndexOfPosition(snapshot.PositionYQ16.[agent])

        // Gerade Agenten starten in Zone 0 (West), ungerade in Zone 1 (Ost).
        let expectedZone = if agent % 2 = 0 then 0 else 1

        if not (NavWorld.IsInsideZone(expectedZone, tileX, tileY)) then
            failwith $"Agent {agent} startet ausserhalb seiner Startzone."

        if snapshot.PathState.[agent] <> byte SimAgentPathState.Idle then
            failwith "Agent startet nicht im Ruhezustand."

    if world.TickIndex <> 0L then
        failwith "Neue Welt ist nicht bei Tick 0."

/// Gleicher Seed und gleicher Plan erzeugen identische Hashketten (AC-T021-06).
let determinismIdenticalChainsForFreshWorldsWithGoldenFixture () =
    let totalTicks = 700
    let plan = CommandPlan.Generate(defaultSeed, totalTicks)

    if plan.Length = 0 then
        failwith "Plan ohne Befehle."

    let _, firstChain, firstChainTicks, firstEnd, _ =
        runPlan defaultSeed totalTicks 100 plan

    let _, secondChain, secondChainTicks, secondEnd, _ =
        runPlan defaultSeed totalTicks 100 plan

    if firstChain <> secondChain then
        failwith "Identische Konfiguration lieferte verschiedene Hashketten."

    if firstChainTicks <> secondChainTicks then
        failwith "Ketten-Stichprobenpunkte weichen ab."

    if List.last firstChain <> firstEnd then
        failwith "Kettenende stimmt nicht mit Endhash ueberein."

    // Golden-Fixture: Schema-/Contentkennung ist der Vertrag (Version 1,
    // fnv1a64-canonical-chain-v1, Welt v1); Abweichung = Vertragsbruch.
    let expectedStart = 0x10e13faf142094dbUL
    let expectedEnd = 0xf5d1448d9ed11665UL

    if List.head firstChain <> expectedStart then
        failwith $"Starthash weicht von der Golden-Fixture ab: {List.head firstChain:x16}"

    if firstEnd <> expectedEnd then
        failwith $"Endhash weicht von der Golden-Fixture ab: {firstEnd:x16}"

/// Fremder Seed oder zeitlich umgeordnete Befehlsfolge aendert den Endhash nachweislich (AC-T021-06).
let changedSeedOrReorderedCommandsChangeOutcome () =
    let totalTicks = 700
    let baseline = CommandPlan.Generate(defaultSeed, totalTicks)

    let _, _, _, foreignEnd, _ =
        runPlan (defaultSeed + 1u) totalTicks 100 (CommandPlan.Generate(defaultSeed + 1u, totalTicks))

    // Umordnung: Zonenzuweisung des ersten und letzten Gruppenbefehls von
    // Gruppe 0 tauschen (zeitliche Reihenfolge der Folgen geaendert).
    let reordered = Array.copy baseline

    let groupZeroIndices =
        [| for index in 0 .. Array.length reordered - 1 do
               if reordered.[index].ScopeGroup = 0 then
                   index |]

    if Array.length groupZeroIndices < 2 then
        failwith "Umordnungsfixture degeneriert."

    let firstIndex = Array.head groupZeroIndices
    let lastIndex = Array.last groupZeroIndices
    let firstCommand = reordered.[firstIndex]
    let lastCommand = reordered.[lastIndex]

    reordered.[firstIndex] <- SimCommand(firstCommand.Tick, 0, firstCommand.Kind, lastCommand.ZoneIndex)
    reordered.[lastIndex] <- SimCommand(lastCommand.Tick, 0, lastCommand.Kind, firstCommand.ZoneIndex)

    let _, _, _, reorderedEnd, _ = runPlan defaultSeed totalTicks 100 reordered

    if foreignEnd = 0xf5d1448d9ed11665UL then
        failwith "Fremder Seed ergab denselben Endhash."

    if reorderedEnd = 0xf5d1448d9ed11665UL then
        failwith "Umgeordnete Befehlsfolge ergab denselben Endhash."

/// Kanonische Ordnung: Eingabereihenfolge gleichzeitiger Befehle bleibt ohne Wirkung (AC-T021-06/03).
let canonicalCommandOrderIsEnforcedInternally () =
    let batch =
        [| SimCommand(999, 4, SimCommandKind.GroupMoveToZone, 3)
           SimCommand(999, 0, SimCommandKind.GroupMoveToZone, 2)
           SimCommand(999, 2, SimCommandKind.GroupMoveToZone, 4)
           SimCommand(999, 1, SimCommandKind.GroupMoveToZone, 3)
           SimCommand(999, 3, SimCommandKind.GroupMoveToZone, 5) |]

    let shuffled = Array.rev batch

    let worldSorted = SimWorld(defaultSeed)
    worldSorted.ApplyCommands(batch)
    worldSorted.Tick()

    let worldShuffled = SimWorld(defaultSeed)
    worldShuffled.ApplyCommands(shuffled)
    worldShuffled.Tick()

    if worldSorted.ComputeStateHash() <> worldShuffled.ComputeStateHash() then
        failwith "Eingabereihenfolge beeinflusste das Ergebnis trotz kanonischer Sortierung."

    for command in batch do
        if worldSorted.TargetZoneOfGroup(command.ScopeGroup) <> command.ZoneIndex then
            failwith $"Ziel der Gruppe {command.ScopeGroup} wurde nicht korrekt angewendet."

/// Pfadhaushalt bleibt je Tick begrenzt und Anfragen werden deterministisch fertig (AC-T021-03/05).
let pathBudgetIsCappedPerTickAndRequestsComplete () =
    let world = SimWorld(defaultSeed)

    // Weit entferntes Ziel fuer alle Gruppen sofort anfordern.
    let farAway =
        [| for group in 0..4 -> SimCommand(0, group, SimCommandKind.GroupMoveToZone, 3) |]

    world.ApplyCommands(farAway)

    for _ in 1..120 do
        world.Tick()

        if world.LastTickNodeExpansions > SimulationContract.PathGlobalExpansionBudgetPerTick then
            failwith "Globaler Knotenhaushalt eines Ticks wurde ueberschritten."

    let afterWarm = world.CreateSnapshot()

    if
        afterWarm.PathState
        |> Array.exists (fun state -> state = byte SimAgentPathState.Unreachable)
    then
        failwith "Erreichbare Zielzonen wurden als unerreichbar gemeldet."

    // Langfristig erreichen Agenten die ferne Zone: keine dauerhafte Suche.
    for _ in 1..6000 do
        world.Tick()

    let settled = world.CreateSnapshot()

    if
        settled.PathState
        |> Array.exists (fun state -> state = byte SimAgentPathState.Unreachable)
    then
        failwith "Unerreichbarkeits-False-Positive nach langer Lauf."

    let arrivedGroupZero =
        [| for agent in 0..249 -> agent |]
        |> Array.filter (fun agent ->
            settled.Group.[agent] = 0uy
            && settled.PathState.[agent] = byte SimAgentPathState.Idle
            && NavWorld.IsInsideZone(
                3,
                NavWorld.TileIndexOfPosition(settled.PositionXQ16.[agent]),
                NavWorld.TileIndexOfPosition(settled.PositionYQ16.[agent])
            ))

    if arrivedGroupZero.Length = 0 then
        failwith "Kein Agent von Gruppe 0 erreichte das ferne Ziel; Pfadsuche unvollstaendig."

/// Fortbewegung auf begehbarer Geometrie mit substanzieller Verschiebung (AC-T021-03).
let agentsRemainOnWalkableTilesAndMoveSubstantially () =
    let world = SimWorld(defaultSeed)
    let initial = world.CreateSnapshot()
    let plan = CommandPlan.Generate(defaultSeed, 900)

    let mutable planIndex = 0

    for tick in 0L .. 899L do
        let firstDue = planIndex

        while planIndex < plan.Length && int64 plan.[planIndex].Tick <= tick do
            planIndex <- planIndex + 1

        if planIndex > firstDue then
            world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue)) |> ignore

        world.Tick()

    let final = world.CreateSnapshot()

    let mutable totalDisplacementQ16 = 0L

    for agent in 0..249 do
        let tileX = NavWorld.TileIndexOfPosition(final.PositionXQ16.[agent])
        let tileY = NavWorld.TileIndexOfPosition(final.PositionYQ16.[agent])

        if not (NavWorld.IsWalkable(tileX, tileY)) then
            failwith $"Agent {agent} verliess die begehbare Geometrie ({tileX},{tileY})."

        let deltaX = final.PositionXQ16.[agent] - initial.PositionXQ16.[agent]
        let deltaY = final.PositionYQ16.[agent] - initial.PositionYQ16.[agent]

        totalDisplacementQ16 <- totalDisplacementQ16 + abs deltaX + abs deltaY

    // Mittelwert deutlich ueber einer Kachel beweist echte Fortbewegung.
    if totalDisplacementQ16 / 250L < FixedPoint.One * 10L then
        failwith $"Fortbewegung zu gering: mittlere Verschiebung {(totalDisplacementQ16 / 250L)} Q16."

/// Reportvertrag akzeptiert das Goldendokument und lehnt Faelschungen ab (AC-T021-04).
let reportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    let goldenErrors = SimReportSchema.Validate(goldenReport)

    if goldenErrors.Count > 0 then
        let joined = String.Join("; ", goldenErrors)
        failwith $"Goldenreport wurde abgelehnt: {joined}"

    // Kennzahl ohne Methodenkennung.
    assertHasError
        "tickTimeMs.method"
        (goldenReport.Replace("\"method\":\"stopwatch-tick-delta\",", ""))
        "Kennzahl ohne Methodenkennung wurde akzeptiert"

    // Typenfremder Messwert.
    assertHasError
        "numerisch"
        (goldenReport.Replace("\"p50\":0.27", "\"p50\":\"0.27\""))
        "Typenfremder Messwert wurde akzeptiert"

    // Grundlos fehlender Pflichtwert.
    assertHasError "tickTimeMs.p99" (goldenReport.Replace(",\"p99\":0.45", "")) "Fehlendes p99 wurde akzeptiert"

    // Erfundener GPU-Messwert ohne Messquelle (headless kann nie messen).
    assertHasError
        "headless Szenario kann diesen Wert nicht messen"
        (goldenReport.Replace(
            "\"gpuTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
            "\"gpuTimeMs\":{\"measured\":true,\"unit\":\"ms\",\"method\":\"bgfx-stats-gpu-timer-p99\",\"p99\":1.2}"
        ))
        "GPU-Wert ohne Messquelle wurde akzeptiert"

    // Unavailable ohne maschinenlesbaren Grund.
    assertHasError
        "reason"
        (goldenReport.Replace(
            "\"workingSetKiB\":{\"measured\":true,\"unit\":\"KiB\",\"method\":\"proc-self-status-vmrss-samples\",\"min\":48000,\"max\":50000,\"end\":49000}",
            "\"workingSetKiB\":{\"measured\":false}"
        ))
        "Unavailable ohne Grund wurde akzeptiert"

    // Unbekanntes Feld.
    assertHasError
        "unbekanntes Feld"
        (goldenReport.Replace("\"schemaVersion\":2,", "\"schemaVersion\":2,\"extraField\":1,"))
        "Unbekanntes Feld wurde akzeptiert"

    // Fremde Schemaversion.
    assertHasError
        "schemaVersion"
        (goldenReport.Replace("\"schemaVersion\":2", "\"schemaVersion\":3"))
        "Fremde Schemaversion wurde akzeptiert"

    // Leere Kettenstichproben.
    assertHasError "mindestens 1" (goldenReport.Replace("[540,1140]", "[]")) "Leere Kettenstichproben wurden akzeptiert"

    // Grossbuchstaben-Hash verletzt die Kanonform.
    assertHasError
        "Hexwert"
        (goldenReport.Replace("\"start\":\"10e13faf142094db\"", "\"start\":\"10E13FAF142094DB\""))
        "Grossbuchstaben-Hash wurde akzeptiert"

    // Beschaedigtes Zwischenartefakt.
    assertHasError "gueltiges JSON" "{beschädigt" "Beschaedigtes Dokument wurde akzeptiert"

/// Gate-Evaluator je Bestehens- und Verletzungsklasse fail-closed (AC-T021-05).
let simBudgetGateCoversEveryClassFailClosed () =
    let limits = SimBudgetLimits.Documented

    if limits.P99TickTimeHardLimitMs <> 16.0 || limits.P99TickTimeTargetMs <> 8.0 then
        failwith "Gate-Grenzwerte weichen von PERFORMANCE_BUDGET.md ab."

    if limits.AllocationsPerWarmTickLimitBytes <> 0L then
        failwith "Allokationsgate spiegelt Abschnitt 0 nicht."

    let passCase = SimBudgetGate.Evaluate(limits, SimBudgetInputs(7.9, 0.0))

    if not passCase.Pass || not passCase.P99TargetMet || passCase.Violations.Count <> 0 then
        failwith "Bestehensklasse des Simulationsgates schlug fehl."

    let targetMissOnly = SimBudgetGate.Evaluate(limits, SimBudgetInputs(12.0, 0.0))

    if not targetMissOnly.Pass then
        failwith "Zielverfehlung unter der harten Grenze darf das Gate nicht allein falten."

    if targetMissOnly.P99TargetMet then
        failwith "8-ms-Zielverfehlung wurde nicht ausgewiesen."

    let hardViolation = SimBudgetGate.Evaluate(limits, SimBudgetInputs(16.001, 0.0))

    if
        hardViolation.Pass
        || not (
            hardViolation.Violations
            |> Seq.exists (fun violation -> violation.Contains("p99-tick-time-ms", StringComparison.Ordinal))
        )
    then
        failwith "Harte Tickzeitgrenze wurde nicht durchgesetzt."

    let allocationViolation = SimBudgetGate.Evaluate(limits, SimBudgetInputs(7.9, 1.0))

    if
        allocationViolation.Pass
        || not (
            allocationViolation.Violations
            |> Seq.exists (fun violation ->
                violation.Contains("managed-allocations-per-warm-tick-bytes", StringComparison.Ordinal))
        )
    then
        failwith "Allokation ueber der Abschnitt-0-Grenze wurde nicht erkannt."

    let nanCase = SimBudgetGate.Evaluate(limits, SimBudgetInputs(Double.NaN, 0.0))

    if nanCase.Pass then
        failwith "NaN-Messwert wurde nicht als fail-closed erkannt."

    let negativeAllocation = SimBudgetGate.Evaluate(limits, SimBudgetInputs(7.9, -0.5))

    if negativeAllocation.Pass then
        failwith "Negativer Messwert wurde nicht als fail-closed erkannt."

/// Szenarioregistry klassifiziert bench-sim implementiert und alles andere explizit (AC-T021-02).
let scenarioRegistryClassifiesImplementedAndPendingScenarios () =
    if
        BenchScenarios.Classify(BenchScenarios.Sim)
        <> BenchScenarios.Support.Implemented
    then
        failwith "bench-sim ist nicht als implementiert klassifiziert."

    if
        BenchScenarios.Classify(BenchScenarios.Empty)
        <> BenchScenarios.Support.Implemented
    then
        failwith "bench-empty verlor seinen Implementierungsstatus."

    for pending in
        [ BenchScenarios.Army
          BenchScenarios.Battle
          BenchScenarios.Base
          BenchScenarios.Path
          BenchScenarios.Load ] do
        if
            BenchScenarios.Classify(pending)
            <> BenchScenarios.Support.RegisteredNotImplemented
        then
            failwith $"Szenario {pending} ist nicht mehr registriert-unimplementiert."

    if BenchScenarios.Classify("bench-nope") <> BenchScenarios.Support.Unknown then
        failwith "Fremde ID wurde nicht abgewiesen."

    if BenchScenarios.Classify(null) <> BenchScenarios.Support.Unknown then
        failwith "Fehlende ID wurde nicht als unbekannt behandelt."

/// Frischer-Prozess-Hilfslauf ueber den oeffentlichen Host (AC-T021-02/06).
let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

let private runAppHost (arguments: string[]) =
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    startInfo.ArgumentList.Add(
        Path.Combine(repositoryRoot, "src", "Riftward.App", "bin", "Release", "net10.0", "Riftward.App.dll")
    )

    for argument in arguments do
        startInfo.ArgumentList.Add(argument)

    use processHandle = Process.Start(startInfo)
    let stdout = processHandle.StandardOutput.ReadToEnd()
    let stderr = processHandle.StandardError.ReadToEnd()
    processHandle.WaitForExit()
    (processHandle.ExitCode, stdout.TrimEnd(), stderr.TrimEnd())

/// Frischer-Prozesslauf mit erwartetem Erfolg. Liefert der dokumentierte
/// Budgetgate Exit 26, wird derselbe Lauf genau einmal wiederholt. Der
/// Exitcode ist klauselunspezifisch: Neben der lastempfindlichen
/// wanduhrabhängigen Tickzeit (harte 16-ms-Grenze) kann unter starker
/// Host-Konkurrenz auch der prozessweite Allokationszähler transient
/// anschlagen, weil GC.GetTotalAllocatedBytes auch Laufzeit-rauschen
/// fremder Threads innerhalb des je-Tick-Messfensters erfasst
/// (Folgereview 2026-08-26: einstellige Bytes je warmem Tick bei
/// Host-Last über 15; der Simulationskern blieb deterministisch bei
/// 0 Bytes, Kettenende in allen Läufen identisch). Eine echte, anhaltende
/// Regression – Tickzeit oder Produktallokation – tritt wegen ihres
/// deterministischen Ursprungs in beiden Versuchen auf und failt
/// weiterhin reproduzierbar. Umgebungskontrakt der Suite ist ein im
/// Übrigen ruhender Gate-Host; unter dauerhaft schwerer CPU-Auslastung
/// scheitert der Lauf ehrlich, weil dort keine belastbare Timing-Evidenz
/// möglich ist. Alle übrigen Verträge (Schema, Agentenbindung,
/// Hashkettengleichheit, Fremdseed-Sensitivität) bleiben unangetastete
/// Bestehensklauseln.
let private runSuccessfulBenchSim (arguments: string[]) =
    let firstExitCode, _, _ = runAppHost arguments

    if firstExitCode <> ExitCodes.Map(PlatformErrorCode.BenchBudgetViolated) then
        firstExitCode
    else
        let retryExitCode, _, _ = runAppHost arguments
        retryExitCode

let private chainOf (path: string) =
    use document = JsonDocument.Parse(File.ReadAllText(path))

    let chain =
        document.RootElement.GetProperty("metrics").GetProperty("stateHashChain")

    [| yield chain.GetProperty("start").GetString()
       for sample in chain.GetProperty("intervalHashes").EnumerateArray() do
           yield sample.GetString()
       yield chain.GetProperty("end").GetString() |]

/// Zwei unabhaengige Fresh-Prozesslaeufe mit identischen Hashketten sowie Negativfaelle (AC-T021-02/06/08).
let cliContractRunsHeadlessSimulationWithReports () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t021-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let reportOne = Path.Combine(temporary, "sim-run1.json")
        let reportTwo = Path.Combine(temporary, "sim-run2.json")

        let argumentsFor path seed =
            [| "bench"
               "--scenario"
               "bench-sim"
               "--report"
               path
               "--seed"
               seed
               "--warmup-ticks"
               "260"
               "--sample-ticks"
               "120" |]

        let exitOne = runSuccessfulBenchSim (argumentsFor reportOne "20260824")

        if exitOne <> 0 then
            failwith $"bench-sim-Lauf ergab Exitcode {exitOne}."

        let exitTwo = runSuccessfulBenchSim (argumentsFor reportTwo "20260824")

        if exitTwo <> 0 then
            failwith $"Zweiter bench-sim-Lauf ergab Exitcode {exitTwo}."

        for path in [ reportOne; reportTwo ] do
            let json = File.ReadAllText(path)

            if not (SimReportSchema.Validate(json).Count = 0) then
                failwith $"Echter Report verletzte den Schemavertrag: {path}"

            use document = JsonDocument.Parse(json)
            let root = document.RootElement

            if root.GetProperty("scenario").GetProperty("agentCount").GetInt32() <> 250 then
                failwith "Report bindet nicht genau 250 Agenten."

            if
                not (
                    root.GetProperty("profiles").EnumerateArray()
                    |> Seq.forall (fun profile -> profile.GetProperty("status").GetString() = "NOT-MEASURED")
                )
            then
                failwith "Pflichtprofile sind im echten Lauf nicht NOT-MEASURED."

        if chainOf reportOne <> chainOf reportTwo then
            failwith "Zwei unabhaengige Fresh-Prozesslaeufe lieferten unterschiedliche Hashketten."

        if
            BenchReportSchema.StructureDifferences(File.ReadAllText(reportOne), File.ReadAllText(reportTwo)).Count
            <> 0
        then
            failwith "Reportstruktur zweier Laeufe ist nicht identisch."

        // Fremder Seed aendert den Endhash nachweislich.
        let reportForeign = Path.Combine(temporary, "sim-foreign.json")
        let exitForeign = runSuccessfulBenchSim (argumentsFor reportForeign "42")

        if exitForeign <> 0 then
            failwith "Fremdseed-Lauf ergab keinen Erfolg."

        let chainBaseline = chainOf reportOne
        let chainForeign = chainOf reportForeign

        if chainBaseline.[chainBaseline.Length - 1] = chainForeign.[chainForeign.Length - 1] then
            failwith "Fremder Seed ergab denselben Endhash."

        // Unbekanntes oder nicht implementiertes Szenario: Exitcode 25 ohne Report.
        let unknownPath = Path.Combine(temporary, "unknown.json")

        for pending in [ "bench-nope"; BenchScenarios.Load ] do
            let exitPending, _, stderrPending =
                runAppHost [| "bench"; "--scenario"; pending; "--report"; unknownPath |]

            if exitPending <> ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable) then
                failwith $"Szenario {pending} ergab nicht Exitcode 25."

            if stderrPending.Length = 0 then
                failwith $"Szenario {pending} brach ohne verstaendliche Meldung ab."

        if File.Exists(unknownPath) then
            failwith "Abgebrochener Lauf schrieb einen Report."

        // Fehlender Reportpfad: Usagefehler.
        let exitUsage, _, _ = runAppHost [| "bench"; "--scenario"; BenchScenarios.Sim |]

        if exitUsage <> ExitCodes.Usage then
            failwith "Fehlender Reportpfad ergab keinen Usagefehler."

        // Horizont vor dem ersten Planbefehl: Usagefehler ohne Report.
        let exitHorizon, _, _ =
            runAppHost
                [| "bench"
                   "--scenario"
                   BenchScenarios.Sim
                   "--report"
                   unknownPath
                   "--warmup-ticks"
                   "30"
                   "--sample-ticks"
                   "60" |]

        if exitHorizon <> ExitCodes.Usage then
            failwith "Zu kurzer Planhorizont ergab keinen Usagefehler."

        if File.Exists(unknownPath) then
            failwith "Abgebrochene Laeufe duerfen keinen Report schreiben."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

/// Fault-Injection: nicht schreibbarer Reportpfad liefert definierten Code ohne Absturz (AC-T021-08).
let unwritableSimReportPathFailsControlled () =
    let blockedDirectory =
        Path.Combine(Path.GetTempPath(), "rift-t021-missing-" + Guid.NewGuid().ToString("N"), "unter")

    let blockedPath = Path.Combine(blockedDirectory, "sim.json")

    if BenchRunner.WriteReportOrDiagnose(blockedPath, "{}") then
        failwith "Schreibvorgang in fehlendes Verzeichnis meldete Erfolg."
    else
        try
            File.WriteAllText(blockedPath, "{}")
            failwith "Unerwartet schreibbarer Pfad: Fixture ungueltig."
        with
        | :? DirectoryNotFoundException
        | :? IOException -> ()

/// Exitcodebedeutungen bleiben stabil; bench-sim teilt die dokumentierten Codes (AC-T021-08).
let exitCodeMappingStaysStableIncludingSimReuse () =
    let expectations =
        [ PlatformErrorCode.Internal, 1
          PlatformErrorCode.BenchScenarioUnavailable, 25
          PlatformErrorCode.BenchBudgetViolated, 26
          PlatformErrorCode.TelemetryInvalid, 27
          PlatformErrorCode.ReportNotWritable, 28 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Exitcode fuer {code} ist {ExitCodes.Map(code)}, dokumentiert ist {expected}."

    if ExitCodes.Ok <> 0 || ExitCodes.Usage <> 2 then
        failwith "Basis-Exitcodes wurden veraendert."

/// Architekturtest: Simulationsprojekt bleibt rein (BCL, C#, kein Fließkomma, keine Native-/Praesentationstypen) (AC-T021-09).
let architectureKeepsSimulationProjectPure () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let root = findRoot (Environment.CurrentDirectory)
    let projectDirectory = Path.Combine(root, "src", "Riftward.Simulation")

    let csproj =
        File.ReadAllText(Path.Combine(projectDirectory, "Riftward.Simulation.csproj"))

    if csproj.Contains("ProjectReference", StringComparison.Ordinal) then
        failwith "Simulationsprojekt referenziert andere Projekte; BCL-only verletzt."

    if csproj.Contains("PackageReference", StringComparison.Ordinal) then
        failwith "Simulationsprojekt besitzt NuGet-Abhängigkeiten."

    if not (Array.isEmpty (Directory.GetFiles(projectDirectory, "*.fs"))) then
        failwith "F#-Quellen im Tick-Hotpath-Projekt gefunden."

    let forbidden =
        [ "SDL"
          "bgfx"
          "Riftward.Platform"
          "Riftward.App"
          "Math."
          "System.Numerics"
          "Stopwatch"
          "DateTime"
          "Environment."
          "new Random(" ]

    // Der FP-Scan gilt fuer die Zustandsuebergangsquellen; SimulationContract.cs
    // spiegelt als Gate-Wertspiegel auch Millisekunden-Grenzwerte ab.
    let hotPathFiles =
        set
            [ "FixedPoint.cs"
              "SimWorld.cs"
              "NavWorld.cs"
              "SimCommand.cs"
              "CommandPlan.cs"
              "SimRandom.cs" ]

    let floatingPointRegex = Regex("\\b(double|float|decimal|Single|Double)\\b")

    for file in Directory.GetFiles(projectDirectory, "*.cs") do
        let fileName = Path.GetFileName(file)
        let sourceLines = File.ReadAllLines(file)

        // Zeilenkommentare entfernen, damit verbietende Erklaerungen nicht
        // selbst als Verstoss gelten.
        let codeOnly =
            sourceLines
            |> Array.map (fun line ->
                let index = line.IndexOf("//", StringComparison.Ordinal)
                if index >= 0 then line.Substring(0, index) else line)

        let source = String.Concat(codeOnly)

        for token in forbidden do
            if source.Contains(token, StringComparison.Ordinal) then
                failwith $"Verbotener Bezeichner '{token}' in {fileName}."

        if Set.contains fileName hotPathFiles && floatingPointRegex.IsMatch(source) then
            failwith $"Fließkomma-Schluesselwort im Numerikkern gefunden: {fileName}."

    // App darf die Simulation referenzieren; die Simulation nie die App/Plattform.
    let appCsproj =
        File.ReadAllText(Path.Combine(root, "src", "Riftward.App", "Riftward.App.csproj"))

    if
        not (
            appCsproj.Contains("../Riftward.Simulation/", StringComparison.Ordinal)
            && appCsproj.Contains("../Riftward.Platform/", StringComparison.Ordinal)
        )
    then
        failwith "App-Bindungen an Simulation oder Plattform fehlen."

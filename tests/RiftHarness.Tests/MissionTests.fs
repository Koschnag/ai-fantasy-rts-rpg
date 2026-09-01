module MissionTests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open Riftward.App
open Riftward.App.Command
open Riftward.Platform
open Riftward.Save
open Riftward.Session
open Riftward.Simulation

// ---------------------------------------------------------------------------
// T-039: kleinster spielbarer Abschluss- und Wiederholungsschritt
// (Abschlussvertrag V1, Abschnitte 0 bis 13). Jede Pruefung bindet Code,
// Vertragsdokument, Schemavertrag und Laufverhalten gegeneinander; keine
// Pruefung antwortet auf eine offene Produktfrage und keine veraendert
// Riftward.Simulation.
// ---------------------------------------------------------------------------

let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

let private readDocument (relative: string) =
    File.ReadAllText(Path.Combine(repositoryRoot, relative))

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

let private rulesFor horizon = ScriptWindowRules(40, horizon)

let private v4Script (horizon: int) (bodyLines: string list) =
    let body = String.concat "\n" bodyLines
    $"graybox-input-script-v4 {horizon}\n{body}\nend\n"

let private parseV4Script (horizon: int) (bodyLines: string list) =
    InputScriptParser.Parse(Encoding.UTF8.GetBytes(v4Script horizon bodyLines), rulesFor horizon).Intents

/// Erkundungs-/Mobilmachungskern der gebundenen T-034-/T-035-/T-036-Basis.
let private explorationBody: string list =
    [ "intent 250 point 149718 44500"
      "intent 251 move 4"
      "intent 252 point 127137 48499"
      "intent 253 move 4"
      "intent 254 point 135215 48547"
      "intent 255 move 4"
      "intent 256 point 144867 47499"
      "intent 257 move 4"
      "intent 260 switch"
      "intent 1200 steer 2"
      "intent 2600 switch"
      "intent 2620 point 46062 45439"
      "intent 2621 move 0"
      "intent 2622 point 51256 48195"
      "intent 2623 move 0"
      "intent 2624 point 52523 43877"
      "intent 2625 move 0"
      "intent 2626 point 48933 45640"
      "intent 2627 move 0"
      "intent 2640 switch"
      "intent 2800 steer 1"
      "intent 4200 steer 5"
      "intent 5200 steer 3"
      "intent 6400 steer 4" ]

/// Vertragliche Zwei-Ketten-Kette (Abschlussvertrag Abschnitte 2 bis 4):
/// Kette 1 endet als Zykluserfolg (Wahl 9200, Ankunft 9200), die Wieder-
/// holen-Aktion setzt an 9400 die gesamte Kette zurueck, und Kette 2
/// durchlaeuft Erkundung mit abweichender Aufsuchfolge, Angebotsableitung,
/// Wahl (Optionenvarianz) und erneutem Erfolg.
let private twoChainBody: string list =
    explorationBody
    @ [ "intent 8000 choose-a"
        "intent 9200 choose-b"
        "intent 9400 repeat"
        "intent 9500 steer 2"
        "intent 11000 steer 1"
        "intent 12400 steer 5"
        "intent 13600 steer 3"
        "intent 14800 steer 0"
        "intent 16500 choose-b" ]

let private runInProcess
    (seed: uint32)
    (horizon: int)
    (bodyLines: string list)
    (explorationEnabled: bool)
    (decisionEnabled: bool)
    (pressureEnabled: bool)
    (missionEnabled: bool)
    : SessionRunResult =
    let intents = parseV4Script horizon bodyLines

    SessionEngine.Run(
        SessionRunRequest(
            Seed = seed,
            ScriptedIntents = intents,
            WarmupTicks = 240,
            HorizonTicks = horizon,
            RunSelfConsistencyPass = false,
            ExplorationEnabled = explorationEnabled,
            DecisionEnabled = decisionEnabled,
            PressureEnabled = pressureEnabled,
            MissionEnabled = missionEnabled
        )
    )

// ---------------------------------------------------------------------------
// Spiegeltest (AC-T039-01/05): Code, Vertragsdokumente, Schemalinie und
// Keymapfamilie haelt ein Test.
// ---------------------------------------------------------------------------

let missionContractMirrorsDocumentedValues () =
    if MissionContract.DocumentPath <> "docs/ABSCHLUSSVERTRAG.md" then
        failwith "Abschlussvertragspfad falsch."

    if MissionContract.ContractVersion <> "1" then
        failwith "Abschlussvertragsversion falsch."

    if
        CommandReportSchema.VersionWithoutExploration <> 2
        || CommandReportSchema.CurrentVersion <> 3
        || CommandReportSchema.VersionWithDecision <> 4
        || CommandReportSchema.VersionWithPressure <> 5
        || CommandReportSchema.VersionWithContinuation <> 6
        || CommandReportSchema.VersionWithMission <> 7
        || MissionContract.ReportSchemaVersionWithMission <> 7
    then
        failwith "Schemaversionen entsprechen nicht dem Vertrag (Bestand 2 bis 7)."

    // Persistenzwahrheit (Abschlussvertrag Abschnitt 5; Savevertrag V3
    // Abschnitt 15): die Kettenlaufzaehlung ist fortsetzbar, die
    // abgeleitete Abschlusswahrheit traegt kein Persistenzbyte, die
    // ausdrueckliche Replay-Ausnahme bleibt.
    if
        not MissionContract.Persisted
        || MissionContract.ReplayContinued
        || MissionContract.CompletionStatePersisted
        || MissionContract.SaveLoadContinuation <> "continued"
        || MissionContract.ReplayNotContinued <> "not-continued"
        || MissionContract.PersistenceStatementId <> "mission-chain-run-counter-persisted-v1"
    then
        failwith "Die versionierte Persistenzaussage widerspricht dem Abschlussvertrag V1."

    // Additive Sektionsflaeche (Savevertrag V3, Abschnitt 15).
    if
        SaveContract.MissionSectionFieldsModelId <> "mission-chain-run-section-fields-v3"
        || SaveContract.LegacySectionEmptinessModelId <> "legacy-section-v1-mission-emptiness-v3"
        || SessionSectionCodec.CurrentSectionVersion <> 2us
        || SessionSectionCodec.LegacySectionVersion <> 1us
    then
        failwith "Die Missions-Sektionsflaeche entspricht nicht dem Savevertrag V3."

    // Keymap-Aktion der bestehenden Familie (Kommandovertrag Abschnitt 13).
    if
        Keymap.RepeatMissionActionName <> "repeat-mission"
        || MissionContract.RepeatActionName <> "repeat-mission"
        || MissionContract.RepeatDefaultScancode <> 64
        || not (Array.contains "repeat-mission" Keymap.SemanticActions)
        || Keymap.Defaults["repeat-mission"] <> [| 64 |]
    then
        failwith "Die Wiederholen-Aktion entspricht nicht der Keymap-Präzisierung."

    // Standardbelegung bleibt kollisionsfrei.
    match Keymap.Validate(Keymap.Defaults) with
    | true, _ -> ()
    | false, error -> failwith $"Keymapvalidierung scheiterte an der Wiederholen-Aktion: {error}"

    let document = readDocument MissionContract.DocumentPath

    for identifier in
        [ MissionContract.ActivationId
          MissionContract.CompletionModelId
          MissionContract.CompletionBoundaryModelId
          MissionContract.RepeatActivationModelId
          MissionContract.ResetScopeId
          MissionContract.PersistenceStatementId
          MissionContract.HudModelId
          MissionContract.ReportBlockId
          MissionContract.ScriptFormatIdV4
          MissionContract.RepeatScriptAction
          MissionContract.RepeatActionName
          MissionContract.RejectReasonRepeatBeforeCompletion
          MissionContract.RejectReasonMissionNotActivated
          MissionContract.CompletionStateCompleted
          MissionContract.CompletionStateOpen
          MissionContract.OpenReasonNoCycleSuccess
          MissionContract.RepeatDispositionApplied
          MissionContract.RepeatDispositionRejectedBeforeCompletion
          "mission-repeat-completion-only-v1" ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Abschlussvertragsdokument nennt die Kennung {identifier} nicht."

    for anchor in
        [ "--mission"
          " — Auftrag: abgeschlossen"
          "repeat-mission"
          "graybox-input-script-v4"
          "F7"
          "full-chain-restart-including-visit-protocol-v1"
          "no-cycle-success-within-run" ] do
        if not (document.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Abschlussvertragsdokument nennt den Anker {anchor} nicht."

    // Die autorisierten Praezisierungen der beruehrten Vertraege sind
    // versioniert ausgewiesen.
    let explorationDocument = readDocument ExplorationContract.DocumentPath

    if
        not (explorationDocument.Contains("registration-uniqueness-per-chain-v3", StringComparison.Ordinal))
        || not (explorationDocument.Contains("Autorisierte additive Ketten-Präzisierung (V3, T-039)", StringComparison.Ordinal))
    then
        failwith "Der Erkundungsvertrag traegt die autorisierte Ketten-Praezisierung V3 nicht."

    let decisionDocument = readDocument DecisionContract.DocumentPath

    if
        not (decisionDocument.Contains("chain-scoped-offer-and-cycle-truth-v4", StringComparison.Ordinal))
        || not (decisionDocument.Contains("Autorisierte additive Ketten-Präzisierung (V4, T-039)", StringComparison.Ordinal))
    then
        failwith "Der Entscheidungsvertrag traegt die autorisierte Ketten-Praezisierung V4 nicht."

    let pressureDocument = readDocument PressureContract.DocumentPath

    if
        not (pressureDocument.Contains("chain-scoped-cycle-counting-v3", StringComparison.Ordinal))
        || not (pressureDocument.Contains("Autorisierte additive Ketten-Präzisierung (V3, T-039)", StringComparison.Ordinal))
    then
        failwith "Der Druckvertrag traegt die autorisierte Ketten-Praezisierung V3 nicht."

    let saveDocument = readDocument SaveContract.DocumentPath

    if
        not (saveDocument.Contains("Autorisierte additive Missions-Sektionsfläche (V3, T-039)", StringComparison.Ordinal))
        || not (saveDocument.Contains("mission-chain-run-section-fields-v3", StringComparison.Ordinal))
        || not (saveDocument.Contains("legacy-section-v1-mission-emptiness-v3", StringComparison.Ordinal))
    then
        failwith "Der Savevertrag traegt die autorisierte Missions-Sektionsflaeche V3 nicht."

    let commandDocument = readDocument SessionContract.DocumentPath

    if
        not (commandDocument.Contains("Wiederholen-Aktion der Keymap-Familie", StringComparison.Ordinal))
        || not (commandDocument.Contains("repeat-mission", StringComparison.Ordinal))
    then
        failwith "Der Kommandovertrag traegt die autorisierte Keymap-Praezisierung nicht."

// ---------------------------------------------------------------------------
// Abschlussableitung und ehrliche Zustaende (AC-T039-02-Testmatrix).
// ---------------------------------------------------------------------------

let completionDerivationIsPureFunctionOfLayerTruths () =
    // Frische Schichten ohne Erfolg: offene Kette mit ehrlichem Grund.
    let withoutSuccess =
        runInProcess 20260826u 9000 explorationBody true true true true

    let mission = withoutSuccess.Mission

    if isNull mission then
        failwith "Aktivierter Lauf lieferte keinen Missionsausweis."

    if
        mission.CompletionState <> MissionContract.CompletionStateOpen
        || mission.CompletionStateReason <> MissionContract.OpenReasonNoCycleSuccess
        || mission.CompletionBoundaryTick <> MissionSession.UnsetBoundaryTick
        || mission.ChainRunCount <> 1L
    then
        failwith "Offene Kette ohne Zykluserfolg trug nicht den ehrlichen Abschlussausweis."

    // Totale Ableitung: ohne Schichtaktivierung ist die Ableitung false.
    if
        MissionSession.IsDerivedCompleted(ExplorationSession(), DecisionSession(), PressureSession())
    then
        failwith "Die Ableitung war ohne Schichtwahrheiten wahr."

    // Wiederholen ohne aktivierte Schicht bleibt grammatisch unmoeglich:
    // unter v1/v2/v3 ist die Aktion UnknownAction (eigene Pruefung unten);
    // mit v4-Kopf ohne Missionsaktivierung weist die Pipeline mit der
    // Auswertungsordnung Stufe 1 ab (eigene Pruefung unten).

let repeatBeforeCompletionIsRejectedAndChangesNothing () =
    // Wiederholen vor dem Abschluss: die Aktion wird mit unterscheidbarer
    // Klasse abgewiesen und veraendert nachweislich nichts.
    let withEarlyRepeat =
        runInProcess
            20260826u
            9000
            (explorationBody @ [ "intent 8900 repeat" ])
            true
            true
            true
            true

    let twinWithoutRepeat = runInProcess 20260826u 9000 explorationBody true true true true

    let mission = withEarlyRepeat.Mission

    if isNull mission then
        failwith "Aktivierter Lauf lieferte keinen Missionsausweis."

    if mission.ChainRunCount <> 1L then
        failwith "Eine abgewiesene Wiederholung veraenderte die Kettenlaufzaehlung."

    if
        mission.RepeatProtocol.Count <> 1
        || mission.RepeatProtocol[0].Disposition <> MissionContract.RepeatDispositionRejectedBeforeCompletion
        || mission.RepeatProtocol[0].ChainRunAfter <> 1L
    then
        failwith "Die abgewiesene Wiederholung trug nicht ihre unterscheidbare Klasse."

    if
        withEarlyRepeat.StartStateHash <> twinWithoutRepeat.StartStateHash
        || withEarlyRepeat.EndStateHash <> twinWithoutRepeat.EndStateHash
        || withEarlyRepeat.IntervalSampleTicks <> twinWithoutRepeat.IntervalSampleTicks
        || withEarlyRepeat.IntervalHashes <> twinWithoutRepeat.IntervalHashes
    then
        failwith "Eine abgewiesene Wiederholung veraenderte die Kette."

    // Auch der Sitzungszustand (Erkundungsprotokoll) bleibt unveraendert.
    if
        withEarlyRepeat.Exploration.VisitedCount <> twinWithoutRepeat.Exploration.VisitedCount
        || withEarlyRepeat.Exploration.Completed <> twinWithoutRepeat.Exploration.Completed
    then
        failwith "Eine abgewiesene Wiederholung veraenderte die Erkundungswahrheit."

let repeatWithoutActivationIsRejectedDistinctly () =
    // v4-Kopf ohne --mission: die Pipeline weist die Aktion mit der
    // vertraglichen Klasse der fehlenden Aktivierung ab.
    let unactivated =
        runInProcess
            20260826u
            9000
            (explorationBody @ [ "intent 8900 repeat"; "intent 9200 repeat" ])
            true
            true
            true
            false

    if not (isNull unactivated.Mission) then
        failwith "Unaktivierter Lauf lieferte einen Missionsausweis."

    let activated =
        runInProcess
            20260826u
            9000
            (explorationBody @ [ "intent 8900 repeat"; "intent 9200 repeat" ])
            true
            true
            true
            true

    if activated.Mission.RepeatProtocol.Count <> 2 then
        failwith "Die Wiederholungsprotokollanzahl widerspricht der Skriptfolge."

    // Ein Zwilling ohne Aktivierung bleibt bei identischer Intentfolge
    // byteidentisch (Abschlussvertrag Abschnitt 9).
    if
        activated.StartStateHash <> unactivated.StartStateHash
        || activated.EndStateHash <> unactivated.EndStateHash
        || activated.IntervalHashes <> unactivated.IntervalHashes
    then
        failwith "Die Aktivierung der Abschlussschicht veraenderte die Kette."

let repeatUnderLegacyHeadersIsUnknownAction () =
    // repeat unter einem v1-/v2-/v3-Kopf ist UnknownAction mit bestehender
    // Bedeutung (keine stille Formatdrift innerhalb einer Version).
    let bodyLines = [ "intent 40 clear"; "intent 50 repeat" ]
    let body = String.concat "\n" bodyLines

    for header in [ "graybox-input-script-v1"; "graybox-input-script-v2"; "graybox-input-script-v3" ] do
        let script = header + " 9000\n" + body + "\nend\n"

        let raised =
            try
                InputScriptParser.Parse(Encoding.UTF8.GetBytes(script), rulesFor 9000) |> ignore
                None
            with
            | :? InputScriptException as detail -> Some detail
            | _ -> None

        match raised with
        | None -> failwith ("repeat unter " + header + " wurde nicht als UnknownAction abgewiesen.")
        | Some detail ->
            if detail.Reason <> InputScriptRejectReason.UnknownAction then
                failwith ("repeat unter " + header + " erhielt die Klasse " + string detail.Reason + " statt UnknownAction.")

    // Die v4-Grammatik ist eine strikte Obermenge: die Legacy-Verbmenge
    // bleibt unter ihren Koepfen gueltig, und der Kopf bindet das Format.
    let legacyBody = [ "intent 40 clear"; "intent 50 point 1000 1000"; "intent 60 switch"; "intent 70 choose-a" ]

    for (header, expectedFormat) in
        [ "graybox-input-script-v1", SessionContract.ScriptFormatId
          "graybox-input-script-v2", ModeContract.ScriptFormatIdV2
          "graybox-input-script-v3", DecisionContract.ScriptFormatIdV3
          "graybox-input-script-v4", MissionContract.ScriptFormatIdV4 ] do
        let lines =
            if header = "graybox-input-script-v1" then
                legacyBody |> List.filter (fun line -> not (line.Contains("switch") || line.Contains("choose")))
            else if header = "graybox-input-script-v2" then
                legacyBody |> List.filter (fun line -> not (line.Contains("choose")))
            else
                legacyBody

        let script = header + " 9000\n" + String.concat "\n" lines + "\nend\n"
        let parsed = InputScriptParser.Parse(Encoding.UTF8.GetBytes(script), rulesFor 9000)

        if parsed.FormatId <> expectedFormat then
            failwith ("Der Kopf " + header + " band nicht seine Formatkennung (" + parsed.FormatId + ").")

// ---------------------------------------------------------------------------
// Zwei-Ketten-Flow (AC-T039-02): Abschluss, Wiederholung, Optionsvarianz,
// Registrierungseindeutigkeit je Kette.
// ---------------------------------------------------------------------------

let twoChainFlowBindsCompletionRepeatAndOptionVariance () =
    let result = runInProcess 20260826u 17500 twoChainBody true true true true

    if result.StateChainSelfConsistent.HasValue && not result.StateChainSelfConsistent.Value then
        failwith "Der Zwei-Ketten-Lauf verlor seine Selbstkonsistenz."

    // Kette 2 Registrierung: abweichende Aufsuchfolge, jede Zone genau
    // einmal je Kette.
    let exploration = result.Exploration

    if exploration.VisitedCount <> 6 then
        failwith "Die Erkundung der neuen Kette registrierte nicht alle Landmarken."

    let zones = exploration.VisitProtocol |> Seq.map (fun visit -> visit.ZoneIndex) |> Seq.toList

    if zones <> [ 4; 2; 1; 5; 3; 0 ] then
        failwith $"Die Aufsuchfolge der neuen Kette wich vom Vertrag ab: {zones}."

    if Set.count (Set.ofList zones) <> 6 then
        failwith "Die neue Kette registrierte eine Zone mehrfach."

    // Optionsvarianz ohne Content: Kette 1 leitet (A=0, B=4) ab, Kette 2
    // (A=4, B=0) aus dem neuen Aufsuchprotokoll.
    let decision = result.Decision

    if
        decision.OptionZoneA <> 4
        || decision.OptionZoneB <> 0
        || decision.DecisionBoundaryTick <> 16500L
        || decision.Choice <> DecisionContract.ChoiceOptionBId
        || decision.FollowUpZoneIndex <> 0
    then
        failwith "Die zweite Kette leitete ihre Optionen nicht als reine Funktion des neuen Protokolls ab."

    // Erfolg der neuen Kette und abgeleiteter Abschluss.
    let pressure = result.Pressure

    if
        pressure.EndStatus <> PressureContract.EndStatusSuccess
        || pressure.CycleCount <> 1L
        || pressure.Windows.Count <> 1
    then
        failwith "Die Druckschicht der neuen Kette trug nicht die Zykluswahrheit ab 1."

    let mission = result.Mission

    if
        mission.CompletionState <> MissionContract.CompletionStateCompleted
        || mission.CompletionBoundaryTick <> 16500L
        || mission.ChainRunCount <> 2L
    then
        failwith "Der Zwei-Ketten-Lauf trug nicht den Abschluss- und Wiederholungsausweis."

    if
        mission.RepeatProtocol.Count <> 1
        || mission.RepeatProtocol[0].Disposition <> MissionContract.RepeatDispositionApplied
        || mission.RepeatProtocol[0].BoundaryTick <> 9400L
        || mission.RepeatProtocol[0].ChainRunAfter <> 2L
    then
        failwith "Die wirksame Wiederholung trug nicht ihr Protokoll."

// ---------------------------------------------------------------------------
// Beobachtungstreue (AC-T039-03): Zwilling ohne Aktivierung, Fremdseed,
// Legacy-Regressionen.
// ---------------------------------------------------------------------------

let missionLayerNeverTouchesSimulationOrHash () =
    let activated = runInProcess 20260826u 17500 twoChainBody true true true true

    let twin =
        runInProcess
            20260826u
            17500
            // Identische Intentfolge ohne die Abschlussaktivierung: die
            // Wiederholen-Aktion wird abgewiesen und veraendert nichts.
            twoChainBody
            true
            true
            true
            false

    if
        activated.StartStateHash <> twin.StartStateHash
        || activated.EndStateHash <> twin.EndStateHash
        || activated.IntervalSampleTicks <> twin.IntervalSampleTicks
        || activated.IntervalHashes <> twin.IntervalHashes
    then
        failwith "Die Abschluss- und Wiederholungsschicht veraenderte Simulationszustand oder Hash."

    if activated.KernelCommandsTotal <> twin.KernelCommandsTotal then
        failwith "Die Abschluss- und Wiederholungsschicht erzeugte einen Kernbefehl."

let foreignSeedChangesHashesButMissionStructureFollowsSession () =
    // Fremdseed-Negativfall nach T-036-Präzedenz: Hashes weichen ab; die
    // Strukturinvarianten der Abschlusswahrheit bleiben, die Grenzen folgen
    // der Sitzung (die Abschlussschicht liest den Seed niemals).
    let baseline = runInProcess 20260826u 17500 twoChainBody true true true true
    let foreign = runInProcess 7u 17500 twoChainBody true true true true

    if
        baseline.StartStateHash = foreign.StartStateHash
        || baseline.EndStateHash = foreign.EndStateHash
    then
        failwith "Ein fremder Seed aenderte Start- oder Endhash nicht nachweislich."

    // Strukturinvarianten: gleicher Abschlusszustand, gleiche
    // Kettenlaufzaehlung, gleiche Dispositionsfolge des Wiederholungs-
    // protokolls; die Vorgrenzen folgen der sitzungsabhängigen Ankunft.
    if
        baseline.Mission.CompletionState <> foreign.Mission.CompletionState
        || baseline.Mission.ChainRunCount <> foreign.Mission.ChainRunCount
        || baseline.Mission.RepeatProtocol.Count <> foreign.Mission.RepeatProtocol.Count
        || (baseline.Mission.RepeatProtocol,
            foreign.Mission.RepeatProtocol)
           ||> Seq.forall2 (fun baselineEntry foreignEntry ->
               baselineEntry.Disposition = foreignEntry.Disposition)
    then
        failwith "Ein fremder Seed aenderte die Struktur der Abschluss- oder Wiederholungswahrheit."

    // Die Landmarkenmenge bleibt seedunabhängig; die Aufsuchfolge folgt der
    // Sitzung.
    if baseline.Exploration.Landmarks <> foreign.Exploration.Landmarks then
        failwith "Ein fremder Seed aenderte die Landmarkenmenge."

let legacyPressureFixtureStaysChainIdenticalWithMission () =
    // Die Legacy-Kette (T-036-Fixture) bleibt mit Missionsaktivierung
    // ketten- und endhashidentisch; nur additive Sitzungsfelder entstehen.
    let fixturePath = Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")
    let rawBytes = File.ReadAllBytes(fixturePath)
    let intents = InputScriptParser.Parse(rawBytes, ScriptWindowRules(240, 11000)).Intents

    let runWith (mission: bool) =
        SessionEngine.Run(
            SessionRunRequest(
                Seed = 20260826u,
                ScriptedIntents = intents,
                WarmupTicks = 240,
                HorizonTicks = 11000,
                RunSelfConsistencyPass = false,
                ExplorationEnabled = true,
                DecisionEnabled = true,
                PressureEnabled = true,
                MissionEnabled = mission
            )
        )

    let baseline = runWith false
    let activated = runWith true

    if
        baseline.EndStateHash <> activated.EndStateHash
        || baseline.IntervalHashes <> activated.IntervalHashes
    then
        failwith "Die Legacy-Kette wich mit Missionsaktivierung ab."

    // Die Missionswahrheit der Legacy-Kette: Erfolg am Endstatus, Abschluss
    // abgeleitet, eine Kette, keine Wiederholung.
    let lastWindowArrival =
        baseline.Pressure.Windows[baseline.Pressure.Windows.Count - 1].ArrivalBoundaryTick

    if
        activated.Mission.CompletionState <> MissionContract.CompletionStateCompleted
        || activated.Mission.CompletionBoundaryTick <> lastWindowArrival
        || activated.Mission.ChainRunCount <> 1L
        || activated.Mission.RepeatProtocol.Count <> 0
    then
        failwith "Die Legacy-Kette trug nicht den abgeleiteten Abschlussausweis."

// ---------------------------------------------------------------------------
// Savevertrag V3 (AC-T039-02/03): additive Sektionsflaeche, Legacy-
// Kompatibilitaet, Aktivierungsgrenzen.
// ---------------------------------------------------------------------------

let private populatedSectionWith
    (missionActive: byte)
    (missionChainRunCount: int64)
    (pressureActive: byte)
    : SessionSectionState =
    SessionSectionState(
        ActiveMode = byte SessionMode.Personal,
        PendingSwitches = [],
        ExplorationActive = 1uy,
        ExplorationVisits =
            [ SessionSectionVisit(1200L, 2, SessionSectionCodec.ModePersonal)
              SessionSectionVisit(2400L, 0, SessionSectionCodec.ModePersonal) ],
        DecisionActive = 1uy,
        DecisionOfferOpened = 1uy,
        DecisionOfferBoundaryTick = 3000L,
        DecisionOptionZoneA = 2,
        DecisionOptionZoneB = 0,
        DecisionDecided = 1uy,
        DecisionBoundaryTick = 3100L,
        DecisionChoiceKind = SessionSectionCodec.ChoiceKindA,
        DecisionModeKind = SessionSectionCodec.ModePersonal,
        DecisionFollowUpZoneIndex = 2,
        DecisionFollowUpCompleted = 0uy,
        DecisionArrivalBoundaryTick = -1L,
        DecisionRejectionsBeforeOffer = 0L,
        DecisionRejectionsInStrategicMode = 0L,
        DecisionRejectionsAfterDecision = 0L,
        PressureActive = pressureActive,
        PressureCycleCount = 0L,
        PressureWindows = [],
        PressureLastFailureBoundaryTick = -1L,
        PressureHasLastFailure = 0uy,
        PressureLastFailureFollowUpZoneIndex = -1,
        PressureLastReopenBoundaryTick = -1L,
        PressureReopenPendingRecording = 0uy,
        MissionActive = missionActive,
        MissionChainRunCount = missionChainRunCount
    )

let private populatedSection () : SessionSectionState = populatedSectionWith 1uy 3L 1uy

/// Wrapper der C#-Valuetupel-Rueckgabe als F#-Optionen (lesbare Pruefung).
let private decodeSection (bytes: byte[]) : SessionSectionRejection option * SessionSectionState option =
    let struct (rejection, state) = SessionSectionCodec.Decode(bytes)
    (Option.ofObj rejection, Option.ofObj state)

/// Ehrliche Legacy-Sektionsversion 1 (Savevertrag V2, Abschnitt 13): der
/// gebundene Leerstand ohne Missionsflaeche.
let private legacyEmptySection () : SessionSectionState =
    SessionSectionState(
        ActiveMode = 0uy,
        PendingSwitches = [],
        ExplorationActive = 0uy,
        ExplorationVisits = [],
        DecisionActive = 0uy,
        DecisionOfferOpened = 0uy,
        DecisionOfferBoundaryTick = -1L,
        DecisionOptionZoneA = -1,
        DecisionOptionZoneB = -1,
        DecisionDecided = 0uy,
        DecisionBoundaryTick = -1L,
        DecisionChoiceKind = SessionSectionCodec.ChoiceKindUnset,
        DecisionModeKind = 0uy,
        DecisionFollowUpZoneIndex = -1,
        DecisionFollowUpCompleted = 0uy,
        DecisionArrivalBoundaryTick = -1L,
        DecisionRejectionsBeforeOffer = 0L,
        DecisionRejectionsInStrategicMode = 0L,
        DecisionRejectionsAfterDecision = 0L,
        PressureActive = 0uy,
        PressureCycleCount = 0L,
        PressureWindows = [],
        PressureLastFailureBoundaryTick = -1L,
        PressureHasLastFailure = 0uy,
        PressureLastFailureFollowUpZoneIndex = -1,
        PressureLastReopenBoundaryTick = -1L,
        PressureReopenPendingRecording = 0uy,
        MissionActive = 0uy,
        MissionChainRunCount = 0L,
        SectionVersion = int SessionSectionCodec.LegacySectionVersion
    )

let sessionSectionV2CarriesMissionFieldsAndLegacyV1StaysEmpty () =
    // Sektionsversion 2: Roundtrip mit Missionsflaeche ist byteidentisch.
    let v2State = populatedSection ()
    let v2Bytes = SessionSectionCodec.Encode(v2State)

    let (v2Rejection, v2Decoded) = decodeSection v2Bytes

    if
        v2Rejection.IsSome
        || v2Decoded.Value.MissionActive <> 1uy
        || v2Decoded.Value.MissionChainRunCount <> 3L
        || v2Decoded.Value.SectionVersion <> int SessionSectionCodec.CurrentSectionVersion
    then
        failwith "Die Sektionsversion 2 trug die Missionsflaeche nicht."

    if not ((SessionSectionCodec.Encode v2Decoded.Value).AsSpan().SequenceEqual(v2Bytes.AsSpan())) then
        failwith "Die Sektionsversion 2 verletzte die Re-Encoding-Gleichheit."

    // Legacy-Sektionsversion 1: ehrliche, maschinenlesbare Missionsleere
    // ohne Migrationserfindung; Re-Encoding bleibt versionsgetreu.
    let legacyBytes = SessionSectionCodec.Encode(legacyEmptySection ())

    let (legacyRejection, legacyDecoded) = decodeSection legacyBytes

    if
        legacyRejection.IsSome
        || legacyDecoded.Value.MissionActive <> 0uy
        || legacyDecoded.Value.MissionChainRunCount <> 0L
        || legacyDecoded.Value.SectionVersion <> int SessionSectionCodec.LegacySectionVersion
    then
        failwith "Die Legacy-Sektionsversion 1 trug nicht die ehrliche Missionsleere."

    if not ((SessionSectionCodec.Encode legacyDecoded.Value).AsSpan().SequenceEqual(legacyBytes.AsSpan())) then
        failwith "Die Legacy-Sektionsversion 1 verletzte die versionsgetreue Re-Encoding-Gleichheit."

    // Zukuenftige Sektionsversionen bleiben ohne Migrationserfindung
    // abgewiesen.
    let futureBytes = Array.copy v2Bytes
    futureBytes[0] <- 3uy

    let (futureRejection, _) = decodeSection futureBytes

    if futureRejection.IsNone || futureRejection.Value.Class <> SessionSectionRejectionClass.Invalid then
        failwith "Die Sektionsversion 3 wurde nicht ohne Migrationserfindung abgewiesen."

    // Relationswahrheiten der Missionsflaeche (fail-closed).
    let invalidState (missionActive: byte) (count: int64) (pressureActive: byte) =
        populatedSectionWith missionActive count pressureActive

    for (active, count, pressure) in [ (0uy, 2L, 1uy); (1uy, 0L, 1uy); (1uy, 1L, 0uy) ] do
        let bytes = SessionSectionCodec.Encode(invalidState active count pressure)
        let struct (rejection, _) = SessionSectionCodec.Decode(bytes)

        if
            isNull rejection
            || rejection.Class <> SessionSectionRejectionClass.Invalid
        then
            failwith $"Die Missionsrelation ({active}, {count}, {pressure}) wurde nicht fail-closed abgewiesen."

// ---------------------------------------------------------------------------
// CLI-Vertrag (AC-T039-02/05): Schemaversion 7, Save-/Lade-Rundtrip,
// Kopplung und stabile Exitcodes ohne neue Bedeutung.
// ---------------------------------------------------------------------------

let private freshSlotDir () =
    let dir =
        Path.Combine(Path.GetTempPath(), "t039-mission-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(dir) |> ignore
    dir

let cliMissionFlowRunsSchema7WithSaveLoadRoundtrip () =
    let slotDir = freshSlotDir ()
    let fixturePath = Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t039-completion-repeat.graybox")

    try
        let baseArguments =
            [ "kommandoschleife"
              "--scenario"; "kommando-graybox"
              "--input-script"; fixturePath
              "--seed"; "20260826"
              "--warmup-ticks"; "240"
              "--horizon-ticks"; "17500"
              "--exploration"
              "--decision"
              "--pressure"
              "--mission"
              "--slot-dir"; slotDir
              "--slot"; "m.rwsaved" ]

        // Speicherlauf in Kette 2 nach der Wiederholung.
        let (saveExit, _, saveStderr) =
            runAppHost (
                Array.ofList (baseArguments @ [ "--report"; Path.Combine(slotDir, "save.json"); "--save-at-tick"; "9600" ])
            )

        if saveExit <> 0 then
            failwith $"Speicherlauf endete mit {saveExit}: {saveStderr}"

        let saveReport =
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(slotDir, "save.json")))

        if saveReport.GetProperty("schemaVersion").GetInt32() <> 7 then
            failwith "Der Missions-Speicherlauf trug nicht die Schemaversion 7."

        let saveSection = saveReport.GetProperty("continuation").GetProperty("sessionSection")

        if saveSection.GetProperty("sectionVersion").GetInt32() <> 2 then
            failwith "Der Missions-Speicherlauf schrieb nicht die Sektionsversion 2."

        let saveMission = saveReport.GetProperty("missionSession")

        if saveMission.GetProperty("chainRunCount").GetInt64() <> 2L then
            failwith "Der Speicherlauf trug die Kettenlaufzaehlung der neuen Kette nicht."

        // Fortsetzungslauf: frischer Prozess, laedt den Slot, Kette 2
        // schlaegt erneut als Erfolg ab.
        let (loadExit, _, loadStderr) =
            runAppHost (
                Array.ofList (baseArguments @ [ "--report"; Path.Combine(slotDir, "load.json"); "--load-slot" ])
            )

        if loadExit <> 0 then
            failwith $"Fortsetzungslauf endete mit {loadExit}: {loadStderr}"

        let loadReport =
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(slotDir, "load.json")))

        let continuation = loadReport.GetProperty("continuation")

        if not (continuation.GetProperty("chainContinuity").GetProperty("verified").GetBoolean()) then
            failwith "Der Missions-Fortsetzungslauf verlor seine Kettenfortsetzung."

        let restoredMission = continuation.GetProperty("restored").GetProperty("mission")

        if
            not (restoredMission.GetProperty("active").GetBoolean())
            || restoredMission.GetProperty("chainRunCount").GetInt64() <> 2L
        then
            failwith "Die restaurierte Missionswahrheit trug nicht die Kettenlaufzaehlung."

        let loadMission = loadReport.GetProperty("missionSession")

        if
            loadMission.GetProperty("completion").GetProperty("state").GetString()
            <> MissionContract.CompletionStateCompleted
            || loadMission.GetProperty("chainRunCount").GetInt64() <> 2L
        then
            failwith "Der Fortsetzungslauf trug nicht den fortgesetzten Abschlusszustand."
    finally
        if Directory.Exists(slotDir) then
            Directory.Delete(slotDir, true)

let cliMissionCouplingAndExitCodesStayStable () =
    let fixturePath = Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t039-completion-repeat.graybox")

    // --mission ohne --pressure: bestehende Usage-Bedeutung (2), keine neue.
    let (usageExit, _, _) =
        runAppHost (
            [| "kommandoschleife"
               "--scenario"; "kommando-graybox"
               "--input-script"; fixturePath
               "--seed"; "20260826"
               "--report"; Path.Combine(freshSlotDir (), "unused.json")
               "--warmup-ticks"; "240"
               "--horizon-ticks"; "17500"
               "--exploration"
               "--decision"
               "--mission" |]
        )

    if usageExit <> ExitCodes.Usage then
        failwith $"--mission ohne --pressure ergab {usageExit} statt der bestehenden Usage-Bedeutung."

    // Legacyskripte unter v3 bleiben gueltig; Schema 5 (Druck) unveraendert.
    let legacyFixture = Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")
    let legacyDir = freshSlotDir ()

    try
        let (legacyExit, _, legacyStderr) =
            runAppHost (
                [| "kommandoschleife"
                   "--scenario"; "kommando-graybox"
                   "--input-script"; legacyFixture
                   "--seed"; "20260826"
                   "--report"; Path.Combine(legacyDir, "legacy.json")
                   "--warmup-ticks"; "240"
                   "--horizon-ticks"; "11000"
                   "--exploration"
                   "--decision"
                   "--pressure" |]
            )

        if legacyExit <> 0 then
            failwith $"Legacyskriptlauf endete mit {legacyExit}: {legacyStderr}"

        let legacyReport =
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(legacyDir, "legacy.json")))

        if legacyReport.GetProperty("schemaVersion").GetInt32() <> 5 then
            failwith "Der Legacyskriptlauf veraenderte die Schemalinie (5 erwartet)."

        if legacyReport.GetProperty("commandContract").GetProperty("scriptFormat").GetString() <> "graybox-input-script-v3" then
            failwith "Der Legacyskriptlauf veraenderte die Formatkennung."
    finally
        if Directory.Exists(legacyDir) then
            Directory.Delete(legacyDir, true)

// ---------------------------------------------------------------------------
// Schema-Matrix (AC-T039-02/03): relationale fail-closed Bindungen des
// Missionsblocks.
// ---------------------------------------------------------------------------

let private goldenMissionishJsonWithCompletionState (state: string) =
    // Vollstaendiger Schemaversion-7-Report eines Zwei-Ketten-Laufs mit
    // kontrolliert austauschbarem Abschlusszustand.
    let completionBlock =
        if state = MissionContract.CompletionStateCompleted then
            "{\"state\":\"completed\",\"boundaryTick\":16500,\"reason\":null,\"gateCoupled\":false}"
        else
            "{\"state\":\"open\",\"boundaryTick\":-1,\"reason\":\"no-cycle-success-within-run\",\"gateCoupled\":false}"

    let missionSession =
        "{\"contract\":{\"document\":\"docs/ABSCHLUSSVERTRAG.md\",\"version\":\"1\"},"
        + "\"activationId\":\"opt-in-mission-activation-v1\","
        + "\"completionModel\":\"derived-completion-state-pure-function-v1\","
        + "\"completionBoundaryModel\":\"derived-completion-first-boundary-observation-v1\","
        + "\"repeatActivationModel\":\"script-v4-plus-keymap-repeat-action-v1\","
        + "\"resetScope\":\"full-chain-restart-including-visit-protocol-v1\","
        + "\"completion\":" + completionBlock + ","
        + "\"chainRunCount\":1,"
        + "\"repeatProtocol\":[],"
        + "\"repeatRejectionsBeforeCompletion\":0,"
        + "\"persistence\":{\"statementId\":\"mission-chain-run-counter-persisted-v1\",\"persisted\":true,\"saveLoad\":\"continued\",\"replay\":\"not-continued\",\"completionStatePersisted\":false,\"gateCoupled\":false},"
        + "\"gateCoupled\":false,"
        + "\"hud\":{\"measured\":false,\"kind\":\"title-hud-mission-completion-v1\",\"reason\":\"headless-run-without-window\"},"
        + "\"repeatKeymap\":{\"measured\":false,\"kind\":\"script-v4-plus-keymap-repeat-action-v1\",\"reason\":\"headless-run-without-window\"}}"

    "{\"schemaVersion\":7,\"mode\":\"kommandoschleife\",\"executionMode\":\"headless\","
    + "\"command\":\"x\",\"scenario\":{\"id\":\"kommando-graybox\",\"seed\":1,\"tickRateHz\":20,\"agentCount\":250,\"worldId\":\"riftward-simworld-graybox-v1\",\"content\":\"synthetic-graybox-command-loop\"},"
    + "\"commandContract\":{\"document\":\"docs/KOMMANDOVERTRAG.md\",\"version\":\"1\",\"scriptFormat\":\"graybox-input-script-v4\",\"selectionModel\":\"graybox-selection-model-v0\",\"cameraModel\":\"graybox-camera-model-v0\",\"diagnosticOnlyReplayDisclaimer\":true,\"modeContract\":{\"document\":\"docs/MODEVERTRAG.md\",\"version\":\"2\"}},"
    + "\"modeSession\":{\"contract\":{\"document\":\"docs/MODEVERTRAG.md\",\"version\":\"2\"},\"initialMode\":\"strategic\",\"finalMode\":\"strategic\",\"switchProtocol\":[],\"strategyIntentsRejectedInPersonalMode\":0,\"steerIntentsRejectedInStrategyMode\":0,\"steerIdleDedupes\":0,\"interactiveContextRejections\":0,\"hud\":{\"measured\":false,\"kind\":\"title-hud-mode-herozone-v1\",\"reason\":\"headless-run-without-window\"},\"switchReactionTicks\":{\"unit\":\"ticks\",\"method\":\"mode-switch-intent-tick-to-first-validity-boundary-in-new-mode\",\"p50\":0,\"p95\":0,\"p99\":0,\"max\":0,\"count\":0,\"target\":2,\"hardLimit\":3,\"gateCoupled\":false}},"
    + "\"explorationSession\":{\"contract\":{\"document\":\"docs/ERKUNDUNGSVERTRAG.md\",\"version\":\"3\"},\"activationId\":\"opt-in-exploration-activation-v1\",\"landmarkModel\":\"graybox-landmark-zone-anchor-v1\",\"visitRule\":\"boundary-visit-personal-mode-only-v1\",\"counterModel\":\"session-local-visit-counter-v1\",\"landmarks\":[],\"visitProtocol\":[],\"progress\":{\"visitedCount\":0,\"landmarkCount\":6,\"completed\":false,\"gateCoupled\":false},\"persistence\":{\"statementId\":\"session-local-save-load-persisted-v2\",\"persisted\":true,\"saveLoad\":\"continued\",\"replay\":\"not-continued\",\"gateCoupled\":false},\"gateCoupled\":false,\"hud\":{\"measured\":false,\"kind\":\"title-hud-expedition-progress-v1\",\"reason\":\"headless-run-without-window\"},\"landmarkChannel\":{\"measured\":false,\"kind\":\"landmark-state-channel-v1\",\"reason\":\"headless-run-without-window\"}},"
    + "\"decisionSession\":{\"contract\":{\"document\":\"docs/ENTSCHEIDUNGSVERTRAG.md\",\"version\":\"4\"},\"activationId\":\"opt-in-decision-activation-v1\",\"offerRule\":\"completion-gated-decision-offer-v1\",\"optionsModel\":\"visit-protocol-zone-options-v1\",\"choiceScopingRule\":\"decision-choose-personal-mode-only-v1\",\"followUpRule\":\"chosen-zone-follow-up-objective-v1\",\"arrivalRule\":\"boundary-arrival-personal-mode-only-v1\",\"offer\":{\"opened\":false,\"boundaryTick\":-1,\"optionZoneA\":-1,\"optionZoneB\":-1,\"reason\":\"exploration-not-completed-within-run\"},\"decision\":{\"decided\":false,\"boundaryTick\":-1,\"choice\":null,\"mode\":null,\"optionZone\":-1},\"followUp\":{\"zoneIndex\":-1,\"completed\":false,\"arrivalBoundaryTick\":-1,\"gateCoupled\":false},\"rejections\":{\"beforeOffer\":0,\"inStrategicMode\":0,\"afterDecision\":0,\"gateCoupled\":false},\"persistence\":{\"statementId\":\"decision-session-local-save-load-persisted-v3\",\"persisted\":true,\"saveLoad\":\"continued\",\"replay\":\"not-continued\",\"gateCoupled\":false},\"gateCoupled\":false,\"hud\":{\"measured\":false,\"kind\":\"title-hud-decision-objective-v1\",\"reason\":\"headless-run-without-window\"},\"followUpChannel\":{\"measured\":false,\"kind\":\"follow-up-marker-channel-v1\",\"reason\":\"headless-run-without-window\"}},"
    + "\"pressureSession\":{\"contract\":{\"document\":\"docs/DRUCKVERTRAG.md\",\"version\":\"3\"},\"activationId\":\"opt-in-pressure-activation-v1\",\"triggerId\":\"decision-coupled-window-v1\",\"timeBasisId\":\"fixed-deterministic-tick-window-v1\",\"failureRuleId\":\"defined-failure-automatic-reopen-v1\",\"restartModelId\":\"session-local-cycle-restart-v1\",\"successRuleId\":\"unchanged-decision-arrival-within-window-v1\",\"windowLengthTicks\":600,\"cycleCount\":0,\"windows\":[],\"lastFailure\":{\"boundaryTick\":null,\"cause\":null,\"gateCoupled\":false},\"reopenBoundaryTick\":-1,\"endStatus\":{\"status\":\"not-started\",\"reason\":\"decision-not-reached-within-run\",\"gateCoupled\":false},\"persistence\":{\"statementId\":\"pressure-session-local-save-load-persisted-v2\",\"persisted\":true,\"saveLoad\":\"continued\",\"replay\":\"not-continued\",\"gateCoupled\":false},\"gateCoupled\":false,\"hud\":{\"measured\":false,\"kind\":\"title-hud-pressure-window-v1\",\"reason\":\"headless-run-without-window\"},\"restartIndicator\":{\"measured\":false,\"kind\":\"pressure-restart-indicator-channel-v1\",\"reason\":\"headless-run-without-window\"}},"
    + "\"missionSession\":" + missionSession + ","
    + "\"simulationContract\":{\"document\":\"docs/SIMULATIONSVERTRAG.md\",\"version\":\"1\",\"numericModel\":\"q16-16-fixed-point-intonly-v1\",\"hashAlgorithm\":\"fnv1a64-canonical-chain-v1\",\"allocationLimitBytesPerWarmTick\":0},"
    + "\"inputScript\":{\"scriptSha256\":\"cbcab89e6961e4bfeaad33f3dde8b63cd17c27e892f66d11b6396cd8c51ffc33\",\"intentPlanHash\":\"4b891064971749c2\",\"horizonTicks\":17500,\"warmupTicks\":240,\"intentsTotal\":1,\"appliedTotal\":0,\"rejectedTotal\":0,\"emptyPointDeselects\":0,\"moveWithoutSelectionRejects\":0,\"noZoneRejects\":0,\"kernelCommandsTotal\":0},"
    + "\"startedAtUtc\":\"2026-08-31T07:42:17.6202299Z\",\"finishedAtUtc\":\"2026-08-31T07:42:18.266729Z\","
    + "\"environment\":{\"os\":{\"type\":\"Linux\",\"kernelRelease\":\"fixture\"},\"cpu\":{\"model\":\"fixture\"},\"rid\":\"linux-x64\",\"commit\":\"068974c9e606e6b023d4708ffc7cc12be5dda7a9\",\"buildMode\":\"Release\",\"display\":{\"measured\":false,\"reason\":\"headless-mode-native-artifacts-not-loaded\"},\"pins\":[{\"id\":\"sdl3\",\"refType\":\"tag\",\"ref\":\"release-3.4.14\",\"commit\":\"c1\",\"sourceSha256\":\"h1\",\"licenseSpdx\":\"zlib\"},{\"id\":\"bgfx\",\"refType\":\"commit\",\"ref\":\"c2\",\"commit\":\"c2\",\"sourceSha256\":\"h2\",\"licenseSpdx\":\"BSD-2-Clause\"},{\"id\":\"bx\",\"refType\":\"commit\",\"ref\":\"c3\",\"commit\":\"c3\",\"sourceSha256\":\"h3\",\"licenseSpdx\":\"BSD-2-Clause\"},{\"id\":\"bimg\",\"refType\":\"commit\",\"ref\":\"c4\",\"commit\":\"c4\",\"sourceSha256\":\"h4\",\"licenseSpdx\":\"BSD-2-Clause\"}]},"
    + "\"measurement\":{\"warmupTicks\":240,\"sampleTicks\":100,\"ticksExecuted\":17500,\"hashSampleIntervalTicks\":60,\"rssSampleIntervalTicks\":60,\"windowCompleted\":true},"
    + "\"metrics\":{\"tickTimeMs\":{\"unit\":\"ms\",\"method\":\"stopwatch-tick-delta\",\"p50\":0.2,\"p95\":0.3,\"p99\":0.4},\"managedAllocationsBytes\":{\"unit\":\"bytes\",\"method\":\"gc-total-allocated-bytes-precise-delta-per-tick-sum\",\"perWarmTick\":0},\"reactionTicks\":{\"unit\":\"ticks\",\"method\":\"command-submission-tick-to-first-effect-state-hash-delta\",\"p50\":1,\"p95\":1,\"p99\":1,\"max\":1,\"count\":0,\"target\":2,\"hardLimit\":3},\"runtimeShaderCompilation\":{\"unit\":\"bool\",\"method\":\"offline-shaderc-binaries-only\",\"value\":false},\"gcPauseSumMs\":{\"unit\":\"ms\",\"method\":\"gc-get-total-pause-duration-delta\",\"value\":0,\"gateCoupled\":false},\"gcPauseCount\":{\"unit\":\"count\",\"method\":\"gc-collection-count-gen0-to2-delta\",\"value\":0,\"gateCoupled\":false},\"activeAgents\":{\"unit\":\"count\",\"method\":\"soa-agent-count-fixed\",\"value\":250,\"gateCoupled\":false},\"workingSetKiB\":{\"measured\":false,\"reason\":\"headless-session-does-not-sample-rss\"},\"frameTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"},\"gpuTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"},\"drawSubmitCallsPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"},\"visibleTrianglesPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"},\"concurrentMarkers\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}},"
    + "\"stateHashChain\":{\"unit\":\"hex64\",\"method\":\"fnv1a64-canonical-chain-v1\",\"start\":\"20f84cdb183a4364\",\"intervalSampleTicks\":[240],\"intervalHashes\":[\"20f84cdb183a4364\"],\"end\":\"20f84cdb183a4364\"},"
    + "\"gate\":{\"limits\":{\"p99TickTimeHardLimitMs\":16,\"p99TickTimeTargetMs\":8,\"allocationsPerWarmTickBytesMax\":0,\"reactionHardLimitTicks\":3,\"reactionTargetTicks\":2,\"runtimeShaderCompilationAllowed\":false,\"switchReactionHardLimitTicks\":3,\"switchReactionTargetTicks\":2},\"stateChainSelfConsistency\":{\"evaluated\":true},\"switchReaction\":{\"evaluated\":false,\"reason\":\"no-effective-mode-switch-in-run\"},\"pass\":true,\"tickTimeTargetMet\":true,\"reactionTargetMet\":true,\"violations\":[]},"
    + "\"openQuestions\":{\"qtec004\":\"open\",\"qtec006\":\"open\",\"qtec010\":\"open\",\"qgam001\":\"open\",\"qgam002\":\"open\",\"qgam003\":\"open\",\"qgam004\":\"open\",\"qgam005\":\"open\",\"qgam006\":\"open\",\"qgam007\":\"open\",\"qgam010\":\"open\",\"qnar002\":\"open\"},"
    + "\"profiles\":[{\"id\":\"hw-pc-min\",\"status\":\"NOT-MEASURED\",\"boundReferenceClass\":null,\"reason\":\"mandatory-profile-not-measured-no-reference-hardware\"},{\"id\":\"hw-mac-min\",\"status\":\"NOT-MEASURED\",\"boundReferenceClass\":null,\"reason\":\"mandatory-profile-not-measured-no-reference-hardware\"},{\"id\":\"hw-pc-high\",\"status\":\"NOT-MEASURED\",\"boundReferenceClass\":null,\"reason\":\"mandatory-profile-not-measured-no-reference-hardware\"}],"
    + "\"baseline\":{\"classification\":\"diagnostic-developer-workstation\",\"protocol\":\"qops001-2026-08-24\"},"
    + "\"frameEvidence\":{\"captured\":false,\"reason\":\"capture-not-requested\"},\"exitCode\":0}"

let missionSchemaRelationsRejectFabricationFailClosed () =
    // Ein vollstaendiger Zwei-Ketten-Lauf traegt konsistente relationale
    // Wahrheiten; die Fabrikationsmatrix weist Verletzungen fail-closed ab.
    let result = runInProcess 20260826u 17500 twoChainBody true true true true

    let mission = result.Mission

    if
        mission.CompletionState = MissionContract.CompletionStateCompleted
        && mission.CompletionBoundaryTick = MissionSession.UnsetBoundaryTick
    then
        failwith "Ein abgeschlossener Lauf trug keinen Abschlussgrenzenausweis."

    if
        mission.CompletionState = MissionContract.CompletionStateOpen
        && mission.CompletionStateReason <> MissionContract.OpenReasonNoCycleSuccess
    then
        failwith "Ein offener Lauf trug nicht den ehrlichen Grund."

    // Teilmatrix: ohne Zykluserfolg ist die Ableitung nie abgeschlossen.
    let openRun =
        runInProcess 20260826u 9000 explorationBody true true true true

    if openRun.Mission.CompletionState <> MissionContract.CompletionStateOpen then
        failwith "Die Ableitung schloss eine Kette ohne Zykluserfolg."

    // Der relationale Schema-Knoten weist Fabrikationen fail-closed ab:
    // ein abgeschlossener Ausweis ohne Schichtkonsistenz ist unzulaessig.
    let fabrication = goldenMissionishJsonWithCompletionState MissionContract.CompletionStateCompleted
    let errors = CommandReportSchema.Validate(fabrication)
    let joined = String.concat "; " errors

    if
        errors.Count = 0
        || not (joined.Contains("ein Abschluss existiert nur nach dem Zykluserfolg der Schichten", StringComparison.Ordinal))
    then
        failwith "Die relationale Abschlussbindung wies die Fabrikation nicht ab."

    // Kettenlaufzaehlung konsistent: eine verschobene Zaehlung ohne
    // zugehoerige Wiederholung wird abgewiesen.
    let drifted = goldenMissionishJsonWithCompletionState MissionContract.CompletionStateOpen
    let driftedErrors = CommandReportSchema.Validate(drifted.Replace("\"chainRunCount\":1", "\"chainRunCount\":5"))

    if driftedErrors.Count = 0 then
        failwith "Die Kettenlaufzaehlungsbindung akzeptierte einen Drift."

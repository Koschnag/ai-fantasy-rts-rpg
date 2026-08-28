module DecisionTests

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open Riftward.App
open Riftward.App.Command
open Riftward.Platform
open Riftward.Session
open Riftward.Simulation

// ---------------------------------------------------------------------------
// T-035: kleinster spielbarer Entscheidungsschritt (Entscheidungsvertrag V1,
// Abschnitte 0 bis 12). Jede Pruefung bindet Code, Vertragsdokument,
// Schemavertrag und Laufverhalten gegeneinander; keine Pruefung antwortet auf
// eine offene Produktfrage und keine veraendert Riftward.Simulation.
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

let private runToleratingTransientGate arguments =
    let exitCode, stdout, stderr = runAppHost arguments

    if exitCode = ExitCodes.Map(PlatformErrorCode.CommandGateViolated) then
        runAppHost arguments
    else
        exitCode, stdout, stderr

let private rulesFor horizon = ScriptWindowRules(40, horizon)

let private v3Script (horizon: int) (bodyLines: string list) =
    let body = String.concat "\n" bodyLines
    $"graybox-input-script-v3 {horizon}\n{body}\nend\n"

let private parseScript (horizon: int) (bodyLines: string list) =
    InputScriptParser.Parse(Encoding.UTF8.GetBytes(v3Script horizon bodyLines), rulesFor horizon).Intents

/// Erkundungs-/Mobilmachungskern der Entscheidungsskripte: der gebundene
/// T-034-Kern (aus der Fixture t034-exploration-separated.graybox) bildet die
/// kernelintente Identische Basis; die Entscheidungsfabrikationen haengen
/// ausschließlich sitzungsseitige choose-Aktionen an.
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

/// Vertraglicher vollstaendiger Entscheidungsflow (Fixturkern): Erkundung
/// laeuft bis zum Abschluss (Angebot an der Abschlussgrenze), Wahl im
/// persoenlichen Modus; Wahl B endet in der zuletzt registrierten Zone, in
/// der der Held an der Entscheidungsgrenze bereits steht.
let private decisionBody (chooseLine: string): string list =
    explorationBody @ [ chooseLine ]

let private runInProcess
    (seed: uint32)
    (horizon: int)
    (bodyLines: string list)
    (decisionEnabled: bool)
    : SessionRunResult =
    let intents = parseScript horizon bodyLines

    SessionEngine.Run(
        SessionRunRequest(
            Seed = seed,
            ScriptedIntents = intents,
            WarmupTicks = 240,
            HorizonTicks = horizon,
            RunSelfConsistencyPass = false,
            ExplorationEnabled = true,
            DecisionEnabled = decisionEnabled
        )
    )

// ---------------------------------------------------------------------------
// Spiegeltest (AC-T035-01/05): Code, Keymap, Marker und Vertragsdokument
// haelt ein Test.
// ---------------------------------------------------------------------------

let decisionContractMirrorsDocumentedValues () =
    if DecisionContract.DocumentPath <> "docs/ENTSCHEIDUNGSVERTRAG.md" then
        failwith "Entscheidungsvertragspfad falsch."

    if DecisionContract.ContractVersion <> "1" then
        failwith "Entscheidungsvertragsversion falsch."

    if
        CommandReportSchema.VersionWithoutExploration <> 2
        || CommandReportSchema.CurrentVersion <> 3
        || CommandReportSchema.VersionWithDecision <> 4
        || DecisionContract.ReportSchemaVersionWithDecision <> 4
    then
        failwith "Schemaversionen entsprechen nicht dem Vertrag (Bestand 2, Erkundung 3, Entscheidung 4)."

    if DecisionContract.Persisted then
        failwith "Die Nichtpersistenzaussage ist verletzt."

    if
        InteractiveView.FollowUpMarkerLowerHeightMeters <> 1.2
        || InteractiveView.FollowUpMarkerMiddleHeightMeters <> 2.4
        || InteractiveView.FollowUpMarkerUpperHeightMeters <> 3.6
        || InteractiveView.FollowUpMarkerLowerSize <> 1.30f
        || InteractiveView.FollowUpMarkerMiddleSize <> 1.15f
        || InteractiveView.FollowUpMarkerUpperSize <> 1.00f
        || InteractiveView.FollowUpMarkerRed <> 0.86f
        || InteractiveView.FollowUpMarkerGreen <> 0.45f
        || InteractiveView.FollowUpMarkerBlue <> 0.98f
    then
        failwith "Folgezielmarker-Abmessungen entsprechen nicht dem dreistufigen Zweikanalvertrag."

    // Interaktive Keymap-Bindung innerhalb der bestehenden Familie
    // (Vertrag Abschnitt 4): die zwei neuen Aktionen sind frei belegbar,
    // Standard Zifferntaste 1/2.
    if
        not (
            Keymap.SemanticActions
            |> Array.exists (fun action -> action = Keymap.ChooseAActionName)
        )
        || not (
            Keymap.SemanticActions
            |> Array.exists (fun action -> action = Keymap.ChooseBActionName)
        )
    then
        failwith "Keymap-Familie fuehrt die Entscheidungsaktionen nicht."

    match Keymap.Defaults.TryGetValue(Keymap.ChooseAActionName) with
    | true, scancodes when scancodes = [| 30 |] -> ()
    | _ -> failwith "Standardbelegung choose-a ist nicht Zifferntaste 1 (Scancode 30)."

    match Keymap.Defaults.TryGetValue(Keymap.ChooseBActionName) with
    | true, scancodes when scancodes = [| 31 |] -> ()
    | _ -> failwith "Standardbelegung choose-b ist nicht Zifferntaste 2 (Scancode 31)."

    let document = readDocument DecisionContract.DocumentPath

    for identifier in
        [ DecisionContract.ActivationId
          DecisionContract.OfferRuleId
          DecisionContract.OptionsModelId
          DecisionContract.ChoiceScopingRuleId
          DecisionContract.FollowUpRuleId
          DecisionContract.ArrivalRuleId
          DecisionContract.NotPersistedStatementId
          DecisionContract.HudModelId
          DecisionContract.FollowUpChannelModelId
          DecisionContract.ScriptFormatIdV3
          DecisionContract.ReportBlockId
          DecisionContract.RejectReasonInsufficientDistinctZones
          DecisionContract.OfferNotOpenedReason
          DecisionContract.RejectReasonDecisionNotActivated
          DecisionContract.RejectReasonChooseBeforeOffer
          DecisionContract.RejectReasonChooseInStrategicMode
          DecisionContract.RejectReasonChooseAfterDecision ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Entscheidungsvertragsdokument nennt die Kennung {identifier} nicht."

    for anchor in
        [ "--decision"
          " — Entscheidung: –"
          "A=Z<a> B=Z<b>"
          " — Folgeziel: Z<f>"
          "abgeschlossen"
          "choose-a"
          "choose-b"
          "graybox-input-script-v3" ] do
        if not (document.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Entscheidungsvertragsdokument nennt den Anker {anchor} nicht."

// ---------------------------------------------------------------------------
// Optionsableitung (AC-T035-02-Testmatrix): reine Funktion des
// Aufsuchprotokolls, seedunabhaengig, fail-closed Degenerationsfall.
// ---------------------------------------------------------------------------

let optionsDerivationIsPureSeedIndependentAndFailsClosed () =
    let visit zone =
        ExplorationVisit(
            EvaluationBoundaryTick = 100L,
            ZoneIndex = zone,
            Mode = ModeContract.ModePersonalId,
            VisitOrder = 1L
        )

    // Reine Funktion: zuerst und zuletzt registrierte Zone.
    let derived = DecisionSession.DeriveOptions([ visit 3; visit 0; visit 5 ])

    if derived <> (3, 5) then
        failwith $"Optionsableitung lieferte {derived} statt (zuerst 3, zuletzt 5)."

    let again = DecisionSession.DeriveOptions([ visit 3; visit 0; visit 5 ])

    if derived <> again then
        failwith "Wiederholte Optionsableitung ist nicht identisch (seedunabhaengig)."

    // Fail-closed Degenerationsfall: weniger als zwei Registrierungen.
    let rejects (protocol: ExplorationVisit list) (needle: string) =
        let raised =
            try
                DecisionSession.DeriveOptions(protocol) |> ignore
                false
            with
            | :? InvalidOperationException as invalidOperation ->
                if
                    not (
                        invalidOperation.Message.Contains(
                            DecisionContract.RejectReasonInsufficientDistinctZones,
                            StringComparison.Ordinal
                        )
                    )
                then
                    failwith "Fail-closed-Ablehnung traegt nicht die vertragliche Kennung."

                true
            | _ -> false

        if not raised then
            failwith $"Degenerationsfall ({needle}) wurde nicht kontrolliert abgewiesen."

    rejects [] "leeres Protokoll"
    rejects [ visit 2 ] "eine Registrierung"
    rejects [ visit 2; visit 2 ] "identische Zonen"

// ---------------------------------------------------------------------------
// Headless Entscheidungs-Flow in-process (AC-T035-02/03): Angebotskopplung,
// Wahl, Folgeabschluss, Beobachtungstreue, Kernelunberuehrtheit.
// ---------------------------------------------------------------------------

/// Vertragliche Grundwahl: Wahl B bei Tick 7300 (persoenlicher Modus,
/// nach der Angebotsöffnung an der Erkundungsabschlussgrenze 7210); der
/// Held steht dort bereits, die Folge schliesst an derselben Vorgrenze.
let private chooseBBody = decisionBody "intent 7300 choose-b"

let private chooseABody = decisionBody "intent 7300 choose-a"

let decisionOfferChoosesAndFollowUpBindContractually () =
    let result = runInProcess 20260826u 8000 chooseBBody true
    let exploration = result.Exploration
    let decision = result.Decision

    if isNull exploration || isNull decision then
        failwith "Aktivierter Entscheidungsfluss lieferte keine Ausweise."

    let protocol = exploration.VisitProtocol

    if protocol.Count <> NavWorld.ZoneCount || not exploration.Completed then
        failwith "Der gebundene Kern suchte nicht saemtliche Landmarken auf."

    // Ausloeseregel (completion-gated-decision-offer-v1): Angebot genau an
    // der ersten Auswertungsgrenze mit abgeschlossenem Auftrag (die letzte
    // Registrierungsgrenze), genau einmal.
    let completionBoundary = protocol.[protocol.Count - 1].EvaluationBoundaryTick

    if
        not decision.OfferOpened
        || decision.OfferBoundaryTick <> completionBoundary
    then
        failwith $"Angebotsoeffnung {decision.OfferBoundaryTick} widerspricht der Abschlussgrenze {completionBoundary}."

    // Optionsableitung (visit-protocol-zone-options-v1): zuerst und zuletzt
    // registrierte Zone; beide dem Spieler persoenlich bekannt.
    if
        decision.OptionZoneA <> protocol.[0].ZoneIndex
        || decision.OptionZoneB <> protocol.[protocol.Count - 1].ZoneIndex
        || decision.OptionZoneA = decision.OptionZoneB
    then
        failwith "Optionszonen widersprechen der reinen Protokollableitung."

    // Wahl (decision-choose-personal-mode-only-v1): wirksam im
    // persoenlichen Modus an der gebundenen Grenze.
    if
        not decision.Decided
        || decision.DecisionBoundaryTick <> 7300L
        || decision.Choice <> DecisionContract.ChoiceOptionBId
        || decision.DecisionMode <> ModeContract.ModePersonalId
    then
        failwith "Die Wahl widerspricht dem vertraglichen Wahlprotokoll."

    // Folgeregel: die gewaehlte Zone ist Folgeziel; da der Held dort steht,
    // schliesst die Folge an der Entscheidungsgrenze selbst ab.
    if
        decision.FollowUpZoneIndex <> decision.OptionZoneB
        || not decision.FollowUpCompleted
        || decision.ArrivalBoundaryTick <> decision.DecisionBoundaryTick
    then
        failwith "Folgenbindung widerspricht der Ankunfts- und Abschlussregel."

    // Keine Abweisungen im gebundenen Gluecksfall.
    if
        decision.ChooseRejectionsBeforeOffer <> 0L
        || decision.ChooseRejectionsInStrategicMode <> 0L
        || decision.ChooseRejectionsAfterDecision <> 0L
    then
        failwith "Der gebundene Flow erzeugte Entscheidungsabweisungen."

let decisionFollowUpRequiresPersonalArrival () =
    // Wahl A (zuerst registrierte Zone); der Held steht in der zuletzt
    // registrierten Zone. Ohne Mobilmachung bleibt die Folge offen.
    let idle = runInProcess 20260826u 8000 chooseABody true
    let decisionIdle = idle.Decision

    if isNull decisionIdle then
        failwith "Aktivierter Entscheidungsfluss lieferte keinen Ausweis."

    if
        not decisionIdle.Decided
        || decisionIdle.FollowUpZoneIndex <> decisionIdle.OptionZoneA
        || decisionIdle.FollowUpCompleted
        || decisionIdle.ArrivalBoundaryTick <> DecisionTelemetry.UnsetBoundaryTick
    then
        failwith "Die Folge schloss ohne persoenliche Ankunft."

    // Moduskopplung der Ankunft (boundary-arrival-personal-mode-only-v1):
    // der Held erreicht die Folgenzone im strategischen Modus; die Folge
    // bleibt offen — strategische Anwesenheit schliesst bewusst nicht.
    let strategicArrivalBody =
        decisionBody "intent 7300 choose-a"
        @ [ "intent 7400 switch" // strategisch ab 7402
            "intent 7420 box 0 0 159000 89000"
            "intent 7430 move 0" ]

    let strategic = runInProcess 20260826u 9000 strategicArrivalBody true
    let decisionStrategic = strategic.Decision

    if isNull decisionStrategic then
        failwith "Aktivierter Entscheidungsfluss lieferte keinen Ausweis."

    if not decisionStrategic.Decided || decisionStrategic.FollowUpCompleted then
        failwith "Strategische Anwesenheit in der Folgenzone schloss die Folge."

    // Persoenliche Ankunft (Vollfluss, Schemaversion-4-Fixture): strategische
    // Mobilmachung zur gewaehlten Zone, Rueckwechsel, Ankunft schliesst.
    let fullFlowBody =
        decisionBody "intent 7300 choose-a"
        @ [ "intent 7400 switch"
            "intent 7420 box 0 0 159000 89000"
            "intent 7430 move 0"
            "intent 7500 switch" ]

    let full = runInProcess 20260826u 12000 fullFlowBody true
    let decisionFull = full.Decision

    if isNull decisionFull then
        failwith "Aktivierter Entscheidungsfluss lieferte keinen Ausweis."

    if
        not decisionFull.FollowUpCompleted
        || decisionFull.ArrivalBoundaryTick < decisionFull.DecisionBoundaryTick
        || decisionFull.FollowUpZoneIndex <> decisionFull.OptionZoneA
    then
        failwith "Die persoenliche Ankunft schloss die Folge nicht."

// ---------------------------------------------------------------------------
// Beobachtungstreue (AC-T035-03): Twin-Kontrolle, Kernelunberuehrtheit,
// Fremdseed-Negativfall.
// ---------------------------------------------------------------------------

let decisionIsObservationOnlyTwinStaysByteIdentical () =
    // (1) Aktivierter Entscheidungsfluss gegen Explorations-Zwilling ohne
    // Entscheidungsschicht: identische Intentfolge bis auf die
    // sitzungsseitige Wahl — byteidentische Ketten, identischer Endhash,
    // identische Kernbefehlsfolge.
    let twin = runInProcess 20260826u 8000 explorationBody false
    let activated = runInProcess 20260826u 8000 chooseBBody true

    if activated.StartStateHash <> twin.StartStateHash then
        failwith "Aktivierter Lauf veraenderte den Starthash der Simulation."

    if activated.EndStateHash <> twin.EndStateHash then
        failwith "Aktivierter Lauf veraenderte den Endhash der Simulation."

    if
        activated.IntervalSampleTicks <> twin.IntervalSampleTicks
        || activated.IntervalHashes <> twin.IntervalHashes
    then
        failwith "Aktivierter Lauf veraenderte die Kettenstichproben."

    if activated.KernelCommandsTotal <> twin.KernelCommandsTotal then
        failwith "Die Kernbefehlsfolge des Entscheidungsflows weicht vom Twin ab."

    if not (isNull twin.Decision) then
        failwith "Unaktivierter Lauf lieferte einen Entscheidungsausweis."

    if isNull activated.Decision then
        failwith "Aktivierter Lauf lieferte keinen Entscheidungsausweis."

    // (2) Entscheidungsintents ohne aktivierte Schicht: kontrollierte
    // Abweisung ohne Kernaenderung; die Ketten bleiben byteidentisch.
    let rejected = runInProcess 20260826u 8000 chooseBBody false

    if
        rejected.StartStateHash <> twin.StartStateHash
        || rejected.EndStateHash <> twin.EndStateHash
        || rejected.IntervalHashes <> twin.IntervalHashes
    then
        failwith "Die nicht aktivierte Entscheidungsschicht veraenderte die Simulation."

    if rejected.KernelCommandsTotal <> twin.KernelCommandsTotal then
        failwith "Abgewiesene Entscheidungen erzeugten Kernbefehle."

    if rejected.RejectedIntents <> twin.RejectedIntents + 1 then
        failwith "Die choose-Abweisung fehlt in der Intentdisposition."

    // (3) A/B-Wahltreue: identische Kernintents, unterscheidbare
    // Entscheidungsreports, byteidentische Ketten.
    let choiceA = runInProcess 20260826u 8000 chooseABody true
    let choiceB = runInProcess 20260826u 8000 chooseBBody true

    if
        choiceA.StartStateHash <> choiceB.StartStateHash
        || choiceA.EndStateHash <> choiceB.EndStateHash
        || choiceA.IntervalHashes <> choiceB.IntervalHashes
        || choiceA.KernelCommandsTotal <> choiceB.KernelCommandsTotal
    then
        failwith "Die Wahl veraenderte die Kernwahrheit (A/B-Treue verletzt)."

    if
        choiceA.Decision.Choice <> DecisionContract.ChoiceOptionAId
        || choiceB.Decision.Choice <> DecisionContract.ChoiceOptionBId
        || choiceA.Decision.FollowUpZoneIndex <> choiceA.Decision.OptionZoneA
        || choiceB.Decision.FollowUpZoneIndex <> choiceB.Decision.OptionZoneB
    then
        failwith "Die Entscheidungsreports unterscheiden die Wahl nicht."

    // (4) Fremdseed: Start-/Endhash nachweislich anders; die
    // Entscheidungsschicht bleibt reine Beobachtung des (seedigten)
    // Sitzungszustands — die Angebotskopplung ist exakt die Abschlussfunktion
    // der Exploration, ohne dass die Optionsableitung selbst seedabhaengig
    // wuerde (rein gebunden an Protokollstruktur und Wahl,
    // optionsDerivationIsPureSeedIndependentAndFailsClosed).
    let foreign = runInProcess 42u 8000 chooseBBody true

    if
        foreign.StartStateHash = activated.StartStateHash
        || foreign.EndStateHash = activated.EndStateHash
    then
        failwith "Ein fremder Seed aenderte Start- oder Endhash nicht nachweislich."

    if foreign.Decision.OfferOpened <> foreign.Exploration.Completed then
        failwith "Die Angebotsöffnung ist nicht exakt die Abschlusskopplung der Exploration."

    if
        foreign.Decision.Decided
        || foreign.Decision.FollowUpZoneIndex <> DecisionTelemetry.UnsetZoneIndex
        || foreign.Decision.ArrivalBoundaryTick <> DecisionTelemetry.UnsetBoundaryTick
    then
        failwith "Ohne abgeschlossenen Auftrag entstand eine Entscheidung oder Folge."

    // Der Report traegt den ehrlichen, maschinenlesbaren Nichtoeffnungsgrund
    // statt stiller Leere (Vertrag Abschnitt 2).
    let foreignBlock =
        CommandLoopRunner.BuildDecisionSession(
            CommandReportSchema.ExecutionHeadless,
            true,
            foreign.Decision
        )
        |> JsonSerializer.Serialize
        |> JsonNode.Parse
        |> fun node -> node.AsObject()

    let foreignOffer = foreignBlock["offer"].AsObject()

    if
        foreignOffer["opened"].GetValue<bool>()
        || foreignOffer["reason"].GetValue<string>()
           <> DecisionContract.OfferNotOpenedReason
    then
        failwith "Der Nichtoeffnungsgrund fehlt oder widerspricht dem Vertrag."

let decisionNotActivatedRejectionIsDistinguishedWithoutKernelEffect () =
    // Auswertungsordnung Stufe 1 (decision-not-activated) an der Pipeline:
    // unterscheidbare Disposition und Zaehler ohne Kernbefehl und ohne
    // Simulationszugriff. Der Vergleichslauf traegt dieselben Kernintents
    // ohne die Entscheidungsaktionen.
    let withChooses =
        [| GrayboxIntent(40, GrayboxIntentKind.BoxSelect, 0L, 0L, 159000L, 89000L)
           GrayboxIntent(50, GrayboxIntentKind.GroupMoveToZone, 2L)
           GrayboxIntent(60, GrayboxIntentKind.ChooseA)
           GrayboxIntent(61, GrayboxIntentKind.ChooseB) |]

    let withoutChooses =
        withChooses
        |> Array.filter (fun intent ->
            intent.Kind <> GrayboxIntentKind.ChooseA
            && intent.Kind <> GrayboxIntentKind.ChooseB)

    let runPipeline intents =
        let world = SimWorld(20260826u)
        let selection = SelectionModel(SessionEngine.ReadAgentGroups(world))
        let pipeline = SessionPipeline(world, selection, intents, null, null)

        for tick in 0L .. 61L do
            let _outcome = pipeline.ProcessBoundary(tick)

            if tick < 61L then
                world.Tick()

        pipeline

    let withDecision = runPipeline withChooses
    let withoutDecision = runPipeline withoutChooses

    if withDecision.ChooseIntentsRejectedWithoutActivationTotal <> 2L then
        failwith "Entscheidungen ohne Aktivierung wurden nicht als Stufe 1 gezaehlt."

    if withDecision.AppliedCommandsTotal <> withoutDecision.AppliedCommandsTotal then
        failwith "Der Kernelbefehlsbestand veraenderte sich durch Entscheidungsabweisungen."

    if withDecision.AppliedIntentsTotal <> withoutDecision.AppliedIntentsTotal then
        failwith "Die Fachintents veraenderten sich durch Entscheidungsabweisungen."

    if
        withDecision.RejectedIntentsTotal
        <> withoutDecision.RejectedIntentsTotal + 2L
    then
        failwith "Die Stufe-1-Abweisungen fehlen in der Intentdisposition."

    // Vertragliche Kopplung (Vertrag Abschnitt 7): Entscheidungsschicht ohne
    // Erkundungsaktivierung ist ein Vertragswiderspruch und wird fail-closed
    // abgewiesen.
    let coupled =
        try
            SessionEngine.Run(
                SessionRunRequest(
                    Seed = 20260826u,
                    ScriptedIntents = parseScript 400 explorationBody,
                    WarmupTicks = 240,
                    HorizonTicks = 400,
                    RunSelfConsistencyPass = false,
                    ExplorationEnabled = false,
                    DecisionEnabled = true
                )
            )
            |> ignore

            false
        with
        | :? ArgumentException -> true
        | _ -> false

    if not coupled then
        failwith "Entscheidungsschicht ohne Erkundungsaktivierung wurde nicht fail-closed abgewiesen."

// ---------------------------------------------------------------------------
// Titel-HUD-Bindung (AC-T035-04, title-hud-decision-objective-v1): die
/// vier festen Formen erscheinen am echten Sitzungszustand; ohne
/// Entscheidungsaktivierung bleibt die Titelzeile byteidentisch.
// ---------------------------------------------------------------------------

let private titleAtTick (bodyLines: string list) (captureTick: int64) : string =
    let horizon = 8000
    let world = SimWorld(20260826u)
    let selection = SelectionModel(SessionEngine.ReadAgentGroups(world))
    let exploration = ExplorationSession()
    let decision = DecisionSession()
    let intents = parseScript horizon bodyLines
    let pipeline = SessionPipeline(world, selection, intents, exploration, decision)
    let mutable title = ""

    for tick in 0L .. (int64 horizon - 1L) do
        let _outcome = pipeline.ProcessBoundary(tick)
        world.Tick()

        if tick = captureTick then
            title <-
                CommandLoopRunner.BuildTitleHudText(
                    pipeline.CurrentEffectiveMode,
                    world,
                    exploration,
                    decision
                )

    title

let titleHudBindsDecisionStatesWithoutChangingLegacyForm () =
    // Ohne Entscheidungsaktivierung: byteidentisch zum Bestandsstand, kein
    // Entscheidungssegment.
    let world = SimWorld(20260826u)
    let exploration = ExplorationSession()
    let legacy = CommandLoopRunner.BuildTitleHudText(SessionMode.Personal, world, exploration)

    if legacy.Contains("Entscheidung", StringComparison.Ordinal) then
        failwith "Unaktivierte Titelzeile traegt ein Entscheidungssegment."

    // Aktiviert, vor der Angebotsöffnung: ' — Entscheidung: –'.
    let beforeOffer = titleAtTick chooseBBody 7000L

    if not (beforeOffer.Contains(" — Entscheidung: –", StringComparison.Ordinal)) then
        failwith $"Titelzeile vor dem Angebot: {beforeOffer}"

    // Angebot offen: beide Optionszonen lesbar.
    let offerOpen = titleAtTick chooseBBody 7210L

    if
        not (
            offerOpen.Contains(
                $" — Entscheidung: A=Z{0} B=Z{4}",
                StringComparison.Ordinal
            )
        )
    then
        failwith $"Titelzeile bei offenem Angebot: {offerOpen}"

    // Entschieden, Folge offen bzw. abgeschlossen.
    let decidedOpen = titleAtTick chooseABody 7300L

    if not (decidedOpen.Contains(" — Folgeziel: Z0", StringComparison.Ordinal)) then
        failwith $"Titelzeile nach Wahl A: {decidedOpen}"

    let decidedCompleted = titleAtTick chooseBBody 7300L

    if
        not (
            decidedCompleted.Contains(
                " — Folgeziel: Z4 abgeschlossen",
                StringComparison.Ordinal
            )
        )
    then
        failwith $"Titelzeile nach abgeschlossener Wahl B: {decidedCompleted}"

// ---------------------------------------------------------------------------
// Headless Entscheidungs-Flow ueber denselben oeffentlichen Befehl
// (AC-T035-02/06): Schemaversion 4, Dual-Prozess-Bindung, Exitcode-Erhaltung.
// ---------------------------------------------------------------------------

let private reportJson (path: string) = File.ReadAllText(path)

let private jsonInt (element: JsonElement) (name: string) = element.GetProperty(name).GetInt32()

let private decisionArguments (scriptPath: string) (seed: string) (horizon: string) targetReport =
    [| "kommandoschleife"
       "--scenario"
       "kommando-graybox"
       "--input-script"
       scriptPath
       "--seed"
       seed
       "--warmup-ticks"
       "240"
       "--horizon-ticks"
       horizon
       "--exploration"
       "--decision"
       "--report"
       targetReport |]

let cliDecisionFlowRunsHeadlessOnSchemaVersion4 () =
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-b.graybox")

    let reportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-Decision-{Guid.NewGuid():N}.json")
    let secondReportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-Decision-{Guid.NewGuid():N}.json")

    try
        let exitCode, stdout, stderr =
            runToleratingTransientGate (decisionArguments scriptPath "20260826" "8000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith $"Entscheidungslauf endete mit {exitCode}: {stderr} {stdout}"

        let json = reportJson reportPath

        if CommandReportSchema.Validate(json).Count <> 0 then
            failwith "Aktivierter Entscheidungsreport widerspricht dem Schemavertrag (Version 4)."

        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        if jsonInt root "schemaVersion" <> 4 then
            failwith "Aktivierter Entscheidungsreport traegt nicht die additive Schemaversion 4."

        let decision = root.GetProperty("decisionSession")
        let offer = decision.GetProperty("offer")
        let choice = decision.GetProperty("decision")
        let followUp = decision.GetProperty("followUp")

        if
            not (offer.GetProperty("opened").GetBoolean())
            || jsonInt offer "optionZoneA" <> 0
            || jsonInt offer "optionZoneB" <> 4
        then
            failwith "Der Angebotsblock widerspricht der gebundenen Optionsableitung."

        if
            not (choice.GetProperty("decided").GetBoolean())
            || choice.GetProperty("choice").GetString() <> DecisionContract.ChoiceOptionBId
            || choice.GetProperty("mode").GetString() <> ModeContract.ModePersonalId
            || jsonInt choice "optionZone" <> 4
        then
            failwith "Der Entscheidungsblock widerspricht der gebundenen Wahl."

        if
            not (followUp.GetProperty("completed").GetBoolean())
            || jsonInt followUp "zoneIndex" <> 4
            || followUp.GetProperty("gateCoupled").GetBoolean()
        then
            failwith "Der Folgoblock widerspricht der gebundenen Abschlusswahrheit."

        if
            decision.GetProperty("activationId").GetString() <> DecisionContract.ActivationId
            || decision.GetProperty("offerRule").GetString() <> DecisionContract.OfferRuleId
            || decision.GetProperty("optionsModel").GetString() <> DecisionContract.OptionsModelId
        then
            failwith "Der Reportblock traegt nicht die vertraglichen Modellkennungen."

        let persistence = decision.GetProperty("persistence")

        if
            persistence.GetProperty("statementId").GetString()
            <> DecisionContract.NotPersistedStatementId
            || persistence.GetProperty("persisted").GetBoolean()
        then
            failwith "Die maschinenlesbare Nichtpersistenzaussage fehlt oder widerspricht."

        let hud = decision.GetProperty("hud")
        let channel = decision.GetProperty("followUpChannel")

        if
            hud.GetProperty("measured").GetBoolean()
            || channel.GetProperty("measured").GetBoolean()
        then
            failwith "Headless behauptet fensterpflichtige Entscheidungsdarstellung."

        if
            hud.GetProperty("kind").GetString() <> DecisionContract.HudModelId
            || channel.GetProperty("kind").GetString() <> DecisionContract.FollowUpChannelModelId
            || String.IsNullOrEmpty(hud.GetProperty("reason").GetString())
            || String.IsNullOrEmpty(channel.GetProperty("reason").GetString())
        then
            failwith "Headless Darstellungsausweise fehlen an Grund statt stiller Behauptung."

        if jsonInt root "exitCode" <> ExitCodes.Ok then
            failwith "Report-Exitcode widerspricht der Laufbeobachtung."

        // Zweiter echter App-Prozess: zeitabhaengige Felder duerfen abweichen,
        // die deterministische Produkt-, Protokoll- und Kettenwahrheit muss
        // auf demselben Builderstand byteidentisch bleiben.
        let secondExitCode, _, _ =
            runToleratingTransientGate (decisionArguments scriptPath "20260826" "8000" secondReportPath)

        if secondExitCode <> ExitCodes.Ok then
            failwith "Zweiter Entscheidungslauf endete fehlerhaft."

        let secondJson = reportJson secondReportPath

        if CommandReportSchema.Validate(secondJson).Count <> 0 then
            failwith "Zweiter Entscheidungsreport widerspricht dem Schemavertrag."

        use secondDocument = JsonDocument.Parse(secondJson)
        let secondRoot = secondDocument.RootElement

        for blockName in
            [ "scenario"
              "inputScript"
              "modeSession"
              "explorationSession"
              "decisionSession"
              "stateHashChain" ] do
            if
                root.GetProperty(blockName).GetRawText()
                <> secondRoot.GetProperty(blockName).GetRawText()
            then
                failwith $"Die deterministische Reportwahrheit '{blockName}' weicht zwischen zwei Fresh-Prozessen ab."
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

        if File.Exists(secondReportPath) then
            File.Delete(secondReportPath)

let cliAbChoicePairDiffersOnlyInDecisionReport () =
    let scriptA =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-a.graybox")

    let scriptB =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-b.graybox")

    let reportA = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecisionA-{Guid.NewGuid():N}.json")
    let reportB = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecisionB-{Guid.NewGuid():N}.json")

    try
        for (script, report) in [ (scriptA, reportA); (scriptB, reportB) ] do
            let exitCode, stdout, stderr =
                runToleratingTransientGate (decisionArguments script "20260826" "8000" report)

            if exitCode <> ExitCodes.Ok then
                failwith $"A/B-Entscheidungslauf endete mit {exitCode}: {stderr} {stdout}"

        use documentA = JsonDocument.Parse(reportJson reportA)
        use documentB = JsonDocument.Parse(reportJson reportB)
        let rootA = documentA.RootElement
        let rootB = documentB.RootElement

        // Byteidentische Kernwahrheit bei unterscheidbaren Entscheidungen.
        for blockName in [ "modeSession"; "explorationSession"; "stateHashChain" ] do
            if
                rootA.GetProperty(blockName).GetRawText()
                <> rootB.GetProperty(blockName).GetRawText()
            then
                failwith $"Der Block '{blockName}' weicht zwischen Wahl A und Wahl B ab."

        let decisionA = rootA.GetProperty("decisionSession")
        let decisionB = rootB.GetProperty("decisionSession")

        if
            decisionA.GetProperty("decision").GetProperty("choice").GetString()
            <> DecisionContract.ChoiceOptionAId
            || decisionB.GetProperty("decision").GetProperty("choice").GetString()
               <> DecisionContract.ChoiceOptionBId
        then
            failwith "Der Entscheidungsblock unterscheidet die Wahl nicht."

        if
            jsonInt (decisionA.GetProperty("decision")) "optionZone"
            = jsonInt (decisionB.GetProperty("decision")) "optionZone"
        then
            failwith "Wahl A und Wahl B erzeugten dieselbe Folgenzone."
    finally
        for report in [ reportA; reportB ] do
            if File.Exists(report) then
                File.Delete(report)

// ---------------------------------------------------------------------------
// CLI-Vertrag, Legacy-Erhaltung und Exitcodes (AC-T035-03/06).
// ---------------------------------------------------------------------------

let cliChooseWithoutActivationAndUsageCouplingStayContractual () =
    let chooseScript =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-b.graybox")

    let explorationScript =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t034-exploration-separated.graybox")

    let reportWithChoose = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecMix-{Guid.NewGuid():N}.json")
    let reportTwin = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecTwin-{Guid.NewGuid():N}.json")

    try
        // Ohne --decision bleibt der Lauf ehrlich: choose wird kontrolliert
        // abgewiesen (rejectedTotal +1), Kern und Kette bleiben identisch
        // zum v2-Zwilling desselben Kernintentsatzes.
        let twinExit, _, _ =
            runToleratingTransientGate
                [| "kommandoschleife"
                   "--scenario"
                   "kommando-graybox"
                   "--input-script"
                   explorationScript
                   "--seed"
                   "20260826"
                   "--warmup-ticks"
                   "240"
                   "--horizon-ticks"
                   "8000"
                   "--exploration"
                   "--report"
                   reportTwin |]

        if twinExit <> ExitCodes.Ok then
            failwith "Der Explorationszwilling endete fehlerhaft."

        let mixedExit, _, _ =
            runToleratingTransientGate
                [| "kommandoschleife"
                   "--scenario"
                   "kommando-graybox"
                   "--input-script"
                   chooseScript
                   "--seed"
                   "20260826"
                   "--warmup-ticks"
                   "240"
                   "--horizon-ticks"
                   "8000"
                   "--exploration"
                   "--report"
                   reportWithChoose |]

        if mixedExit <> ExitCodes.Ok then
            failwith "Der gemischte Lauf endete fehlerhaft."

        use twinDocument = JsonDocument.Parse(reportJson reportTwin)
        use mixedDocument = JsonDocument.Parse(reportJson reportWithChoose)

        if
            twinDocument.RootElement.GetProperty("inputScript").GetProperty("kernelCommandsTotal").GetInt32()
            <> mixedDocument.RootElement.GetProperty("inputScript").GetProperty("kernelCommandsTotal").GetInt32()
        then
            failwith "Abgewiesene Entscheidungen veraenderten die Kernbefehlsfolge."

        if
            twinDocument.RootElement.GetProperty("stateHashChain").GetRawText()
            <> mixedDocument.RootElement.GetProperty("stateHashChain").GetRawText()
        then
            failwith "Die nicht aktivierte Entscheidungsschicht veraenderte die Hashkette."

        if
            (mixedDocument.RootElement.GetProperty("inputScript").GetProperty("rejectedTotal").GetInt32())
            <> (twinDocument.RootElement.GetProperty("inputScript").GetProperty("rejectedTotal").GetInt32()) + 1
        then
            failwith "Die choose-Abweisung wurde nicht kontrolliert gezaehlt."

        if mixedDocument.RootElement.TryGetProperty("decisionSession") |> fst then
            failwith "Unaktivierter Lauf traegt einen Entscheidungsblock."
    finally
        for report in [ reportWithChoose; reportTwin ] do
            if File.Exists(report) then
                File.Delete(report)

    // --decision ohne --exploration ist eine Usage-Fehlanwendung (bestehende
    // Bedeutung 2), keine neue Exitcodebedeutung; kein Report entsteht.
    let reportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecUsage-{Guid.NewGuid():N}.json")

    try
        let usageExit, _, stderr =
            runAppHost
                [| "kommandoschleife"
                   "--scenario"
                   "kommando-graybox"
                   "--input-script"
                   chooseScript
                   "--seed"
                   "20260826"
                   "--warmup-ticks"
                   "240"
                   "--horizon-ticks"
                   "8000"
                   "--decision"
                   "--report"
                   reportPath |]

        if usageExit <> ExitCodes.Usage then
            failwith $"--decision ohne --exploration ergab {usageExit} statt der Usage-Bedeutung."

        if File.Exists(reportPath) then
            failwith "Die Usage-Fehlanwendung erzeugte einen Report."

        if not (stderr.Contains("--exploration", StringComparison.Ordinal)) then
            failwith "Die Usage-Meldung nennt nicht die vertragliche Kopplung."
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

let cliChooseTokensUnderLegacyHeadersAreUnknownActions () =
    // Keine stille Formatdrift innerhalb einer Version: choose-Aktionen unter
    // einem v1-/v2-Kopf sind UnknownAction (Exit 37, kein Report).
    let legacyHeaders = [ "graybox-input-script-v1"; "graybox-input-script-v2" ]

    for header in legacyHeaders do
        let scriptPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecLegacy-{Guid.NewGuid():N}.graybox")

        let reportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecLegacy-{Guid.NewGuid():N}.json")

        try
            File.WriteAllText(scriptPath, $"{header} 420\nintent 300 choose-a\nend\n")

            let exitCode, _, _ =
                runAppHost
                    [| "kommandoschleife"
                       "--scenario"
                       "kommando-graybox"
                       "--input-script"
                       scriptPath
                       "--seed"
                       "20260826"
                       "--warmup-ticks"
                       "240"
                       "--horizon-ticks"
                       "420"
                       "--report"
                       reportPath |]

            if exitCode <> ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable) then
                failwith $"{header} mit choose-Aktion ergab {exitCode} statt der UnknownAction-Bedeutung."

            if File.Exists(reportPath) then
                failwith "Die UnknownAction-Ablehnung erzeugte einen Report."
        finally
            File.Delete(scriptPath)
            if File.Exists(reportPath) then
                File.Delete(reportPath)

// ---------------------------------------------------------------------------
// Schemadispatch und relationale NF-007-Bindung (AC-T035-05): einzeln
// wohlgeformte Werte duerfen weder Angebot, Wahl noch Folge widersprechen.
// ---------------------------------------------------------------------------

let decisionSchemaDispatchRejectsCrossVariants () =
    let chooseScript =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-b.graybox")

    let reportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecSchema-{Guid.NewGuid():N}.json")

    try
        let exitCode, _, _ =
            runToleratingTransientGate (decisionArguments chooseScript "20260826" "8000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith "Golden-Entscheidungslauf endete fehlerhaft."

        let golden = reportJson reportPath

        if CommandReportSchema.Validate(golden).Count <> 0 then
            failwith "Golden-Entscheidungsreport verletzte den Schemavertrag."

        // Schemaversion 4 ohne vollstaendigen Block wird erkannt.
        let withoutBlock =
            golden.Replace("\"decisionSession\"", "\"decisionSessionRemoved\"")

        if
            not (
                CommandReportSchema.Validate(withoutBlock)
                |> Seq.exists (fun error -> error.Contains("decisionSession", StringComparison.Ordinal))
            )
        then
            failwith "Ein Version-4-Report ohne Entscheidungsblock wurde nicht erkannt."

        // Schemaversion 3 toleriert keinen Entscheidungsblock (die
        // Entscheidungsaktivierung ist vertraglich strikt additiv auf
        // Schemaversion 4 gebunden).
        let downgraded =
            golden
                .Replace("\"schemaVersion\":4", "\"schemaVersion\":3")
                .Replace("\"decisionSession\"", "\"decisionSessionRemoved\"")

        if
            not (
                CommandReportSchema.Validate(downgraded)
                |> Seq.exists (fun error -> error.Contains("decisionSession", StringComparison.Ordinal))
            )
        then
            failwith "Die Schemaversionen werden nicht fail-closed dispatcht."
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

let decisionSchemaRelationsRejectFabrication () =
    let chooseScript =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t035-decision-choose-b.graybox")

    let reportPath = Path.Combine(Path.GetTempPath(), $"RiftHarness-DecRel-{Guid.NewGuid():N}.json")

    try
        let exitCode, _, _ =
            runToleratingTransientGate (decisionArguments chooseScript "20260826" "8000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith "Golden-Entscheidungslauf endete fehlerhaft."

        let golden = reportJson reportPath

        if CommandReportSchema.Validate(golden).Count <> 0 then
            failwith "Golden-Entscheidungsreport verletzte den relationalen Schemavertrag."

        let decisionOf (root: JsonObject) = root["decisionSession"].AsObject()

        let reject (label: string) (needle: string) (mutate: JsonObject -> unit) =
            let root = JsonNode.Parse(golden).AsObject()
            mutate root
            let errors = CommandReportSchema.Validate(root.ToJsonString())

            if errors.Count = 0 then
                failwith $"{label}: relational gefaelschter Report wurde akzeptiert."

            if
                not (
                    errors
                    |> Seq.exists (fun error -> error.Contains(needle, StringComparison.Ordinal))
                )
            then
                let joinedErrors = String.concat "; " errors
                failwith $"{label}: erwartete Fehlerkennung '{needle}' fehlt: {joinedErrors}"

        // Wahl-Zonen-Zuordnung: Wahl a muss Optionszone A gewaehlt haben.
        reject "Wahlzuordnung verdreht" "Wahl a" (fun root ->
            let decision = decisionOf root
            decision["decision"].AsObject()["choice"] <- JsonValue.Create(DecisionContract.ChoiceOptionAId))

        // Folgenzone ist die gewaehlte Zone.
        reject "Fremde Folgenzone" "Folgenzone" (fun root ->
            let decision = decisionOf root
            decision["followUp"].AsObject()["zoneIndex"] <- JsonValue.Create(0))

        // Ankunft liegt an oder nach der Entscheidungsgrenze.
        reject "Ankunft vor der Wahl" "Ankunft" (fun root ->
            let decision = decisionOf root
            let followUp = decision["followUp"].AsObject()
            followUp["arrivalBoundaryTick"] <- JsonValue.Create(7299)
            followUp["completed"] <- JsonValue.Create(true))

        // Optionszonen sind verschieden.
        reject "Gleichzeitige Optionen" "verschieden" (fun root ->
            let decision = decisionOf root
            let offer = decision["offer"].AsObject()
            offer["optionZoneB"] <- offer["optionZoneA"].DeepClone())

        // Ohne Angebot gibt es keine Entscheidung.
        reject "Entscheidung ohne Angebot" "ohne Angebot" (fun root ->
            let decision = decisionOf root
            let offer = decision["offer"].AsObject()
            offer["opened"] <- JsonValue.Create(false))

        // Abschluss und Ankunftsgrenze tragen dieselbe Aussage.
        reject "Abschluss ohne Ankunft" "dieselbe Aussage" (fun root ->
            let decision = decisionOf root
            decision["followUp"].AsObject()["arrivalBoundaryTick"] <- JsonValue.Create(-1))

        // Headless darf keine fensterpflichtige Darstellung behaupten.
        reject "Headless-Scheinmessung" "measured" (fun root ->
            let decision = decisionOf root
            decision["hud"].AsObject()["measured"] <- JsonValue.Create(true))
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

let decisionBuilderEhrlichkeitBindetDarstellungsausweise () =
    // Builder-Ehrlichkeit ohne echtes Display: Nur ein vollstaendig
    // abgeschlossenes Interaktivfenster darf HUD und Folgezielkanal als
    // gemessen ausweisen; Exception-Teilreports bewahren die Aktivierung.
    let telemetry = DecisionSession().ToTelemetry()

    let presentationMeasured execution windowCompleted =
        let source =
            CommandLoopRunner.BuildDecisionSession(execution, windowCompleted, telemetry)

        let block = JsonNode.Parse(JsonSerializer.Serialize(source)).AsObject()

        let hudMeasured = (block["hud"].AsObject()["measured"]).GetValue<bool>()

        let channelMeasured =
            (block["followUpChannel"].AsObject()["measured"]).GetValue<bool>()

        hudMeasured, channelMeasured

    if
        presentationMeasured CommandReportSchema.ExecutionInteractive true
        <> (true, true)
    then
        failwith "Vollstaendiger Interaktivlauf wies seine Entscheidungsdarstellung nicht messend aus."

    if
        presentationMeasured CommandReportSchema.ExecutionInteractive false
        <> (false, false)
    then
        failwith "Early-Quit-Interaktivlauf behauptete eine nicht abgeschlossene Entscheidungsdarstellung."

    if
        presentationMeasured CommandReportSchema.ExecutionHeadless true
        <> (false, false)
    then
        failwith "Headless-Lauf behauptete fensterpflichtige Entscheidungsdarstellung."

    let preserved = CommandLoopRunner.ResolveIncompleteDecision(true, null)

    if
        isNull preserved
        || preserved.OfferOpened
        || preserved.Decided
    then
        failwith "Exception-Teilreport verlor die angeforderte Entscheidungsaktivierung."

    if not (isNull (CommandLoopRunner.ResolveIncompleteDecision(false, telemetry))) then
        failwith "Teilreport ohne Opt-in erfand einen Entscheidungsblock."

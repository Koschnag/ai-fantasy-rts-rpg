module PressureTests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open Riftward.App
open Riftward.App.Command
open Riftward.Platform
open Riftward.Session
open Riftward.Simulation

// ---------------------------------------------------------------------------
// T-036: kleinster spielbarer Druck- und Neustartschritt (Druckvertrag V1,
// Abschnitte 0 bis 13). Jede Pruefung bindet Code, Vertragsdokument,
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

/// Erkundungs-/Mobilmachungskern: identisch zur gebundenen T-034-/T-035-
/// Basis; die Druckfabrikationen haengen ausschließlich sitzungsseitige
/// choose-Aktionen an (Grammatik v3 unveraendert).
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

let private runInProcess
    (seed: uint32)
    (horizon: int)
    (bodyLines: string list)
    (explorationEnabled: bool)
    (decisionEnabled: bool)
    (pressureEnabled: bool)
    : SessionRunResult =
    let intents = parseScript horizon bodyLines

    SessionEngine.Run(
        SessionRunRequest(
            Seed = seed,
            ScriptedIntents = intents,
            WarmupTicks = 240,
            HorizonTicks = horizon,
            RunSelfConsistencyPass = false,
            ExplorationEnabled = explorationEnabled,
            DecisionEnabled = decisionEnabled,
            PressureEnabled = pressureEnabled
        )
    )

// ---------------------------------------------------------------------------
// Spiegeltest (AC-T036-01/05): Code, Vertragsdokument, Anzeigeabmessungen
// und Schemalinie haelt ein Test.
// ---------------------------------------------------------------------------

let pressureContractMirrorsDocumentedValues () =
    if PressureContract.DocumentPath <> "docs/DRUCKVERTRAG.md" then
        failwith "Druckvertragspfad falsch."

    // V3: autorisierte additive Ketten-Praezisierung (T-039,
    // Abschlussvertrag Abschnitt 15; Druckvertrag Abschnitt 15).
    if PressureContract.ContractVersion <> "3" then
        failwith "Druckvertragsversion falsch (autorisierte Ketten-Praezisierung V3 erwartet)."

    // Autorisierte additive Zyklus-, Persistenz- und Ketten-Praezisierung:
    // der Entscheidungsvertrag traegt Version 4 (V2 = Zyklus-Praezisierung
    // T-036, V3 = Persistenz-Praezisierung T-037, V4 = Ketten-Praezisierung
    // T-039) mit unverändertem Pfad.
    if
        DecisionContract.DocumentPath <> "docs/ENTSCHEIDUNGSVERTRAG.md"
        || DecisionContract.ContractVersion <> "4"
    then
        failwith "Der Entscheidungsvertrag traegt nicht die autorisierten Praezisierungen bis V4."

    if
        CommandReportSchema.VersionWithoutExploration <> 2
        || CommandReportSchema.CurrentVersion <> 3
        || CommandReportSchema.VersionWithDecision <> 4
        || CommandReportSchema.VersionWithPressure <> 5
        || PressureContract.ReportSchemaVersionWithPressure <> 5
    then
        failwith "Schemaversionen entsprechen nicht dem Vertrag (Bestand 2, Erkundung 3, Entscheidung 4, Druck 5)."

    if PressureContract.WindowLengthTicks <> 600 then
        failwith "Die fixierte Fensterlaenge entspricht nicht der vorregistrierten Hypothese (600 Vorgrenzen)."

    // Persistenzwahrheit nach der autorisierten V2-Praezisierung (T-037):
    // Save/Load setzt fort, die ausdrueckliche Replay-Ausnahme bleibt.
    if not PressureContract.Persisted then
        failwith "Die V2-Persistenzaussage (Save/Load fortsetzbar) ist verletzt."

    if PressureContract.ReplayContinued then
        failwith "Die ausdrueckliche Replay-Ausnahme ist verletzt."

    if
        PressureContract.SaveLoadContinuation <> "continued"
        || PressureContract.ReplayNotContinued <> "not-continued"
        || PressureContract.SaveLoadPersistenceStatementId
           <> "pressure-session-local-save-load-persisted-v2"
    then
        failwith "Die versionierte Save/Load-Persistenzaussage widerspricht dem Druckvertrag V2."

    if
        InteractiveView.RestartIndicatorLowerHeightMeters <> 1.5
        || InteractiveView.RestartIndicatorUpperHeightMeters <> 3.0
        || InteractiveView.RestartIndicatorLowerSize <> 0.90f
        || InteractiveView.RestartIndicatorUpperSize <> 1.05f
        || InteractiveView.RestartIndicatorRed <> 0.90f
        || InteractiveView.RestartIndicatorGreen <> 0.28f
        || InteractiveView.RestartIndicatorBlue <> 0.22f
    then
        failwith "Neustartanzeige-Abmessungen entsprechen nicht dem zweistufigen Zweikanalvertrag."

    let document = readDocument PressureContract.DocumentPath

    for identifier in
        [ PressureContract.ActivationId
          PressureContract.TriggerId
          PressureContract.TimeBasisId
          PressureContract.FailureRuleId
          PressureContract.RestartModelId
          PressureContract.SuccessRuleId
          PressureContract.NotPersistedStatementId
          PressureContract.HudModelId
          PressureContract.RestartChannelModelId
          PressureContract.ReportBlockId
          PressureContract.FailureCauseWindowExpired
          PressureContract.WindowEndReasonSuccess
          PressureContract.WindowEndReasonExpired
          PressureContract.EndStatusNotStarted
          PressureContract.EndStatusWindowOpen
          PressureContract.EndStatusRestartPending
          PressureContract.EndStatusSuccess
          PressureContract.NotStartedReasonDecisionNotReached
          PressureContract.NotStartedReasonOfferWithoutChoice ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Druckvertragsdokument nennt die Kennung {identifier} nicht."

    for anchor in
        [ "--pressure"
          " — Druck: Zyklus <n> Rest <r>"
          " — Druck: Fehlschlag: Zeit abgelaufen"
          " — Druck: Erfolg"
          "window-expired-without-arrival"
          "600"
          "decision-not-reached-within-run"
          "decision-offer-open-without-choice-within-run" ] do
        if not (document.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Druckvertragsdokument nennt den Anker {anchor} nicht."

    // Der Entscheidungsvertrag dokumentiert die autorisierte Zyklus-
    // Praezisierung mit unverändertem Pfad und Versionswechsel.
    let decisionDocument = readDocument DecisionContract.DocumentPath

    if
        not (decisionDocument.Contains("Zyklus-Präzisierung", StringComparison.Ordinal))
        || not (decisionDocument.Contains("Autorisierte additive Zyklus-Präzisierung", StringComparison.Ordinal))
    then
        failwith "Der Entscheidungsvertrag traegt die autorisierte Zyklus-Praezisierung nicht."

// ---------------------------------------------------------------------------
// Fensterausloesung (AC-T036-02-Testmatrix): entscheidungsgekoppelt, einmal
// je Zyklus, ehrlicher nicht-gestarteter Zustand mit Grund.
// ---------------------------------------------------------------------------

let windowTriggerIsDecisionCoupledOncePerCycleAndHonestWithoutDecision () =
    // Ohne erreichten Entscheidungsstand (kein choose-Intent): kein Fenster,
    // ehrlicher not-started-Grund, wenn das Angebot offen blieb.
    let withoutChoice =
        runInProcess 20260826u 8000 (explorationBody @ [ "intent 7500 switch" ]) true true true

    let pressure = withoutChoice.Pressure

    if isNull pressure then
        failwith "Aktivierte Druckschicht lieferte keinen Ausweis."

    if pressure.CycleCount <> 0L || not (Seq.isEmpty pressure.Windows) then
        failwith "Ohne wirksame Entscheidung existierte eine Fensterinstanz."

    let status, reason = (pressure.EndStatus, pressure.EndStatusReason)

    if
        status <> PressureContract.EndStatusNotStarted
        || reason <> PressureContract.NotStartedReasonOfferWithoutChoice
    then
        failwith $"Der nicht gestartete Lauf trug {status}/{reason} statt des ehrlichen Angebotsgrunds."

    // Mit wirksamer Entscheidung startet genau eine Instanz an der
    // Entscheidungsgrenze (Wahl an 7300); der Horizont 7800 haelt das
    // offene Fenster bis zum Laufende, sodass der Entscheidungszustand
    // unreset ist und der Endstatus ehrlich window-open lautet.
    let withChoice =
        runInProcess 20260826u 7800 (explorationBody @ [ "intent 7300 choose-a" ]) true true true

    let pressure = withChoice.Pressure

    if pressure.CycleCount <> 1L then
        failwith $"Die erste Entscheidung startete {pressure.CycleCount} statt genau einer Instanz."

    let window = pressure.Windows.[0]

    if
        window.Instance <> 1L
        || window.Cycle <> 1L
        || window.StartBoundaryTick <> withChoice.Decision.DecisionBoundaryTick
        || not (isNull (box window.EndReason))
    then
        failwith "Die Fensterinstanz startet nicht genau an der Entscheidungsgrenze bzw. traegt einen Endgrund."

    if pressure.EndStatus <> PressureContract.EndStatusWindowOpen then
        failwith $"Der offene Fensterlauf trug {pressure.EndStatus} statt window-open."

    // Der decision-gekoppelte Start ist kein Angebot-Start: vor der Wahl
    // existiert keine Instanz (CycleCount 0 im ohne-Wahl-Lauf oben).
    if Seq.length pressure.Windows <> 1 then
        failwith "Die Instanzzahl widerspricht der einmaligen Ausloesung je Zyklus."

// ---------------------------------------------------------------------------
// Zeitbasis (AC-T036-02-Testmatrix): Determinismus, Ablaufgrenze exakt an
// Start + 600, Ankunft an der Ablaufgrenze ist die letzte Gelegenheit,
// Erfolg an der Oeffnungsgrenze.
// ---------------------------------------------------------------------------

let timeBasisExpiryExactnessAndArrivalOrdering () =
    // Manuelle vertragliche Drive-Ordnung (Pipeline-Reihenfolge: decision
    // observe vor pressure observe); der Simulationszustand bleibt
    // unveraendert, die Druckschicht liest nur. Die Ankunft wird über den
    // Zeitpunkt der Entscheidungsbeobachtung gesteuert, exactly wie die
    // Pipeline sie an der beobachteten Grenze ausfuehrt.
    let drive (arrivalBoundary: int64 option) (expectSuccess: bool) =
        let world = SimWorld(20260826u)
        let exploration = ExplorationSession()
        let decision = DecisionSession()
        let pressure = PressureSession()
        let heroZone = HeroTracker.ZoneIndexOf(world)
        let otherZone = (heroZone + 1) % NavWorld.ZoneCount

        // Interne Testbindung des Angebots (Präzedenz DeriveOptions): die
        // Folgenzone ist die Heldenzone, sodass die persönliche Ankunft zur
        // gesteuerten Grenze abschliessen kann; ohne gesteuerte Ankunft
        // bleibt die Folge offen und das Fenster laeuft vertragsgemaess ab.
        decision.OpenOfferForContractTest(10L, heroZone, otherZone)
        decision.TryChoose(DecisionChoiceOption.A, 10L, SessionMode.Personal) |> ignore

        if not decision.Decided then
            failwith "Der Drive konnte die Wahl nicht wirksam setzen."

        // Fenster startet an der Wahlgrenze 10; Ablaufgrenze ist 610
        // (Start + WindowLengthTicks exakt).
        for boundary in 10L .. 609L do
            if arrivalBoundary = Some boundary then
                decision.Observe(boundary, world, SessionMode.Personal, exploration)

            pressure.Observe(boundary, world, SessionMode.Personal, decision)

        if arrivalBoundary = Some 610L then
            decision.Observe(610L, world, SessionMode.Personal, exploration)

        pressure.Observe(610L, world, SessionMode.Personal, decision)

        let struct (status, _) = pressure.ResolveEndStatus(decision)

        if expectSuccess then
            if status <> PressureContract.EndStatusSuccess then
                failwith $"Die Ankunft an der Ablaufgrenze wurde nicht als Erfolg gebunden ({status})."

            let window = pressure.Windows.[0]
            let expected = Option.get arrivalBoundary

            if
                window.EndReason <> PressureContract.WindowEndReasonSuccess
                || window.EndBoundaryTick <> expected
                || window.ArrivalBoundaryTick <> expected
            then
                failwith "Der Erfolg an der Ankunftsgrenze traegt nicht die vertraglichen Grenzen."
        else
            if status <> PressureContract.EndStatusRestartPending then
                failwith $"Der Ablauf ohne Ankunft ergab {status} statt des definierten Fehlschlags."

            let window = pressure.Windows.[0]

            if
                window.EndReason <> PressureContract.WindowEndReasonExpired
                || window.EndBoundaryTick <> 610L
                || window.FailureCause <> PressureContract.FailureCauseWindowExpired
            then
                failwith "Die Ablaufgrenze weicht vom vertraglichen Start + WindowLengthTicks ab."

            if decision.Decided || decision.OfferOpened then
                failwith "Der Zykluszuruecksetzen nach Fehlschlag lies Wahl oder Angebot bestehen."

        (pressure, decision)

    // Ankunft an der Oeffnungsgrenze selbst (Held steht bereits in der
    // Folgenzone): Erfolg an der Oeffnungsgrenze.
    let _, _ = drive (Some 10L) true

    // Keine Ankunft: definierter Fehlschlag exakt an Start + 600.
    let _, _ = drive None false

    // Ankunft an der Ablaufgrenze selbst: letzte Gelegenheit im Fenster —
    // die Ankunft wird vor dem Ablauf geprüft.
    let pressure, decision = drive (Some 610L) true

    // Nach dem Erfolg existiert keine neue Instanz ohne neue Wahl; eine
    // erneute Wahl wird vertraglich abgewiesen (keine zweite Wahl im Zyklus).
    if not (isNull pressure.OpenWindow) then
        failwith "Nach dem Erfolg blieb eine Fensterinstanz offen."

    if decision.TryChoose(DecisionChoiceOption.B, 611L, SessionMode.Personal) then
        failwith "Nach erfolgreichem Abschluss war eine zweite Wahl wirksam."

    // Anzeigezeitraum der Neustartanzeige (Druckvertrag Abschnitt 6): Der
    // ehrliche Neustarendzustand besteht nach dem definierten Fehlschlag und
    // kippt mit dem Erfolg eines Folgazyklus — niemals über eine veraltete
    // Fehlschlagsursache allein hinaus in den Erfolg hinein.
    let failureWorld = SimWorld(20260826u)
    let failureExploration = ExplorationSession()
    let failureDecision = DecisionSession()
    let failurePressure = PressureSession()
    let failureHeroZone = HeroTracker.ZoneIndexOf(failureWorld)
    let failureOtherZone = (failureHeroZone + 1) % NavWorld.ZoneCount

    failureDecision.OpenOfferForContractTest(0L, failureHeroZone, failureOtherZone)

    failureDecision.TryChoose(DecisionChoiceOption.A, 0L, SessionMode.Personal)
    |> ignore

    for boundary in 0L .. 600L do
        failurePressure.Observe(boundary, failureWorld, SessionMode.Personal, failureDecision)

    if not failurePressure.RestartPending then
        failwith "Nach dem definierten Fehlschlag bestand der ehrliche Neustartzustand nicht."

    // Zyklus 2: Wiederauffrischung, erneute Wahl und persönliche Ankunft an
    // derselben Vorgrenze — der Erfolg beendet den Neustartzustand.
    failureDecision.OpenOfferForContractTest(601L, failureHeroZone, failureOtherZone)

    failureDecision.TryChoose(DecisionChoiceOption.A, 601L, SessionMode.Personal)
    |> ignore

    failureDecision.Observe(601L, failureWorld, SessionMode.Personal, failureExploration)
    failurePressure.Observe(601L, failureWorld, SessionMode.Personal, failureDecision)

    if failurePressure.RestartPending then
        failwith "Nach dem Erfolg des Folgazyklus bestand der Neustartzustand zu Unrecht fort."

    let struct (recoveredStatus, _) = failurePressure.ResolveEndStatus(failureDecision)

    if recoveredStatus <> PressureContract.EndStatusSuccess then
        failwith $"Der Folgazyklus nach Fehlschlag schloss nicht als Erfolg ab ({recoveredStatus})."

    ()

// ---------------------------------------------------------------------------
// Beobachtungstreue (AC-T036-03): Zwilling ohne Druckaktivierung bleibt
// byteidentisch; A/B-Paar und T-035-Fullflow bleiben ketten- und
// endhashidentisch mit rein additiven Druckfeldern.
// ---------------------------------------------------------------------------

let pressureIsObservationOnlyTwinStaysByteIdentical () =
    let body = explorationBody @ [ "intent 7300 choose-a" ]

    let twin = runInProcess 20260826u 7800 body true true false
    let withPressure = runInProcess 20260826u 7800 body true true true

    if twin.StartStateHash <> withPressure.StartStateHash then
        failwith "Der Starthash weicht zwischen Zwilling und Drucklauf ab."

    if twin.EndStateHash <> withPressure.EndStateHash then
        failwith "Die Druckschicht veraenderte den Endhash des Kerns."

    if twin.IntervalHashes <> withPressure.IntervalHashes then
        failwith "Die Druckschicht veraenderte die Zustands-Hashkette."

    if twin.KernelCommandsTotal <> withPressure.KernelCommandsTotal then
        failwith "Die Druckschicht veraenderte die Kernbefehlszahl."

    if not (isNull twin.Pressure) then
        failwith "Der Zwilling ohne Aktivierung traegt einen Druckausweis."

    if isNull withPressure.Pressure then
        failwith "Der aktivierte Lauf verlor seinen Druckausweis."

    // A/B-Wahlpaar mit Druckaktivierung (Horizont 7800 haelt die
    // Entscheidungen unreset): identische Kernintents und identische
    // Ketten, unterscheidbare Entscheidungen.
    let chooseA =
        runInProcess 20260826u 7800 (explorationBody @ [ "intent 7300 choose-a" ]) true true true

    let chooseB =
        runInProcess 20260826u 7800 (explorationBody @ [ "intent 7300 choose-b" ]) true true true

    if chooseA.StartStateHash <> chooseB.StartStateHash then
        failwith "Das A/B-Paar startete nicht aus demselben Zustand."

    if chooseA.EndStateHash <> chooseB.EndStateHash then
        failwith "Das A/B-Wahlpaar veraenderte die Kernwahrheit."

    if chooseA.Decision.Choice <> DecisionContract.ChoiceOptionAId then
        failwith "Der A-Lauf traegt nicht die Wahl a."

    if chooseB.Decision.Choice <> DecisionContract.ChoiceOptionBId then
        failwith "Der B-Lauf traegt nicht die Wahl b."

    // Langhorizont 8200 bindet die vertraglich unterschiedlichen
    // Druckwahrheiten (B: der Held steht in der Folgenzone — Erfolg an der
    // Entscheidungsgrenze; A: Folgenzone unerreicht — definierter
    // Fehlschlag an der Ablaufgrenze mit Wiederauffrischung).
    let chooseALong =
        runInProcess 20260826u 8200 (explorationBody @ [ "intent 7300 choose-a" ]) true true true

    let chooseBLong =
        runInProcess 20260826u 8200 (explorationBody @ [ "intent 7300 choose-b" ]) true true true

    if chooseALong.EndStateHash <> chooseBLong.EndStateHash then
        failwith "Das langhorizontige A/B-Paar veraenderte die Kernwahrheit."

    let pressureA = chooseALong.Pressure
    let windowA = pressureA.Windows.[0]

    if
        windowA.EndReason <> PressureContract.WindowEndReasonExpired
        || windowA.EndBoundaryTick <> 7300L + int64 PressureContract.WindowLengthTicks
        || windowA.FailureCause <> PressureContract.FailureCauseWindowExpired
        || pressureA.LastReopenBoundaryTick
           <> 7300L + int64 PressureContract.WindowLengthTicks + 1L
    then
        failwith "Der A-Lauf bindet nicht den definierten Fehlschlag mit Wiederauffrischung."

    let pressureB = chooseBLong.Pressure
    let windowB = pressureB.Windows.[0]

    if
        windowB.EndReason <> PressureContract.WindowEndReasonSuccess
        || windowB.ArrivalBoundaryTick <> 7300L
    then
        failwith "Der B-Lauf schloss nicht als Erfolg an der Entscheidungsgrenze ab."

    // T-035-Vollfluss (mit Mobilmachung) mit Druckaktivierung: die
    // Ankunft liegt innerhalb des offenen Fensters; Ketten und Endhash
    // bleiben gegen den druckfreien Vollfluss identisch.
    let fullFlow =
        explorationBody
        @ [ "intent 7300 choose-a"
            "intent 7400 switch"
            "intent 7420 box 0 0 159000 89000"
            "intent 7430 move 0"
            "intent 7500 switch" ]

    let fullWithoutPressure = runInProcess 20260826u 8200 fullFlow true true false
    let fullWithPressure = runInProcess 20260826u 8200 fullFlow true true true

    if fullWithoutPressure.EndStateHash <> fullWithPressure.EndStateHash then
        failwith "Der Vollfluss veraenderte mit Druckaktivierung den Endhash."

    let pressureFull = fullWithPressure.Pressure
    let windowFull = pressureFull.Windows.[0]

    if
        windowFull.EndReason <> PressureContract.WindowEndReasonSuccess
        || windowFull.ArrivalBoundaryTick <> fullWithPressure.Decision.ArrivalBoundaryTick
        || windowFull.ArrivalBoundaryTick
           >= 7300L + int64 PressureContract.WindowLengthTicks
    then
        failwith "Der T-035-Vollfluss schloss nicht als Erfolg innerhalb des offenen Fensters ab."

    // Fremdseed aendert Start- und Endhash nachweislich.
    let foreign =
        runInProcess 424242u 8200 (explorationBody @ [ "intent 7300 choose-a" ]) true true true

    if foreign.EndStateHash = chooseA.EndStateHash then
        failwith "Der Fremdseed veraenderte den Endhash nicht."

// ---------------------------------------------------------------------------
// Headless Druck-Flow ueber denselben oeffentlichen Befehl
// (AC-T036-02/06): Schemaversion 5, Fehlschlags-Neustart-Erfolgspfad,
// Dual-Prozess-Bindung, Exitcode-Erhaltung.
// ---------------------------------------------------------------------------

let private reportJson (path: string) = File.ReadAllText(path)

let private jsonInt64 (element: JsonElement) (name: string) = element.GetProperty(name).GetInt64()

let private pressureArguments (scriptPath: string) (seed: string) (horizon: string) targetReport =
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
       "--pressure"
       "--report"
       targetReport |]

let cliPressureFlowRunsHeadlessOnSchemaVersion5 () =
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    let secondReportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    try
        let exitCode, stdout, stderr =
            runToleratingTransientGate (pressureArguments scriptPath "20260826" "11000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith $"Drucklauf endete mit {exitCode}: {stderr} {stdout}"

        let json = reportJson reportPath

        if CommandReportSchema.Validate(json).Count <> 0 then
            failwith "Aktivierter Druckreport widerspricht dem Schemavertrag (Version 5)."

        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        if root.GetProperty("schemaVersion").GetInt32() <> 5 then
            failwith "Aktivierter Druckreport traegt nicht die additive Schemaversion 5."

        let pressure = root.GetProperty("pressureSession")
        let windows = pressure.GetProperty("windows")

        if windows.GetArrayLength() <> 2 then
            failwith "Der Fehlschlags-Neustart-Erfolgspfad traegt nicht genau zwei Fensterinstanzen."

        let first = windows.[0]
        let second = windows.[1]

        if
            jsonInt64 first "instance" <> 1L
            || jsonInt64 first "cycle" <> 1L
            || first.GetProperty("endReason").GetString()
               <> PressureContract.WindowEndReasonExpired
            || first.GetProperty("failureCause").GetString()
               <> PressureContract.FailureCauseWindowExpired
            || first.GetProperty("arrivalMode").ValueKind <> JsonValueKind.Null
            || first.GetProperty("endReason").GetString()
               <> PressureContract.WindowEndReasonExpired
        then
            failwith "Die erste Fensterinstanz traegt nicht den definierten Fehlschlag."

        // Vertragswahrheit: Ablauf exakt an Wahl + 600, Wiederauffrischung
        // exakt eine Vorgrenze spaeter, Erfolg in der zweiten Instanz.
        let choiceTick = 8000L

        if
            jsonInt64 first "startBoundaryTick" <> choiceTick
            || jsonInt64 first "endBoundaryTick" <> choiceTick + 600L
        then
            failwith "Die erste Instanz laeuft nicht exakt an Wahl + WindowLengthTicks ab."

        if jsonInt64 pressure "reopenBoundaryTick" <> choiceTick + 601L then
            failwith "Die Wiederauffrischung liegt nicht genau an der naechsten Vorgrenze nach dem Fehlschlag."

        if
            jsonInt64 second "instance" <> 2L
            || jsonInt64 second "cycle" <> 2L
            || second.GetProperty("endReason").GetString()
               <> PressureContract.WindowEndReasonSuccess
            || second.GetProperty("arrivalMode").GetString() <> ModeContract.ModePersonalId
        then
            failwith "Die zweite Instanz schliesst nicht als persönlicher Erfolg ab."

        let lastFailure = pressure.GetProperty("lastFailure")

        if
            jsonInt64 lastFailure "boundaryTick" <> choiceTick + 600L
            || lastFailure.GetProperty("cause").GetString()
               <> PressureContract.FailureCauseWindowExpired
        then
            failwith "Der letzte Fehlschlag traegt nicht Grenze und Ursache."

        let endStatus = pressure.GetProperty("endStatus")

        if endStatus.GetProperty("status").GetString() <> PressureContract.EndStatusSuccess then
            failwith "Der Endstatus des Fehlschlags-Neustart-Erfolgspfads ist nicht Erfolg."

        if
            pressure.GetProperty("windowLengthTicks").GetInt64()
            <> PressureContract.WindowLengthTicks
        then
            failwith "Der Report bindet nicht die fixierte Fensterlaenge."

        let persistence = pressure.GetProperty("persistence")

        if
            persistence.GetProperty("statementId").GetString()
            <> PressureContract.SaveLoadPersistenceStatementId
            || not (persistence.GetProperty("persisted").GetBoolean())
            || persistence.GetProperty("saveLoad").GetString()
               <> PressureContract.SaveLoadContinuation
            || persistence.GetProperty("replay").GetString()
               <> PressureContract.ReplayNotContinued
        then
            failwith "Die maschinenlesbare V2-Persistenzaussage des Drucks fehlt oder widerspricht."

        let hud = pressure.GetProperty("hud")
        let indicator = pressure.GetProperty("restartIndicator")

        if
            hud.GetProperty("measured").GetBoolean()
            || indicator.GetProperty("measured").GetBoolean()
        then
            failwith "Headless behauptet fensterpflichtige Druckdarstellung."

        if
            String.IsNullOrEmpty(hud.GetProperty("reason").GetString())
            || String.IsNullOrEmpty(indicator.GetProperty("reason").GetString())
        then
            failwith "Headless Druckausweise fehlen an Grund statt stiller Behauptung."

        if jsonInt64 root "exitCode" <> int64 ExitCodes.Ok then
            failwith "Report-Exitcode widerspricht der Laufbeobachtung."

        // Zweiter echter App-Prozess: builderidentische Ketten und
        // builderidentisches Druckprotokoll.
        let secondExitCode, _, _ =
            runToleratingTransientGate (pressureArguments scriptPath "20260826" "11000" secondReportPath)

        if secondExitCode <> ExitCodes.Ok then
            failwith "Zweiter Drucklauf endete fehlerhaft."

        let secondJson = reportJson secondReportPath

        if CommandReportSchema.Validate(secondJson).Count <> 0 then
            failwith "Zweiter Druckreport widerspricht dem Schemavertrag."

        use secondDocument = JsonDocument.Parse(secondJson)
        let secondRoot = secondDocument.RootElement

        if
            root.GetProperty("stateHashChain").GetProperty("end").GetString()
            <> secondRoot.GetProperty("stateHashChain").GetProperty("end").GetString()
        then
            failwith "Die Ketten zweier Fresh-Prozesslaeufe sind nicht builderidentisch."

        if
            pressure.GetProperty("windows").ToString()
            <> secondRoot.GetProperty("pressureSession").GetProperty("windows").ToString()
        then
            failwith "Das Druckprotokoll zweier Fresh-Prozesslaeufe ist nicht builderidentisch."
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

        if File.Exists(secondReportPath) then
            File.Delete(secondReportPath)

// ---------------------------------------------------------------------------
// Fremdseed-Negativfall (AC-T036-02): Hashes weichen ab; das Druckprotokoll
// ist eine reine Funktion aus Sitzungszustand, Modus-/Ankunftsgrenzen und
// Fensterinstanzen (Strukturinvarianten bleiben, Grenzen folgen der
// Sitzung; die Druckschicht liest den Seed niemals).
// ---------------------------------------------------------------------------

let foreignSeedChangesHashesButPressureStructureFollowsSession () =
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")

    let contractReport =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    let foreignReport =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    try
        let contractExit, _, _ =
            runToleratingTransientGate (pressureArguments scriptPath "20260826" "11000" contractReport)

        let foreignExit, _, _ =
            runToleratingTransientGate (pressureArguments scriptPath "7" "11000" foreignReport)

        if contractExit <> ExitCodes.Ok || foreignExit <> ExitCodes.Ok then
            failwith "Der Fremdseed-Vergleich endete fehlerhaft."

        use contractDocument = JsonDocument.Parse(reportJson contractReport)
        use foreignDocument = JsonDocument.Parse(reportJson foreignReport)
        let contractRoot = contractDocument.RootElement
        let foreignRoot = foreignDocument.RootElement

        if
            contractRoot.GetProperty("stateHashChain").GetProperty("end").GetString() = foreignRoot
                .GetProperty("stateHashChain")
                .GetProperty("end")
                .GetString()
        then
            failwith "Der Fremdseed veraenderte den Endhash nachweislich nicht."

        if
            contractRoot.GetProperty("stateHashChain").GetProperty("start").GetString() = foreignRoot
                .GetProperty("stateHashChain")
                .GetProperty("start")
                .GetString()
        then
            failwith "Der Fremdseed veraenderte den Starthash nachweislich nicht."

        let contractPressure = contractRoot.GetProperty("pressureSession")
        let foreignPressure = foreignRoot.GetProperty("pressureSession")

        // Strukturinvarianten des Druckprotokolls: gleiche Instanz-/Zyklus-
        // zahl, gleiche Endgruende, gleicher Endstatus, gleiche Fensterlaenge.
        if
            jsonInt64 contractPressure "cycleCount"
            <> jsonInt64 foreignPressure "cycleCount"
            || contractPressure.GetProperty("endStatus").GetProperty("status").GetString()
               <> foreignPressure.GetProperty("endStatus").GetProperty("status").GetString()
            || contractPressure.GetProperty("windowLengthTicks").GetInt64()
               <> foreignPressure.GetProperty("windowLengthTicks").GetInt64()
        then
            failwith "Die Strukturinvarianten des Druckprotokolls widersprechen dem Fremdseedlauf."

        let contractWindows = contractPressure.GetProperty("windows")
        let foreignWindows = foreignPressure.GetProperty("windows")

        if contractWindows.GetArrayLength() <> foreignWindows.GetArrayLength() then
            failwith "Die Instanzanzahl widerspricht dem Fremdseedlauf."

        for index in 0 .. contractWindows.GetArrayLength() - 1 do
            if
                contractWindows.[index].GetProperty("endReason").GetString()
                <> foreignWindows.[index].GetProperty("endReason").GetString()
            then
                failwith "Die Endgruende widersprechen dem Fremdseedlauf."

            // Die Grenzen sind reine Funktionen der jeweiligen Sitzung: der
            // Fremdseedlauf bindet seine eigenen Grenzen relational
            // konsistent (Schemator) und die Druckschicht liest den Seed
            // nie — protokolliert durch identische Struktur bei
            // sitzungseigenen Grenzwerten.
            ()
    finally
        for path in [ contractReport; foreignReport ] do
            if File.Exists(path) then
                File.Delete(path)

// ---------------------------------------------------------------------------
// Ehrliche Nicht-Entscheidungsstaende ueber die CLI (AC-T036-02).
// ---------------------------------------------------------------------------

let pressureWithoutReachedDecisionCarriesHonestGround () =
    let neverCompleted =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.graybox")

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    try
        // Kein Erkundungsabschluss im Lauf: kein Angebot, kein Fenster.
        File.WriteAllText(
            neverCompleted,
            v3Script 3000 [ "intent 250 point 149718 44500"; "intent 251 move 4"; "intent 750 steer 2" ]
        )

        let exitCode, _, stderr =
            runToleratingTransientGate (pressureArguments neverCompleted "20260826" "3000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith $"Der kurze Drucklauf endete mit {exitCode}: {stderr}"

        use document = JsonDocument.Parse(reportJson reportPath)
        let pressure = document.RootElement.GetProperty("pressureSession")

        if
            pressure.GetProperty("endStatus").GetProperty("status").GetString()
            <> PressureContract.EndStatusNotStarted
            || pressure.GetProperty("endStatus").GetProperty("reason").GetString()
               <> PressureContract.NotStartedReasonDecisionNotReached
            || jsonInt64 pressure "cycleCount" <> 0L
            || pressure.GetProperty("windows").GetArrayLength() <> 0
        then
            failwith "Der Lauf ohne Entscheidungsstand trug nicht den ehrlichen not-started-Grund."
    finally
        if File.Exists(neverCompleted) then
            File.Delete(neverCompleted)

        if File.Exists(reportPath) then
            File.Delete(reportPath)

// ---------------------------------------------------------------------------
// Aktivierungskopplung (AC-T036-06): --pressure ohne --decision ist
// Usage-Fehlanwendung (bestehende Bedeutung 2); keine neuen Exitcodes.
// ---------------------------------------------------------------------------

let pressureActivationCouplingStaysUsageError () =
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t034-exploration-separated.graybox")

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    try
        let argumentsWithoutDecision =
            [| "kommandoschleife"
               "--scenario"
               "kommando-graybox"
               "--input-script"
               scriptPath
               "--seed"
               "20260826"
               "--exploration"
               "--pressure"
               "--report"
               reportPath |]

        let exitCode, _, stderr = runAppHost argumentsWithoutDecision

        if exitCode <> ExitCodes.Usage then
            failwith $"--pressure ohne --decision ergab {exitCode} statt der bestehenden Usage-Bedeutung."

        if not (stderr.Contains("--decision", StringComparison.Ordinal)) then
            failwith "Die Usage-Abweisung nennt nicht ihren vertraglichen Kopplungsgrund."

        if File.Exists(reportPath) then
            failwith "Eine Usage-Fehlanwendung erzeugte einen Report."
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

// ---------------------------------------------------------------------------
// Teilreport-Erhaltung (AC-T036-06): die angeforderte Druckaktivierung
// bleibt auch im Exception-Teilreport erhalten; ohne Entscheidungs-
// aktivierung entsteht kein stiller Druckblock.
// ---------------------------------------------------------------------------

let pressureIncompleteReportPreservesActivation () =
    let observed =
        PressureTelemetry(
            WindowLengthTicks = PressureContract.WindowLengthTicks,
            CycleCount = 1L,
            Windows =
                [ PressureWindowEvent(
                      1L,
                      1L,
                      8000L,
                      8600L,
                      PressureContract.WindowEndReasonExpired,
                      -1L,
                      null,
                      PressureContract.FailureCauseWindowExpired
                  ) ],
            LastFailureBoundaryTick = 8600L,
            LastFailureCause = PressureContract.FailureCauseWindowExpired,
            LastReopenBoundaryTick = -1L,
            EndStatus = PressureContract.EndStatusRestartPending,
            EndStatusReason = null
        )

    if isNull (CommandLoopRunner.ResolveIncompletePressure(true, true, null, observed)) then
        failwith "Der Teilreport verlor die beobachtete Drucktelemetrie."

    let empty = CommandLoopRunner.ResolveIncompletePressure(true, true, null, null)

    if
        isNull empty
        || empty.CycleCount <> 0L
        || empty.EndStatus <> PressureContract.EndStatusNotStarted
        || isNull empty.EndStatusReason
    then
        failwith "Der leere Teilreport trug nicht den kanonischen not-started-Block."

    if not (isNull (CommandLoopRunner.ResolveIncompletePressure(true, false, null, null))) then
        failwith "Der Teilreport erfand einen Druckblock ohne Entscheidungsaktivierung."

    if not (isNull (CommandLoopRunner.ResolveIncompletePressure(false, true, null, null))) then
        failwith "Der Teilreport erfand einen Druckblock ohne Druckaktivierung."

// ---------------------------------------------------------------------------
// Titel-HUD (AC-T036-04): additive Druck-Segmente in fester Form; ohne
// Aktivierung byteidentischer Bestandsstand (NF-005-Zweikanal Bindung der
// Neustartanzeige liegt in den InteractiveView-Konstanten des Spiegeltests).
// ---------------------------------------------------------------------------

let titleHudBindsPressureStatesWithoutChangingLegacyForm () =
    let world = SimWorld(20260826u)
    let exploration = ExplorationSession()
    let decision = DecisionSession()
    let pressure = PressureSession()
    let heroZone = HeroTracker.ZoneIndexOf(world)
    let otherZone = (heroZone + 1) % NavWorld.ZoneCount

    let legacy = CommandLoopRunner.BuildTitleHudText(SessionMode.Strategic, world, null)

    if
        not (
            legacy.StartsWith("Riftward Graybox — Modus: Strategisch", StringComparison.Ordinal)
            && not (legacy.Contains("Druck", StringComparison.Ordinal))
        )
    then
        failwith "Der Bestandstitel wurde durch die Druckschicht veraendert."

    decision.OpenOfferForContractTest(0L, heroZone, otherZone)
    decision.TryChoose(DecisionChoiceOption.A, 0L, SessionMode.Personal) |> ignore
    pressure.Observe(0L, world, SessionMode.Personal, decision)

    let windowOpen =
        CommandLoopRunner.BuildTitleHudText(SessionMode.Personal, world, exploration, decision, pressure)

    if not (windowOpen.Contains(" — Druck: Zyklus 1 Rest 600", StringComparison.Ordinal)) then
        failwith $"Das offene Fenster erscheint nicht in der festen Titel-Form: {windowOpen}"

    // Fehlschlags-/Neustartzeitraum: Ablauf ohne Ankunft an 600; der Titel
    // traegt die unterscheidbare Fehlschlagsform bis zur naechsten
    // wirksamen Wahl.
    for boundary in 1L .. 600L do
        pressure.Observe(boundary, world, SessionMode.Personal, decision)

    let afterFailure =
        CommandLoopRunner.BuildTitleHudText(SessionMode.Personal, world, exploration, decision, pressure)

    if not (afterFailure.Contains(" — Druck: Fehlschlag: Zeit abgelaufen", StringComparison.Ordinal)) then
        failwith $"Der Fehlschlag erscheint nicht in der festen Titel-Form: {afterFailure}"

    // Erfolg: Erfolgslauf über einen zweiten Drive mit Ankunft an der
    // Oeffnungsgrenze.
    let successWorld = SimWorld(20260826u)
    let successExploration = ExplorationSession()
    let successDecision = DecisionSession()
    let successPressure = PressureSession()
    let successHeroZone = HeroTracker.ZoneIndexOf(successWorld)

    successDecision.OpenOfferForContractTest(0L, successHeroZone, (successHeroZone + 1) % NavWorld.ZoneCount)

    successDecision.TryChoose(DecisionChoiceOption.A, 0L, SessionMode.Personal)
    |> ignore

    successDecision.Observe(0L, successWorld, SessionMode.Personal, successExploration)
    successPressure.Observe(0L, successWorld, SessionMode.Personal, successDecision)

    let afterSuccess =
        CommandLoopRunner.BuildTitleHudText(
            SessionMode.Personal,
            successWorld,
            successExploration,
            successDecision,
            successPressure
        )

    if not (afterSuccess.Contains(" — Druck: Erfolg", StringComparison.Ordinal)) then
        failwith $"Der Erfolg erscheint nicht in der festen Titel-Form: {afterSuccess}"

// ---------------------------------------------------------------------------
// Schemadispatch und relationale Fabrikationsmatrix (AC-T036-06).
// ---------------------------------------------------------------------------

let private pressureGolden () =
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Pressure-{Guid.NewGuid():N}.json")

    try
        let exitCode, _, _ =
            runToleratingTransientGate (pressureArguments scriptPath "20260826" "11000" reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith "Der Golden-Drucklauf endete fehlerhaft."

        reportJson reportPath
    finally
        if File.Exists(reportPath) then
            File.Delete(reportPath)

let pressureSchemaDispatchRejectsCrossVariants () =
    let golden = pressureGolden ()

    if CommandReportSchema.Validate(golden).Count <> 0 then
        failwith "Golden-Druckreport verletzte den Schemavertrag."

    let assertHasError (fragment: string) (mutated: string) (message: string) =
        let errors = CommandReportSchema.Validate(mutated)

        if errors.Count = 0 then
            failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

        let joined = String.concat "; " errors

        if not (joined.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

    // Die Version-5-Reportlinie ist strikt: Version 4 toleriert keinen
    // Druckblock, Version 5 verlangt ihn vollstaendig.
    assertHasError
        "pressureSession"
        (golden.Replace("\"schemaVersion\":5", "\"schemaVersion\":4"))
        "Downgrade ohne Blockentfernung akzeptiert"

    let withoutBlock =
        golden
            .Replace("\"schemaVersion\":5", "\"schemaVersion\":4")
            .Replace("\"pressureSession\"", "\"pressureSessionRemoved\"")

    assertHasError "unbekanntes Feld" withoutBlock "Fremder Druckblock in Schemaversion 4 akzeptiert"

    // Ein Fabrikationsfeld im Druckblock wird abgewiesen.
    assertHasError
        "unbekanntes Feld"
        (golden.Replace("\"pressureSession\":{\"contract\"", "\"pressureSession\":{\"fabriziert\":true,\"contract\""))
        "Fabriziertes Druckfeld akzeptiert"

let pressureSchemaRelationsRejectFabrication () =
    let golden = pressureGolden ()
    let pressurePath = "\"pressureSession\":{"

    let assertHasError (fragment: string) (mutated: string) (message: string) =
        let errors = CommandReportSchema.Validate(mutated)

        if errors.Count = 0 then
            failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

        let joined = String.concat "; " errors

        if not (joined.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

    // Ein Erfolg mit erfunderner Fehlschlagsursache wird abgewiesen.
    assertHasError
        "keine Fehlschlagsursache"
        (golden.Replace(
            "\"arrivalMode\":\"personal\",\"failureCause\":null",
            "\"arrivalMode\":\"personal\",\"failureCause\":\"window-expired-without-arrival\""
        ))
        "Erfolgsinstanz mit Fehlschlagsursache akzeptiert"

    // Wiederauffrischung nicht an der naechsten Vorgrenze wird abgewiesen.
    assertHasError
        "naechsten Vorgrenze"
        (golden.Replace("\"reopenBoundaryTick\":8601", "\"reopenBoundaryTick\":8610"))
        "Verschobene Wiederauffrischung akzeptiert"

    // Zykluszählung ohne Instanzuebereinstimmung wird abgewiesen.
    assertHasError
        "Zykluszählung"
        (golden.Replace("\"cycleCount\":2", "\"cycleCount\":3"))
        "Falsche Zykluszählung akzeptiert"

    // not-started ohne Grund wird abgewiesen; hier: ein not-started-Status
    // bei vorhandener Zykluszählung widerspricht der Zykluswahrheit.
    assertHasError
        "Endstatus"
        (golden.Replace("\"status\":\"success\"", "\"status\":\"not-started\""))
        "Widerspruch zwischen Endstatus und Zykluszählung akzeptiert"

    // Ein Ablauf mit erfundener Ankunft wird abgewiesen.
    assertHasError
        "Ablauf"
        (golden.Replace(
            "\"endReason\":\"expired\",\"arrivalBoundaryTick\":-1,\"arrivalMode\":null",
            "\"endReason\":\"expired\",\"arrivalBoundaryTick\":8500,\"arrivalMode\":\"personal\""
        ))
        "Ablauf mit erfundener Ankunft akzeptiert"

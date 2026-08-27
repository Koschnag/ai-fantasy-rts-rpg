module ModeSwitchTests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open Riftward.App
open Riftward.App.Bench
open Riftward.App.Command
open Riftward.Platform
open Riftward.Session
open Riftward.Simulation

// ---------------------------------------------------------------------------
// T-033: kleinster Hybrid-Mode-Switch-Prototyp (Modevertrag V1, Abschnitte
// 0 bis 11; KOMMANDOVERTRAG Abschnitt 12). Jede Pruefung bindet Code,
// Vertragsdokument und Gateverhalten gegeneinander; kein Test antwortet auf
// eine offene Produktfrage.
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

let private rulesFor horizon = ScriptWindowRules(40, horizon)

let private v2Script (horizon: int) (bodyLines: string list) =
    let body = String.concat "\n" bodyLines
    $"graybox-input-script-v2 {horizon}\n{body}\nend\n"

let private v1Script (horizon: int) (bodyLines: string list) =
    let body = String.concat "\n" bodyLines
    $"graybox-input-script-v1 {horizon}\n{body}\nend\n"

let private sha256Hex (bytes: byte[]) =
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()

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

let private endHashOf (path: string) =
    let json = File.ReadAllText(path)
    use document = JsonDocument.Parse(json)
    document.RootElement.GetProperty("stateHashChain").GetProperty("end").GetString()

// ---------------------------------------------------------------------------
// Vertragsspiegel (AC-T033-01/10): ModeContract.cs ↔ MODEVERTRAG.md ↔
// KOMMANDOVERTRAG.md Abschnitt 12 ↔ Keymap.
// ---------------------------------------------------------------------------

let modeContractMirrorsDocumentedValues () =
    if ModeContract.DocumentPath <> "docs/MODEVERTRAG.md" then
        failwith "Modevertragspfad falsch."

    if ModeContract.ContractVersion <> "1" then
        failwith "Modevertragsversion falsch."

    if
        ModeContract.HeroAgentIndex <> 0
        || ModeContract.HeroGroupIndex <> 0
    then
        failwith "Vertragsheld ist nicht Agentenindex 0 in Vertragsgruppe 0."

    if
        ModeContract.SwitchReactionTargetTicks <> 2
        || ModeContract.SwitchReactionHardLimitTicks <> 3
    then
        failwith "Wechselreaktionsgrenzen entsprechen nicht der Ableitung 100 ms bzw. 150 ms bei 50 ms je Tick."

    if ModeContract.ScriptFormatIdV2 <> "graybox-input-script-v2" then
        failwith "v2-Formatkennung falsch."

    if ModeContract.SwitchActionName <> "mode-switch" then
        failwith "Umschaltaktionsname falsch."

    let modeDocument = readDocument "docs/MODEVERTRAG.md"

    for identifier in
        [ ModeContract.HeroDesignationId
          ModeContract.SteeringModelId
          ModeContract.CameraModelId
          ModeContract.BadgeModelId
          ModeContract.HudModelId
          ModeContract.SwitchActionId
          ModeContract.SwitchActionName
          ModeContract.SwitchRuleId
          ModeContract.ScopingRuleId
          ModeContract.ContextRejectionPolicyId
          ModeContract.ScriptFormatIdV2
          ModeContract.RejectReasonStrategyIntentInPersonalMode
          ModeContract.RejectReasonSteerIntentInStrategyMode
          ModeContract.RejectReasonSteerDirectionWithoutZone
          ModeContract.ModeStrategicId
          ModeContract.ModePersonalId ] do
        if not (modeDocument.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Modevertragsdokument nennt die Kennung {identifier} nicht."

    // Wechselreaktionsableitung mit transparenter Arithmetik und Same-Tick-
    // Grenzregel stehen zeichentreu im Dokument (AC-T033-01).
    for anchor in
        [ "M = S + 2"
          "⌊150 ms ÷ 50 ms/Tick⌋"
          "⌊100 ms ÷ 50 ms/Tick⌋"
          "mode-scoping-v1"
          "same-tick-switch-last-effective-next-next-v1" ] do
        if not (modeDocument.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Modevertragsdokument nennt den Anker {anchor} nicht."

    // Die autorisierte additive Präzisierung des Kommandovertrags ist als
    // dessen Abschnitt 12 vorhanden und nennt beide Dispositionskennungen
    // (AC-T033-01/10 Konsistenzabgleich).
    let commandDocument = readDocument "docs/KOMMANDOVERTRAG.md"

    for identifier in
        [ "## 12"
          ModeContract.ScopingRuleId
          ModeContract.RejectReasonStrategyIntentInPersonalMode
          ModeContract.RejectReasonSteerIntentInStrategyMode
          ModeContract.SwitchActionName
          "Tab (Scancode 43" ] do
        if not (commandDocument.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Kommandovertrag nennt die Präzisierungskennung {identifier} nicht."

let keymapBindsModeSwitchWithoutCollisions () =
    let ok, error = Keymap.Validate(Keymap.Defaults)

    if not ok then
        failwith $"Default-Keymap mit mode-switch ungueltig: {error}"

    if not (Array.contains ModeContract.SwitchActionName Keymap.SemanticActions) then
        failwith "Keymap kennt die semantische Umschaltaktion mode-switch nicht."

    if Keymap.Resolve(43) <> ModeContract.SwitchActionName then
        failwith "Tab (Scancode 43) loest nicht die Umschaltaktion aus."

    // Kein Bestandsscancode darf durch die Erweiterung doppelt belegt sein;
    // Validate deckt Doppelbindungen ab, die Einzelprüfung hier bindet den
    // T-032-Stand zusätzlich gegen versehentliche Umbelegung.
    for pair in Keymap.Defaults do
        if pair.Key <> ModeContract.SwitchActionName && Array.contains 43 pair.Value then
            failwith $"Aktion {pair.Key} kollidiert mit der Tab-Belegung."

// ---------------------------------------------------------------------------
// Parser (AC-T033-07).
// ---------------------------------------------------------------------------

let parserV2AcceptsSupersetGrammarAndBindsHashes () =
    let content =
        v2Script
            120
            [ "intent 40 clear"
              "intent 41 point 20000 30000"
              "intent 42 steer 3"
              "intent 43 switch"
              "intent 44 steer 0"
              "intent 45 switch"
              "intent 46 move 2" ]

    let parsed = InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes content, rulesFor 120)

    if parsed.Intents.Length <> 7 then
        failwith "v2-Parser verlor Intents."

    if parsed.FormatId <> ModeContract.ScriptFormatIdV2 then
        failwith "v2-Parser band die Formatkennung nicht."

    let expectedSha = sha256Hex (Text.Encoding.UTF8.GetBytes content)

    if parsed.ScriptSha256Hex <> expectedSha then
        failwith "Skript-SHA-256 bindet nicht die Rohbytes."

    let second = InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes content, rulesFor 120)

    if second.IntentPlanHash <> parsed.IntentPlanHash then
        failwith "Planhash ist nicht deterministisch."

    // Die neuen Arten sind kanonisch geordnet (steer 4 < switch 5) und
    // wirksam im Planhash.
    let steers =
        parsed.Intents
        |> Array.filter (fun intent -> intent.Kind = GrayboxIntentKind.SteerGroupToZone)

    let switches =
        parsed.Intents
        |> Array.filter (fun intent -> intent.Kind = GrayboxIntentKind.SwitchMode)

    if steers.Length <> 2 || switches.Length <> 2 then
        failwith "v2-Parser verlor Steer- oder Switch-Intents."

    if steers.[0].A <> 0L || steers.[1].A <> 3L then
        failwith "Steer-Intents verloren ihre Zonenparameter."

    if steers.[0].CompareTo(steers.[1]) >= 0 then
        failwith "Steer-Intents sind nicht kanonisch geordnet."

    // Eine Aenderung eines steer-Ziels aendert den Planhash nachweislich.
    let changedSteer =
        v2Script
            120
            [ "intent 40 clear"
              "intent 41 point 20000 30000"
              "intent 42 steer 4"
              "intent 43 switch"
              "intent 44 steer 0"
              "intent 45 switch"
              "intent 46 move 2" ]

    let changedParsed = InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes changedSteer, rulesFor 120)

    if changedParsed.IntentPlanHash = parsed.IntentPlanHash then
        failwith "Planhash folgt nicht dem steer-Ziel."

let parserRejectsEveryMalformedClassDistinctly () =
    let expectRejectV2 (reason: InputScriptRejectReason) (body: string) (message: string) =
        try
            InputScriptParser.Parse(
                Text.Encoding.UTF8.GetBytes (v2Script 120 [ body ]),
                rulesFor 120
            )
            |> ignore

            failwith $"{message}: Parser akzeptierte die Zeile unerwartet."
        with
        | :? InputScriptException as error ->
            if error.Reason <> reason then
                failwith $"{message}: Klasse war {error.Reason}, erwartet {reason}."

    // Tokenzahlen aller Verben sind erzwungen (auch clear und switch).
    expectRejectV2 InputScriptRejectReason.LineMalformed "intent 40 steer" "Steer ohne Parameter"
    expectRejectV2 InputScriptRejectReason.LineMalformed "intent 40 steer 2 3" "Steer mit Zusatzparameter"
    expectRejectV2 InputScriptRejectReason.LineMalformed "intent 40 switch now" "Switch mit Zusatzparameter"
    expectRejectV2 InputScriptRejectReason.LineMalformed "intent 40 clear extra" "Clear mit Zusatztoken (v2)"
    expectRejectV2 InputScriptRejectReason.RangeViolation "intent 40 steer 6" "Steer-Zone ausserhalb"
    expectRejectV2 InputScriptRejectReason.RangeViolation "intent 40 steer -1" "Steer-Zone negativ"
    expectRejectV2 InputScriptRejectReason.UnknownAction "intent 40 dance" "Unbekannte Aktion unter v2"

    // Legacy-v1 verhaelt sich zeichentreu: fremde Verben bleiben
    // UnknownAction, und die Tokenzahlstrengheit gilt formatsunabhaengig.
    try
        InputScriptParser.Parse(
            Text.Encoding.UTF8.GetBytes (v1Script 120 [ "intent 40 steer 2" ]),
            rulesFor 120
        )
        |> ignore

        failwith "v1-Kopf akzeptierte steer unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.UnknownAction then
            failwith $"v1-Steer-Klasse war {error.Reason}, erwartet UnknownAction."

    try
        InputScriptParser.Parse(
            Text.Encoding.UTF8.GetBytes (v1Script 120 [ "intent 40 switch" ]),
            rulesFor 120
        )
        |> ignore

        failwith "v1-Kopf akzeptierte switch unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.UnknownAction then
            failwith $"v1-Switch-Klasse war {error.Reason}, erwartet UnknownAction."

    try
        InputScriptParser.Parse(
            Text.Encoding.UTF8.GetBytes (v1Script 120 [ "intent 40 clear extra" ]),
            rulesFor 120
        )
        |> ignore

        failwith "v1 akzeptierte Zusatztokens nach clear unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.LineMalformed then
            failwith $"v1-Clear-Klasse war {error.Reason}, erwartet LineMalformed."

    // Ein gueltiges Legacy-v1-Skript bleibt byteidentisch gueltig und bindet
    // die v1-Formatkennung (keine stille Formatdrift).
    let legacy = v1Script 120 [ "intent 40 clear"; "intent 50 move 2" ]
    let parsedLegacy = InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes legacy, rulesFor 120)

    if parsedLegacy.FormatId <> SessionContract.ScriptFormatId then
        failwith "Legacy-v1-Skript verlor seine Formatkennung."

    if parsedLegacy.Intents.Length <> 2 then
        failwith "Legacy-v1-Skript verlor Intents."

let intentCodecEncodesModeKindsDeterministically () =
    // Goldbytes der beiden neuen Arten (21 Bytes Festbreite, Little-Endian,
    // ungenutzte Slots null): steer tick 40 zone 3, switch tick 41.
    let steer = GrayboxIntent(40, GrayboxIntentKind.SteerGroupToZone, 3L)
    let switch = GrayboxIntent(41, GrayboxIntentKind.SwitchMode)

    let steerBytes = IntentCodec.EncodeToArray(steer)
    let switchBytes = IntentCodec.EncodeToArray(switch)

    if steerBytes.Length <> IntentCodec.EncodedSize || switchBytes.Length <> IntentCodec.EncodedSize then
        failwith "Kodierung der neuen Arten verliess die Festbreite."

    let expectedSteer = Array.create IntentCodec.EncodedSize 0uy
    expectedSteer.[0] <- 40uy
    expectedSteer.[4] <- 4uy
    expectedSteer.[5] <- 3uy

    if steerBytes <> expectedSteer then
        failwith "Steer-Encoding verletzte die Goldbytefolge."

    let expectedSwitch = Array.create IntentCodec.EncodedSize 0uy
    expectedSwitch.[0] <- 41uy
    expectedSwitch.[4] <- 5uy

    if switchBytes <> expectedSwitch then
        failwith "Switch-Encoding verletzte die Goldbytefolge."

    // Unabhaengige FNV-1a-64-Nachrechnung ueber die Kodierungsfolge bindet
    // den Planhash an die Kanonisierung.
    let fnv1a64 (bytes: byte[]) =
        let mutable hash = 0xCBF29CE484222325UL

        for value in bytes do
            hash <- (hash ^^^ (uint64 value)) * 0x100000001B3UL

        hash

    let concatenated = Array.append steerBytes switchBytes

    if IntentCodec.HashOf([ steer; switch ]) <> fnv1a64 concatenated then
        failwith "Planhash folgt nicht dem vertraglichen FNV-1a-64 über der Festbreitenkodierung."

// ---------------------------------------------------------------------------
// Kontexttrennung und Same-Tick-Kanonisierung (AC-T033-04, Modevertrag §4/5).
// ---------------------------------------------------------------------------

let private newPipeline seed intents =
    let world = SimWorld(seed)
    let groups = SessionEngine.ReadAgentGroups(world)
    let selection = SelectionModel(groups)
    (world, SessionPipeline(world, selection, intents))

let contextRejectionMatrixBindsDistinctDispositionsWithoutKernelCommands () =
    let intents =
        [| GrayboxIntent(40, GrayboxIntentKind.SteerGroupToZone, 2L) // strategisch: abgewiesen
           GrayboxIntent(41, GrayboxIntentKind.SwitchMode) // wirksam ab 43
           GrayboxIntent(43, GrayboxIntentKind.GroupMoveToZone, 1L) // persoenlich: abgewiesen
           GrayboxIntent(43, GrayboxIntentKind.SteerGroupToZone, 3L) // persoenlich: Kernbefehl
           GrayboxIntent(44, GrayboxIntentKind.SteerGroupToZone, 3L) // Ruhezustand: Dedupe
           GrayboxIntent(45, GrayboxIntentKind.SwitchMode) // wirksam ab 47
           GrayboxIntent(47, GrayboxIntentKind.SteerGroupToZone, 2L) // strategisch: abgewiesen
           GrayboxIntent(47, GrayboxIntentKind.PointSelect, 20000L, 30000L) |]

    let world, pipeline = newPipeline(20260827u, intents)

    let mutable strategyRejectedSeen = 0
    let mutable steerRejectedSeen = 0
    let mutable commandsSeen = 0

    for tick in 40L..48L do
        let outcome = pipeline.ProcessBoundary(tick)
        strategyRejectedSeen <- strategyRejectedSeen + outcome.RejectedStrategyInPersonal
        steerRejectedSeen <- steerRejectedSeen + outcome.RejectedSteerInStrategy
        commandsSeen <- commandsSeen + outcome.CommandCount
        world.Tick()

    // Kontextabweisungen sind unterscheidbar und erzeugen keinen Kernbefehl:
    // genau der eine durchgelassene Steer-Intent erreichte den Kern.
    if strategyRejectedSeen <> 1 then
        failwith $"Erwartete 1 strategische Abweisung im persönlichen Modus, sah {strategyRejectedSeen}."

    if steerRejectedSeen <> 2 then
        failwith $"Erwartete 2 Steer-Abweisungen im strategischen Modus, sah {steerRejectedSeen}."

    if commandsSeen <> 1 then
        failwith $"Erwartete genau einen Kernbefehl aus der persönlichen Lenkung, sah {commandsSeen}."

    if pipeline.AppliedCommandsTotal <> 1L then
        failwith "Pipeline-Kernbefehlzaehler widerspricht der Vorgrenzensicht."

    if pipeline.StrategyIntentsRejectedInPersonalModeTotal <> 1L then
        failwith "Zähler strategischer Abweisungen im persönlichen Modus falsch."

    if pipeline.SteerIntentsRejectedInStrategyModeTotal <> 2L then
        failwith "Zähler persönlicher Abweisungen im strategischen Modus falsch."

    if pipeline.SteerIdleDedupeTotal <> 1L then
        failwith "Ruhezustands-Dedupe der persönlichen Lenkung falsch."

    // Modusgrenzen: strategisch → persönlich (ab 43) → strategisch (ab 47).
    if pipeline.CurrentEffectiveMode <> SessionMode.Strategic then
        failwith "Endmodus nach dem Wechselpaar falsch."

    if pipeline.SwitchProtocol.Count <> 2 then
        failwith $"Wechselprotokoll enthielt {pipeline.SwitchProtocol.Count} statt 2 Einträge."

    for entry in pipeline.SwitchProtocol do
        if not entry.EffectiveInRun then
            failwith "Wechsel innerhalb des Laufs blieb unwirksam."

        if entry.EffectiveBoundaryTick <> entry.IntentTick + 2L then
            failwith "Wirksamkeitsgrenze entspricht nicht M = S + 2."

        if entry.SwitchReactionTicks <> 2L then
            failwith "Wechselreaktion entspricht nicht der Vertragszahlbasis."

        if entry.HeroZoneIndex < -1 || entry.HeroZoneIndex >= NavWorld.ZoneCount then
            failwith "Heldenzonenausweis außerhalb des Vertragsbereichs."

        if entry.HeroPositionXMm < 0L || entry.HeroPositionYMm < 0L then
            failwith "Heldenpositionsausweis außerhalb der Vertragswelt."

let sameTickSwitchIsEvaluatedLastAndEffectiveAtSPlusTwo () =
    // Wechsel an Tick 40: Intents desselben Ticks und an 41 bleiben im
    // vorherigen (strategischen) Modus gültig; ab 42 (M = S + 2) gilt der
    // neue Modus.
    let intents =
        [| GrayboxIntent(40, GrayboxIntentKind.SwitchMode)
           GrayboxIntent(40, GrayboxIntentKind.GroupMoveToZone, 2L) // Same-Tick, vorheriger Modus
           GrayboxIntent(41, GrayboxIntentKind.GroupMoveToZone, 2L) // S+1: vorheriger Modus
           GrayboxIntent(42, GrayboxIntentKind.GroupMoveToZone, 2L) |] // M: abgewiesen

    let world, pipeline = newPipeline(20260826u, intents)

    if pipeline.CurrentEffectiveMode <> SessionMode.Strategic then
        failwith "Sitzung startete nicht strategisch."

    let mutable appliedMoves = 0
    let mutable rejectedInPersonal = 0

    for tick in 40L..43L do
        let outcome = pipeline.ProcessBoundary(tick)
        rejectedInPersonal <- rejectedInPersonal + outcome.RejectedStrategyInPersonal
        appliedMoves <- appliedMoves + (outcome.CommandCount / SimulationContract.GroupCount)
        world.Tick()

    if appliedMoves <> 2 then
        failwith $"Same-Tick-Fenster wandte {appliedMoves} statt 2 Bewegungen im vorherigen Modus an."

    if rejectedInPersonal <> 1 then
        failwith "Bewegung an M = S + 2 war nicht im persönlichen Modus abgewiesen."

    if pipeline.StrategyIntentsRejectedInPersonalModeTotal <> 1L then
        failwith "Zähler strategischer Abweisungen im persönlichen Modus falsch."

    if pipeline.SwitchProtocol.Count <> 1 then
        failwith "Wechselprotokoll verlor den Same-Tick-Wechsel."

    let entry = pipeline.SwitchProtocol.[0]

    if entry.IntentTick <> 40L || entry.EffectiveBoundaryTick <> 42L || entry.SwitchReactionTicks <> 2L then
        failwith "Same-Tick-Wechsel verletzte die Grenzkette S → M = S + 2."

    if entry.PreviousMode <> SessionMode.Strategic || entry.NewMode <> SessionMode.Personal then
        failwith "Wechselrichtung falsch."

// ---------------------------------------------------------------------------
// Twin-Kontinuität und Kernbefehlsäquivalenz (AC-T033-02/03).
// ---------------------------------------------------------------------------

let hybridFlowTwinContinuityIdenticalChainsAndEndHash () =
    // Hybrid-Flow mit drei Wechseln (vollständiger persönlich → strategisch →
    // persönlich-Zyklus); die persönlichen Phasen sind reine Beobachtung,
    // sodass der Twin (dieselben Intents ohne die Wechsel) dieselben
    // Kernbefehle erzeugt (AC-T033-02).
    let hybridBody =
        [ "intent 250 box 4000 16000 36000 50000"
          "intent 255 move 2"
          "intent 260 switch"
          "intent 270 switch"
          "intent 275 move 3"
          "intent 280 point 20000 30000"
          "intent 285 switch" ]

    let hybrid =
        InputScriptParser
            .Parse(Text.Encoding.UTF8.GetBytes (v2Script 420 hybridBody), rulesFor 420)
            .Intents

    let twinBody =
        hybridBody
        |> List.filter (fun line -> not (line.EndsWith(" switch", StringComparison.Ordinal)))

    let twin =
        InputScriptParser
            .Parse(Text.Encoding.UTF8.GetBytes (v2Script 420 twinBody), rulesFor 420)
            .Intents

    let runHybrid seed =
        SessionEngine.Run(SessionRunRequest(seed, hybrid, 240, 420, false))

    let runTwin seed =
        SessionEngine.Run(SessionRunRequest(seed, twin, 240, 420, false))

    let first = runHybrid(20260826u)
    let second = runHybrid(20260826u)

    if first.EndStateHash <> second.EndStateHash then
        failwith "Zwei Läufe desselben Hybrid-Flows lieferten unterschiedliche Endhashes."

    if first.IntervalSampleTicks <> second.IntervalSampleTicks || first.IntervalHashes <> second.IntervalHashes then
        failwith "Kettenstichproben zweier Hybridläufe sind nicht byteidentisch."

    let twinRun = runTwin(20260826u)

    if
        first.StartStateHash <> twinRun.StartStateHash
        || first.EndStateHash <> twinRun.EndStateHash
    then
        failwith "Twin ohne Wechsel-Intents verletzte die Hashketten-Kontinuität."

    if
        first.IntervalSampleTicks <> twinRun.IntervalSampleTicks
        || first.IntervalHashes <> twinRun.IntervalHashes
    then
        failwith "Twin-Kettenstichproben sind nicht byteidentisch."

    // Fremder Seed ändert Start- und Endhash nachweislich.
    let foreign = runHybrid(42u)

    if
        foreign.StartStateHash = first.StartStateHash
        || foreign.EndStateHash = first.EndStateHash
    then
        failwith "Fremder Seed änderte die Hashkette nicht."

    // Wechsel erzeugen zu keinem Zeitpunkt einen Kernbefehl: Der Hybridlauf
    // erhält exakt die Kernbefehle seiner Bewegungsintents.
    if first.KernelCommandsTotal <> twinRun.KernelCommandsTotal then
        failwith "Wechsel-Intents erzeugten Kernelbefehle oder verloren sie."

    if first.Telemetry.SwitchProtocol.Count <> 3 then
        failwith $"Wechselprotokoll des Hybridlaufs enthielt {first.Telemetry.SwitchProtocol.Count} statt 3 Einträge."

    // Jeder Wechsel liegt weit vor dem Horizont und ist wirksam.
    for entry in first.Telemetry.SwitchProtocol do
        if not entry.EffectiveInRun then
            failwith "Wechsel vor dem Horizont blieb unwirksam."

        if entry.SwitchReactionTicks <> 2L then
            failwith "Wechselreaktion verletzte die Zielgrenze 2."

let steeringProducesKernelCommandEquivalentToDirectSurface () =
    // Sitzungspfad: Wechsel an 39 (wirksam ab 41), Lenkung an 41 auf Zone 3.
    let sessionWorld, pipeline =
        newPipeline(
            11u,
            [| GrayboxIntent(39, GrayboxIntentKind.SwitchMode)
               GrayboxIntent(41, GrayboxIntentKind.SteerGroupToZone, 3L) |]
        )

    // Kontrollkern: derselbe Befehl direkt über die unveränderte öffentliche
    // Kernbefehlsfläche am selben Tick.
    let controlWorld = SimWorld(11u)

    for tick in 0L..59L do
        pipeline.ProcessBoundary(tick) |> ignore

        if tick = 41L then
            let direct = [| SimCommand(41, 0, SimCommandKind.GroupMoveToZone, 3) |]
            controlWorld.ApplyCommands(direct)

        sessionWorld.Tick()
        controlWorld.Tick()

        if sessionWorld.ComputeStateHash() <> controlWorld.ComputeStateHash() then
            failwith $"Persönliche Lenkung wich am Tick {tick} vom direkten Kernbefehl ab."

    if sessionWorld.TargetZoneOfGroup(0) <> 3 then
        failwith "Lenkung setzte nicht das Kernziel der Vertragsgruppe 0."

// ---------------------------------------------------------------------------
// Gate-Matrix Kriterium 6 (AC-T033-05): fail-closed ohne Vakuumpass.
// ---------------------------------------------------------------------------

let commandGateSwitchReactionCriterionIsFailClosedWithoutVacuumPass () =
    let limits = CommandGateLimits.Documented

    if
        limits.SwitchReactionTargetTicks <> 2
        || limits.SwitchReactionHardLimitTicks <> 3
    then
        failwith "Gategrenzen von Kriterium 6 binden nicht die Modevertragswerte."

    let evaluate maxSwitch samples =
        CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, maxSwitch, samples))

    // Ohne wirksamen Wechsel ist das Kriterium ausdrücklich NICHT
    // auswertbar; der Vakuumfall wird nie als gemessener Pass ausgegeben.
    let vacuum = CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 0L, 0))

    if vacuum.SwitchReactionEvaluated then
        failwith "Kriterium 6 war ohne wirksamen Wechsel als ausgewertet ausgegeben."

    if not vacuum.Pass then
        failwith "Nichtauswertung von Kriterium 6 faltete das Gate fälschlich."

    if vacuum.Violations.Count <> 0 then
        failwith "Nichtauswertung von Kriterium 6 erzeugte eine Verletzung."

    // Messung am Ziel: kein Verstoß, Ziel erfüllt.
    let atTarget = CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 2L, 1))

    if
        not atTarget.SwitchReactionEvaluated
        || not atTarget.SwitchReactionTargetMet
        || not atTarget.Pass
    then
        failwith "Wechselreaktion am Ziel (2 Ticks) erfüllte Kriterium 6 nicht."

    // Innerhalb der harten Grenze, über dem Ziel: kein Gate-Fail, Ziel
    // verfehlt (T-032-Präzedenz).
    let aboveTarget = CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 3L, 1))

    if not aboveTarget.Pass || aboveTarget.SwitchReactionTargetMet then
        failwith "Reaktion 3 muss passieren und das Ziel verfehlen."

    // Fault-Injection: Messung über der harten Grenze verletzt fail-closed.
    let violated = CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 4L, 1))

    if violated.Pass then
        failwith "Wechselreaktion 4 Ticks passierte das Gate."

    if not (violated.Violations.Contains("switch-reaction-ticks-above-hard-limit")) then
        failwith "Kriterium-6-Verletzung trug nicht die stabile Verletzungskennung."

// ---------------------------------------------------------------------------
// CLI-Vertrag (AC-T033-02/07/09): v2-Hybrid über den öffentlichen Befehl.
// ---------------------------------------------------------------------------

let cliContractRunsV2HybridHeadlessWithModeReportAndTwin () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t033-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let hybridPath = Path.Combine(temporary, "hybrid.txt")

        File.WriteAllText(
            hybridPath,
            "graybox-input-script-v2 420\n"
            + "intent 250 box 4000 16000 36000 50000\n"
            + "intent 255 move 2\n"
            + "intent 260 switch\n"
            + "intent 270 switch\n"
            + "intent 275 move 3\n"
            + "intent 280 point 20000 30000\n"
            + "intent 285 switch\n"
            + "end\n"
        )

        let twinPath = Path.Combine(temporary, "twin.txt")

        File.WriteAllText(
            twinPath,
            "graybox-input-script-v2 420\n"
            + "intent 250 box 4000 16000 36000 50000\n"
            + "intent 255 move 2\n"
            + "intent 275 move 3\n"
            + "intent 280 point 20000 30000\n"
            + "end\n"
        )

        let argumentsFor scriptPath reportPath seed =
            [| "kommandoschleife"
               "--scenario"
               "kommando-graybox"
               "--input-script"
               scriptPath
               "--seed"
               seed
               "--report"
               reportPath
               "--warmup-ticks"
               "240"
               "--horizon-ticks"
               "420" |]

        let reportHybridOne = Path.Combine(temporary, "hybrid1.json")
        let reportHybridTwo = Path.Combine(temporary, "hybrid2.json")
        let reportTwin = Path.Combine(temporary, "twin.json")

        let exitOne, _, _ =
            runToleratingTransientGate (argumentsFor hybridPath reportHybridOne "20260826")

        if exitOne <> 0 then
            failwith $"Hybrid-Lauf ergab Exitcode {exitOne}."

        let exitTwo, _, _ =
            runToleratingTransientGate (argumentsFor hybridPath reportHybridTwo "20260826")

        if exitTwo <> 0 then
            failwith $"Zweiter Hybrid-Lauf ergab Exitcode {exitTwo}."

        for path in [ reportHybridOne; reportHybridTwo ] do
            let json = File.ReadAllText(path)

            if not (CommandReportSchema.Validate(json).Count = 0) then
                failwith $"Hybrid-Report verletzte den Schemavertrag: {path}"

        // K2: zwei unabhängige Fresh-Prozessläufe sind builderidentisch.
        if endHashOf reportHybridOne <> endHashOf reportHybridTwo then
            failwith "Zwei Hybrid-Prozessläufe lieferten unterschiedliche Endhashes."

        // Twin über denselben öffentlichen Befehl: denselben Endhash.
        let exitTwin, _, _ =
            runToleratingTransientGate (argumentsFor twinPath reportTwin "20260826")

        if exitTwin <> 0 then
            failwith $"Twin-Lauf ergab Exitcode {exitTwin}."

        if endHashOf reportHybridOne <> endHashOf reportTwin then
            failwith "Hybrid-Flow und Twin lieferten unterschiedliche Endhashes."

        // Der Hybrid-Report bindet Wechselprotokoll, Heldenstatus und
        // Kriterium 6 als ausgewertet am Ziel.
        let hybridJson = File.ReadAllText(reportHybridOne)
        use document = JsonDocument.Parse(hybridJson)
        let modeSession = document.RootElement.GetProperty("modeSession")
        let protocol = modeSession.GetProperty("switchProtocol")

        if protocol.GetArrayLength() <> 3 then
            failwith "Hybrid-Report band kein dreieintragiges Wechselprotokoll."

        for entry in protocol.EnumerateArray() do
            if entry.GetProperty("switchReactionTicks").GetInt64() <> 2L then
                failwith "Wechselreaktion im Report verletzte die Zielgrenze."

            if
                entry.GetProperty("effectiveBoundaryTick").GetInt64()
                <> entry.GetProperty("intentTick").GetInt64() + 2L
            then
                failwith "Wirksamkeitsgrenze entspricht nicht M = S + 2."

        let switchGate = document.RootElement.GetProperty("gate").GetProperty("switchReaction")

        if
            not (switchGate.GetProperty("evaluated").GetBoolean())
            || switchGate.GetProperty("max").GetInt64() <> 2L
            || not (switchGate.GetProperty("targetMet").GetBoolean())
        then
            failwith "Kriterium 6 war im echten Hybridlauf nicht als gemessener Zielpass gebunden."

        if modeSession.GetProperty("finalMode").GetString() <> "personal" then
            failwith "Hybrid-Report wies nicht den persönlichen Endmodus aus."

        // Fremder Seed ändert das Ergebnis nachweislich.
        let reportForeign = Path.Combine(temporary, "foreign.json")

        let exitForeign, _, _ =
            runToleratingTransientGate (argumentsFor hybridPath reportForeign "42")

        if exitForeign <> 0 then
            failwith $"Fremdseedlauf ergab Exitcode {exitForeign}."

        if endHashOf reportHybridOne = endHashOf reportForeign then
            failwith "Fremder Seed änderte den Endhash nicht."

        // v1-Kopf mit steer bleibt Code 37 ohne Report (UnknownAction).
        let legacyMixedPath = Path.Combine(temporary, "mixed.txt")
        File.WriteAllText(legacyMixedPath, v1Script 420 [ "intent 260 steer 2" ])

        let exitLegacy, _, _ = runAppHost (argumentsFor legacyMixedPath reportHybridOne "20260826")

        if exitLegacy <> ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable) then
            failwith $"Legacy-v1-Skript mit steer ergab Exitcode {exitLegacy} statt Code 37."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)
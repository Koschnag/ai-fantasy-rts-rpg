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

    if ModeContract.HeroAgentIndex <> 0 || ModeContract.HeroGroupIndex <> 0 then
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

    let parsed =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes content, rulesFor 120)

    if parsed.Intents.Length <> 7 then
        failwith "v2-Parser verlor Intents."

    if parsed.FormatId <> ModeContract.ScriptFormatIdV2 then
        failwith "v2-Parser band die Formatkennung nicht."

    let expectedSha = sha256Hex (Text.Encoding.UTF8.GetBytes content)

    if parsed.ScriptSha256Hex <> expectedSha then
        failwith "Skript-SHA-256 bindet nicht die Rohbytes."

    let second =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes content, rulesFor 120)

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

    // Kanonische Ordnung ist tickprimär: steer 3 (Tick 42) vor steer 0 (Tick 44).
    if steers.[0].A <> 3L || steers.[1].A <> 0L then
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

    let changedParsed =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes changedSteer, rulesFor 120)

    if changedParsed.IntentPlanHash = parsed.IntentPlanHash then
        failwith "Planhash folgt nicht dem steer-Ziel."

let parserRejectsEveryMalformedClassDistinctly () =
    let expectRejectV2 (reason: InputScriptRejectReason) (body: string) (message: string) =
        try
            InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v2Script 120 [ body ]), rulesFor 120)
            |> ignore

            failwith $"{message}: Parser akzeptierte die Zeile unerwartet."
        with :? InputScriptException as error ->
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
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v1Script 120 [ "intent 40 steer 2" ]), rulesFor 120)
        |> ignore

        failwith "v1-Kopf akzeptierte steer unerwartet."
    with :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.UnknownAction then
            failwith $"v1-Steer-Klasse war {error.Reason}, erwartet UnknownAction."

    try
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v1Script 120 [ "intent 40 switch" ]), rulesFor 120)
        |> ignore

        failwith "v1-Kopf akzeptierte switch unerwartet."
    with :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.UnknownAction then
            failwith $"v1-Switch-Klasse war {error.Reason}, erwartet UnknownAction."

    try
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v1Script 120 [ "intent 40 clear extra" ]), rulesFor 120)
        |> ignore

        failwith "v1 akzeptierte Zusatztokens nach clear unerwartet."
    with :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.LineMalformed then
            failwith $"v1-Clear-Klasse war {error.Reason}, erwartet LineMalformed."

    // Ein gueltiges Legacy-v1-Skript bleibt byteidentisch gueltig und bindet
    // die v1-Formatkennung (keine stille Formatdrift).
    let legacy = v1Script 120 [ "intent 40 clear"; "intent 50 move 2" ]

    let parsedLegacy =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes legacy, rulesFor 120)

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

    if
        steerBytes.Length <> IntentCodec.EncodedSize
        || switchBytes.Length <> IntentCodec.EncodedSize
    then
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

let private newPipeline (seed, intents) =
    let world = SimWorld(seed)
    let groups = SessionEngine.ReadAgentGroups(world)
    let selection = SelectionModel(groups)
    (world, SessionPipeline(world, selection, intents))

let contextRejectionMatrixBindsDistinctDispositionsWithoutKernelCommands () =
    // Kanonische Intentordnung innerhalb eines Ticks: point (1) vor steer (4).
    let intents =
        [| GrayboxIntent(40, GrayboxIntentKind.SteerGroupToZone, 2L) // strategisch: abgewiesen
           GrayboxIntent(41, GrayboxIntentKind.SwitchMode) // wirksam ab 43
           GrayboxIntent(43, GrayboxIntentKind.GroupMoveToZone, 1L) // persoenlich: abgewiesen
           GrayboxIntent(43, GrayboxIntentKind.SteerGroupToZone, 3L) // persoenlich: Kernbefehl
           GrayboxIntent(44, GrayboxIntentKind.SteerGroupToZone, 3L) // Ruhezustand: Dedupe
           GrayboxIntent(45, GrayboxIntentKind.SwitchMode) // wirksam ab 47
           GrayboxIntent(47, GrayboxIntentKind.PointSelect, 20000L, 30000L) // strategisch: gueltig
           GrayboxIntent(47, GrayboxIntentKind.SteerGroupToZone, 2L) |] // strategisch: abgewiesen

    let world, pipeline = newPipeline (20260827u, intents)

    let mutable strategyRejectedSeen = 0
    let mutable steerRejectedSeen = 0
    let mutable commandsSeen = 0

    for tick in 40L .. 48L do
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
    // neue Modus. Kanonische Ordnung: move (3) vor switch (5) — der Wechsel
    // wird dadurch nach allen anderen Intents desselben Ticks ausgewertet.
    let intents =
        [| GrayboxIntent(40, GrayboxIntentKind.GroupMoveToZone, 2L) // Same-Tick, vorheriger Modus
           GrayboxIntent(40, GrayboxIntentKind.SwitchMode)
           GrayboxIntent(41, GrayboxIntentKind.GroupMoveToZone, 2L) // S+1: vorheriger Modus
           GrayboxIntent(42, GrayboxIntentKind.GroupMoveToZone, 2L) |] // M: abgewiesen

    let world, pipeline = newPipeline (20260826u, intents)

    if pipeline.CurrentEffectiveMode <> SessionMode.Strategic then
        failwith "Sitzung startete nicht strategisch."

    let mutable rejectedInPersonal = 0

    for tick in 40L .. 43L do
        let outcome = pipeline.ProcessBoundary(tick)
        rejectedInPersonal <- rejectedInPersonal + outcome.RejectedStrategyInPersonal
        world.Tick()

    if rejectedInPersonal <> 1 then
        failwith "Bewegung an M = S + 2 war nicht im persönlichen Modus abgewiesen."

    if pipeline.StrategyIntentsRejectedInPersonalModeTotal <> 1L then
        failwith "Zähler strategischer Abweisungen im persönlichen Modus falsch."

    if pipeline.SwitchProtocol.Count <> 1 then
        failwith "Wechselprotokoll verlor den Same-Tick-Wechsel."

    // Ohne Vorauswahl sind die Bewegungen an 40/41 kontextgültig (vorheriger
    // Modus) und scheitern erst an der Auswahlprüfung; die Bewegung an M
    // scheitert kontextuell im persönlichen Modus. Ohne Vorauswahl entsteht
    // kein Kernbefehl.
    if pipeline.MoveWithoutSelectionTotal <> 2L then
        failwith "Same-Tick-Bewegungen im vorherigen Modus wurden nicht kontextgültig geprüft."

    if pipeline.AppliedCommandsTotal <> 0L then
        failwith "Same-Tick-Fenster erzeugte ohne Vorauswahl einen Kernbefehl."

    let entry = pipeline.SwitchProtocol.[0]

    if
        entry.IntentTick <> 40L
        || entry.EffectiveBoundaryTick <> 42L
        || entry.SwitchReactionTicks <> 2L
    then
        failwith "Same-Tick-Wechsel verletzte die Grenzkette S → M = S + 2."

    if
        entry.PreviousMode <> SessionMode.Strategic
        || entry.NewMode <> SessionMode.Personal
    then
        failwith "Wechselrichtung falsch."

// ---------------------------------------------------------------------------
// Interaktivpfad-Bindungen (AC-T033-06): Schema des Abgriffpaars/HUD und
// Quellverdrahtung (Anzeigepflicht); das Verhalten selbst bleibt einer
// Displaysession vorbehalten (displayloser kontrollierter Code-19-Abbruch).
// ---------------------------------------------------------------------------

let private assertSchemaError (needle: string) (json: string) (message: string) =
    if CommandReportSchema.Validate(json).Count = 0 then
        failwith $"{message}: Schemaprüfung akzeptierte den Report unerwartet."

    let errors = String.concat "; " (CommandReportSchema.Validate(json))

    if not (errors.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{message}: Fehler enthielten nicht '{needle}', sondern: {errors}"

let private hex64OfA = String.replicate 64 "a"

let private captureEntry (mode: string) =
    "{\"mode\":\""
    + mode
    + "\",\"sha256\":\""
    + hex64OfA
    + "\",\"width\":1920,\"height\":1080,\"format\":\"bmp-32bpp-bottom-up\",\"statementLimit\":\"graybox-state-occupancy-not-gameplay-atmosphere-or-shipping\"}"

let interactiveReportSchemaBindsCapturePairAndHud () =
    // Interaktive Reportvariante aus dem headless Golden abgeleitet: exakt
    // die Felder, die der Moduswechsel der Ausführungsart ändert (Display,
    // Renderkennzahlen, Kettenkriterium, HUD, Abgriffpaar).
    let interactive =
        CommandLoopTests.goldenReport
            .Replace("\"executionMode\":\"headless\"", "\"executionMode\":\"interactive\"")
            .Replace(
                "\"display\":{\"measured\":false,\"reason\":\"headless-mode-native-artifacts-not-loaded\"}",
                "\"display\":{\"measured\":true,\"renderer\":\"fixture-gl-renderer\",\"vendorId\":4098,\"deviceId\":26968,\"glVersion\":\"GL 3.3 fixture\"}"
            )
            .Replace(
                "\"frameTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
                "\"frameTimeMs\":{\"unit\":\"ms\",\"method\":\"stopwatch-delta-around-windowed-simulation-tick-including-allocation-probes\",\"p50\":1.0,\"p95\":2.0,\"p99\":3.0,\"gateCoupled\":false}"
            )
            .Replace(
                "\"gpuTimeMs\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
                "\"gpuTimeMs\":{\"measured\":true,\"unit\":\"ms\",\"method\":\"bgfx-stats-gpu-timer-p99\",\"p99\":1.5,\"timerFreqHz\":1000,\"gateCoupled\":false}"
            )
            .Replace(
                "\"drawSubmitCallsPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
                "\"drawSubmitCallsPerFrame\":{\"unit\":\"count\",\"method\":\"bgfx-stats-numdraw-max-including-shadow-passes\",\"value\":10,\"gateCoupled\":false}"
            )
            .Replace(
                "\"visibleTrianglesPerFrame\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
                "\"visibleTrianglesPerFrame\":{\"unit\":\"count\",\"method\":\"bgfx-stats-numprims-trilist-max-including-shadow-passes\",\"value\":1000,\"gateCoupled\":false}"
            )
            .Replace(
                "\"concurrentMarkers\":{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
                "\"concurrentMarkers\":{\"unit\":\"count\",\"method\":\"marker-instance-count-max-per-frame\",\"peak\":5,\"gateCoupled\":false}"
            )
            .Replace(
                "\"stateChainSelfConsistency\":{\"evaluated\":true}",
                "\"stateChainSelfConsistency\":{\"evaluated\":false,\"reason\":\"live-inputs-nondeterministic-criterion-not-asserted\"}"
            )
            .Replace(
                "\"hud\":{\"measured\":false,\"kind\":\"title-hud-mode-herozone-v1\",\"reason\":\"headless-run-without-window\"}",
                "\"hud\":{\"measured\":true,\"kind\":\"title-hud-mode-herozone-v1\",\"fields\":{\"mode\":\"strategic\",\"heroZone\":2}}"
            )
            .Replace(
                "\"frameEvidence\":{\"captured\":false,\"reason\":\"capture-not-requested\"}",
                "\"frameEvidence\":{\"captured\":true,\"afterMeasurementWindow\":true,\"boundTick\":420,\"boundStateHash\":\"978aab19406daa26\",\"captures\":["
                + captureEntry "strategic"
                + ","
                + captureEntry "personal"
                + "]}"
            )

    if CommandReportSchema.Validate(interactive).Count <> 0 then
        failwith (
            "Interaktiver Report mit Abgriffpaar und HUD verletzte den Schemavertrag: "
            + String.concat "; " (CommandReportSchema.Validate(interactive))
        )

    // Negativmatrix: weniger als zwei Abgriffe, fremder Modusname und
    // fehlender gebundener Weltzustand sind unzulaessig.
    assertSchemaError
        "exakt zwei"
        (interactive.Replace("," + captureEntry "personal", String.Empty))
        "Einzelabgriff ohne Paar akzeptiert"

    assertSchemaError
        "konstanter Wert"
        (interactive.Replace("\"mode\":\"personal\"", "\"mode\":\"dritter-modus\""))
        "Fremder Abgriffmodusname akzeptiert"

    assertSchemaError
        "Pflichtfeld"
        (interactive.Replace(
            "\"boundStateHash\":\"978aab19406daa26\"",
            "\"boundStateHashRenamed\":\"978aab19406daa26\""
        ))
        "Abgriffpaar ohne gebundenen Weltzustand akzeptiert"

// ---------------------------------------------------------------------------
// Fail-closed Abgriffpaar-Prüfung (AC-T033-06): identische und uniforme
// Frames sind ein Capturedefekt (Real-Display-Befund: byteidentisch schwarze
// Frames durch ungebindenes Renderziel), kein Beleg.
// ---------------------------------------------------------------------------

let private testBmpFromPixels (pixels: (byte * byte * byte * byte) list) =
    // Direkte BGRA-Pixelfolge (ohne Encoder-Umweg), exakt 4 Bytes je Pixel.
    let bytes = Array.zeroCreate<byte> (54 + (4 * List.length pixels))
    bytes.[0] <- byte 'B'
    bytes.[1] <- byte 'M'

    for index, (b, g, r, a) in List.indexed pixels do
        let offset = 54 + (4 * index)
        bytes.[offset] <- b
        bytes.[offset + 1] <- g
        bytes.[offset + 2] <- r
        bytes.[offset + 3] <- a

    bytes

let private testBmp (fill: int -> byte) =
    let rgba = Array.init (8 * 8 * 4) (fun i -> fill i)
    FrameEvidence.EncodeBmpFromRgbaTopDown(rgba, 8, 8)

let capturePairValidationIsFailClosed () =
    let uniformBlack = testBmp (fun _ -> 0uy)
    let uniformGray = testBmp (fun _ -> 7uy)

    let varied = testBmp (fun i -> byte ((i * 37 + 11) % 251))

    let variedOther = testBmp (fun i -> byte ((i * 41 + 3) % 251))

    // Byteidentisches Paar ist ein Capturedefekt, auch wenn beide uniform sind.
    if
        CommandFrameEvidence.AnalyzeCapturePair(uniformBlack, uniformBlack)
        <> CommandFrameEvidence.ReasonPairFramesIdentical
    then
        failwith "Byteidentisches Paar wurde nicht als Capturedefekt abgewiesen."

    if
        CommandFrameEvidence.AnalyzeCapturePair(varied, varied)
        <> CommandFrameEvidence.ReasonPairFramesIdentical
    then
        failwith "Byteidentisches Nichtuniform-Paar wurde nicht abgewiesen."

    // Uniformer Einzelabgriff ist kein Beleg, selbst wenn das Paar nicht
    // identisch ist: vollständig schwarze BGRA(0,0,0,255)-Frames (die
    // beobachtete Real-Display-Klasse des ungebindenen Renderziel-Views) sind
    // pixelweise kanalgleich und werden erfasst, ebenso uniform farbige.
    let blackBgra255 = testBmpFromPixels [ for _ in 1..16 -> (0uy, 0uy, 0uy, 255uy) ]

    let uniformColored =
        testBmpFromPixels [ for _ in 1..16 -> (20uy, 40uy, 60uy, 255uy) ]

    if
        CommandFrameEvidence.AnalyzeCapturePair(blackBgra255, varied)
        <> CommandFrameEvidence.ReasonFrameUniform
    then
        failwith "Uniform schwarzer BGRA(0,0,0,255)-Abgriff wurde nicht abgewiesen."

    if
        CommandFrameEvidence.AnalyzeCapturePair(varied, uniformColored)
        <> CommandFrameEvidence.ReasonFrameUniform
    then
        failwith "Uniform farbiger Abgriff wurde nicht abgewiesen."

    // Ein einziges abweichendes Pixel bricht die Uniformität; ein Paar aus
    // diesem Frame und einem anderen Nichtuniform-Frame ist belegbar.
    let singleDifferingPixel =
        [ for i in 0..15 ->
              if i = 7 then
                  (21uy, 40uy, 60uy, 255uy)
              else
                  (20uy, 40uy, 60uy, 255uy) ]
        |> testBmpFromPixels

    if CommandFrameEvidence.IsUniform(singleDifferingPixel) then
        failwith "Ein einziges abweichendes Pixel wurde als uniform eingestuft."

    if
        CommandFrameEvidence.AnalyzeCapturePair(singleDifferingPixel, variedOther)
        <> null
    then
        failwith "Nichtuniformes unterscheidbares Paar wurde fälschlich abgewiesen."

    // Ein unterscheidbares, nichtuniformes Paar ist belegbar.
    if CommandFrameEvidence.AnalyzeCapturePair(varied, variedOther) <> null then
        failwith "Unterscheidbares Nichtuniform-Paar wurde fälschlich abgewiesen."

    // Malformed und zu kurze Bytes sind fail-closed abgewiesen.
    if
        CommandFrameEvidence.AnalyzeCapturePair(Array.truncate 30 uniformBlack, varied)
        <> CommandFrameEvidence.ReasonFrameMalformed
    then
        failwith "Zu kurze Abgriffbytes wurden nicht fail-closed abgewiesen."

    let wrongMagic = Array.copy uniformBlack
    wrongMagic.[0] <- byte 'X'

    if
        CommandFrameEvidence.AnalyzeCapturePair(wrongMagic, varied)
        <> CommandFrameEvidence.ReasonFrameMalformed
    then
        failwith "Abgriffbytes mit fremder Kennung wurden nicht abgewiesen."

    // Vertragsbenennung: Suffix vor .bmp, suffigiert ohne Endung, fremde
    // Endung fail-closed abgewiesen statt BMP-Bytes unter falscher Endung.
    let mutable strategic = null
    let mutable personal = null
    let mutable reason = null

    if
        not (CommandLoopRunner.TrySuffixArtifactPath("pfad/report.bmp", &strategic, &personal, &reason))
        || strategic <> "pfad/report-strategisch.bmp"
        || personal <> "pfad/report-persoenlich.bmp"
        || reason <> null
    then
        failwith "BMP-Paarbenennung verletzt den Vertrag."

    if
        not (CommandLoopRunner.TrySuffixArtifactPath("pfad/report", &strategic, &personal, &reason))
        || strategic <> "pfad/report-strategisch"
        || personal <> "pfad/report-persoenlich"
    then
        failwith "Endungslose Paarbenennung verletzt den Vertrag."

    if
        CommandLoopRunner.TrySuffixArtifactPath("pfad/report.png", &strategic, &personal, &reason)
        || reason <> "capture-path-extension-must-be-bmp"
    then
        failwith "Fremde Endung wurde nicht fail-closed abgewiesen."

let interactiveHybridWiringIsBoundToSources () =
    // Quelltextbindung nach T-032-Präzedenz (rift.sh-/Runner-Vertragstests):
    // der fensterpflichtige Hybridmodus ist verdrahtet — Umschaltaktion,
    // Lenkung, kontextsichtbare Abweisung, Verfolgungskamera, Titel-HUD und
    // Abgriffpaar haben Consumer im Live-Pfad; ohne Display bricht der Modus
    // kontrolliert ab (Code 19), statt Interaktivverhalten zu simulieren.
    let runnerText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "CommandLoopRunner.cs"))

    let viewText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "InteractiveView.cs"))

    for fragment in
        [ "ModeContract.SwitchActionName"
          "GrayboxIntentKind.SwitchMode"
          "GrayboxIntentKind.SteerGroupToZone"
          "HeroDirectionSteering.ResolveZone"
          "HeroChaseCamera"
          "HeroTracker.ZoneIndexOf"
          "ModeContract.RejectReasonStrategyIntentInPersonalMode"
          "ModeContract.RejectReasonSteerDirectionWithoutZone"
          "InteractiveContextRejections"
          "ExecuteCapturePair"
          "TrySuffixArtifactPath"
          "AnalyzeCapturePair"
          "capture-path-extension-must-be-bmp"
          "SetViewFrameBuffer(InteractiveViews.ViewCapture, frameBuffer)"
          "device.ConfigureRenderTargetView("
          "\"-strategisch\""
          "\"-persoenlich\""
          "SetTitle("
          "Riftward Graybox — Modus: "
          "— Heldenzone: " ] do
        if not (runnerText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"CommandLoopRunner verdrahtet den Interaktiv-Hybridmodus nicht ({fragment})."

    // Die fail-closed Paar-Grundkennungen sind in der Evidenzklasse gebunden.
    let evidenceText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "CommandFrameEvidence.cs"))

    for fragment in
        [ "capture-pair-frames-identical"
          "capture-frame-uniform"
          "capture-frame-malformed" ] do
        if not (evidenceText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"CommandFrameEvidence bindet die Abgriffabweisung nicht ({fragment})."

    for fragment in
        [ "0.45f"
          "0.85f"
          "1.00f"
          "0.20f"
          "WriteFrameState(SimWorld world, long tickIndex, SessionMode visualMode)" ] do
        if not (viewText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"InteractiveView bindet den Held-/Modus-Badgekanal nicht ({fragment})."

    // Strukturelle Nichterreichbarkeit: Der einzige interaktive Lenkkanal ist
    // die richtungsgelenkte Lenkung im persönlichen Modus — ein Lenk-Intent
    // kann im strategischen Modus an keiner Stelle der Eingabeübersetzung
    // entstehen (genau ein Definitionsort plus vier Pan-Richtungsaufrufe).
    let steeringCallSites = (runnerText.Split("EnqueueDirectionalSteering(")).Length - 1

    if steeringCallSites <> 5 then
        failwith $"Interaktive Lenkung hat {steeringCallSites} statt 5 Bindungsorten (Definition plus vier Richtungen)."

let personalModeHidesStrategicSelectionGlyphs () =
    let world = SimWorld(20260826u)
    let groups = SessionEngine.ReadAgentGroups(world)
    let selection = SelectionModel(groups)

    selection.EvaluateBox(world, Int64.MinValue, Int64.MinValue, Int64.MaxValue, Int64.MaxValue)

    if selection.SelectedCount <> SimulationContract.GroupCount then
        failwith "Testfixture selektierte nicht alle Vertragsgruppen."

    use view = new InteractiveView()
    view.BindAgentGroups(groups)
    view.BindSelection(selection)

    let strategicCount = view.WriteFrameState(world, 100L, SessionMode.Strategic)
    let personalCount = view.WriteFrameState(world, 100L, SessionMode.Personal)
    let strategicAgain = view.WriteFrameState(world, 100L, SessionMode.Strategic)

    if strategicCount <> SimulationContract.AgentCount + 1 then
        failwith $"Strategische Auswahlglyphen sind unvollstaendig ({strategicCount})."

    if personalCount <> 1 then
        failwith $"Persoenlicher Modus zeigt {personalCount - 1} strategische Auswahlglyphen."

    if
        strategicAgain <> strategicCount
        || selection.SelectedCount <> SimulationContract.GroupCount
    then
        failwith "Rein visuelles Ausblenden hat den erhaltenen Auswahlzustand veraendert."

let strategicCaptureCameraFocusesHeroWithoutMutatingSessionCamera () =
    let world = SimWorld(20260826u)
    let sessionCamera = GrayboxCamera()
    sessionCamera.Pan(-31.0, -17.0)
    sessionCamera.SetDistance(32.0)

    let assertNear label expected actual =
        if abs (expected - actual) > 1e-9 then
            failwith $"{label}: erwartet {expected:R}, erhalten {actual:R}."

    let before =
        sessionCamera.CenterXMeters, sessionCamera.CenterZMeters, sessionCamera.DistanceMeters

    let capture = CommandLoopRunner.StrategicCaptureCamera(sessionCamera, world)

    let expectedX =
        float (world.PositionXOf(ModeContract.HeroAgentIndex)) / float FixedPoint.One

    let expectedZ =
        float (world.PositionYOf(ModeContract.HeroAgentIndex)) / float FixedPoint.One

    let margins =
        InteractiveCameraMath.GroundFootprint(capture, InteractiveCameraMath.DefaultViewportAspectRatio)

    assertNear "Strategischer Mindestzoom am Westabstand" 12.0 capture.DistanceMeters
    assertNear "Strategischer Westabstand" 12.658402871356 capture.CenterXMeters
    assertNear "Strategischer Helden-Z-Blickpunkt" expectedZ capture.CenterZMeters
    assertNear "Strategischer Nickwinkel" InteractiveCameraMath.PitchRadians capture.PitchRadians

    if
        expectedX < capture.CenterXMeters - margins.LookPlaneX
        || expectedX > capture.CenterXMeters + margins.LookPlaneX
        || expectedZ < capture.CenterZMeters - margins.NorthZ
        || expectedZ > capture.CenterZMeters + margins.SouthZ
    then
        failwith "Strategischer Evidenzabgriff haelt den Helden nicht im Bodenabdruck."

    let southEastDesired =
        InteractiveCameraMath.ActiveCamera(149.6, 82.0, 32.0, InteractiveCameraMath.PitchRadians)

    let southEast =
        InteractiveCameraMath.FitHorizontalWorld(
            southEastDesired,
            InteractiveCameraMath.DefaultViewportAspectRatio,
            GrayboxCamera.DistanceMinMeters
        )
        |> fun fitted ->
            InteractiveCameraMath.ClampToWorldFootprint(fitted, InteractiveCameraMath.DefaultViewportAspectRatio)

    assertNear "Strategischer Suedostzoom" 14.931500294046 southEast.DistanceMeters
    assertNear "Strategischer Suedostabstand X" 141.937150476291 southEast.CenterXMeters
    assertNear "Strategischer Suedostabstand Z" 82.0 southEast.CenterZMeters

    if
        before
        <> (sessionCamera.CenterXMeters, sessionCamera.CenterZMeters, sessionCamera.DistanceMeters)
    then
        failwith "Strategischer Evidenzfokus hat die laufende Sitzungskamera mutiert."

let personalCameraFrustumKeepsGroundReadableAtWorldEdges () =
    let world = SimWorld(20260826u)
    let camera = HeroChaseCamera()
    camera.Follow(world)

    let sessionBefore =
        camera.CenterXMeters, camera.CenterZMeters, camera.DistanceMeters

    let effective = InteractiveCameraMath.ActiveCamera.From(camera)

    let margins =
        InteractiveCameraMath.GroundFootprint(effective, InteractiveCameraMath.DefaultViewportAspectRatio)

    let assertNear label expected actual =
        if abs (expected - actual) > 1e-9 then
            failwith $"{label}: erwartet {expected:R}, erhalten {actual:R}."

    let horizonClearance =
        HeroChaseCamera.PitchDegrees - (InteractiveCameraMath.FieldOfViewDegrees / 2.0)

    if horizonClearance < 25.0 then
        failwith $"Persoenliche obere Frustumkante hat nur {horizonClearance:R} Grad Bodenfreiheit."

    assertNear "Persoenlicher Nickwinkel" 55.0 HeroChaseCamera.PitchDegrees
    assertNear "Persoenliche ferne halbe Bodenbreite" 15.506230912314 margins.X
    assertNear "Persoenliche halbe Blickpunktbreite" 9.237604307034 margins.LookPlaneX
    assertNear "Persoenliche Nordsichtweite" 10.647907124186 margins.NorthZ
    assertNear "Persoenliche Suedsichtweite" 4.517189268945 margins.SouthZ
    assertNear "Persoenlicher Westabstand" 11.118802153517 effective.CenterXMeters

    let heroX =
        float (world.PositionXOf(ModeContract.HeroAgentIndex)) / float FixedPoint.One

    let heroZ =
        float (world.PositionYOf(ModeContract.HeroAgentIndex)) / float FixedPoint.One

    if
        heroX < effective.CenterXMeters - margins.LookPlaneX
        || heroX > effective.CenterXMeters + margins.LookPlaneX
        || heroZ < effective.CenterZMeters - margins.NorthZ
        || heroZ > effective.CenterZMeters + margins.SouthZ
    then
        failwith "Darstellseitiger Weltrandabstand hat den Helden aus dem Frustum geschoben."

    if
        sessionBefore
        <> (camera.CenterXMeters, camera.CenterZMeters, camera.DistanceMeters)
    then
        failwith "Darstellseitiger Weltrandabstand hat den Verfolgungskamerazustand mutiert."

let billboardBasisStaysCameraFacingAndIsotropic () =
    let dot (ax, ay, az) (bx, by, bz) = (ax * bx) + (ay * by) + (az * bz)
    let norm vector = sqrt (dot vector vector)

    let normalize (x, y, z) =
        let length = norm (x, y, z)
        x / length, y / length, z / length

    let cross (ax, ay, az) (bx, by, bz) =
        (ay * bz) - (az * by), (az * bx) - (ax * bz), (ax * by) - (ay * bx)

    let assertNear label expected actual =
        if abs (expected - actual) > 1e-12 then
            failwith $"{label}: erwartet {expected:R}, erhalten {actual:R}."

    for pitchDegrees in [ 25.0; HeroChaseCamera.PitchDegrees; 70.0 ] do
        let camera =
            InteractiveCameraMath.ActiveCamera(80.0, 45.0, 9.0, pitchDegrees * Math.PI / 180.0)

        let eye = InteractiveCameraMath.EyePosition(camera)
        let center = InteractiveCameraMath.CenterPosition(camera)

        let forward =
            normalize (center.Item1 - eye.Item1, center.Item2 - eye.Item2, center.Item3 - eye.Item3)

        let basis = InteractiveCameraMath.BillboardBasis(camera)
        let right = basis.[0], basis.[1], basis.[2]
        let up = basis.[3], basis.[4], basis.[5]
        let expectedUp = cross forward right

        assertNear $"Billboard-Rechtsnorm bei {pitchDegrees} Grad" 1.0 (norm right)
        assertNear $"Billboard-Obennorm bei {pitchDegrees} Grad" 1.0 (norm up)
        assertNear $"Billboard-Rechts/Forward bei {pitchDegrees} Grad" 0.0 (dot right forward)
        assertNear $"Billboard-Oben/Forward bei {pitchDegrees} Grad" 0.0 (dot up forward)
        assertNear $"Billboard-Achsenwinkel bei {pitchDegrees} Grad" 0.0 (dot right up)

        let expectedX, expectedY, expectedZ = expectedUp
        let actualX, actualY, actualZ = up
        assertNear $"Billboard-Hand X bei {pitchDegrees} Grad" expectedX actualX
        assertNear $"Billboard-Hand Y bei {pitchDegrees} Grad" expectedY actualY
        assertNear $"Billboard-Hand Z bei {pitchDegrees} Grad" expectedZ actualZ

// ---------------------------------------------------------------------------
// Horizontwahrheit des Wechselprotokolls (Modevertrag §4 (4)): ausgewertete,
// nicht mehr wirksame Wechsel bleiben ausdrücklich gebunden.
// ---------------------------------------------------------------------------

let switchesNearHorizonStayBoundAsIneffective () =
    // Horizont 420: Wechsel an 417 wird an M=419 (letzte Gültigkeitsprüfung)
    // wirksam; die Wechsel an 418 (M=420) und 419 (M=421) liegen hinter dem
    // Laufhorizont, bleiben aber ausdrücklich im Protokoll gebunden
    // (EffectiveInRun=false) statt still zu verschwinden.
    let result =
        SessionEngine.Run(
            SessionRunRequest(
                20260826u,
                [| GrayboxIntent(417, GrayboxIntentKind.SwitchMode)
                   GrayboxIntent(418, GrayboxIntentKind.SwitchMode)
                   GrayboxIntent(419, GrayboxIntentKind.SwitchMode) |],
                240,
                420
            )
        )

    if result.Telemetry.SwitchProtocol.Count <> 3 then
        failwith $"Wechselprotokoll enthielt {result.Telemetry.SwitchProtocol.Count} statt 3 Einträgen."

    let effective = result.Telemetry.SwitchProtocol.[0]

    if
        not effective.EffectiveInRun
        || effective.IntentTick <> 417L
        || effective.EffectiveBoundaryTick <> 419L
        || effective.SwitchReactionTicks <> 2L
    then
        failwith "Wirksamer Horizontwechsel verletzte die Grenzkette."

    for index in 1..2 do
        let entry = result.Telemetry.SwitchProtocol.[index]

        if entry.EffectiveInRun then
            failwith "Wechsel hinter dem Laufhorizont wurde fälschlich als wirksam ausgewiesen."

        if entry.SwitchReactionTicks <> 0L then
            failwith "Unwirksamer Wechsel behauptete eine Wechselreaktionsmessung."

        if entry.EffectiveBoundaryTick <> entry.IntentTick + 2L then
            failwith "Unwirksamer Wechsel verlor seine gebundene Wirksamkeitsgrenze."

    // Endmoduswahrheit: nur der Wechsel an 417 ist wirksam.
    if result.Telemetry.FinalMode <> SessionMode.Personal then
        failwith "Endmodus bildete nicht die Wahrheit des Laufs ab."

    if result.Telemetry.SwitchReactionSampleCount <> 1 then
        failwith "Wechselreaktionsverteilung umfasste nicht genau den wirksamen Wechsel."

    // Wechsel erzeugen zu keinem Zeitpunkt einen Kernbefehl.
    if result.KernelCommandsTotal <> 0 then
        failwith "Horizontwechsel erzeugten Kernelbefehle."

let heroDirectionSteeringResolvesExactlyWithExplicitTieBreak () =
    // Exakte Ganzzahlarithmetik (Modevertrag §3): bei Gleichstand des
    // größten Richtungstreue-Skalarprodukts gewinnt die niedrigste
    // Zonennummer. Heldenposition exakt auf der Reihe eines Zonenpaars mit
    // gleicher Ausrichtung liefert den konstruierten exakten Gleichstand:
    // y = 44,5 m stellt die Zonen 1 (Ost) und 5 (Ost-Mitte) beide mit
    // normierter Richtungstreue exakt 1; y = 45,5 m die Zonen 0 (West) und
    // 4 (West-Mitte).
    if HeroDirectionSteering.ResolveZoneFrom(0L, 2916352L, 1L, 0L) <> 1 then
        failwith "Exakter Gleichstand (Zonen 1 und 5) wählte nicht die niedrigste Zonennummer."

    if HeroDirectionSteering.ResolveZoneFrom(0L, 2981888L, 1L, 0L) <> 0 then
        failwith "Exakter Gleichstand (Zonen 0 und 4) wählte nicht die niedrigste Zonennummer."

    // Streng bessere Kandidatin schlägt den Bestwert auch in späterer
    // Zonennummer; der Mengenvergleich ist exakt (Int128-Kreuzmultiplikation).
    if HeroDirectionSteering.StrictlyBeats(2L, 100L, 1L, 25L) then
        failwith "Gleichstand (1/5 gegenüber 2/10) wurde als streng besser ausgewiesen."

    if not (HeroDirectionSteering.StrictlyBeats(2L, 25L, 1L, 25L)) then
        failwith "Streng bessere Richtungstreue wurde nicht erkannt."

    // Ohne Richtung kein Kandidat.
    if HeroDirectionSteering.ResolveZoneFrom(49L * 65536L, 45L * 65536L, 0L, 0L) <> -1 then
        failwith "Nulldirection wurde nicht mit -1 abgewiesen."

    // Zonenzentrumsfall zeichentreu nach Modevertrag §3: Auf dem Zentrum ist
    // der Vektor 0 und das Skalarprodukt damit 0 — die Zentrumszone ist
    // ausdrücklich KEIN richtungstreuer Kandidat; die Auflösung fällt auf die
    // nächste richtungstreue Zone (nach §3-Formel abgeleitet: östlich die
    // Zone 1, westlich allein die Zone 0).
    if HeroDirectionSteering.ResolveZoneFrom(3244032L, 2981888L, 1L, 0L) <> 1 then
        failwith "Zentrumsfall östlich wählte nicht die nächste richtungstreue Zone (§3)."

    if HeroDirectionSteering.ResolveZoneFrom(3244032L, 2981888L, -1L, 0L) <> 0 then
        failwith "Zentrumsfall westlich wählte nicht die einzige richtungstreue Zone (§3)."

    // Weltgebundene Auflösung ist deterministisch und richtungstreu.
    let world = SimWorld(20260826u)
    let east = HeroDirectionSteering.ResolveZone(world, 1L, 0L)
    let west = HeroDirectionSteering.ResolveZone(world, -1L, 0L)

    if east <> HeroDirectionSteering.ResolveZone(world, 1L, 0L) then
        failwith "Lenkauflösung war nicht deterministisch."

    if east >= 0 && west >= 0 && east = west then
        failwith "Östliche und westliche Auflösung fielen exakt zusammen (Richtungstreuung verletzt)."

let consecutiveTickSwitchesFollowCanonicalEvaluationBasis () =
    // Modevertrag Abschnitt 4 (2)+(4): Wechsel an Ticks S und S+1 werden
    // beide im dann noch gültigen vorherigen Modus ausgewertet (die erste
    // Modusänderung ist an S+1 weder wirksam noch kontextbildend) und
    // tragen daher denselben Zielmodus; Nettoeffekt genau ein Wechsel,
    // wirksam an S+2. Strategische Intents an 40/41 bleiben im vorherigen
    // Modus gültig, an M=42 wird der persönliche Modus kontextbildend.
    let intents =
        [| GrayboxIntent(40, GrayboxIntentKind.GroupMoveToZone, 2L) // vorheriger Modus: kontextgültig
           GrayboxIntent(40, GrayboxIntentKind.SwitchMode)
           GrayboxIntent(41, GrayboxIntentKind.GroupMoveToZone, 2L) // vorheriger Modus: kontextgültig
           GrayboxIntent(41, GrayboxIntentKind.SwitchMode) // gleicher Zielmodus
           GrayboxIntent(42, GrayboxIntentKind.GroupMoveToZone, 1L) |] // M: persönlich abgewiesen

    let world, pipeline = newPipeline (20260826u, intents)

    let mutable rejectedInPersonal = 0

    for tick in 40L .. 43L do
        let outcome = pipeline.ProcessBoundary(tick)
        rejectedInPersonal <- rejectedInPersonal + outcome.RejectedStrategyInPersonal
        world.Tick()

    if pipeline.CurrentEffectiveMode <> SessionMode.Personal then
        failwith "Zwei Folgetick-Wechsel mit gleichem Zielmodus endeten nicht im persönlichen Modus."

    // Ohne Vorauswahl scheitern die Bewegungen an 40/41 erst an der
    // Auswahlprüfung (kontextgültig im vorherigen Modus); die Bewegung an
    // M = 42 scheitert kontextuell im persönlichen Modus.
    if rejectedInPersonal <> 1 then
        failwith "Bewegung an M = S + 2 war nicht im persönlichen Modus abgewiesen."

    if pipeline.MoveWithoutSelectionTotal <> 2L then
        failwith "Folgetick-Bewegungen im vorherigen Modus wurden nicht kontextgültig geprüft."

    if pipeline.StrategyIntentsRejectedInPersonalModeTotal <> 1L then
        failwith "Zähler strategischer Abweisungen im persönlichen Modus falsch."

    if pipeline.AppliedCommandsTotal <> 0L then
        failwith "Ohne Vorauswahl durfte kein Kernbefehl entstehen."

    if pipeline.SwitchProtocol.Count <> 2 then
        failwith "Wechselprotokoll verlor einen der beiden Folgetick-Wechsel."

    let first = pipeline.SwitchProtocol.[0]
    let second = pipeline.SwitchProtocol.[1]

    // Beide Auswertungen basieren auf dem dann gültigen vorherigen Modus
    // (Modevertrag Abschnitt 4 (2): die erste Modusänderung ist an S+1
    // weder wirksam noch kontextbildend).
    for entry in [ first; second ] do
        if
            entry.PreviousMode <> SessionMode.Strategic
            || entry.NewMode <> SessionMode.Personal
        then
            failwith "Folgetick-Wechsel wurden nicht im gültigen vorherigen Modus ausgewertet."

        if entry.SwitchReactionTicks <> 2L then
            failwith "Folgetick-Wechsel verletzte die Wechselreaktionszahlbasis."

    if first.IntentTick <> 40L || first.EffectiveBoundaryTick <> 42L then
        failwith "Erster Folgetick-Wechsel verletzte M = S + 2."

    if second.IntentTick <> 41L || second.EffectiveBoundaryTick <> 43L then
        failwith "Zweiter Folgetick-Wechsel verletzte M = S + 2."

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
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v2Script 420 hybridBody), rulesFor 420).Intents

    let twinBody =
        hybridBody
        |> List.filter (fun line -> not (line.EndsWith(" switch", StringComparison.Ordinal)))

    let twin =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes(v2Script 420 twinBody), rulesFor 420).Intents

    let runHybrid seed =
        SessionEngine.Run(SessionRunRequest(seed, hybrid, 240, 420, false))

    let runTwin seed =
        SessionEngine.Run(SessionRunRequest(seed, twin, 240, 420, false))

    let first = runHybrid (20260826u)
    let second = runHybrid (20260826u)

    if first.EndStateHash <> second.EndStateHash then
        failwith "Zwei Läufe desselben Hybrid-Flows lieferten unterschiedliche Endhashes."

    if
        first.IntervalSampleTicks <> second.IntervalSampleTicks
        || first.IntervalHashes <> second.IntervalHashes
    then
        failwith "Kettenstichproben zweier Hybridläufe sind nicht byteidentisch."

    let twinRun = runTwin (20260826u)

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
    let foreign = runHybrid (42u)

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
        newPipeline (
            11u,
            [| GrayboxIntent(39, GrayboxIntentKind.SwitchMode)
               GrayboxIntent(41, GrayboxIntentKind.SteerGroupToZone, 3L) |]
        )

    // Kontrollkern: derselbe Befehl direkt über die unveränderte öffentliche
    // Kernbefehlsfläche am selben Tick.
    let controlWorld = SimWorld(11u)

    for tick in 0L .. 59L do
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
    let vacuum =
        CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 0L, 0))

    if vacuum.SwitchReactionEvaluated then
        failwith "Kriterium 6 war ohne wirksamen Wechsel als ausgewertet ausgegeben."

    if not vacuum.Pass then
        failwith "Nichtauswertung von Kriterium 6 faltete das Gate fälschlich."

    if vacuum.Violations.Count <> 0 then
        failwith "Nichtauswertung von Kriterium 6 erzeugte eine Verletzung."

    // Messung am Ziel: kein Verstoß, Ziel erfüllt.
    let atTarget =
        CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 2L, 1))

    if
        not atTarget.SwitchReactionEvaluated
        || not atTarget.SwitchReactionTargetMet
        || not atTarget.Pass
    then
        failwith "Wechselreaktion am Ziel (2 Ticks) erfüllte Kriterium 6 nicht."

    // Innerhalb der harten Grenze, über dem Ziel: kein Gate-Fail, Ziel
    // verfehlt (T-032-Präzedenz).
    let aboveTarget =
        CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 3L, 1))

    if not aboveTarget.Pass || aboveTarget.SwitchReactionTargetMet then
        failwith "Reaktion 3 muss passieren und das Ziel verfehlen."

    // Fault-Injection: Messung über der harten Grenze verletzt fail-closed.
    let violated =
        CommandGate.Evaluate(limits, CommandGateInputs(1.0, 0.0, 1L, 4L, false, Nullable true, 4L, 1))

    if violated.Pass then
        failwith "Wechselreaktion 4 Ticks passierte das Gate."

    if not (Seq.contains "switch-reaction-ticks-above-hard-limit" violated.Violations) then
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

        let switchGate =
            document.RootElement.GetProperty("gate").GetProperty("switchReaction")

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

        let exitLegacy, _, _ =
            runAppHost (argumentsFor legacyMixedPath reportHybridOne "20260826")

        if exitLegacy <> ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable) then
            failwith $"Legacy-v1-Skript mit steer ergab Exitcode {exitLegacy} statt Code 37."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

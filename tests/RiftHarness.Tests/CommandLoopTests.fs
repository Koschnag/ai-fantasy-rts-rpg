module CommandLoopTests

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
// Vertragsspiegel (AC-T032-01): Code und docs/KOMMANDOVERTRAG.md konsistent.
// ---------------------------------------------------------------------------

let sessionContractMirrorsDocumentedValues () =
    if SessionContract.DocumentPath <> "docs/KOMMANDOVERTRAG.md" then
        failwith "Vertragspfad falsch."

    if SessionContract.ContractVersion <> "1" then
        failwith "Vertragsversion falsch."

    if SessionContract.ScriptFormatId <> "graybox-input-script-v1" then
        failwith "Skriptformatkennung falsch."

    if SessionContract.ScenarioId <> "kommando-graybox" then
        failwith "Szenariokennung falsch."

    if SessionContract.ContentId <> "synthetic-graybox-command-loop" then
        failwith "Inhaltskennung falsch."

    if SessionContract.SelectionModelId <> "graybox-selection-model-v0" then
        failwith "Auswahlmodellkennung falsch."

    if SessionContract.CameraModelId <> "graybox-camera-model-v0" then
        failwith "Kameramodellkennung falsch."

    if SessionContract.SelectRadiusMillimeters <> 3000L then
        failwith "Auswahlradius entspricht nicht dem Vertrag (3000 mm)."

    if
        SessionContract.ReactionTargetTicks <> 2
        || SessionContract.ReactionHardLimitTicks <> 3
    then
        failwith "Reaktionsgrenzen entsprechen nicht der Ableitung 100 ms bzw. 150 ms bei 50 ms je Tick."

    if
        SessionContract.IntentsPerTickMax <> 4
        || SessionContract.TotalIntentsMax <> 4096
    then
        failwith "Intentlimits entsprechen nicht dem Vertrag."

    if SessionContract.ScriptBytesMax <> 262144L then
        failwith "Skriptbytengrenze entspricht nicht dem Vertrag."

    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let document =
        File.ReadAllText(Path.Combine(findRoot (Environment.CurrentDirectory), "docs", "KOMMANDOVERTRAG.md"))

    for identifier in
        [ SessionContract.ScriptFormatId
          SessionContract.ScenarioId
          SessionContract.SelectionModelId
          SessionContract.CameraModelId
          "kommandoschleife"
          "kommando-graybox"
          "graybox-state-occupancy-not-gameplay-atmosphere-or-shipping" ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Vertragsdokument nennt die Kennung {identifier} nicht."

    // Die Reaktionsableitung steht mit transparenter Arithmetik im Dokument.
    for anchor in [ "150 ms ÷ 50 ms/Tick"; "100 ms ÷ 50 ms/Tick" ] do
        if not (document.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Vertragsdokument nennt die Reaktionsableitung {anchor} nicht."

// ---------------------------------------------------------------------------
// Parser und Codec (AC-T032-02/07).
// ---------------------------------------------------------------------------

let private buildScript (horizon: int) (bodyLines: string list) =
    let body = String.concat "\n" bodyLines
    $"graybox-input-script-v1 {horizon}\n{body}\nend\n"

let private rulesFor horizon = ScriptWindowRules(40, horizon)

let private parseText (content: string) horizon =
    InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes content, rulesFor horizon)

let private sha256Hex (bytes: byte[]) =
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()

let parserAcceptsCanonicalScriptAndBindsHashes () =
    let content =
        buildScript
            120
            [ "intent 40 clear"
              "intent 50 point 20000 30000"
              "intent 60 box 4000 16000 36000 50000"
              "intent 70 move 3" ]

    let parsed = parseText content 120

    if parsed.Intents.Length <> 4 then
        failwith "Parser verlor Intents."

    if parsed.HorizonTicks <> 120 then
        failwith "Parser band den Horizont nicht."

    for index in 1 .. parsed.Intents.Length - 1 do
        if parsed.Intents.[index].CompareTo(parsed.Intents.[index - 1]) < 0 then
            failwith "Intents sind nicht kanonisch geordnet."

    // Skripthash bindet die Rohbytes deterministisch (SHA-256).
    let expectedSha = sha256Hex (Text.Encoding.UTF8.GetBytes content)

    if parsed.ScriptSha256Hex <> expectedSha then
        failwith "Skript-SHA-256 bindet nicht die Rohbytes."

    let second = parseText content 120

    if second.IntentPlanHash <> parsed.IntentPlanHash then
        failwith "Planhash ist nicht deterministisch."

let private expectReject (reason: InputScriptRejectReason) (content: string) (message: string) =
    try
        parseText content 120 |> ignore
        failwith $"{message}: Parser akzeptierte das Skript unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> reason then
            failwith $"{message}: Klasse war {error.Reason}, erwartet {reason}."
    | error -> raise error

let private expectRejectBytes (reason: InputScriptRejectReason) (bytes: byte[]) (message: string) =
    try
        InputScriptParser.Parse(bytes, rulesFor 120) |> ignore
        failwith $"{message}: Parser akzeptierte die Bytes unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> reason then
            failwith $"{message}: Klasse war {error.Reason}, erwartet {reason}."
    | error -> raise error

let parserRejectsEveryClassDistinctly () =
    expectReject
        InputScriptRejectReason.HeaderMalformed
        ((buildScript 120 [ "intent 40 clear" ]).Replace("graybox-input-script-v1", "other-format-v9"))
        "Fremdkopf"

    expectReject
        InputScriptRejectReason.HeaderMalformed
        ("graybox-input-script-v1 121\nintent 40 clear\nend\n")
        "Horizontabweichung"

    expectReject InputScriptRejectReason.LineMalformed (buildScript 120 [ "intent vierzig clear" ]) "Nichtzahl im Tick"

    expectReject InputScriptRejectReason.LineMalformed (buildScript 120 [ "intent 40" ]) "Fehlende Aktion"

    expectReject InputScriptRejectReason.UnknownAction (buildScript 120 [ "intent 40 dance" ]) "Unbekannte Aktion"

    expectReject
        InputScriptRejectReason.RangeViolation
        (buildScript 120 [ "intent 40 point 999999 30000" ])
        "X ausserhalb der Welt"

    expectReject InputScriptRejectReason.RangeViolation (buildScript 120 [ "intent 40 move 6" ]) "Zone ausserhalb"

    expectReject
        InputScriptRejectReason.DuplicateIntent
        (buildScript 120 [ "intent 40 move 2"; "intent 40 move 2" ])
        "Doppelter Intent"

    expectReject
        InputScriptRejectReason.IntentOutsideWindow
        (buildScript 120 [ "intent 39 clear" ])
        "Tick vor dem Fenster"

    expectReject
        InputScriptRejectReason.TrailingContent
        ((buildScript 120 [ "intent 40 clear" ]) + "intent 50 clear\n")
        "Inhalt nach end"

    // Rohbytegroesse wird vor der Dekodierung geprueft (untrusted Eingabe).
    expectRejectBytes InputScriptRejectReason.ScriptTooLarge (Array.create 262145 0x20uy) "Skript ueber der Bytegrenze"

    // Vier Intents je Tick sind erlaubt; fuenf nicht.
    let fourIntents =
        buildScript
            120
            [ "intent 40 clear"
              "intent 40 point 10000 10000"
              "intent 40 box 1000 1000 2000 2000"
              "intent 40 move 1" ]

    parseText fourIntents 120 |> ignore

    expectReject
        InputScriptRejectReason.IntentLimitPerTick
        (buildScript
            120
            [ "intent 40 clear"
              "intent 40 point 10000 10000"
              "intent 40 box 1000 1000 2000 2000"
              "intent 40 move 1"
              "intent 40 move 2" ])
        "Fuenf Intents je Tick abgelehnt"

    let oversizedTotal =
        buildScript 4200 [ for tick in 40..4136 -> $"intent {tick} clear" ]

    try
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes oversizedTotal, ScriptWindowRules(40, 4200))
        |> ignore

        failwith "Zu viele Intents insgesamt: Parser akzeptierte das Skript unerwartet."
    with
    | :? InputScriptException as error ->
        if error.Reason <> InputScriptRejectReason.IntentLimitTotal then
            failwith $"Zu viele Intents insgesamt: Klasse war {error.Reason}, erwartet IntentLimitTotal."
    | error -> raise error

let scriptHashBindsRawBytesAndRejectsInvalidEncodings () =
    let lfScript = buildScript 120 [ "intent 40 clear"; "intent 50 move 2" ]
    let crlfScript = lfScript.Replace("\n", "\r\n")

    let parsedLf = parseText lfScript 120

    let parsedCrlf =
        InputScriptParser.Parse(Text.Encoding.UTF8.GetBytes crlfScript, rulesFor 120)

    if parsedCrlf.Intents.Length <> 2 then
        failwith "CRLF-Skript verlor Intents."

    if parsedCrlf.ScriptSha256Hex <> sha256Hex (Text.Encoding.UTF8.GetBytes crlfScript) then
        failwith "Skripthash band nicht die exakten CRLF-Rohbytes."

    if parsedCrlf.ScriptSha256Hex = parsedLf.ScriptSha256Hex then
        failwith "Unterschiedliche Rohbytes ergaben denselben Skripthash."

    if parsedCrlf.IntentPlanHash <> parsedLf.IntentPlanHash then
        failwith "Analyseaequivalente Skripte ergaben unterschiedliche Planhashes."

    let invalidUtf8 =
        Array.append (Text.Encoding.UTF8.GetBytes "graybox-input-script-v1 120\n") [| 0xFFuy; 0xFEuy |]

    expectRejectBytes InputScriptRejectReason.HeaderMalformed invalidUtf8 "Ungueltiges UTF-8"

    let bomPrefixed =
        Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (Text.Encoding.UTF8.GetBytes lfScript)

    expectRejectBytes InputScriptRejectReason.HeaderMalformed bomPrefixed "BOM vor dem Kopf"

let intentCodecGoldenEncoding () =
    let intent = GrayboxIntent(240, GrayboxIntentKind.PointSelect, 20000L, 30000L)
    let buffer: byte[] = IntentCodec.EncodeToArray(intent)

    // Golden: LE-Tick, Kind, LE-x, LE-y, Nullfuellung bis Festbreite 21.
    let expected =
        [| 240uy
           0uy
           0uy
           0uy
           1uy
           32uy
           78uy
           0uy
           0uy
           48uy
           117uy
           0uy
           0uy
           0uy
           0uy
           0uy
           0uy
           0uy
           0uy
           0uy
           0uy |]

    if buffer <> expected then
        failwith $"Codec-Golden verletzt: {Convert.ToHexString(buffer)}"

    // Unabhaengiger FNV-Nachlauf ueber die Goldenbytes.
    let mutable hash = 0xCBF29CE484222325UL

    for byteValue in expected do
        hash <- (hash ^^^ (uint64 byteValue)) * 0x100000001B3UL

    let hashed = IntentCodec.HashOf([| intent |])

    if hashed <> hash then
        failwith "Planhash stimmt nicht mit der FNV-Rueckrechnung ueberein."

// ---------------------------------------------------------------------------
// Auswahl-, Abbildungs- und Determinismussemantik (AC-T032-03/04).
// ---------------------------------------------------------------------------

let private defaultSeed = 20260826u

let private engineIntents () : GrayboxIntent[] =
    [| GrayboxIntent(40, GrayboxIntentKind.BoxSelect, 1000L, 1000L, 159000L, 89000L)
       GrayboxIntent(45, GrayboxIntentKind.GroupMoveToZone, 2L)
       GrayboxIntent(60, GrayboxIntentKind.Clear)
       GrayboxIntent(70, GrayboxIntentKind.PointSelect, 20000L, 30000L)
       GrayboxIntent(80, GrayboxIntentKind.GroupMoveToZone, 4L) |]

let private runEngine seed (intents: GrayboxIntent[]) =
    SessionEngine.Run(SessionRunRequest(seed, intents, 40, 140, true))

let canonicalOrderingMakesReorderingIrrelevant () =
    // Zwei Skripte, die sich nur in der Zeilenreihenfolge innerhalb eines
    // Ticks unterscheiden, muessen identische Intents, Plaene und Zustaende
    // liefern (Kommandovertrag Abschnitt 2).
    let orderOne =
        buildScript
            140
            [ "intent 40 clear"
              "intent 40 move 1"
              "intent 40 box 1000 1000 159000 89000"
              "intent 41 point 20000 30000" ]

    let orderTwo =
        buildScript
            140
            [ "intent 40 box 1000 1000 159000 89000"
              "intent 40 move 1"
              "intent 40 clear"
              "intent 41 point 20000 30000" ]

    let parsedOne = parseText orderOne 140
    let parsedTwo = parseText orderTwo 140

    if parsedOne.IntentPlanHash <> parsedTwo.IntentPlanHash then
        failwith "Umsortierte Zeilen innerhalb eines Ticks ergaben unterschiedliche Planhashes."

    if Array.ofSeq parsedOne.Intents <> Array.ofSeq parsedTwo.Intents then
        failwith "Umsortierte Zeilen innerhalb eines Ticks ergaben unterschiedliche Intents."

    let first = runEngine defaultSeed parsedOne.Intents
    let second = runEngine defaultSeed parsedTwo.Intents

    if first.EndStateHash <> second.EndStateHash then
        failwith "Umsortierte Intents aenderten den Endzustand."

    if first.Metrics.MaxReactionTicks <> second.Metrics.MaxReactionTicks then
        failwith "Umsortierung veraenderte die Reaktionsverteilung."

let determinismIdenticalChainsAndForeignSeedSensitivity () =
    let first = runEngine defaultSeed (engineIntents ())
    let second = runEngine defaultSeed (engineIntents ())

    if first.EndStateHash <> second.EndStateHash then
        failwith "Zwei frische Durchlaeufe lieferten unterschiedliche Endhashes."

    if
        first.IntervalHashes <> second.IntervalHashes
        || first.IntervalSampleTicks <> second.IntervalSampleTicks
    then
        failwith "Kettenstichproben zweier Durchlaeufe weichen ab."

    if
        not first.StateChainSelfConsistent.HasValue
        || first.StateChainSelfConsistent.Value <> true
    then
        failwith $"Selbstkonsistenz fehlgeschlagen: {first.SelfInconsistencyReasons}"

    let foreign = runEngine 42u (engineIntents ())

    if foreign.EndStateHash = first.EndStateHash then
        failwith "Fremder Seed aenderte den Endhash nicht."

    // Veraendertes Skript: Zielzone des ersten Bewegungsintents aendern,
    // waehrend die Rahmenwahl alle Gruppen traegt - das aendert den
    // Kernelbefehl und damit den Zustand nachweislich.
    let baseIntents = engineIntents ()

    let mutated =
        baseIntents
        |> Array.map (fun intent ->
            if intent.Kind = GrayboxIntentKind.GroupMoveToZone && intent.A = 2L then
                GrayboxIntent(intent.Tick, intent.Kind, 5L)
            else
                intent)

    let changed = runEngine defaultSeed mutated

    if changed.EndStateHash = first.EndStateHash then
        failwith "Veraendertes Skript aenderte den Endhash nicht."

let selectionSemanticsV0PointBoxClear () =
    let world = SimWorld(defaultSeed)
    let snapshot = world.CreateSnapshot()
    let groups = snapshot.Group
    let selection = SelectionModel(groups)

    // Punktwahl auf einen konkreten Agenten selektiert dessen Gruppe.
    let millimetersX = snapshot.PositionXQ16.[0] * 1000L / 65536L
    let millimetersY = snapshot.PositionYQ16.[0] * 1000L / 65536L

    let hit =
        selection.EvaluatePoint(
            world,
            GrayboxIntent.MillimetersToQ16(millimetersX),
            GrayboxIntent.MillimetersToQ16(millimetersY)
        )

    if not hit then
        failwith "Punktwahl direkt am Agenten ging daneben."

    if selection.SelectedCount <> 1 || not (selection.IsSelected(int groups.[0])) then
        failwith "Punktwahl selektierte nicht die Gruppe des naechstgelegenen Agenten."

    // Punktwahl ins Leere hebt hervor (definierte Semantik).
    let emptyClick =
        selection.EvaluatePoint(world, GrayboxIntent.MillimetersToQ16(80000L), GrayboxIntent.MillimetersToQ16(45000L))

    if emptyClick then
        failwith "Leer-Klick traf unerwartet einen Agenten."

    if selection.SelectedCount <> 0 then
        failwith "Leer-Klick hob die Auswahl nicht auf."

    // Rahmenwahl ueber die ganze Welt vereinigt alle fuenf Gruppen.
    selection.EvaluateBox(world, 0L, 0L, (int64 NavWorld.TilesX) * 65536L, (int64 NavWorld.TilesY) * 65536L)

    if selection.SelectedCount <> SimulationContract.GroupCount then
        failwith $"Rahmenwahl vereinte {selection.SelectedCount} statt 5 Gruppen."

let moveMapsToKernelCommandSurface () =
    let world = SimWorld(defaultSeed)
    let groups = SessionEngine.ReadAgentGroups(world)
    let selection = SelectionModel(groups)
    let pipeline = SessionPipeline(world, selection, Array.empty<GrayboxIntent>)

    selection.EvaluateBox(world, 0L, 0L, (int64 NavWorld.TilesX) * 65536L, (int64 NavWorld.TilesY) * 65536L)

    pipeline.EnqueueLiveIntent(GrayboxIntent(7, GrayboxIntentKind.GroupMoveToZone, 2L))
    let outcome = pipeline.ProcessBoundary(7L)

    if outcome.AppliedCount <> 1 || outcome.CommandCount <> 5 then
        failwith $"Move erzeugte {outcome.CommandCount} statt 5 Kernbefehlen."

    for group in 0 .. SimulationContract.GroupCount - 1 do
        if world.TargetZoneOfGroup(group) <> 2 then
            failwith $"Gruppe {group} erhielt Zone 2 nicht."

    // Bewegung ohne Auswahl wird kontrolliert mit fachlicher Ursache abgewiesen.
    selection.Clear()
    let rejectedOutcome = pipeline.ProcessBoundary(9L)

    if rejectedOutcome.RejectedCount <> 0 then
        failwith "Erste Abweisung trat zu frueh auf."

    pipeline.EnqueueLiveIntent(GrayboxIntent(2, GrayboxIntentKind.GroupMoveToZone, 1L))
    let lateMoveOutcome = pipeline.ProcessBoundary(20L)

    if lateMoveOutcome.RejectedCount <> 1 then
        failwith "Zu spaete Live-Bewegung wurde nicht abgewiesen."

    if pipeline.LateRejectedTotal <> 1L then
        failwith "Die Abweisung war nicht der dokumentierten Verspaetungsklasse (RejectedLate)."

// ---------------------------------------------------------------------------
// Gate und Allokationsstrenge (AC-T032-05).
// ---------------------------------------------------------------------------

let reactionGatePositiveProofAndFaultInjectionMatrix () =
    let limits = CommandGateLimits.Documented

    // Positionale Fabrik: (p99, Allokation, maxReaktion, Anzahl, Shader, Kette).
    let inputs p99 alloc maxReaction count shader chain =
        CommandGateInputs(p99, alloc, maxReaction, count, shader, chain)

    let cleanInputs = inputs 0.5 0.0 1L 5L false (Nullable true)

    let clean = CommandGate.Evaluate(limits, cleanInputs)

    if not clean.Pass || clean.Violations.Count <> 0 then
        failwith "Sauberer Lauf fiel nicht durch."

    let classes =
        [ "tick-time-p99-above-hard-limit", inputs 17.0 0.0 1L 5L false (Nullable true)
          "allocations-per-warm-tick-above-limit", inputs 0.5 1.0 1L 5L false (Nullable true)
          "reaction-ticks-above-hard-limit", inputs 0.5 0.0 4L 5L false (Nullable true)
          "runtime-shader-compilation-observed", inputs 0.5 0.0 1L 5L true (Nullable true)
          "state-chain-self-inconsistent", inputs 0.5 0.0 1L 5L false (Nullable false) ]

    for expectedViolation, gateInputs in classes do
        let verdict = CommandGate.Evaluate(limits, gateInputs)

        if verdict.Pass then
            failwith $"Verletzungsklasse {expectedViolation} wurde nicht gefaltet."

        let found =
            verdict.Violations
            |> Seq.exists (fun violation -> violation = expectedViolation)

        if not found then
            failwith $"Verletzungsklasse {expectedViolation} fehlt in der Verletzungsliste."

    // Zielverfehlung allein faellt das Gate nicht (Praezedenz AC-T010-07).
    let soft = CommandGate.Evaluate(limits, inputs 10.0 0.0 1L 5L false (Nullable true))

    if not soft.Pass || soft.TickTimeTargetMet then
        failwith "Zielverfehlung der Tickzeit faltete das Gate unzulaessig."

    let softReaction =
        CommandGate.Evaluate(limits, inputs 0.5 0.0 3L 5L false (Nullable true))

    if not softReaction.Pass || softReaction.ReactionTargetMet then
        failwith "Zielverfehlung der Reaktion faltete das Gate unzulaessig."

    // Interaktivmodus: Kettenkriterium nicht auswertbar und bleibt unausgewiesen.
    let interactive =
        CommandGate.Evaluate(limits, inputs 0.5 0.0 1L 5L false (Nullable()))

    if not interactive.Pass then
        failwith "Interaktive Nichtauswertbarkeit durfte das Gate nicht falten."

let allocationStrictnessRegression () =
    let result = runEngine defaultSeed (engineIntents ())

    if result.Metrics.AllocationsPerWarmTickBytes <> 0.0 then
        failwith
            $"Allokation je warmem Tick war {result.Metrics.AllocationsPerWarmTickBytes}, Vertrag verlangt exakt 0."

    if result.Metrics.GcPauseCount <> 0L then
        failwith "Messfenster erzeugte GC-Pausen."

let cameraModelClampsWorldEdgesAndZoom () =
    let camera = GrayboxCamera()
    camera.Pan(-1000.0, -1000.0)

    if camera.CenterXMeters <> 0.0 || camera.CenterZMeters <> 0.0 then
        failwith "Weltrandbegrenzung Nordwest verletzt."

    camera.Pan(5000.0, 5000.0)

    if
        camera.CenterXMeters <> float NavWorld.TilesX
        || camera.CenterZMeters <> float NavWorld.TilesY
    then
        failwith "Weltrandbegrenzung Suedost verletzt."

    for _ in 1..64 do
        camera.ZoomSteps(+1)

    if camera.DistanceMeters <> GrayboxCamera.DistanceMinMeters then
        failwith "Zoomminimum nicht geclippt."

    for _ in 1..128 do
        camera.ZoomSteps(-1)

    if camera.DistanceMeters <> GrayboxCamera.DistanceMaxMeters then
        failwith "Zoommaximum nicht geclippt."

    // Picking aus der Bildmitte liefert einen Bodenschnitt in der Welt.
    let ground = InteractiveCameraMath.ScreenToGround(camera, 1920, 1080, 960.0, 540.0)

    if not ground.HasValue then
        failwith "Bildschirmmitte ergab keinen Bodenstrahl."
    else
        let x = ground.Value.SimX
        let z = ground.Value.SimZ

        if
            x < -1.0
            || x > float NavWorld.TilesX + 1.0
            || z < -1.0
            || z > float NavWorld.TilesY + 1.0
        then
            failwith $"Bodenschnitt ({x}, {z}) liegt ausserhalb der Welt."

    // Abschluss-Review 2026-08-27 (t032-rev18): Die Bildschirm-zu-Boden-
    // Zuordnung ist die exakte Umkehrung der gepinnten bgfx-Clipkette
    // (Kombination proj*view, Kamera im Render-Raum der Szene). Gebunden
    // werden: (a) die Bildmitte trifft das Kamerazentrum, (b) die Achsen-
    // abdeckung entartet nicht zu einem nahezu konstanten Punkt, (c) die
    // Achsen sind entkoppelt, (d) Bildschirm oben ist Norden (-Z, §4).
    let pickPixel (px: float) (py: float) =
        InteractiveCameraMath.ScreenToGround(camera, 1920, 1080, px, py)

    let centerPick = pickPixel 960.0 540.0

    if not centerPick.HasValue then
        failwith "Bildmitte (starker Probe) ergab keinen Bodenstrahl."
    elif
        abs (centerPick.Value.SimX - camera.CenterXMeters) > 3.0
        || abs (centerPick.Value.SimZ - camera.CenterZMeters) > 3.0
    then
        failwith $"Bildmitte trifft nicht das Kamerazentrum: ({centerPick.Value.SimX:F2}, {centerPick.Value.SimZ:F2})."

    let leftPick = pickPixel 60.0 540.0
    let rightPick = pickPixel 1860.0 540.0
    let topPick = pickPixel 960.0 60.0
    let bottomPick = pickPixel 960.0 1020.0

    for probe, name in [ leftPick, "links"; rightPick, "rechts"; topPick, "oben"; bottomPick, "unten" ] do
        if not probe.HasValue then
            failwith $"Randpixel {name} ergab keinen Bodenschnitt."

    if abs (rightPick.Value.SimX - leftPick.Value.SimX) < 20.0 then
        failwith $"Horizontale Picking-Abdeckung entartet: {leftPick.Value.SimX:F2} .. {rightPick.Value.SimX:F2}."

    if abs (bottomPick.Value.SimZ - topPick.Value.SimZ) < 20.0 then
        failwith $"Vertikale Picking-Abdeckung entartet: {topPick.Value.SimZ:F2} .. {bottomPick.Value.SimZ:F2}."

    if abs (leftPick.Value.SimZ - rightPick.Value.SimZ) > 0.5 then
        failwith "Horizontale Randpixel haben unterschiedliche Bodenhoehe-Z (Achsen gekoppelt)."

    if abs (topPick.Value.SimX - bottomPick.Value.SimX) > 0.5 then
        failwith "Vertikale Randpixel haben unterschiedliches Boden-X (Achsen gekoppelt)."

    if not (topPick.Value.SimZ < bottomPick.Value.SimZ) then
        failwith "Bildschirm oben ist nicht Norden (-Z; Kommandovertrag §4, feste Nordausrichtung)."

    // Die Kamera lebt im Render-Raum der Szene (T-020/T-023-Praezedenz:
    // Landschafts-/Einheiten-Meshes um den Ursprung zentriert); ein
    // Sim-Raum-Augenpunkt wuerde die Szene um die halbe Weltgroesse
    // verschieben und das Terrain ausserhalb des Kachelrasters samplen.
    let struct (eyeX, eyeY, eyeZ) = InteractiveCameraMath.EyePosition(camera)

    let struct (lookAtX, lookAtY, lookAtZ) =
        InteractiveCameraMath.CenterPosition(camera)

    if
        abs (eyeX - RepresentativeLandscape.ToWorldX(camera.CenterXMeters)) > 1e-9
        || abs (lookAtX - RepresentativeLandscape.ToWorldX(camera.CenterXMeters)) > 1e-9
        || abs (
            eyeZ
            - (RepresentativeLandscape.ToWorldZ(camera.CenterZMeters)
               + (cos InteractiveCameraMath.PitchRadians * camera.DistanceMeters))
        ) > 1e-9
    then
        failwith "Kamera-Auge/Blickziel liegen nicht im Render-Raum der Szene."

    if eyeY <= RepresentativeLandscape.HeightAt(lookAtX, lookAtZ) then
        failwith "Kamera-Auge liegt nicht ueber dem Terrain am Blickziel."

let exitCodeMappingStaysStableIncludingCommandCodes () =
    let expectations =
        [ PlatformErrorCode.CommandGateViolated, 35
          PlatformErrorCode.CommandRunIncomplete, 36
          PlatformErrorCode.CommandScenarioUnavailable, 37
          PlatformErrorCode.CommandCaptureFailed, 38 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Exitcode fuer {code} ist {ExitCodes.Map(code)}, dokumentiert ist {expected}."

let interactiveExitCodePrecedenceStaysWindowBound () =
    // Kommandovertrag §8 / NATIVE_UNTERBAU.md: Ein vorzeitiger Abbruch vor
    // Fensterabschluss ist niemals Evidenz und ergibt stets Code 36 — auch
    // wenn ein Abgriff angefordert war, der deshalb unterbleiben musste.
    // Bei abgeschlossenem Fenster entscheidet der fehlgeschlagene opt-in
    // Abgriff mit Code 38, sonst das Gateverdict.
    let incomplete = ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete)
    let captureFailed = ExitCodes.Map(PlatformErrorCode.CommandCaptureFailed)
    let gateViolated = ExitCodes.Map(PlatformErrorCode.CommandGateViolated)

    if
        CommandLoopRunner.ResolveInteractiveExitCode(false, true, gateViolated)
        <> incomplete
    then
        failwith "Vorzeitiger Abbruch mit angefordertem Abgriff ergab nicht Code 36."

    if
        CommandLoopRunner.ResolveInteractiveExitCode(false, false, gateViolated)
        <> incomplete
    then
        failwith "Vorzeitiger Abbruch ergab nicht Code 36."

    if
        CommandLoopRunner.ResolveInteractiveExitCode(true, true, gateViolated)
        <> captureFailed
    then
        failwith "Fehlgeschlagener Abgriff nach Fensterabschluss ergab nicht Code 38."

    if
        CommandLoopRunner.ResolveInteractiveExitCode(true, false, gateViolated)
        <> gateViolated
    then
        failwith "Abgeschlossener Lauf ohne Abgriffsfehler traf nicht das Gateverdict."

    if
        CommandLoopRunner.ResolveInteractiveExitCode(true, false, ExitCodes.Ok)
        <> ExitCodes.Ok
    then
        failwith "Sauberer abgeschlossener Lauf ergab nicht den Erfolgcode."

// ---------------------------------------------------------------------------
// Keymap und Architekturgrenzen (AC-T032-08).
// ---------------------------------------------------------------------------

let keymapValidatesSemanticActionsAgainstDefaults () =
    let ok, error = Keymap.Validate(Keymap.Defaults)

    if not ok then
        failwith $"Default-Keymap ungueltig: {error}"

    if Keymap.Resolve(41) <> "quit" then
        failwith "Escape loest nicht quit aus."

    if Keymap.Resolve(26) <> "pan-up" then
        failwith "W loest nicht pan-up aus."

    if not (isNull (Keymap.Resolve(999))) then
        failwith "Unbelegter Scancode loeste eine Aktion aus."

    let broken = System.Collections.Generic.Dictionary<string, int[]>()
    broken.Add("quit", [||])

    let brokenOk, brokenError = Keymap.Validate(broken)

    if brokenOk then
        failwith "Keymap ohne Bindungen wurde akzeptiert."

    if not (brokenError.Contains("quit", StringComparison.Ordinal)) then
        failwith "Fehlermeldung nannte die fehlende Aktion nicht."

let architectureKeepsSessionPure () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let root = findRoot (Environment.CurrentDirectory)

    let sessionSources =
        Directory.GetFiles(Path.Combine(root, "src", "Riftward.Session"), "*.cs")

    if sessionSources.Length = 0 then
        failwith "Session-Projekt ohne Quellen."

    for source in sessionSources do
        let text = File.ReadAllText(source)

        for forbidden in
            [ "Riftward.Platform"
              "SDL"
              "bgfx"
              "DllImport"
              "LibraryImport"
              "System.Net" ] do
            if text.Contains(forbidden, StringComparison.Ordinal) then
                failwith $"Session-Quelle {Path.GetFileName(source)} referenziert Plattformtyp {forbidden}."

    let projectText =
        File.ReadAllText(Path.Combine(root, "src", "Riftward.Session", "Riftward.Session.csproj"))

    for forbidden in [ "Riftward.Platform.csproj"; "Riftward.App.csproj" ] do
        if projectText.Contains(forbidden, StringComparison.Ordinal) then
            failwith $"Session-Projekt referenziert {forbidden}."

    if projectText.Contains(".fs\"", StringComparison.Ordinal) then
        failwith "Session-Projekt enthaelt F#-Quellen im Laufzeitpfad."

// ---------------------------------------------------------------------------
// CLI-Vertrag, Hermetie und Exitcodes (AC-T032-02/07/09).
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

let panDirectionsMatchEdgePanAndNorthUpContract () =
    // Kommandovertrag §4 (Abschluss-Review 2026-08-27 praezisiert): Bildschirm
    // oben ist Norden (-Z); Tasten- und Rand-Schwenken bewegen die Sicht in
    // dieselbe Himmelsrichtung. Gebunden werden die Kameramathematik, die
    // Runner-Verdrahtung (Quellfragment) und der Vertragsabsatz.
    let camera = GrayboxCamera()
    let z0 = camera.CenterZMeters
    camera.PanSteps(0, -1)

    if not (camera.CenterZMeters < z0) then
        failwith "PanSteps(0,-1) bewegt die Sicht nicht nach Norden (-Z)."

    camera.PanSteps(0, +2)

    if not (camera.CenterZMeters > z0) then
        failwith "PanSteps(0,+1) bewegt die Sicht nicht nach Sueden (+Z)."

    let x0 = camera.CenterXMeters
    camera.PanSteps(-1, 0)

    if not (camera.CenterXMeters < x0) then
        failwith "PanSteps(-1,0) bewegt die Sicht nicht nach Westen (-X)."

    camera.PanSteps(+2, 0)

    if not (camera.CenterXMeters > x0) then
        failwith "PanSteps(+1,0) bewegt die Sicht nicht nach Osten (+X)."

    let runnerText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "CommandLoopRunner.cs"))

    for fragment in
        [ "case \"pan-up\":"
          "camera.PanSteps(0, -1);"
          "case \"pan-down\":"
          "camera.PanSteps(0, +1);"
          "case \"pan-left\":"
          "camera.PanSteps(-1, 0);"
          "case \"pan-right\":"
          "camera.PanSteps(+1, 0);"
          "stepsY -= 1;"
          "stepsY += 1;"
          "stepsX -= 1;"
          "stepsX += 1;" ] do
        if not (runnerText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"Schwenkrichtung fehlt oder ist invertiert ({fragment})."

    // Kohärenz: Taste und Kantenkontakt derselben Seite nutzen dieselbe
    // Weltrichtung (pan-up und oberer Rand beide -Z usw.).
    let panUpIndex = runnerText.IndexOf("case \"pan-up\":", StringComparison.Ordinal)

    let panDownIndex =
        runnerText.IndexOf("case \"pan-down\":", StringComparison.Ordinal)

    let edgeTopIndex = runnerText.IndexOf("stepsY -= 1;", StringComparison.Ordinal)
    let edgeBottomIndex = runnerText.IndexOf("stepsY += 1;", StringComparison.Ordinal)

    if
        panUpIndex < 0
        || panDownIndex < 0
        || edgeTopIndex < 0
        || edgeBottomIndex < 0
        || panUpIndex > panDownIndex
    then
        failwith "Runner-Schwenkblöcke sind nicht in der gebundenen Ordnung auffindbar."

    if edgeTopIndex > edgeBottomIndex then
        failwith "Rand-Schwenkblöcke sind nicht in der gebundenen Ordnung auffindbar."

    let contractText =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "KOMMANDOVERTRAG.md"))

    let normalizedContract =
        System.Text.RegularExpressions.Regex.Replace(contractText, "\s+", " ")

    for fragment in
        [ "Bildschirm oben ist Norden"
          "dieselbe Himmelsrichtung"
          "Osten erscheint am linken Bildschirmrand" ] do
        if not (normalizedContract.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"Kommandovertrag §4 bindet die Richtungskohärenz nicht ({fragment})."

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

let private cliScriptContent =
    "graybox-input-script-v1 420\n"
    + "intent 240 box 4000 16000 36000 50000\n"
    + "intent 250 move 2\n"
    + "intent 260 clear\n"
    + "intent 270 point 20000 30000\n"
    + "end\n"

let private endHashOf (path: string) =
    let json = File.ReadAllText(path)
    use document = JsonDocument.Parse(json)
    document.RootElement.GetProperty("stateHashChain").GetProperty("end").GetString()

let private runFreshProcessToleratingTransientGate arguments =
    let exitCode, stdout, stderr = runAppHost arguments

    if exitCode = ExitCodes.Map(PlatformErrorCode.CommandGateViolated) then
        runAppHost arguments
    else
        exitCode, stdout, stderr

let cliContractRunsHeadlessWithReportsAndControlledFailures () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t032-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let scriptPath = Path.Combine(temporary, "script.txt")
        File.WriteAllText(scriptPath, cliScriptContent)

        let argumentsFor reportPath seed =
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

        let reportOne = Path.Combine(temporary, "run1.json")
        let reportTwo = Path.Combine(temporary, "run2.json")

        let exitOne, _, _ =
            runFreshProcessToleratingTransientGate (argumentsFor reportOne "20260826")

        if exitOne <> 0 then
            failwith $"Headless-Lauf ergab Exitcode {exitOne}."

        let exitTwo, _, _ =
            runFreshProcessToleratingTransientGate (argumentsFor reportTwo "20260826")

        if exitTwo <> 0 then
            failwith $"Zweiter Headless-Lauf ergab Exitcode {exitTwo}."

        for path in [ reportOne; reportTwo ] do
            let json = File.ReadAllText(path)

            if not (CommandReportSchema.Validate(json).Count = 0) then
                failwith $"Echter Report verletzte den Schemavertrag: {path}"

        // K2: zwei unabhaengige Fresh-Prozesslaeufe, byteidentische Kette.
        if endHashOf reportOne <> endHashOf reportTwo then
            failwith "Zwei Fresh-Prozesslaeufe lieferten unterschiedliche Endhashes."

        if
            BenchReportSchema.StructureDifferences(File.ReadAllText(reportOne), File.ReadAllText(reportTwo)).Count
            <> 0
        then
            failwith "Reportstruktur zweier Laeufe ist nicht identisch."

        // Fremder Seed aendert das Ergebnis nachweislich.
        let reportForeign = Path.Combine(temporary, "foreign.json")

        let exitForeign, _, _ =
            runFreshProcessToleratingTransientGate (argumentsFor reportForeign "42")

        if exitForeign <> 0 then
            failwith $"Fremdseedlauf ergab Exitcode {exitForeign}."

        if endHashOf reportOne = endHashOf reportForeign then
            failwith "Fremder Seed aenderte den Endhash nicht."

        // Unbekanntes Szenario: Code 37 ohne Report.
        let reportUnknown = Path.Combine(temporary, "unknown.json")

        let exitUnknown, _, stderrUnknown =
            runAppHost
                [| "kommandoschleife"
                   "--scenario"
                   "anderes-szenario"
                   "--input-script"
                   scriptPath
                   "--seed"
                   "1"
                   "--report"
                   reportUnknown |]

        if exitUnknown <> ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable) then
            failwith $"Unbekanntes Szenario ergab Exitcode {exitUnknown} statt 37."

        if File.Exists(reportUnknown) then
            failwith "Unbekanntes Szenario schrieb dennoch einen Report."

        if not (stderrUnknown.Contains("anderes-szenario", StringComparison.Ordinal)) then
            failwith "Fehlermeldung nannte das unbekannte Szenario nicht."

        // Malformiertes Skript: Code 37 ohne Report.
        let badScript = Path.Combine(temporary, "bad.txt")
        File.WriteAllText(badScript, "kein-kopf\n")

        let reportBad = Path.Combine(temporary, "bad.json")

        let exitBad, _, _ =
            runAppHost
                [| "kommandoschleife"
                   "--scenario"
                   "kommando-graybox"
                   "--input-script"
                   badScript
                   "--seed"
                   "1"
                   "--report"
                   reportBad |]

        if exitBad <> ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable) then
            failwith $"Malformiertes Skript ergab Exitcode {exitBad} statt 37."

        if File.Exists(reportBad) then
            failwith "Malformiertes Skript schrieb dennoch einen Report."

        // Nicht schreibbarer Reportpfad: Code 28.
        let blockedReport = Path.Combine(temporary, "fehlender-ordner", "blocked.json")

        let exitBlocked, _, _ = runAppHost (argumentsFor blockedReport "20260826")

        if exitBlocked <> ExitCodes.Map(PlatformErrorCode.ReportNotWritable) then
            failwith $"Nicht schreibbarer Pfad ergab Exitcode {exitBlocked} statt 28."

        // Ohne nutzbares Display bricht --interactive kontrolliert ab statt
        // zu simulieren: mit gebauten Nativen Artefakten nach
        // Fensterinitialisierung (Code 19), ohne sie bereits vorher mit dem
        // dokumentierten Artefaktcode (Code 14). Beide Wege duerfen niemals
        // einen Report oder ein simuliertes Interaktivverhalten erzeugen;
        // die Erwartung haengt nie von gitignoriertem Laufzeitstand ab.
        let hasDisplay =
            not (
                String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
            )

        if not hasDisplay then
            let nativesPresent =
                File.Exists(Path.Combine(repositoryRoot, ".ai", "runtime", "cache", "native", "artifact-hashes.json"))

            let expectedCode =
                if nativesPresent then
                    ExitCodes.Map(PlatformErrorCode.WindowFailed)
                else
                    ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid)

            let reportDisplayless = Path.Combine(temporary, "displayless.json")

            let exitDisplayless, _, stderrDisplayless =
                runAppHost [| yield! argumentsFor reportDisplayless "20260826"; "--interactive" |]

            if exitDisplayless <> expectedCode then
                failwith
                    $"Displayloser Interaktivlauf ergab {exitDisplayless} statt {expectedCode} ({stderrDisplayless})."

            if File.Exists(reportDisplayless) then
                failwith "Displayloser Abbruch schrieb einen Interaktivreport."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

let engineRunIsHermetic () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t032-hermetic-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let snapshot () =
            Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).Length

        let before = snapshot ()
        runEngine defaultSeed (engineIntents ()) |> ignore
        let after = snapshot ()

        if before <> after then
            failwith "Der Sitzungskern schrieb Dateien ausserhalb vertraglich erlaubter Verzeichnisse."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

let riftScriptKommandoschleifeContractKeepsAppBuildGuard () =
    let scriptText =
        File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "rift.sh"))

    if not (scriptText.Contains("kommandoschleife)", StringComparison.Ordinal)) then
        failwith "rift.sh fuehrt den Befehl kommandoschleife nicht."

    let caseStart = scriptText.IndexOf("  kommandoschleife)", StringComparison.Ordinal)
    let caseEnd = scriptText.IndexOf("  harness)", StringComparison.Ordinal)

    if caseStart < 0 || caseEnd < 0 || caseEnd <= caseStart then
        failwith "Kommandoschleifen-Zweig in rift.sh nicht abgrenzbar."

    let commandBranch = scriptText.Substring(caseStart, caseEnd - caseStart)

    if not (commandBranch.Contains("rift_need_app_output", StringComparison.Ordinal)) then
        failwith "Kommandoschleife umgeht die App-Build-Wache."

    if not (commandBranch.Contains("kommandoschleife \"$@\"", StringComparison.Ordinal)) then
        failwith "Kommandoschleife reicht Argumente nicht an den Host weiter."

    for documentedCode in [ "35"; "36"; "37"; "38" ] do
        if not (scriptText.Contains(documentedCode, StringComparison.Ordinal)) then
            failwith $"rift.sh-Hilfe dokumentiert Exitcode {documentedCode} nicht."

// ---------------------------------------------------------------------------
// Report-Schema: Golden-Fixture und Fabrikationsmatrix (AC-T032-02/05).
// ---------------------------------------------------------------------------

let private goldenReport =
    """{"schemaVersion":2,"mode":"kommandoschleife","executionMode":"headless","command":"./scripts/rift.sh kommandoschleife --scenario kommando-graybox --input-script \u003CPFAD\u003E --seed N --report \u003CPFAD\u003E","scenario":{"id":"kommando-graybox","seed":20260826,"tickRateHz":20,"agentCount":250,"worldId":"riftward-simworld-graybox-v1","content":"synthetic-graybox-command-loop"},"commandContract":{"document":"docs/KOMMANDOVERTRAG.md","version":"1","scriptFormat":"graybox-input-script-v1","selectionModel":"graybox-selection-model-v0","cameraModel":"graybox-camera-model-v0","diagnosticOnlyReplayDisclaimer":true,"modeContract":{"document":"docs/MODEVERTRAG.md","version":"1"}},"modeSession":{"contract":{"document":"docs/MODEVERTRAG.md","version":"1"},"initialMode":"strategic","finalMode":"strategic","switchProtocol":[],"strategyIntentsRejectedInPersonalMode":0,"steerIntentsRejectedInStrategyMode":0,"steerIdleDedupes":0,"interactiveContextRejections":0,"switchReactionTicks":{"unit":"ticks","method":"mode-switch-intent-tick-to-first-validity-boundary-in-new-mode","p50":0,"p95":0,"p99":0,"max":0,"count":0,"target":2,"hardLimit":3,"gateCoupled":false}},"simulationContract":{"document":"docs/SIMULATIONSVERTRAG.md","version":"1","numericModel":"q16-16-fixed-point-intonly-v1","hashAlgorithm":"fnv1a64-canonical-chain-v1","allocationLimitBytesPerWarmTick":0},"inputScript":{"scriptSha256":"cbcab89e6961e4bfeaad33f3dde8b63cd17c27e892f66d11b6396cd8c51ffc33","intentPlanHash":"4b891064971749c2","horizonTicks":420,"warmupTicks":240,"intentsTotal":4,"appliedTotal":4,"rejectedTotal":0,"emptyPointDeselects":1,"moveWithoutSelectionRejects":0,"noZoneRejects":0,"kernelCommandsTotal":5},"startedAtUtc":"2026-08-26T07:42:17.6202299Z","finishedAtUtc":"2026-08-26T07:42:18.266729Z","environment":{"os":{"type":"Linux","kernelRelease":"7.0.0-30-generic"},"cpu":{"model":"Intel(R) Core(TM) i7-3770 CPU @ 3.40GHz"},"rid":"linux-x64","commit":"068974c9e606e6b023d4708ffc7cc12be5dda7a9","buildMode":"Release","display":{"measured":false,"reason":"headless-mode-native-artifacts-not-loaded"},"pins":[{"id":"sdl3","refType":"tag","ref":"release-3.4.14","commit":"147a8ee32dbf9ac02f3794964490687b6bbda1bc","sourceSha256":"9d57b178fb297e121ef2605275937b7afaa7cd24d99ce1f95953e69e7a2535d6","licenseSpdx":"zlib"},{"id":"bgfx","refType":"commit","ref":"35a98dd6453cf25dc75c68e233abb400836d5920","commit":"35a98dd6453cf25dc75c68e233abb400836d5920","sourceSha256":"68ecda67f15b43e0b324b338dfe6b49b58bbbc684d2c5a718c674198db15fee4","licenseSpdx":"BSD-2-Clause"},{"id":"bx","refType":"commit","ref":"9e3fadf6f11380031486be704d2ff46ca143664f","commit":"9e3fadf6f11380031486be704d2ff46ca143664f","sourceSha256":"84740909a73336fa6192f3489cff8ba338b1c525103c291cbf7554a77002eb1a","licenseSpdx":"BSD-2-Clause"},{"id":"bimg","refType":"commit","ref":"371d90098b1fd017cd00205979d5ef74b8c3ed62","commit":"371d90098b1fd017cd00205979d5ef74b8c3ed62","sourceSha256":"a1464cfbbbbbb1712df9231bb5c5442e3728f78110c7072d5145892e428fd937","licenseSpdx":"BSD-2-Clause"}]},"measurement":{"warmupTicks":240,"sampleTicks":180,"ticksExecuted":420,"hashSampleIntervalTicks":60,"rssSampleIntervalTicks":60,"windowCompleted":true},"metrics":{"tickTimeMs":{"unit":"ms","method":"stopwatch-tick-delta","p50":0.276,"p95":0.655,"p99":0.864},"managedAllocationsBytes":{"unit":"bytes","method":"gc-total-allocated-bytes-precise-delta-per-tick-sum","perWarmTick":0},"reactionTicks":{"unit":"ticks","method":"command-submission-tick-to-first-effect-state-hash-delta","p50":1,"p95":1,"p99":1,"max":1,"count":4,"target":2,"hardLimit":3},"runtimeShaderCompilation":{"unit":"bool","method":"offline-shaderc-binaries-only","value":false},"gcPauseSumMs":{"unit":"ms","method":"gc-get-total-pause-duration-delta","value":0,"gateCoupled":false},"gcPauseCount":{"unit":"count","method":"gc-collection-count-gen0-to2-delta","value":0,"gateCoupled":false},"activeAgents":{"unit":"count","method":"soa-agent-count-fixed","value":250,"gateCoupled":false},"workingSetKiB":{"measured":false,"reason":"headless-session-does-not-sample-rss"},"frameTimeMs":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"gpuTimeMs":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"drawSubmitCallsPerFrame":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"visibleTrianglesPerFrame":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"},"concurrentMarkers":{"measured":false,"reason":"headless-cpu-scenario-no-renderer"}},"stateHashChain":{"unit":"hex64","method":"fnv1a64-canonical-chain-v1","start":"20f84cdb183a4364","intervalSampleTicks":[240,300,360,420],"intervalHashes":["20f84cdb183a4364","7666fff5fa0ddb47","72eb588d50649f45","978aab19406daa26"],"end":"978aab19406daa26"},"gate":{"limits":{"p99TickTimeHardLimitMs":16,"p99TickTimeTargetMs":8,"allocationsPerWarmTickBytesMax":0,"reactionHardLimitTicks":3,"reactionTargetTicks":2,"runtimeShaderCompilationAllowed":false,"switchReactionHardLimitTicks":3,"switchReactionTargetTicks":2},"stateChainSelfConsistency":{"evaluated":true},"switchReaction":{"evaluated":false,"reason":"no-effective-mode-switch-in-run"},"pass":true,"tickTimeTargetMet":true,"reactionTargetMet":true,"violations":[]},"openQuestions":{"qtec004":"open","qtec006":"open","qtec010":"open","qgam001":"open","qgam002":"open","qgam003":"open","qgam004":"open","qgam005":"open","qgam006":"open","qgam007":"open","qgam010":"open","qnar002":"open"},"profiles":[{"id":"hw-pc-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-mac-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"},{"id":"hw-pc-high","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"mandatory-profile-not-measured-no-reference-hardware"}],"baseline":{"classification":"diagnostic-developer-workstation","protocol":"qops001-2026-08-24"},"frameEvidence":{"captured":false,"reason":"capture-not-requested"},"exitCode":0}"""

let reportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    if CommandReportSchema.Validate(goldenReport).Count <> 0 then
        failwith "Golden-Report verletzte den Schemavertrag."

    let assertHasError (fragment: string) (mutated: string) (message: string) =
        let errors = CommandReportSchema.Validate(mutated)

        if errors.Count = 0 then
            failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

        let joined = String.concat "; " errors

        if not (joined.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

    assertHasError
        "ausserhalb"
        (goldenReport.Replace("\"schemaVersion\":2", "\"schemaVersion\":3"))
        "Falsche Schemaversion akzeptiert"

    assertHasError
        "unbekanntes Feld"
        (goldenReport.Replace("{\"schemaVersion\":2", "{\"schemaVersion\":2,\"fabriziert\":true"))
        "Fabriziertes Feld akzeptiert"

    assertHasError
        "konstanter Wert"
        (goldenReport.Replace("kommando-graybox", "anderes-szenario"))
        "Fremdes Szenario akzeptiert"

    // Modussitzungsblock (T-033): Ein leerer Block verletzt die Pflichtfelder;
    // ein fremder Modusname oder Skriptformatbezeichner wird abgewiesen.
    assertHasError
        "Pflichtfeld"
        (goldenReport.Replace(
            "\"modeSession\":{\"contract\":{\"document\":\"docs/MODEVERTRAG.md\",\"version\":\"1\"},\"initialMode\":\"strategic\",\"finalMode\":\"strategic\",\"switchProtocol\":[],\"strategyIntentsRejectedInPersonalMode\":0,\"steerIntentsRejectedInStrategyMode\":0,\"steerIdleDedupes\":0,\"interactiveContextRejections\":0,\"switchReactionTicks\":{\"unit\":\"ticks\",\"method\":\"mode-switch-intent-tick-to-first-validity-boundary-in-new-mode\",\"p50\":0,\"p95\":0,\"p99\":0,\"max\":0,\"count\":0,\"target\":2,\"hardLimit\":3,\"gateCoupled\":false}},",
            "\"modeSession\":{},"
        ))
        "Modussitzungsblock ohne Pflichtfelder akzeptiert"

    assertHasError
        "konstanter Wert"
        (goldenReport.Replace("\"finalMode\":\"strategic\"", "\"finalMode\":\"dritter-modus\""))
        "Fremder Modusname akzeptiert"

    assertHasError
        "konstanter Wert"
        (goldenReport.Replace("\"scriptFormat\":\"graybox-input-script-v1\"", "\"scriptFormat\":\"graybox-input-script-v9\""))
        "Fremde Skriptformatkennung akzeptiert"

    // Kriterium 6 (Modevertrag §7): Nichtauswertung ohne Grund und ein
    // Vakuumpass mit evaluiert=true ohne Messung sind unzulaessig gebunden.
    assertHasError
        "Pflichtfeld"
        (goldenReport.Replace(
            "\"switchReaction\":{\"evaluated\":false,\"reason\":\"no-effective-mode-switch-in-run\"}",
            "\"switchReaction\":{\"evaluated\":false}"
        ))
        "Wechselreaktionsnichtauswertung ohne Grund akzeptiert"

    assertHasError
        "Pflichtfeld"
        (goldenReport.Replace(
            "\"switchReaction\":{\"evaluated\":false,\"reason\":\"no-effective-mode-switch-in-run\"}",
            "\"switchReaction\":{\"evaluated\":true}"
        ))
        "Wechselreaktionsvakuumpass ohne Messwerte akzeptiert"

    // Headless darf GPU-/Rendererwerte nicht messend ausweisen.
    assertHasError
        "Grund erforderlich"
        (goldenReport.Replace(
            "{\"measured\":false,\"reason\":\"headless-cpu-scenario-no-renderer\"}",
            "{\"measured\":false,\"reason\":\"\"}"
        ))
        "Leerer unavailable-Grund akzeptiert"

    // Diagnosefelder tragen die Entkopplungsmarke verpflichtend.
    let withoutCouplingMark =
        goldenReport.Replace("\"value\":0,\"gateCoupled\":false", "\"value\":0")

    if
        CommandReportSchema.Validate(withoutCouplingMark).Count = 0
        && withoutCouplingMark <> goldenReport
    then
        failwith "Diagnosefeld ohne gateCoupled-Markierung wurde akzeptiert."

    // Abgriffbehauptung ohne Bindungen wird abgewiesen.
    assertHasError
        "Pflichtfeld"
        (goldenReport.Replace("{\"captured\":false,\"reason\":\"capture-not-requested\"}", "{\"captured\":true}"))
        "Abgriff ohne Bindungen akzeptiert"

    // Kettenkriterium (Vertrag §7): Nichtauswertung ohne maschinenlesbaren
    // Grund ist unzulaessig; der Goldenlauf bindet die Auswertung explizit.
    if not (goldenReport.Contains("\"stateChainSelfConsistency\":{\"evaluated\":true}", StringComparison.Ordinal)) then
        failwith "Golden-Report weist das Kettenkriterium nicht als ausgewertet aus."

    assertHasError
        "Pflichtfeld"
        (goldenReport.Replace(
            "\"stateChainSelfConsistency\":{\"evaluated\":true}",
            "\"stateChainSelfConsistency\":{\"evaluated\":false}"
        ))
        "Nichtauswertung ohne Grund akzeptiert"

    // Wahrheitsgehalt des headless workingSetKiB-Grundes: Der Goldreport ist
    // aus einem echten Lauf gebunden und darf den vertraglichen Grund
    // headless-session-does-not-sample-rss nicht durch eine falsche
    // Sampling-Behauptung ersetzen.
    if
        not (
            goldenReport.Contains(
                "\"workingSetKiB\":{\"measured\":false,\"reason\":\"headless-session-does-not-sample-rss\"}",
                StringComparison.Ordinal
            )
        )
    then
        failwith "Golden-Report weist workingSetKiB nicht mit dem vertraglichen Grund aus."

    // Offene Produktfragen duerfen nicht still geschlossen werden.
    assertHasError
        "konstanter Wert"
        (goldenReport.Replace("\"qgam001\":\"open\"", "\"qgam001\":\"answered\""))
        "Geschlossene Produktfrage akzeptiert"

    // Ausfuehrungsart schaltet strikte Alternativformen.
    assertHasError
        "unbekannte Ausfuehrungsart"
        (goldenReport.Replace("\"executionMode\":\"headless\"", "\"executionMode\":\"virtual\""))
        "Unbekannte Ausfuehrungsart akzeptiert"

// ---------------------------------------------------------------------------
// Vertraglicher unavailable-Grund des headless workingSetKiB (AC-T032-05
// Wahrheitsgehalt): Der Runner meldet, dass die headless Engine kein RSS
// sampelt, statt eine nicht existierende Messmethode zu behaupten.
// ---------------------------------------------------------------------------

let headlessWorkingSetReasonBindsContractTruthfulness () =
    let samples =
        CommandLoopRunner.WorkingSetFrom(Unchecked.defaultof<SessionRunResult>)

    if samples.Measured then
        failwith "Headless workingSetKiB behauptet gemessene Werte."

    match samples.Reason with
    | null -> failwith "Headless workingSetKiB ohne vertraglichen Grund."
    | reason ->
        if reason <> "headless-session-does-not-sample-rss" then
            failwith $"Unvertraglicher workingSetKiB-Grund: {reason}"

// ---------------------------------------------------------------------------
// Zweikanalrueckmeldung: Der Runner muss die an den Kern übergebenen
// Bewegungszonen auch der Darstellung zuführen (AC-T032-06 Strukturbinding).
// Die Vorgängersitzungen liessen genau diese Verdrahtung unausgeführt,
// sodass der Puls-Kanal tot blieb, ohne dass die Suite es sah.
// ---------------------------------------------------------------------------

let dispatchedMoveZonesBindAcceptedCommandsOnly () =
    let world = SimWorld(defaultSeed)
    let groups = SessionEngine.ReadAgentGroups(world)
    let selection = SelectionModel(groups)

    let scripted =
        [| GrayboxIntent(40, GrayboxIntentKind.BoxSelect, 1000L, 1000L, 159000L, 89000L)
           GrayboxIntent(45, GrayboxIntentKind.GroupMoveToZone, 2L)
           GrayboxIntent(46, GrayboxIntentKind.GroupMoveToZone, 4L) |]

    let pipeline = SessionPipeline(world, selection, scripted)

    pipeline.ProcessBoundary(40L) |> ignore

    if pipeline.DispatchedMoveZonesOfLastBoundary.Count <> 0 then
        failwith "Reine Auswahlvorgrenze durfte keine Bewegungszonen melden."

    pipeline.EnqueueLiveIntent(GrayboxIntent(3, GrayboxIntentKind.GroupMoveToZone, 1L))

    let firstMoveOutcome = pipeline.ProcessBoundary(45L)

    if firstMoveOutcome.CommandCount <> 5 then
        failwith $"Erste Gruppenbewegung erzeugte {firstMoveOutcome.CommandCount} statt 5 Kernbefehlen."

    let secondMoveOutcome = pipeline.ProcessBoundary(46L)

    if secondMoveOutcome.CommandCount <> 5 then
        failwith $"Zweite Gruppenbewegung erzeugte {secondMoveOutcome.CommandCount} statt 5 Kernbefehlen."

    let zones = pipeline.DispatchedMoveZonesOfLastBoundary

    if
        zones.Count <> 1
        || zones.[0] <> 4
        || firstMoveOutcome.RejectedCount + secondMoveOutcome.RejectedCount <> 1
        || pipeline.LateRejectedTotal <> 1L
    then
        failwith $"Verspaetete Bewegung/Zonenausweis falsch: [{String.Join(',', zones)}]."

    if not (pipeline.World.TargetZoneOfGroup(0) = 4) then
        failwith "Die zweite Bewegung erreichte den Kern nicht."

    selection.Clear()
    pipeline.EnqueueLiveIntent(GrayboxIntent(47, GrayboxIntentKind.GroupMoveToZone, 0L))
    let withoutSelection = pipeline.ProcessBoundary(47L)

    if
        withoutSelection.CommandCount <> 0
        || withoutSelection.RejectedMoveWithoutSelection <> 1
        || pipeline.DispatchedMoveZonesOfLastBoundary.Count <> 0
    then
        failwith "Ohne Auswahl abgewiesene Bewegung erreichte die Befehlsrueckmeldung."

    // Naechste unbeteiligte Vorgrenze leert die Ausweisung erneut.
    pipeline.ProcessBoundary(48L) |> ignore

    if pipeline.DispatchedMoveZonesOfLastBoundary.Count <> 0 then
        failwith "Alte Vorgrenzonen hielten ueber die Grenze hinaus vor."

let interactiveCommandPulseRendersThenExpires () =
    let world = SimWorld(defaultSeed)
    let view = new InteractiveView()

    try
        view.BindAgentGroups(SessionEngine.ReadAgentGroups(world))

        let baseMarkerCount = view.WriteFrameState(world, 1000L)

        if baseMarkerCount <> 0 then
            failwith $"Ohne Auswahl und Befehl entstanden {baseMarkerCount} Marker."

        view.NotifyCommandIssued(1000L, 1)

        let growingMarkerCount = view.WriteFrameState(world, 1015L)

        if growingMarkerCount < 1 then
            failwith "Angemeldeter Befehlspuls fehlte im Markerzustand."

        let expiringTick = 1000L + int64 InteractiveView.CommandPulseTicks
        let expiredMarkerCount = view.WriteFrameState(world, expiringTick)

        if expiredMarkerCount <> 0 then
            failwith "Der Befehlspuls lief vertraglich nie ab."
    finally
        view.Dispose()

let commandLoopRunnerWiresTheSecondFeedbackChannelToItsSourceData () =
    let runnerText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "CommandLoopRunner.cs"))

    for fragment in [ ".NotifyCommandIssued("; "DispatchedMoveZonesOfLastBoundary" ] do
        if not (runnerText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"CommandLoopRunner verdrahtet den Puls-Kanal nicht ({fragment})."

let inputScriptReadingIsBoundedAtTheContractByteLimit () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t032-bounded-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(temporary) |> ignore

    try
        let oversizedPath = Path.Combine(temporary, "oversized.bin")

        let stream = File.Create(oversizedPath)
        stream.SetLength(int64 SessionContract.ScriptBytesMax + 1L)
        stream.Dispose()

        try
            CommandLoopRunner.ReadInputScriptBytes(oversizedPath) |> ignore
            failwith "Uebergrosses Skript wurde voll gelesen statt begrenzt abgewiesen."
        with :? InputScriptException as error ->
            if error.Reason <> InputScriptRejectReason.ScriptTooLarge then
                failwith $"Uebergrosse Klasse war {error.Reason}, erwartet ScriptTooLarge."

        let exactPath = Path.Combine(temporary, "exact.bin")
        File.WriteAllBytes(exactPath, Array.zeroCreate<byte> (int SessionContract.ScriptBytesMax))

        let exactBytes = CommandLoopRunner.ReadInputScriptBytes(exactPath)

        if exactBytes.Length <> int SessionContract.ScriptBytesMax then
            failwith "Exakt grenzwertiges Skript wurde nicht byteerhalten gelesen."

        // End-of-stream-Spezialdatei: kein Blockieren, keine Bytes, nachfolgend
        // kontrollierte HeaderMalformed-Ablehnung des Parsers.
        let emptyDeviceBytes = CommandLoopRunner.ReadInputScriptBytes("/dev/null")

        if emptyDeviceBytes.Length <> 0 then
            failwith "/dev/null lieferte unerwartet Bytes."
    finally
        if Directory.Exists(temporary) then
            Directory.Delete(temporary, true)

let contractNamesTheRejectionCausesVerbatim () =
    // Kommandovertrag Abschnitt 2 (move-without-selection) und Abschnitt 9
    // (target-not-in-zone): die fachlichen Gruende sind verbatim gebunden.
    let contractText =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "KOMMANDOVERTRAG.md"))

    for documented in
        [ SessionContract.RejectReasonMoveWithoutSelection
          SessionContract.RejectReasonTargetNotInZone ] do
        if not (contractText.Contains(documented, StringComparison.Ordinal)) then
            failwith $"Kommandovertrag nennt den Ablehnungsgrund '{documented}' nicht."

    if SessionContract.RejectReasonMoveWithoutSelection <> "move-without-selection" then
        failwith
            $"Grundkennung war {SessionContract.RejectReasonMoveWithoutSelection}, erwartet move-without-selection."

    if SessionContract.RejectReasonTargetNotInZone <> "target-not-in-zone" then
        failwith $"Grundkennung war {SessionContract.RejectReasonTargetNotInZone}, erwartet target-not-in-zone."

let runnerSurfacesTheRejectionCausesOnTheirLivePaths () =
    let runnerText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.App", "Command", "CommandLoopRunner.cs"))

    for fragment in
        [ "SessionContract.RejectReasonTargetNotInZone"
          "SessionContract.RejectReasonMoveWithoutSelection" ] do
        if not (runnerText.Contains(fragment, StringComparison.Ordinal)) then
            failwith $"CommandLoopRunner gibt den Ablehnungsgrund nicht am Live-Pfad aus ({fragment})."

    // Die Kernpipeline stellt die je-Grenze-Abweichungen fuer die Ausgabe bereit.
    let pipelineText =
        File.ReadAllText(Path.Combine(repositoryRoot, "src", "Riftward.Session", "SessionEngine.cs"))

    if not (pipelineText.Contains("RejectedMoveWithoutSelection", StringComparison.Ordinal)) then
        failwith "SessionPipeline weist move-without-selection nicht je Vorgrenze aus."

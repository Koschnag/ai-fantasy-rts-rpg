module ExplorationTests

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open Riftward.App
open Riftward.App.Command
open Riftward.Platform
open Riftward.Session
open Riftward.Simulation

// ---------------------------------------------------------------------------
// T-034: kleinster spielbarer Erkundungsauftrag-Loop (Erkundungsvertrag V1,
// Abschnitte 0 bis 10). Jede Pruefung bindet Code, Vertragsdokument,
// Schemavertrag und Laufverhalten gegeneinander; keine Pruefung antwortet
// auf eine offene Produktfrage und keine veraendert Riftward.Simulation.
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

/// Vertraglicher vollstaendiger Aufsuchflow: strategische Mobilmachung mit
/// Auswahl der Vertragsheldengruppe und Gruppenbefehl, Wechsel in den
/// persoenlichen Modus fuer das Aufsuchen, Rueckwechsel zur naechsten
/// Mobilmachung; mindestens ein vollstaendiger strategisch -> persoenlich ->
/// strategischer Zyklus je Moduswechsel (Vertrag Abschnitte 3 und 6).
let private explorationScript (horizon: int) =
    // Die strategische Rahmenwahl mobilisiert alle fünf Vertragsgruppen und
    // damit ausdrücklich auch die Heldengruppe 0. Das vermeidet eine
    // künstliche Tick-0-Punktwahl nach der bereits bewegten Warmphase und
    // räumt den dichten Spawn gemeinsam, statt den Vertragshelden zwischen
    // 200 stehenbleibenden Agenten festzuhalten.
    let lines =
        [ 250, "intent 250 box 0 0 159000 89000"
          260, "intent 260 switch" // persoenlich ab 262: Registrierung Zone 0 (Startzone)
          400, "intent 400 switch" // strategisch ab 402: Mobilmachung
          410, "intent 410 move 4"
          1300, "intent 1300 switch" // persoenlich ab 1302: Aufsuchen Zone 4
          1500, "intent 1500 switch" // strategisch ab 1502
          1510, "intent 1510 move 2"
          2600, "intent 2600 switch" // persoenlich ab 2602: Aufsuchen Zone 2
          2800, "intent 2800 switch" // strategisch ab 2802
          2810, "intent 2810 move 3"
          3900, "intent 3900 switch" // persoenlich ab 3902: Aufsuchen Zone 3
          4200, "intent 4200 switch" // strategisch ab 4202
          4210, "intent 4210 move 5"
          5300, "intent 5300 switch" // persoenlich ab 5302: Aufsuchen Zone 5
          5500, "intent 5500 switch" // strategisch ab 5502
          5510, "intent 5510 move 1"
          6200, "intent 6200 switch" ] // persoenlich ab 6202: Aufsuchen Zone 1
        |> List.filter (fun (tick, _) -> tick < horizon)
        |> List.map snd
        |> String.concat "\n"

    $"graybox-input-script-v2 {horizon}\n{lines}\nend\n"

let private writeTempScript (horizon: int) =
    let path =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Exploration-{Guid.NewGuid():N}.graybox")

    File.WriteAllText(path, explorationScript horizon)
    path

// ---------------------------------------------------------------------------
// Spiegeltest (AC-T034-01/05): Code und Vertragsdokument haelt ein Test.
// ---------------------------------------------------------------------------

let explorationContractMirrorsDocumentedValues () =
    if ExplorationContract.DocumentPath <> "docs/ERKUNDUNGSVERTRAG.md" then
        failwith "Erkundungsvertragspfad falsch."

    if ExplorationContract.ContractVersion <> "2" then
        failwith "Erkundungsvertragsversion falsch."

    if
        ExplorationContract.ReportSchemaVersionWithoutExploration <> 2
        || ExplorationContract.ReportSchemaVersionWithExploration <> 3
    then
        failwith "Schemaversionen entsprechen nicht dem Vertrag (Bestand 2, aktiviert 3)."

    // Persistenzwahrheit nach der autorisierten V2-Praezisierung (T-037):
    // Save/Load setzt fort, die ausdrueckliche Replay-Ausnahme bleibt.
    if not ExplorationContract.Persisted then
        failwith "Die V2-Persistenzaussage (Save/Load fortsetzbar) ist verletzt."

    if ExplorationContract.ReplayContinued then
        failwith "Die ausdrueckliche Replay-Ausnahme ist verletzt."

    if
        ExplorationContract.SaveLoadContinuation <> "continued"
        || ExplorationContract.ReplayNotContinued <> "not-continued"
        || ExplorationContract.SaveLoadPersistenceStatementId <> "session-local-save-load-persisted-v2"
    then
        failwith "Die versionierte Save/Load-Persistenzaussage widerspricht dem Erkundungsvertrag V2."

    if
        InteractiveView.LandmarkMarkerHeightMeters <> 1.6
        || InteractiveView.LandmarkMarkerSize <> 1.15f
        || InteractiveView.RegisteredLandmarkLowerHeightMeters <> 1.4
        || InteractiveView.RegisteredLandmarkUpperHeightMeters <> 3.6
        || InteractiveView.RegisteredLandmarkLowerSize <> 1.25f
        || InteractiveView.RegisteredLandmarkUpperSize <> 1.05f
    then
        failwith "Landmarkenmarker-Abmessungen entsprechen nicht dem lesbaren Zweistufenvertrag."

    let document = readDocument ExplorationContract.DocumentPath

    for identifier in
        [ ExplorationContract.ActivationId
          ExplorationContract.LandmarkModelId
          ExplorationContract.VisitRuleId
          ExplorationContract.CounterModelId
          ExplorationContract.NotPersistedStatementId
          ExplorationContract.HudModelId
          ExplorationContract.LandmarkChannelModelId
          ExplorationContract.RejectReasonZoneWithoutWalkableTile
          ExplorationContract.ReportBlockId ] do
        if not (document.Contains(identifier, StringComparison.Ordinal)) then
            failwith $"Erkundungsvertragsdokument nennt die Kennung {identifier} nicht."

    for anchor in
        [ "--exploration"
          " — Erkundung: <n>/<m>"
          "same-tick-switch-last-effective-next-next-v1"
          "NavWorld.ZoneCount"
          "NavWorld.IsInsideZone"
          "NavWorld.IsWalkable" ] do
        if not (document.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Erkundungsvertragsdokument nennt den Anker {anchor} nicht."

// ---------------------------------------------------------------------------
// Landmarken-Ableitung (AC-T034-02-Testmatrix): Determinismus ueber
// Seedabhaengigkeit, Bereichsgrenzen, keine Kollision mit unpassierbaren
// Zellen, kontrollierter Fail-closed-Randfall.
// ---------------------------------------------------------------------------

let landmarkDerivationIsDeterministicZonalAndWalkable () =
    let landmarks = ExplorationAnchors.DeriveLandmarks()

    if landmarks.Length <> NavWorld.ZoneCount then
        failwith $"Landmarkenmenge ist {landmarks.Length} statt je Vertragszone eine ({NavWorld.ZoneCount})."

    for expected, landmark in Seq.indexed landmarks do
        if landmark.ZoneIndex <> expected then
            failwith $"Landmarken erscheinen nicht in fester Zonenordnung an Position {expected}."

        if not (NavWorld.IsInsideZone(landmark.ZoneIndex, landmark.AnchorTileX, landmark.AnchorTileY)) then
            failwith $"Anker der Zone {landmark.ZoneIndex} liegt ausserhalb der Vertragszonenschranken."

        if
            not landmark.Walkable
            || not (NavWorld.IsWalkable(landmark.AnchorTileX, landmark.AnchorTileY))
        then
            failwith $"Anker der Zone {landmark.ZoneIndex} liegt auf einer unpassierbaren Kachel."

    // Seedunabhaengigkeit: die Ableitung ist eine reine Funktion der
    // gebundenen Weltgeometrie; wiederholte Ableitung ist identisch und
    // ein fremder Seed aendert die Landmarkenmenge nicht.
    let again = ExplorationAnchors.DeriveLandmarks()

    if not (Seq.forall2 (fun a b -> a = b) landmarks again) then
        failwith "Wiederholte Landmarken-Ableitung ist nicht identisch."

let landmarkDerivationFailsClosedWithoutWalkableTile () =
    // Kontrollierter Fail-closed-Randfall (Vertrag Abschnitt 2): Eine Zone
    // ohne betretbare Kachel bricht die Ableitung kontrolliert ab, statt
    // einen undefinierten Anker zu bilden. Im gebundenen Vertragsweltstand
    // unerreichbar (NavWorld.ValidateZones erzeugt beim Prozessstart einen
    // kontrollierten Fehler); die Regel selbst ist hier an einer
    // synthetischen Begehbarkeit gebunden.
    let raised =
        try
            ExplorationAnchors.DeriveFrom(2, NavWorld.TilesX, NavWorld.TilesY, (fun _ _ _ -> true), (fun _ _ -> false))
            |> ignore

            false
        with
        | :? InvalidOperationException as invalidOperation ->
            if
                not (
                    invalidOperation.Message.Contains(
                        ExplorationContract.RejectReasonZoneWithoutWalkableTile,
                        StringComparison.Ordinal
                    )
                )
            then
                failwith "Fail-closed-Ablehnung traegt nicht die vertragliche Kennung."

            true
        | _ -> false

    if not raised then
        failwith "Ableitung ohne betretbare Ankerkachel brach nicht kontrolliert fail-closed ab."

// ---------------------------------------------------------------------------
// Schreibschutzgrenze der Sitzung (Controller-Reparaturbindung): weder
// Backing-Array noch cast-veraenderbare List darf entweichen.
// ---------------------------------------------------------------------------

let explorationViewsResistExternalMutation () =
    let session = ExplorationSession()
    let telemetry = session.ToTelemetry()

    // Kein Backing-Typ entweicht als konkreter mutierbarer Typ.
    if box session.Landmarks :? ExplorationLandmark[] then
        failwith "Das Backing-Array der Landmarkenmenge entweicht als konkreter Arraytyp."

    if box telemetry.Landmarks :? ExplorationLandmark[] then
        failwith "Der Telemetrieausweis gibt das Backing-Array der Landmarkenmenge heraus."

    if box session.VisitProtocol :? List<ExplorationVisit> then
        failwith "Die Protokollliste entwaechst als konkreter Listentyp."

    if box telemetry.VisitProtocol :? List<ExplorationVisit> then
        failwith "Der Telemetrieausweis gibt die Protokollliste als konkreten Listentyp heraus."

    if box telemetry.VisitProtocol :? ExplorationVisit[] then
        failwith "Der Telemetrieausweis gibt eine indexweise veraenderbare Arraykopie heraus."

    // Jede Schreibweise über die Sammlungsschnittstelle ist kontrolliert
    // abgewiesen (ReadOnlyCollection: IsReadOnly=true, Add wirft).
    let assertReadOnly (label: string) (view: IReadOnlyList<'a>) =
        match box view with
        | :? ICollection<'a> as collection ->
            if not collection.IsReadOnly then
                failwith $"{label}: Die View ist als schreibbar ausgewiesen."

            try
                collection.Add(Unchecked.defaultof<'a>) |> ignore
                failwith $"{label}: Ein Schreibzugriff auf die read-only View wurde akzeptiert."
            with :? NotSupportedException ->
                ()
        | _ -> failwith $"{label}: Keine Sammlungssicht erhalten."

    assertReadOnly "Landmarks" session.Landmarks
    assertReadOnly "ToTelemetry().Landmarks" telemetry.Landmarks
    assertReadOnly "VisitProtocol" session.VisitProtocol
    assertReadOnly "ToTelemetry().VisitProtocol" telemetry.VisitProtocol

    // Auch die IList-Indexmutation muss scheitern. ICollection.Add allein
    // haette die fruehere nackte Arraykopie uebersehen, weil Arrays Add
    // ablehnen, ihre vorhandenen Elemente aber sehr wohl ersetzen lassen.
    let assertIndexReadOnly (label: string) (view: IReadOnlyList<'a>) =
        match box view with
        | :? IList<'a> as list when list.Count > 0 ->
            let original = list.[0]

            try
                list.[0] <- original
                failwith $"{label}: Indexmutation der read-only View wurde akzeptiert."
            with :? NotSupportedException ->
                ()
        | :? IList<'a> -> ()
        | _ -> failwith $"{label}: Keine indexpruefbare Sammlungssicht erhalten."

    // Einen echten Eintrag erzeugen und dessen Telemetrieansicht pruefen.
    session.Observe(100L, (SimWorld(20260826u)), SessionMode.Personal)
    let populatedTelemetry = session.ToTelemetry()
    assertIndexReadOnly "ToTelemetry().VisitProtocol" populatedTelemetry.VisitProtocol

    // Die defensive Telemetrie-Kopie ist unabhängig von späteren
    // Registrierungen (kanonischer Ausweis des Ausweiszeitpunkts).
    if telemetry.VisitedCount <> 0 then
        failwith "Die Telemetrie-Kopie veraendert sich nachtraeglich mit der Sitzung."

// ---------------------------------------------------------------------------
// Aufsuch- und Moduskopplungsregel (AC-T034-02/03-Testmatrix): Anwesenheit,
// Moduskopplung, Doppelbesuch ohne Mehrfachzaehlung, kein Kernbefehl.
// ---------------------------------------------------------------------------

let observationEnforcesModeCouplingAndSingleRegistration () =
    let world = SimWorld(20260826u)

    if HeroTracker.ZoneIndexOf(world) <> 0 then
        failwith "Der Vertragsheld startete nicht in Vertragszone 0 (gebundener Spawn)."

    let session = ExplorationSession()

    // Strategische Anwesenheit registriert bewusst nicht (kein stiller
    // Zaehler, keine Nachwirkung).
    session.Observe(100L, world, SessionMode.Strategic)

    if session.VisitedCount <> 0 || session.VisitProtocol.Count <> 0 then
        failwith "Strategische Anwesenheit erzeugte eine Registrierung."

    // Persoenliche Anwesenheit an der Vorgrenze registriert genau einmal.
    session.Observe(101L, world, SessionMode.Personal)

    if session.VisitedCount <> 1 || not (session.IsRegistered(0)) then
        failwith "Persoenliche Anwesenheit in der Landmarkenzone registrierte nicht."

    let visit = session.VisitProtocol.[0]

    if
        visit.EvaluationBoundaryTick <> 101L
        || visit.ZoneIndex <> 0
        || visit.Mode <> ModeContract.ModePersonalId
        || visit.VisitOrder <> 1L
    then
        failwith "Aufsuchprotokolleintrag widerspricht dem Vertrag (Grenze, Zone, Modus, Reihenfolge)."

    // Doppelbesuch ohne Mehrfachzaehlung.
    session.Observe(102L, world, SessionMode.Personal)
    session.Observe(103L, world, SessionMode.Personal)

    if session.VisitedCount <> 1 || session.VisitProtocol.Count <> 1 then
        failwith "Doppelbesuch wurde mehrfach gezaehlt."

    if session.Completed then
        failwith "Abschluss vor vollstaendiger Landmarkenmenge behauptet."

    // Reihenfolgeunabhaengigkeit: das Protokoll traegt die 1-basierte
    // Registrierungsfolge; der Wertebereich bleibt reihenfolgefrei.
    if session.VisitProtocol |> Seq.sumBy (fun entry -> int entry.VisitOrder) <> 1 then
        failwith "Registrierungsreihenfolge falsch."

    let telemetry = session.ToTelemetry()

    if
        telemetry.LandmarkCount <> NavWorld.ZoneCount
        || telemetry.VisitedCount <> 1
        || telemetry.Completed
    then
        failwith "Telemetrie widerspricht der Sitzung."

let finalBoundaryHudMatchesMeasuredReportBeforeAutoExit () =
    let world = SimWorld(20260826u)
    let exploration = ExplorationSession()

    let before =
        CommandLoopRunner.BuildTitleHudText(SessionMode.Personal, world, exploration)

    if not (before.Contains("Erkundung: 0/6", StringComparison.Ordinal)) then
        failwith "Initialer Erkundungs-HUD-Ausweis war nicht 0/6."

    // Modelliert die letzte im Auto-Exit-Fenster verarbeitete Vorgrenze:
    // Die Registrierung entsteht erst an der Boundary; danach darf kein
    // weiterer Schleifendurchlauf noetig sein, damit HUD und finaler Report
    // denselben Zustand ausweisen.
    exploration.Observe(7999L, world, SessionMode.Personal)
    let telemetry = exploration.ToTelemetry()

    let after =
        CommandLoopRunner.BuildTitleHudText(SessionMode.Personal, world, exploration)

    let report =
        CommandLoopRunner.BuildExplorationSession(CommandReportSchema.ExecutionInteractive, true, telemetry)

    let reportHud = report["hud"] :?> Dictionary<string, obj>
    let reportFields = reportHud["fields"] :?> Dictionary<string, obj>

    if
        after = before
        || not (after.Contains("Erkundung: 1/6", StringComparison.Ordinal))
        || not (unbox<bool> reportHud["measured"])
        || unbox<int> reportFields["visitedCount"] <> telemetry.VisitedCount
        || unbox<int> reportFields["landmarkCount"] <> telemetry.LandmarkCount
    then
        failwith "HUD und gemessener Report wichen nach der finalen Vorgrenzenregistrierung ab."

    if CommandLoopRunner.ShouldContinueInteractiveLoop(false, true, true) then
        failwith "Auto-Exit blieb nach dem final gemessenen Fenster offen."

    // Caller-Bindung: Das echte Fenster aktualisiert den Titel nach der
    // Boundary-Catch-up-Schleife und vor dem Rendern. So kann Auto-Exit nie
    // mit einem vor der letzten Registrierung gebauten Titel schliessen.
    let source = readDocument "src/Riftward.App/Command/CommandLoopRunner.cs"

    let loopStart =
        source.IndexOf("private static InteractiveLoopOutcome RunInteractiveLoop(", StringComparison.Ordinal)

    let boundary =
        source.IndexOf("var outcome = pipeline.ProcessBoundary(tick);", loopStart, StringComparison.Ordinal)

    let hudUpdate =
        source.IndexOf(
            "UpdateTitleHud(window, pipeline, world, exploration, decision, pressure, ref lastTitleState);",
            boundary,
            StringComparison.Ordinal
        )

    let render =
        source.IndexOf("var markerCount = RenderFrame(", hudUpdate, StringComparison.Ordinal)

    if
        loopStart < 0
        || boundary < loopStart
        || hudUpdate < boundary
        || render < hudUpdate
    then
        failwith "Interaktiver Caller bindet das HUD nicht nach Boundary-Catch-up und vor Render."

// ---------------------------------------------------------------------------
// Beobachtungstreue (AC-T034-03): Twin-Kontrolllauf, byteidentische Ketten,
// identische Kernbefehlsfolge; Fremdseed-Negativfall.
// ---------------------------------------------------------------------------

let private twinIntents =
    [| GrayboxIntent(40, GrayboxIntentKind.BoxSelect, 0L, 0L, 159000L, 89000L)
       GrayboxIntent(50, GrayboxIntentKind.GroupMoveToZone, 2L)
       GrayboxIntent(60, GrayboxIntentKind.SwitchMode)
       GrayboxIntent(120, GrayboxIntentKind.SwitchMode) |]

let private runSession seed exploration =
    SessionEngine.Run(
        SessionRunRequest(
            Seed = seed,
            ScriptedIntents = twinIntents,
            WarmupTicks = 30,
            HorizonTicks = 400,
            RunSelfConsistencyPass = false,
            ExplorationEnabled = exploration
        )
    )

let explorationObservationIsObservationOnlyTwinStaysByteIdentical () =
    let twin = runSession 20260826u false
    let activated = runSession 20260826u true

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
        failwith "Die Kernbefehlsfolge des Erkundungslaufs weicht vom Twin ab."

    if
        activated.AppliedIntents <> twin.AppliedIntents
        || activated.RejectedIntents <> twin.RejectedIntents
    then
        failwith "Die Intentdispositionen des Erkundungslaufs weichen vom Twin ab."

    if isNull activated.Exploration then
        failwith "Aktivierter Lauf lieferte keinen Erkundungsausweis."

    if not (isNull twin.Exploration) then
        failwith "Unaktivierter Lauf lieferte einen Erkundungsausweis."

let foreignSeedChangesHashesButNotLandmarkSet () =
    let baseline = runSession 20260826u true
    let foreign = runSession 42u true

    if
        baseline.StartStateHash = foreign.StartStateHash
        || baseline.EndStateHash = foreign.EndStateHash
    then
        failwith "Ein fremder Seed aenderte Start- oder Endhash nicht nachweislich."

    let baseLandmarks = baseline.Exploration.Landmarks
    let foreignLandmarks = foreign.Exploration.Landmarks

    if not (Seq.forall2 (fun a b -> a = b) baseLandmarks foreignLandmarks) then
        failwith "Ein fremder Seed veraenderte die seedunabhaengige Landmarkenmenge."

// ---------------------------------------------------------------------------
// Headless Erkundungs-Flow ueber denselben oeffentlichen Befehl
// (AC-T034-02): vollstaendiger Aufsuchlauf, additiver Report Schemaversion 3,
// Schema- und Bestandsbindung; Exitcode-Erhaltung.
// ---------------------------------------------------------------------------

let private reportJson (path: string) = File.ReadAllText(path)

let private jsonInt (element: JsonElement) (name: string) = element.GetProperty(name).GetInt32()

let headlessExplorationRunVisitsAllLandmarksOnSchemaVersion3 () =
    // Dieselbe versionierte, armeegetrennte Fixture traegt Headless-
    // Determinismus und den echten Display-Playtest. Dadurch ist der
    // visuelle Abnahmelauf auf einem frischen Checkout reproduzierbar und
    // nicht von einem ignorierten lokalen Artefakt abhaengig.
    let scriptPath =
        Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t034-exploration-separated.graybox")

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Exploration-{Guid.NewGuid():N}.json")

    let secondReportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Exploration-{Guid.NewGuid():N}.json")

    let explorationArguments targetReport =
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
           "8000"
           "--exploration"
           "--report"
           targetReport |]

    try
        let exitCode, stdout, stderr =
            runToleratingTransientGate (explorationArguments reportPath)

        if exitCode <> ExitCodes.Ok then
            failwith $"Erkundungslauf endete mit {exitCode}: {stderr} {stdout}"

        let json = reportJson reportPath

        if CommandReportSchema.Validate(json).Count <> 0 then
            failwith "Aktivierter Report widerspricht dem Schemavertrag (Version 3)."

        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        if jsonInt root "schemaVersion" <> 3 then
            failwith "Aktivierter Report traegt nicht die additive Schemaversion 3."

        let exploration = root.GetProperty("explorationSession")
        let progress = exploration.GetProperty("progress")

        if jsonInt progress "landmarkCount" <> NavWorld.ZoneCount then
            failwith "Report-Landmarkenmenge entspricht nicht NavWorld.ZoneCount."

        let visitedCount = jsonInt progress "visitedCount"

        if
            visitedCount <> NavWorld.ZoneCount
            || not (progress.GetProperty("completed").GetBoolean())
        then
            failwith $"Der Headless-Flow suchte nicht saemtliche Landmarken auf ({visitedCount}/{NavWorld.ZoneCount})."

        if progress.GetProperty("gateCoupled").GetBoolean() then
            failwith "Fortschrittsfelder koppeln an ein Gate."

        let persistence = exploration.GetProperty("persistence")

        if
            persistence.GetProperty("statementId").GetString()
            <> ExplorationContract.SaveLoadPersistenceStatementId
            || not (persistence.GetProperty("persisted").GetBoolean())
            || persistence.GetProperty("saveLoad").GetString()
               <> ExplorationContract.SaveLoadContinuation
            || persistence.GetProperty("replay").GetString()
               <> ExplorationContract.ReplayNotContinued
        then
            failwith "Die maschinenlesbare V2-Persistenzaussage fehlt oder widerspricht."

        let landmarks = exploration.GetProperty("landmarks")
        let mutable zone = 0

        for landmark in landmarks.EnumerateArray() do
            if landmark.GetProperty("zoneIndex").GetInt32() <> zone then
                failwith "Landmarken erscheinen nicht in fester Zonenordnung im Report."

            if
                not (landmark.GetProperty("walkable").GetBoolean())
                || not (
                    NavWorld.IsInsideZone(
                        zone,
                        landmark.GetProperty("anchorTileX").GetInt32(),
                        landmark.GetProperty("anchorTileY").GetInt32()
                    )
                )
                || not (
                    NavWorld.IsWalkable(
                        landmark.GetProperty("anchorTileX").GetInt32(),
                        landmark.GetProperty("anchorTileY").GetInt32()
                    )
                )
            then
                failwith $"Reportanker der Zone {zone} widerspricht der Kernelgeometrie."

            zone <- zone + 1

        if zone <> NavWorld.ZoneCount then
            failwith "Report-Landmarkenmenge ist unvollstaendig."

        let protocol = exploration.GetProperty("visitProtocol")
        let mutable order = 1
        let mutable seenZones = 0

        for visit in protocol.EnumerateArray() do
            if visit.GetProperty("visitOrder").GetInt32() <> order then
                failwith "Aufsuchprotokoll traegt nicht die kanonische 1-basierte Registrierungsfolge."

            if visit.GetProperty("mode").GetString() <> ModeContract.ModePersonalId then
                failwith "Eine Registrierung ist nicht an den persoenlichen Modus gekoppelt."

            if visit.GetProperty("gateCoupled").GetBoolean() then
                failwith "Protokollfeld koppelt an ein Gate."

            seenZones <- seenZones ||| (1 <<< visit.GetProperty("zoneIndex").GetInt32())
            order <- order + 1

        if
            protocol.GetArrayLength() <> NavWorld.ZoneCount
            || seenZones <> (1 <<< NavWorld.ZoneCount) - 1
        then
            failwith "Aufsuchprotokoll deckt nicht jede Landmarke genau einmal ab."

        let hud = exploration.GetProperty("hud")

        if hud.GetProperty("measured").GetBoolean() then
            failwith "Headless behauptet einen Titel-HUD-Ausweis."

        if
            hud.GetProperty("kind").GetString() <> ExplorationContract.HudModelId
            || String.IsNullOrEmpty(hud.GetProperty("reason").GetString())
        then
            failwith "Headless HUD-Ausweis fehlt an Grund statt stiller Behauptung."

        let channel = exploration.GetProperty("landmarkChannel")

        if channel.GetProperty("measured").GetBoolean() then
            failwith "Headless behauptet einen Landmarkenzustandskanal."

        if
            channel.GetProperty("kind").GetString()
            <> ExplorationContract.LandmarkChannelModelId
            || String.IsNullOrEmpty(channel.GetProperty("reason").GetString())
        then
            failwith "Headless Kanalausweis fehlt an Grund statt stiller Behauptung."

        if jsonInt root "exitCode" <> ExitCodes.Ok then
            failwith "Report-Exitcode widerspricht der Laufbeobachtung."

        // Zweiter echter App-Prozess: zeit-/messwertabhängige Felder dürfen
        // abweichen, die deterministische Produkt- und Kettenwahrheit muss
        // auf demselben Builderstand byteidentisch bleiben (AC-T034-02).
        let secondExitCode, secondStdout, secondStderr =
            runToleratingTransientGate (explorationArguments secondReportPath)

        if secondExitCode <> ExitCodes.Ok then
            failwith $"Zweiter Erkundungslauf endete mit {secondExitCode}: {secondStderr} {secondStdout}"

        let secondJson = reportJson secondReportPath

        if CommandReportSchema.Validate(secondJson).Count <> 0 then
            failwith "Zweiter aktivierter Report widerspricht dem Schemavertrag."

        use secondDocument = JsonDocument.Parse(secondJson)
        let secondRoot = secondDocument.RootElement

        for blockName in
            [ "scenario"
              "inputScript"
              "modeSession"
              "explorationSession"
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

let legacyRunWithoutExplorationStaysByteIdenticalSchema2 () =
    // Bestandsreport (Schemaversion 2) bleibt ohne Aktivierung gueltig und
    // traegt keinen Erkundungsblock (Vertrag Abschnitt 6).
    let scriptPath = writeTempScript 420

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Legacy-{Guid.NewGuid():N}.json")

    try
        let exitCode, stdout, stderr =
            runToleratingTransientGate
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

        if exitCode <> ExitCodes.Ok then
            failwith $"Bestandslauf endete mit {exitCode}: {stderr} {stdout}"

        let json = reportJson reportPath

        if CommandReportSchema.Validate(json).Count <> 0 then
            failwith "Bestandsreport widerspricht dem Schemavertrag."

        if json.Contains("\"explorationSession\"", StringComparison.Ordinal) then
            failwith "Unaktivierter Report traegt einen Erkundungsblock."

        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        if jsonInt root "schemaVersion" <> 2 then
            failwith "Unaktivierter Report traegt nicht die Bestandsschemaversion 2."

        // Schemaversion-Auswahl 2/3 ist kein Exitcode: identische Intents und
        // identischer Seed ergeben in beiden Varianten denselben Exitcode.
        let explorationExitCode, _, _ =
            runToleratingTransientGate
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
                   "--exploration"
                   "--report"
                   (reportPath + ".exploration") |]

        if explorationExitCode <> exitCode then
            failwith "Die Aktivierung veraenderte die Exitcodebedeutung."

        if File.Exists(reportPath + ".exploration") then
            File.Delete(reportPath + ".exploration")
    finally
        File.Delete(scriptPath)

        if File.Exists(reportPath) then
            File.Delete(reportPath)

let explorationSchemaDispatchRejectsCrossVariants () =
    // Fail-closed: Schemaversion 2 toleriert keinen Erkundungsblock,
    // Schemaversion 3 verlangt ihn vollstaendig.
    let scriptPath = writeTempScript 420

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Schema-{Guid.NewGuid():N}.json")

    try
        let exitCode, _, _ =
            runToleratingTransientGate
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

        if exitCode <> ExitCodes.Ok then
            failwith "Bestandslauf fuer den Schemadispatch schlug fehl."

        let json = reportJson reportPath

        let withBlock =
            json.Replace("\"exitCode\"", "\"explorationSession\":{},\"exitCode\"")

        if CommandReportSchema.Validate(withBlock).Count = 0 then
            failwith "Schemaversion 2 tolerierte einen Erkundungsblock."

        let withoutBlock =
            json
                .Replace("\"schemaVersion\":2", "\"schemaVersion\":3")
                .Replace("\"modeSession\"", "\"explorationSession\":{},\"modeSession\"")

        if
            (CommandReportSchema.Validate(withoutBlock)
             |> Seq.exists (fun error -> error.Contains("explorationSession", StringComparison.Ordinal)))
            |> not
        then
            failwith "Ein Version-3-Report ohne vollstaendigen Block wurde nicht erkannt."
    finally
        File.Delete(scriptPath)

        if File.Exists(reportPath) then
            File.Delete(reportPath)

// ---------------------------------------------------------------------------
// Adversariale NF-007-Bindung: einzeln wohlgeformte Werte duerfen weder die
// Kernelgeometrie noch Protokoll/Fortschritt/Abschluss widersprechen. Dazu
// Early-Quit-/Exception-Ehrlichkeit der additiven Schemaversion 3.
// ---------------------------------------------------------------------------

let explorationSchemaRelationsRejectFabrication () =
    let scriptPath = writeTempScript 420

    let reportPath =
        Path.Combine(Path.GetTempPath(), $"RiftHarness-Exploration-Schema-{Guid.NewGuid():N}.json")

    try
        let exitCode, stdout, stderr =
            runToleratingTransientGate
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
                   "--exploration"
                   "--report"
                   reportPath |]

        if exitCode <> ExitCodes.Ok then
            failwith $"Golden-Erkundungsreport endete mit {exitCode}: {stderr} {stdout}"

        let golden = reportJson reportPath

        if CommandReportSchema.Validate(golden).Count <> 0 then
            failwith "Golden-Erkundungsreport verletzte den relationalen Schemavertrag."

        let explorationOf (root: JsonObject) = root["explorationSession"].AsObject()

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

        reject "Fremder/unbetretbarer Anker" "Ankerkachel" (fun root ->
            let exploration = explorationOf root
            let landmark = (exploration["landmarks"].AsArray()[0]).AsObject()
            landmark["anchorTileX"] <- JsonValue.Create(0)
            landmark["anchorTileY"] <- JsonValue.Create(0))

        reject "Strategischer Scheinbesuch" "persoenlichen Modus" (fun root ->
            let exploration = explorationOf root
            let visit = (exploration["visitProtocol"].AsArray()[0]).AsObject()
            visit["mode"] <- JsonValue.Create(ModeContract.ModeStrategicId))

        reject "Nichtfortlaufende Besuchsreihenfolge" "fortlaufender Wert" (fun root ->
            let exploration = explorationOf root
            let visit = (exploration["visitProtocol"].AsArray()[0]).AsObject()
            visit["visitOrder"] <- JsonValue.Create(2))

        reject "Protokoll-/Zaehlerwiderspruch" "Laenge des Aufsuchprotokolls" (fun root ->
            let exploration = explorationOf root
            let progress = exploration["progress"].AsObject()
            progress["visitedCount"] <- JsonValue.Create(0))

        reject "Falscher Abschluss" "visitedCount == landmarkCount" (fun root ->
            let exploration = explorationOf root
            let progress = exploration["progress"].AsObject()
            progress["completed"] <- JsonValue.Create(true))

        reject "Doppelte Landmarkenzone" "mehrfach registriert" (fun root ->
            let exploration = explorationOf root
            let protocol = exploration["visitProtocol"].AsArray()
            let duplicate = protocol[0].DeepClone().AsObject()
            let previousTick = duplicate["evaluationBoundaryTick"].GetValue<int64>()
            duplicate["evaluationBoundaryTick"] <- JsonValue.Create(previousTick + 1L)
            duplicate["visitOrder"] <- JsonValue.Create(2)
            protocol.Add(duplicate)
            exploration["progress"].AsObject()["visitedCount"] <- JsonValue.Create(2))

        // Builder-Ehrlichkeit ohne echtes Display: Nur ein vollstaendig
        // abgeschlossenes Interaktivfenster darf HUD und Landmarkenkanal als
        // gemessen ausweisen.
        let telemetry = ExplorationSession().ToTelemetry()

        let presentationMeasured execution windowCompleted =
            let source =
                CommandLoopRunner.BuildExplorationSession(execution, windowCompleted, telemetry)

            let block = JsonNode.Parse(JsonSerializer.Serialize(source)).AsObject()

            let hudMeasured = (block["hud"].AsObject()["measured"]).GetValue<bool>()

            let channelMeasured =
                (block["landmarkChannel"].AsObject()["measured"]).GetValue<bool>()

            hudMeasured, channelMeasured

        if
            presentationMeasured CommandReportSchema.ExecutionInteractive true
            <> (true, true)
        then
            failwith "Vollstaendiger Interaktivlauf wies seine Erkundungsdarstellung nicht messend aus."

        if
            presentationMeasured CommandReportSchema.ExecutionInteractive false
            <> (false, false)
        then
            failwith "Early-Quit-Interaktivlauf behauptete eine nicht abgeschlossene Erkundungsdarstellung."

        if
            presentationMeasured CommandReportSchema.ExecutionHeadless true
            <> (false, false)
        then
            failwith "Headless-Lauf behauptete fensterpflichtige Erkundungsdarstellung."

        let preserved = CommandLoopRunner.ResolveIncompleteExploration(true, null)

        if
            isNull preserved
            || preserved.LandmarkCount <> NavWorld.ZoneCount
            || preserved.VisitedCount <> 0
        then
            failwith "Exception-Teilreport verlor die angeforderte Erkundungsaktivierung."

        if not (isNull (CommandLoopRunner.ResolveIncompleteExploration(false, telemetry))) then
            failwith "Teilreport ohne Opt-in erfand einen Erkundungsblock."
    finally
        File.Delete(scriptPath)

        if File.Exists(reportPath) then
            File.Delete(reportPath)

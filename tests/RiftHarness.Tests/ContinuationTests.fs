module ContinuationTests

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
// T-037: kleinster spielbarer Fortsetzungsschritt (Savevertrag V2,
// Abschnitt 13). Jede Pruefung bindet Code, Vertragsdokument, Schemavertrag
// und Laufverhalten gegeneinander; keine Pruefung antwortet auf eine offene
// Produktfrage und keine veraendert Riftward.Simulation.
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

let private reportJson (path: string) =
    if not (File.Exists(path)) then
        failwith $"Report wurde nicht geschrieben: {path}"

    File.ReadAllText(path)

let private jsonInt (element: JsonElement) (name: string) = element.GetProperty(name).GetInt32()

let private jsonInt64 (element: JsonElement) (name: string) = element.GetProperty(name).GetInt64()

let private jsonBool (element: JsonElement) (name: string) = element.GetProperty(name).GetBoolean()

let private jsonString (element: JsonElement) (name: string) = element.GetProperty(name).GetString()

/// Wrapper der C#-Valuetupel-Rueckgabe als F#-Optionen (lesbare Pruefung).
let private decodeSection (bytes: byte[]) : SessionSectionRejection option * SessionSectionState option =
    let struct (rejection, state) = SessionSectionCodec.Decode(bytes)
    (Option.ofObj rejection, Option.ofObj state)

let private validateDocument (bytes: byte[]) : SaveRejection option * LoadedSaveDocument option =
    let struct (rejection, document) = SaveDocumentValidator.Validate(bytes)
    (Option.ofObj rejection, Option.ofObj document)

// ---------------------------------------------------------------------------
// Vollstaendig aktivierte Fortsetzungskette (Vertragsfixture): die gebundene
// T-034/T-035/T-036-Kette mit Fehlschlag, Wiederauffrischung und Erfolg; der
// Speicherlauf endet nach der ersten wirksamen Wahl, der Fortsetzungslauf
// traegt den kompletten Fehlschlags-Neustart-Erfolgspfad.
// ---------------------------------------------------------------------------

let private continuationFixturePath =
    Path.Combine(repositoryRoot, "tests", "fixtures", "command", "t036-pressure-restart.graybox")

let private continuationSeed = "20260826"

let private continuationHorizon = "11000"

let private saveBoundary = "8100"

let private privateTempDir () =
    Path.Combine(Path.GetTempPath(), $"RiftHarness-Continuation-{Guid.NewGuid():N}")

let private saveArguments (slotDir: string) (reportPath: string) =
    [| "kommandoschleife"
       "--scenario"
       "kommando-graybox"
       "--input-script"
       continuationFixturePath
       "--seed"
       continuationSeed
       "--report"
       reportPath
       "--warmup-ticks"
       "240"
       "--horizon-ticks"
       continuationHorizon
       "--slot-dir"
       slotDir
       "--slot"
       "slot-t037.rwsaved"
       "--save-at-tick"
       saveBoundary
       "--exploration"
       "--decision"
       "--pressure" |]

let private loadArguments (slotDir: string) (reportPath: string) =
    [| "kommandoschleife"
       "--scenario"
       "kommando-graybox"
       "--input-script"
       continuationFixturePath
       "--seed"
       continuationSeed
       "--report"
       reportPath
       "--warmup-ticks"
       "240"
       "--horizon-ticks"
       continuationHorizon
       "--slot-dir"
       slotDir
       "--slot"
       "slot-t037.rwsaved"
       "--load-slot"
       "--exploration"
       "--decision"
       "--pressure" |]

// ---------------------------------------------------------------------------
// Sektionscodec: Roundtrip je Schicht, Re-Encoding-Gleichheit, kontrollierte
// Ablehnungsmatrix (AC-T037-02/03-Testmatrix).
// ---------------------------------------------------------------------------

let private populatedSection () : SessionSectionState =
    // Echte Sektionswahrheit in allen vier Ebenen; die Felder entsprechen
    // exakt der Capture-Form der Sitzungsschicht (Savevertrag V2 13.1).
    SessionSectionState(
        ActiveMode = byte SessionMode.Personal,
        PendingSwitches =
            [ SessionSectionPendingSwitch(300L, 302L, byte SessionMode.Strategic, byte SessionMode.Personal) ],
        ExplorationActive = 1uy,
        ExplorationVisits =
            [ SessionSectionVisit(1200L, 2, SessionSectionCodec.ModePersonal)
              SessionSectionVisit(2400L, 0, SessionSectionCodec.ModePersonal) ],
        DecisionActive = 1uy,
        DecisionOfferOpened = 1uy,
        DecisionOfferBoundaryTick = 2600L,
        DecisionOptionZoneA = 0,
        DecisionOptionZoneB = 2,
        DecisionDecided = 1uy,
        DecisionBoundaryTick = 2800L,
        DecisionChoiceKind = SessionSectionCodec.ChoiceKindA,
        DecisionModeKind = 1uy,
        DecisionFollowUpZoneIndex = 0,
        DecisionFollowUpCompleted = 1uy,
        DecisionArrivalBoundaryTick = 3000L,
        DecisionRejectionsBeforeOffer = 2L,
        DecisionRejectionsInStrategicMode = 1L,
        DecisionRejectionsAfterDecision = 0L,
        PressureActive = 1uy,
        PressureCycleCount = 2L,
        PressureWindows =
            [ SessionSectionWindow(
                  1L,
                  1L,
                  2800L,
                  3400L,
                  SessionSectionCodec.EndReasonExpired,
                  -1L,
                  SessionSectionCodec.ArrivalModeNone,
                  SessionSectionCodec.CauseKindWindowExpired
              )
              SessionSectionWindow(
                  2L,
                  2L,
                  3401L,
                  -1L,
                  SessionSectionCodec.EndReasonOpen,
                  -1L,
                  SessionSectionCodec.ArrivalModeNone,
                  SessionSectionCodec.CauseKindNone
              ) ],
        PressureLastFailureBoundaryTick = 3400L,
        PressureHasLastFailure = 1uy,
        PressureLastFailureFollowUpZoneIndex = 0,
        PressureLastReopenBoundaryTick = 3401L,
        PressureReopenPendingRecording = 0uy
    )

let sessionSectionCodecRoundtripIsByteIdenticalPerLayer () =
    let section = populatedSection ()
    let encoded = SessionSectionCodec.Encode(section)
    let (rejection, decoded) = decodeSection encoded

    if rejection.IsSome || decoded.IsNone then
        failwith $"Kanonische Sektion wurde abgewiesen: {rejection}"

    let reencoded = SessionSectionCodec.Encode(decoded.Value)

    if reencoded <> encoded then
        failwith "Re-Encoding der Sektion wich von den Originalbytes ab (Kanonform verletzt)."

    // Ehrliche Leere ist ein vollstaendiger, rueckkodierbarer Zustand.
    let emptyEncoded = SessionSectionCodec.Encode(SessionSectionState.Empty)
    let (emptyRejection, emptyDecoded) = decodeSection emptyEncoded

    if emptyRejection.IsSome || emptyDecoded.IsNone then
        failwith "Ehrliche Sitzungsleere wurde abgewiesen."

    if SessionSectionCodec.Encode(emptyDecoded.Value) <> emptyEncoded then
        failwith "Re-Encoding der Sitzungsleere wich ab."

    // Schichtwahrheit je Ebene.
    if decoded.Value.ActiveMode <> byte SessionMode.Personal then
        failwith "Aktiver Modus wurde nicht bytegetreu erhalten."

    if decoded.Value.PendingSwitches.Count <> 1 then
        failwith "Schwebender Wechsel fehlt in der Sektion."

    if
        decoded.Value.PendingSwitches.[0].IntentTick <> 300L
        || decoded.Value.PendingSwitches.[0].EffectiveBoundaryTick <> 302L
        || decoded.Value.PendingSwitches.[0].PreviousMode <> byte SessionMode.Strategic
        || decoded.Value.PendingSwitches.[0].NewMode <> byte SessionMode.Personal
    then
        failwith "Schwebender Wechsel widerspricht der Same-Tick-Wahrheit."

    if
        decoded.Value.ExplorationVisits.Count <> 2
        || decoded.Value.ExplorationVisits.[1].ZoneIndex <> 0
    then
        failwith "Aufsuchprotokoll wurde nicht in kanonischer Folge erhalten."

    if
        decoded.Value.DecisionChoiceKind <> SessionSectionCodec.ChoiceKindA
        || decoded.Value.DecisionFollowUpZoneIndex <> 0
        || decoded.Value.DecisionArrivalBoundaryTick <> 3000L
    then
        failwith "Entscheidungswahrheit wurde nicht erhalten."

    if
        decoded.Value.PressureWindows.Count <> 2
        || decoded.Value.PressureWindows.[1].EndBoundaryTick <> -1L
        || decoded.Value.PressureHasLastFailure <> 1uy
    then
        failwith "Druck- und Zykluswahrheit wurde nicht erhalten."

let sessionSectionCodecRejectsCorruptionMatrix () =
    // Rohbyteschreiber fuer die Rahmenniveau-Truncationsfaelle: Little-
    // Endian-Festbreiten exakt wie der Sektionscodec.
    let writeI64 (bytes: byte[]) (offset: int) (value: int64) =
        for i in 0..7 do
            bytes.[offset + i] <- byte ((value >>> (8 * i)) &&& 0xFFL)

    let writeI32 (bytes: byte[]) (offset: int) (value: int) =
        for i in 0..3 do
            bytes.[offset + i] <- byte ((value >>> (8 * i)) &&& 0xFF)

    let writePendingSwitch
        (bytes: byte[])
        (offset: int)
        (intent: int64)
        (effective: int64)
        (previous: byte)
        (newMode: byte)
        =
        writeI64 bytes offset intent
        writeI64 bytes (offset + 8) effective
        bytes.[offset + 16] <- previous
        bytes.[offset + 17] <- newMode

    let writeVisit (bytes: byte[]) (offset: int) (boundary: int64) (zone: int) (mode: byte) =
        writeI64 bytes offset boundary
        writeI32 bytes (offset + 8) zone
        bytes.[offset + 12] <- mode

    let writeDecisionEmpty (bytes: byte[]) (offset: int) =
        bytes.[offset] <- 0uy // Entscheidung inaktiv
        bytes.[offset + 1] <- 0uy // Angebot nicht geoeffnet
        writeI64 bytes (offset + 2) -1L
        writeI32 bytes (offset + 10) -1
        writeI32 bytes (offset + 14) -1
        bytes.[offset + 18] <- 0uy // keine Wahl
        writeI64 bytes (offset + 19) -1L
        bytes.[offset + 27] <- SessionSectionCodec.ChoiceKindUnset
        bytes.[offset + 28] <- 0uy
        writeI32 bytes (offset + 29) -1
        bytes.[offset + 33] <- 0uy
        writeI64 bytes (offset + 34) -1L
        writeI64 bytes (offset + 42) 0L
        writeI64 bytes (offset + 50) 0L
        writeI64 bytes (offset + 58) 0L

    let encoded = SessionSectionCodec.Encode(populatedSection ())

    // Sektionsversion unbekannt: kontrollierte Klasse ohne Migrationserfindung.
    let futureVersion = Array.copy encoded
    futureVersion.[0] <- 0x09uy
    futureVersion.[1] <- 0x00uy

    let (futureRejection, _) = decodeSection futureVersion

    if
        futureRejection.IsNone
        || futureRejection.Value.Class <> SessionSectionRejectionClass.Invalid
    then
        failwith "Unbekannte Sektionsversion wurde nicht kontrolliert abgewiesen."

    // Abgeschnittene Sektion: kontrollierte Klasse statt Absturz.
    let truncated = encoded.[0 .. encoded.Length - 9]
    let (truncatedRejection, _) = decodeSection truncated

    if truncatedRejection.IsNone then
        failwith "Abgeschnittene Sektion wurde nicht kontrolliert abgewiesen."

    // Fremde Zone: Referenzverletzung.
    let foreignZone =
        SessionSectionCodec.Encode(
            SessionSectionState(
                ActiveMode = byte SessionMode.Personal,
                PendingSwitches = [],
                ExplorationActive = 1uy,
                ExplorationVisits = [ SessionSectionVisit(1200L, 99, SessionSectionCodec.ModePersonal) ],
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
                PressureReopenPendingRecording = 0uy
            )
        )

    let (foreignRejection, _) = decodeSection foreignZone

    if foreignRejection.IsNone then
        failwith "Fremde Besuchzone wurde nicht kontrolliert abgewiesen."

    // Identische Zonen: Relationsverletzung (Doppelregistrierung).
    let duplicateZone =
        SessionSectionCodec.Encode(
            SessionSectionState(
                ActiveMode = byte SessionMode.Personal,
                PendingSwitches = [],
                ExplorationActive = 1uy,
                ExplorationVisits =
                    [ SessionSectionVisit(1200L, 0, SessionSectionCodec.ModePersonal)
                      SessionSectionVisit(2400L, 0, SessionSectionCodec.ModePersonal) ],
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
                PressureReopenPendingRecording = 0uy
            )
        )

    let (duplicateRejection, _) = decodeSection duplicateZone

    if duplicateRejection.IsNone then
        failwith "Doppelregistrierung derselben Zone wurde nicht abgewiesen."

    // Strategische Registrierung: vertragswidrig (Erkundungsvertrag V2).
    let strategicVisit =
        SessionSectionCodec.Encode(
            SessionSectionState(
                ActiveMode = byte SessionMode.Personal,
                PendingSwitches = [],
                ExplorationActive = 1uy,
                ExplorationVisits = [ SessionSectionVisit(1200L, 0, SessionSectionCodec.ModeStrategic) ],
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
                PressureReopenPendingRecording = 0uy
            )
        )

    let (strategicRejection, _) = decodeSection strategicVisit

    if strategicRejection.IsNone then
        failwith "Strategische Registrierung wurde nicht kontrolliert abgewiesen."

    // Rahmenniveau-Truncation nach der Wechselliste: die Lesestelle des
    // Erkundungskopfs liegt jenseits des Sektionsendes (untrusted Bytes
    // enden kontrolliert, niemals mit einer unkontrollierten Ausnahme).
    let truncatedAfterSwitches = Array.zeroCreate<byte> 115
    truncatedAfterSwitches.[0] <- 1uy // Sektionsversion 1 (Little-Endian u16)

    for i in 0..5 do
        writePendingSwitch truncatedAfterSwitches (7 + i * 18) 0L 2L 0uy 1uy

    let (switchTruncationRejection, _) = decodeSection truncatedAfterSwitches

    if
        switchTruncationRejection.IsNone
        || switchTruncationRejection.Value.Class <> SessionSectionRejectionClass.Invalid
    then
        failwith "Abschneidung nach der Wechselliste wurde nicht kontrolliert abgewiesen."

    // Rahmenniveau-Truncation nach dem Entscheidungsblock: die Lesestelle
    // des Druckkopfs liegt jenseits des Sektionsendes.
    let truncatedAfterDecision = Array.zeroCreate<byte> 117
    truncatedAfterDecision.[0] <- 1uy
    truncatedAfterDecision.[7] <- 1uy // Erkundung aktiv
    truncatedAfterDecision.[8] <- 3uy // drei gueltige Registrierungen (13 Bytes je)

    for i in 0..2 do
        writeVisit truncatedAfterDecision (12 + i * 13) (int64 (i + 1)) i SessionSectionCodec.ModePersonal

    writeDecisionEmpty truncatedAfterDecision 51 // konsistenter Block ohne Angebot/Wahl

    let (decisionTruncationRejection, _) = decodeSection truncatedAfterDecision

    if
        decisionTruncationRejection.IsNone
        || decisionTruncationRejection.Value.Class <> SessionSectionRejectionClass.Invalid
    then
        failwith "Abschneidung nach dem Entscheidungsblock wurde nicht kontrolliert abgewiesen."

    // Fenster-/Instanzkonsistenz: eine offene Instanz, der geschlossene
    // Instanzen nachfolgen, ist keine echte Sitzungswahrheit.
    let openWindowNotLast =
        SessionSectionCodec.Encode(
            SessionSectionState(
                ActiveMode = byte SessionMode.Personal,
                PendingSwitches =
                    [ SessionSectionPendingSwitch(300L, 302L, byte SessionMode.Strategic, byte SessionMode.Personal) ],
                ExplorationActive = 1uy,
                ExplorationVisits =
                    [ SessionSectionVisit(1200L, 2, SessionSectionCodec.ModePersonal)
                      SessionSectionVisit(2400L, 0, SessionSectionCodec.ModePersonal) ],
                DecisionActive = 1uy,
                DecisionOfferOpened = 1uy,
                DecisionOfferBoundaryTick = 2600L,
                DecisionOptionZoneA = 0,
                DecisionOptionZoneB = 2,
                DecisionDecided = 1uy,
                DecisionBoundaryTick = 2800L,
                DecisionChoiceKind = SessionSectionCodec.ChoiceKindA,
                DecisionModeKind = 1uy,
                DecisionFollowUpZoneIndex = 0,
                DecisionFollowUpCompleted = 1uy,
                DecisionArrivalBoundaryTick = 3000L,
                DecisionRejectionsBeforeOffer = 2L,
                DecisionRejectionsInStrategicMode = 1L,
                DecisionRejectionsAfterDecision = 0L,
                PressureActive = 1uy,
                PressureCycleCount = 2L,
                PressureWindows =
                    [ SessionSectionWindow(
                          1L,
                          1L,
                          2800L,
                          -1L,
                          SessionSectionCodec.EndReasonOpen,
                          -1L,
                          SessionSectionCodec.ArrivalModeNone,
                          SessionSectionCodec.CauseKindNone
                      )
                      SessionSectionWindow(
                          2L,
                          2L,
                          3401L,
                          4001L,
                          SessionSectionCodec.EndReasonSuccess,
                          3900L,
                          SessionSectionCodec.ArrivalModePersonal,
                          SessionSectionCodec.CauseKindNone
                      ) ],
                PressureLastFailureBoundaryTick = 3400L,
                PressureHasLastFailure = 0uy,
                PressureLastFailureFollowUpZoneIndex = -1,
                PressureLastReopenBoundaryTick = 3401L,
                PressureReopenPendingRecording = 0uy
            )
        )

    let (openWindowRejection, _) = decodeSection openWindowNotLast

    if openWindowRejection.IsNone then
        failwith "Eine offene Fensterinstanz vor geschlossenen wurde nicht abgewiesen."

// ---------------------------------------------------------------------------
// Umschlag V2 und V1-Kompatibilitaet: Dokumentdispatch, ehrliche
// Sitzungsleere, strikte Versionsmonotonie (AC-T037-02/03).
// ---------------------------------------------------------------------------

let private buildSimState (seed: uint32) (ticks: int) =
    let world = SimWorld(seed)
    let plan = CommandPlan.Generate(seed, ticks)
    let mutable planIndex = 0

    while world.TickIndex < int64 ticks do
        let firstDue = planIndex

        while planIndex < Array.length plan && int64 plan.[planIndex].Tick <= world.TickIndex do
            planIndex <- planIndex + 1

        if planIndex > firstDue then
            world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue)) |> ignore

        world.Tick()

    (SimulationSaveAdapter.Capture(world), world.ComputeStateHash())

let v2EnvelopeRoundtripAndLegacyV1Emptiness () =
    let (state, stateHash) = buildSimState 20260824u 900

    let metadata =
        SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))

    let section = SessionSectionCodec.Encode(populatedSection ())

    let v2Document =
        CanonicalSaveCodec.WriteDocumentV2(state, stateHash, 42UL, "test", metadata, section)

    let (v2Rejection, v2Loaded) = validateDocument v2Document

    if v2Rejection.IsSome || v2Loaded.IsNone then
        failwith $"V2-Dokument wurde abgewiesen: {v2Rejection}"

    if v2Loaded.Value.SaveSchemaVersion <> 2us then
        failwith "V2-Dokument traegt nicht die Schemaversion 2."

    if v2Loaded.Value.FromLegacyV1Document then
        failwith "V2-Dokument wurde als Legacy fehlgekennzeichnet."

    if v2Loaded.Value.SessionSection.ExplorationVisits.Count <> 2 then
        failwith "Sektion des V2-Dokuments traegt nicht die Besuchsprotokollwahrheit."

    // V1-Kompatibilitaet (Savevertrag V2 Abschnitt 13.5): das Legacydokument
    // laedt unveraendert mit ehrlicher, maschinenlesbarer Sitzungsleere.
    let v1Document =
        CanonicalSaveCodec.WriteDocumentV1(state, stateHash, 42UL, "test", metadata)

    let (v1Rejection, v1Loaded) = validateDocument v1Document

    if v1Rejection.IsSome || v1Loaded.IsNone then
        failwith $"V1-Dokument wurde nach der V2-Erweiterung abgewiesen: {v1Rejection}"

    if v1Loaded.Value.SaveSchemaVersion <> 1us then
        failwith "Legacydokument traegt nicht seine Schemaversion 1."

    if not v1Loaded.Value.FromLegacyV1Document then
        failwith "Legacydokument wurde nicht als ehrliche Sitzungsleere gekennzeichnet."

    if
        v1Loaded.Value.SessionSection.ExplorationVisits.Count <> 0
        || v1Loaded.Value.SessionSection.PressureCycleCount <> 0L
        || v1Loaded.Value.SessionSection.DecisionActive <> 0uy
    then
        failwith "Die ehrliche Sitzungsleere des Legacydokuments ist nicht leer."

    // Strikte Monotonie: Version 0 und 3 bleiben ohne Migrationserfindung
    // abgewiesen.
    let mutated (version: int) =
        let copy = Array.copy v1Document
        copy.[SaveContract.MagicLength] <- byte version
        copy.[SaveContract.MagicLength + 1] <- byte (version >>> 8)
        copy

    for version in [ 0; 3 ] do
        let (rejection, _) = validateDocument (mutated version)

        if
            rejection.IsNone
            || rejection.Value.Class <> SaveRejectionClass.SchemaVersionUnsupported
        then
            failwith $"Schemaversion {version} wurde nicht ohne Migrationserfindung abgewiesen."

    // Migrator: unterstuetzte Versionen sind identische No-ops; Zukuenftiges
    // bleibt abgewiesen.
    let productOutcome = SaveMigrator.Product.MigrateToCurrentVersionOnCopy(v1Document)

    if
        not productOutcome.Success
        || productOutcome.AppliedSteps.Count <> 0
        || productOutcome.MigratedBytes <> v1Document
    then
        failwith "Legacy-V1 ist keine identische No-op-Erreichbarkeit des Migrators."

    let futureOutcome = SaveMigrator.Product.MigrateToCurrentVersionOnCopy(mutated 3)

    if
        futureOutcome.Success
        || isNull futureOutcome.Rejection
        || futureOutcome.Rejection.Class <> SaveRejectionClass.SchemaVersionUnsupported
    then
        failwith "Zukuenftige Version wurde vom Migrator nicht ohne Erfindung abgewiesen."

let untrustedSlotActivationGuardsRejectMismatch () =
    let (state, stateHash) = buildSimState 20260824u 600
    let section = SessionSectionCodec.Encode(SessionSectionState.Empty)

    let metadata =
        SaveEnvelopeMetadata.CreateFresh(DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))

    let slotDir = privateTempDir ()
    Directory.CreateDirectory(slotDir) |> ignore

    try
        let document =
            CanonicalSaveCodec.WriteDocumentV2(state, stateHash, 0UL, "test", metadata, section)

        let store = SlotStore(slotDir)
        let write = store.WriteSlotAtomic("slot-guards.rwsaved", document)

        if not write.Success then
            failwith $"Atomarer Slot-Schreibvorgang scheiterte: {write.Error}"

        // V2-Slot wird bei passendem Seed akzeptiert.
        let struct (accepted, acceptanceRejection) =
            ContinuationRunner.LoadSlot(slotDir, "slot-guards.rwsaved", 20260824u)

        if not (isNull acceptanceRejection) || isNull accepted then
            failwith $"Passender Slot wurde abgewiesen: {acceptanceRejection}"

        // Fremder Seed: unterscheidbare Ablehnung ohne Aktivierung.
        let struct (foreignSeedCapture, foreignSeedRejection) =
            ContinuationRunner.LoadSlot(slotDir, "slot-guards.rwsaved", 777u)

        if not (isNull foreignSeedCapture) then
            failwith "Fremdseed-Slot wurde aktiviert."

        if
            isNull foreignSeedRejection
            || foreignSeedRejection.Reason <> SaveContract.RejectionForeignSeed
        then
            failwith "Fremdseed-Ablehnung traegt nicht die vertragliche Kennung."

        // Fehlender Slot: unterscheidbare Ablehnung.
        let struct (missingCapture, missingRejection) =
            ContinuationRunner.LoadSlot(slotDir, "slot-fehlt.rwsaved", 20260824u)

        if not (isNull missingCapture) || isNull missingRejection then
            failwith "Fehlender Slot wurde nicht kontrolliert abgewiesen."
    finally
        try
            Directory.Delete(slotDir, true)
        with :? IOException ->
            ()

// ---------------------------------------------------------------------------
// Kettenfortsetzung ueber die Vorgrenze: Speichererfassung, frische
// Wiederherstellung, byteidentische Fortsetzungskette (AC-T037-02).
// ---------------------------------------------------------------------------

let private continuationIntents () =
    InputScriptParser.Parse(File.ReadAllBytes(continuationFixturePath), ScriptWindowRules(240, 11000))

let private freshCapture (seed: uint32) (boundary: int64) =
    SessionEngine.RunWithSaveBoundary(
        SessionRunRequest(
            Seed = seed,
            ScriptedIntents = (continuationIntents ()).Intents,
            WarmupTicks = 240,
            HorizonTicks = 11000,
            RunSelfConsistencyPass = true,
            ExplorationEnabled = true,
            DecisionEnabled = true,
            PressureEnabled = true
        ),
        boundary
    )

let headlessContinuationChainIsByteIdenticalOverBoundary () =
    let struct (saveResult, capture) = freshCapture 20260826u 8100L

    if saveResult.StateChainSelfConsistent <> Nullable true then
        failwith "Der Speicherlauf verletzte seine eigene Kettenkonsistenz."

    if capture.BoundaryTick <> 8100L then
        failwith "Die Speichervorgrenze weicht vom angeforderten Tick ab."

    // Frische Wiederherstellung und Fortsetzung bis zum Horizont.
    let continuation =
        SessionEngine.RunFromSessionSave(
            SessionRunRequest(
                Seed = 20260826u,
                ScriptedIntents = (continuationIntents ()).Intents,
                WarmupTicks = 240,
                HorizonTicks = 11000,
                RunSelfConsistencyPass = true,
                ExplorationEnabled = true,
                DecisionEnabled = true,
                PressureEnabled = true
            ),
            capture
        )

    if not continuation.ChainContinuityVerified then
        failwith $"Fortsetzungskette wich von der unterbrochenen Referenz ab: {continuation.ContinuityReasons}"

    if continuation.ComparedSampleCount <= 0 then
        failwith "Die Kettenfortsetzung verglich keine Stichproben."

    // Restaurierte Kettenwahrheit: Modus, Erkundung, Entscheidung, Druck.
    let section = capture.Session

    // Moduswahrheit der gebundenen Kette: der Wechsel an Vorgrenze 2642
    // stellt den persoenlichen Modus wieder her; die Wahl an 8000 ist
    // persoenlich wirksam.
    if section.ActiveMode <> byte SessionMode.Personal then
        failwith $"Die Sektion traegt an der Speichervorgrenze nicht den wirksamen Modus ({section.ActiveMode})."

    if section.DecisionDecided <> 1uy || section.DecisionFollowUpCompleted <> 0uy then
        failwith "Die Entscheidungswahrheit an der Speichervorgrenze ist verletzt."

    if section.PressureCycleCount <> 1L then
        failwith "Die Zykluswahrheit an der Speichervorgrenze ist verletzt."

    // Druck fortsetzt den Fehlschlag-Neustart-Erfolgspfad im geladenen
    // Prozess: genau drei Fensterinstanzen (erste Wahl, Fehlschlag,
    // zweite Wahl mit Erfolg).
    if isNull continuation.Result.Pressure then
        failwith "Fortsetzungslauf lieferte keinen Druckausweis."

    let pressure = continuation.Result.Pressure

    if pressure.CycleCount <> 2L then
        failwith $"Der Fortsetzungslauf fuehrte nicht zu Zyklus 2 ({pressure.CycleCount})."

    if pressure.EndStatus <> PressureContract.EndStatusSuccess then
        failwith $"Der Fortsetzungspfad endete nicht im Erfolg ({pressure.EndStatus})."

let foreignSeedChangesHashesButContinuityHolds () =
    let struct (baselineSave, _) = freshCapture 20260826u 8100L
    let struct (foreignSave, foreignCapture) = freshCapture 424242u 8100L

    if baselineSave.StartStateHash = foreignSave.StartStateHash then
        failwith "Fremdseed liess den Starthash unveraendert."

    if baselineSave.EndStateHash = foreignSave.EndStateHash then
        failwith "Fremdseed liess den Endhash des Speicherlaufs unveraendert."

    let continuation =
        SessionEngine.RunFromSessionSave(
            SessionRunRequest(
                Seed = 424242u,
                ScriptedIntents = (continuationIntents ()).Intents,
                WarmupTicks = 240,
                HorizonTicks = 11000,
                RunSelfConsistencyPass = true,
                ExplorationEnabled = true,
                DecisionEnabled = true,
                PressureEnabled = true
            ),
            foreignCapture
        )

    if not continuation.ChainContinuityVerified then
        failwith $"Fremdseed-Fortsetzung wich von ihrer Referenz ab: {continuation.ContinuityReasons}"

    // Die restaurierte Kettenwahrheit folgt ausschliesslich der Sektion:
    // Modus, Besuchszonen und Zykluszahl sind unveraendert restauriert.
    if foreignCapture.Session.PressureCycleCount <> 1L then
        failwith "Die restaurierte Zykluswahrheit folgt nicht der Sektion."

// ---------------------------------------------------------------------------
// CLI-Vertrag: Speicherlauf und Fortsetzungslauf als unabhaengige
// Fresh-Prozesse; Schemaversion 6, restaurierte Kettenwahrheit, builder-
// identische Fresh-Prozesspaare (AC-T037-02/05).
// ---------------------------------------------------------------------------

let private freshProcessPair (slotDir: string) (saveReport: string) (loadReport: string) =
    let saveExit, _, saveStderr = runAppHost (saveArguments slotDir saveReport)

    if saveExit <> ExitCodes.Ok then
        failwith $"Speicherlauf endete mit {saveExit}: {saveStderr}"

    let loadExit, _, loadStderr = runAppHost (loadArguments slotDir loadReport)

    if loadExit <> ExitCodes.Ok then
        failwith $"Fortsetzungslauf endete mit {loadExit}: {loadStderr}"

let cliContinuationFlowRunsSaveAndLoadOnSchemaVersion6 () =
    let slotDir = privateTempDir ()
    Directory.CreateDirectory(slotDir) |> ignore

    let saveReport = Path.Combine(slotDir, "save-report.json")
    let loadReport = Path.Combine(slotDir, "load-report.json")

    try
        freshProcessPair slotDir saveReport loadReport

        let saveJson = reportJson saveReport

        if CommandReportSchema.Validate(saveJson).Count <> 0 then
            failwith "Speicherlauf-Report widerspricht dem Schemavertrag (Version 6)."

        use saveDocument = JsonDocument.Parse(saveJson)
        let saveRoot = saveDocument.RootElement

        if saveRoot.GetProperty("schemaVersion").GetInt32() <> 6 then
            failwith "Speicherlauf traegt nicht die additive Schemaversion 6."

        let saveContinuation = saveRoot.GetProperty("continuation")

        if
            jsonString saveContinuation "runKind" <> "save"
            || jsonInt64 saveContinuation "saveBoundaryTick" <> 8100L
            || not (jsonBool saveContinuation "slotWritten")
            || jsonString saveContinuation "replay" <> "not-continued"
        then
            failwith "Der Speicherlauf-Report bindet nicht die Speicherwahrheit."

        // Fortsetzungslauf: vollstaendig validierter Slot, restaurierte
        // Kettenwahrheit, byteidentische Fortsetzungskette.
        let loadJson = reportJson loadReport

        if CommandReportSchema.Validate(loadJson).Count <> 0 then
            failwith "Fortsetzungslauf-Report widerspricht dem Schemavertrag (Version 6)."

        use loadDocument = JsonDocument.Parse(loadJson)
        let loadRoot = loadDocument.RootElement

        if loadRoot.GetProperty("schemaVersion").GetInt32() <> 6 then
            failwith "Fortsetzungslauf traegt nicht die additive Schemaversion 6."

        let continuation = loadRoot.GetProperty("continuation")

        if
            jsonString continuation "runKind" <> "load"
            || not (jsonBool continuation "loadAccepted")
            || jsonInt64 continuation "loadBoundaryTick" <> 8100L
            || jsonBool continuation "fromLegacyV1Document"
        then
            failwith "Der Fortsetzungslauf bindet nicht die Ladewahrheit."

        if not (jsonBool (continuation.GetProperty("chainContinuity")) "verified") then
            failwith "Die Kettenfortsetzung des CLI-Laufs ist nicht byteidentisch."

        let restored = continuation.GetProperty("restored")

        if
            jsonString restored "mode" <> ModeContract.ModePersonalId
            || not (jsonBool (restored.GetProperty("decision")) "decided")
            || jsonBool (restored.GetProperty("decision")) "followUpCompleted"
            || jsonInt64 (restored.GetProperty("pressure")) "cycleCount" <> 1L
        then
            failwith "Die restaurierte Kettenwahrheit widerspricht dem Speicherzustand."

        // Druckfortsetzung: der geladene Prozess faehrt den
        // Fehlschlag-Neustart-Erfolgspfad und endet im Erfolg.
        let pressure = loadRoot.GetProperty("pressureSession")

        if
            jsonInt64 pressure "cycleCount" <> 2L
            || pressure.GetProperty("endStatus").GetProperty("status").GetString()
               <> PressureContract.EndStatusSuccess
        then
            failwith "Der Fortsetzungslauf fuehrte den Fehlschlag-Neustart-Erfolgspfad nicht aus."

        // Schichtbloecke erscheinen genau mit ihrer Aktivierung; die
        // Persistenzpraezisierung traegt die V2-/V3-Wahrheit.
        let explorationPersistence =
            loadRoot.GetProperty("explorationSession").GetProperty("persistence")

        if
            explorationPersistence.GetProperty("statementId").GetString()
            <> ExplorationContract.SaveLoadPersistenceStatementId
        then
            failwith "Der Fortsetzungslauf bindet nicht die V2-Persistenzwahrheit der Erkundung."
    finally
        try
            Directory.Delete(slotDir, true)
        with :? IOException ->
            ()

let cliContinuationPairsAreBuilderIdentical () =
    let runPair () =
        let slotDir = privateTempDir ()
        Directory.CreateDirectory(slotDir) |> ignore

        let saveReport = Path.Combine(slotDir, "save-a.json")
        let loadReport = Path.Combine(slotDir, "load-a.json")

        try
            freshProcessPair slotDir saveReport loadReport
            (reportJson saveReport, reportJson loadReport)
        finally
            try
                Directory.Delete(slotDir, true)
            with :? IOException ->
                ()

    let (saveA, loadA) = runPair ()
    let (saveB, loadB) = runPair ()

    let chainOf (json: string) =
        use document = JsonDocument.Parse(json)
        let root = document.RootElement
        let chain = root.GetProperty("stateHashChain")
        (chain.GetProperty("start").GetString(), chain.GetProperty("end").GetString())

    if chainOf saveA <> chainOf saveB then
        failwith "Zwei Fresh-Speicherläufe waren nicht builderidentisch."

    if chainOf loadA <> chainOf loadB then
        failwith "Zwei Fresh-Fortsetzungsläufe waren nicht builderidentisch."

    let continuationTruthOf (json: string) =
        use document = JsonDocument.Parse(json)
        let continuation = document.RootElement.GetProperty("continuation")
        let chain = continuation.GetProperty("chainContinuity")

        (jsonInt64 continuation "loadBoundaryTick",
         jsonString (continuation.GetProperty("restored")) "mode",
         chain.GetProperty("continuationEndHash").GetString(),
         chain.GetProperty("referenceEndHash").GetString())

    if continuationTruthOf loadA <> continuationTruthOf loadB then
        failwith "Die Fortsetzungsidentitaet der Fresh-Prozesspaare weicht ab."

// ---------------------------------------------------------------------------
// Ablehnungsmatrix der CLI: unpassende Slots enden kontrolliert unvollständig
// ohne neue Exitcodebedeutung (AC-T037-03/05).
// ---------------------------------------------------------------------------

let cliContinuationRejectionsStayControlledAndExitCodesStable () =
    let slotDir = privateTempDir ()
    Directory.CreateDirectory(slotDir) |> ignore

    let loadReport = Path.Combine(slotDir, "rejected-report.json")

    try
        // Fehlender Slot: Fortsetzungslauf endet kontrolliert mit Code 36.
        let missingExit, _, _ = runAppHost (loadArguments slotDir loadReport)

        if missingExit <> ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete) then
            failwith $"Fehlender Slot ergab {missingExit} statt kontrolliertem Code 36."

        let missingJson = reportJson loadReport

        if CommandReportSchema.Validate(missingJson).Count <> 0 then
            failwith "Ablehnungs-Teilreport widerspricht dem Schemavertrag (Version 6)."

        use missingDocument = JsonDocument.Parse(missingJson)
        let missingRoot = missingDocument.RootElement

        if missingRoot.GetProperty("exitCode").GetInt32() <> missingExit then
            failwith "Ablehnungs-Teilreport bindet nicht seinen Exitcode."

        let missingContinuation = missingRoot.GetProperty("continuation")

        if missingContinuation.GetProperty("rejection").ValueKind <> JsonValueKind.Object then
            failwith "Die Ablehnung ist nicht maschinenlesbar gebunden."

        if missingRoot.GetProperty("gate").GetProperty("pass").GetBoolean() then
            failwith "Ein abgewiesener Lauf wurde als Gatepass markiert."

        if missingRoot.GetProperty("measurement").GetProperty("windowCompleted").GetBoolean() then
            failwith "Ein abgewiesener Lauf wurde als vollständiges Fenster markiert."

        // Fremdseed am CLI: kontrollierte Ablehnung mit vertraglicher
        // Kennung statt stiller Aktivierung. Der Slot existiert aus dem
        // Speicherlauf mit dem Vertragssseed; der Fremdseed-Lauf widerspricht
        // ihm und wird vor Aktivierung abgewiesen.
        let saveOk, _, saveStderr =
            runAppHost (saveArguments slotDir (Path.Combine(slotDir, "seed-save.json")))

        if saveOk <> ExitCodes.Ok then
            failwith $"Speicherlauf des Fremdseed-Falls endete mit {saveOk}: {saveStderr}"

        let foreignArguments =
            loadArguments slotDir (Path.Combine(slotDir, "foreign-report.json"))
            |> Array.map (fun argument -> if argument = continuationSeed then "424242" else argument)

        let foreignExit, _, _ = runAppHost foreignArguments

        if foreignExit <> ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete) then
            failwith $"Fremdseed ergab {foreignExit} statt kontrolliertem Code 36."

        let foreignJson = reportJson (Path.Combine(slotDir, "foreign-report.json"))

        use foreignDocument = JsonDocument.Parse(foreignJson)

        let foreignRejection =
            foreignDocument.RootElement.GetProperty("continuation").GetProperty("rejection")

        if jsonString foreignRejection "reason" <> SaveContract.RejectionForeignSeed then
            failwith "Fremdseed-Ablehnung traegt nicht die vertragliche Kennung."
    finally
        try
            Directory.Delete(slotDir, true)
        with :? IOException ->
            ()

// ---------------------------------------------------------------------------
// Vertragsbindung: Savevertrag V2 dokumentiert die Aktivierungs- und
// Sektionswahrheit; das Kommandovertragsdokument bleibt byteidentisch.
// ---------------------------------------------------------------------------

let saveContractDocumentsBindContinuationTruth () =
    let savevertrag = readDocument "docs/SAVEVERTRAG.md"

    for anchor in
        [ "opt-in-continuation-flags-v2"
          "opt-in-interactive-slot-actions-v2"
          "save-slot"
          "load-slot"
          "session-section-full-state-v1"
          "SESSION_SECTION_INTEGRITY_VIOLATION"
          "SESSION_SECTION_INVALID"
          "legacy-v1-session-emptiness-v2"
          "untrusted-slot-activation-guards-v2"
          "session-section-persisted-in-save-load-with-explicit-replay-exception-t037"
          "Schemaversion 6" ] do
        if not (savevertrag.Contains(anchor, StringComparison.Ordinal)) then
            failwith $"Savevertrag V2 nennt den Anker {anchor} nicht."

    // Kommandovertrag bleibt byteidentisch: die Slot-Aktionen sind im
    // Savevertrag V2 dokumentiert, nicht als Kommandovertragsänderung.
    if readDocument("docs/KOMMANDOVERTRAG.md").Contains("save-slot", StringComparison.Ordinal) then
        failwith "Der Kommandovertrag wurde geändert; die Slot-Aktionen leben im Savevertrag V2."

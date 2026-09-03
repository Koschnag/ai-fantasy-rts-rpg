namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

/// Ein Laufkandidat der Retention-Vorschau.
type RetentionCandidate =
    { RunId: string
      Status: string
      FinishedAtUtc: string option
      ExpiresAtUtc: string option
      HistoryProof: string option
      Deletable: bool
      Reasons: string list }

/// Read-only Vorschau einer Retention-Entscheidung (Dry-Run).
type RetentionPlan =
    { GeneratedAtUtc: string
      RetentionDays: int
      Candidates: RetentionCandidate list }

/// Nachweis einer ausgefuehrten, bestätigten Bereinigung.
type RetentionExecutionReceipt =
    { DeletedRunIds: string list
      ConsideredCount: int
      PlanSha256: string
      ExecutedAtUtc: string }

[<RequireQualifiedAccess>]
module Retention =

    // logging.rawRunRetentionDays ist in Harness v1 fest auf 180 Tage gepinnt.
    [<Literal>]
    let RawRunRetentionDays = 180

    let private terminalStatuses = set [ "succeeded"; "failed"; "cancelled" ]

    let private historyDirectory (locations: WorkspacePaths) =
        Path.Combine(locations.State, "history", "accepted")

    let private historyProofsFor (locations: WorkspacePaths) (runId: string) =
        let directory = historyDirectory locations

        if not (Directory.Exists(directory)) then
            []
        else
            Directory.EnumerateFiles(directory, "*.md")
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
            |> Seq.choose (fun file ->
                try
                    let info = FileInfo(file)

                    if isNull info.LinkTarget && info.Length <= Constants.MaxPayloadBytes then
                        let text = File.ReadAllText(file, Constants.Utf8NoBom)

                        if text.IndexOf(runId, StringComparison.Ordinal) >= 0 then
                            Some(Path.GetFileName(file))
                        else
                            None
                    else
                        None
                with _ ->
                    None)
            |> Seq.toList

    let private candidateFor root (locations: WorkspacePaths) (nowUtc: DateTimeOffset) (runId: string) =
        try
            let metadata = RunStore.metadataOf root runId

            match metadata.Status with
            | status when not (terminalStatuses.Contains(status)) ->
                { RunId = runId
                  Status = status
                  FinishedAtUtc = None
                  ExpiresAtUtc = None
                  HistoryProof = None
                  Deletable = false
                  Reasons = [ "Der Lauf ist nicht gueltig abgeschlossen." ] }
            | _ ->
                let finishedText =
                    metadata.FinishedAtUtc
                    |> Option.defaultWith (fun () -> Internal.fail "Abgeschlossener Lauf hat kein finishedAtUtc.")

                let finished =
                    match Internal.tryParseUtc finishedText with
                    | Some value -> value
                    | None -> Internal.fail "Abschlusszeitpunkt des Laufs ist ungueltig."

                let expiresAt = finished.AddDays(float RawRunRetentionDays)
                let expired = expiresAt <= nowUtc.ToUniversalTime()
                let verifyErrors = RunStore.verifyRun root runId
                let proofs = historyProofsFor locations runId
                let proof = proofs |> List.tryHead

                let reasons = ResizeArray<string>()

                if not expired then
                    reasons.Add(
                        $"Aufbewahrungsfrist von {RawRunRetentionDays} Tagen endet erst am {Internal.utcText expiresAt}."
                    )

                if not (List.isEmpty verifyErrors) then
                    reasons.Add("Die Abschlusspruefung des Laufs ist fehlgeschlagen.")

                if proof.IsNone then
                    reasons.Add("Kein akzeptierter bereinigter Bericht referenziert diesen Lauf.")

                { RunId = runId
                  Status = metadata.Status
                  FinishedAtUtc = Some finishedText
                  ExpiresAtUtc = Some(Internal.utcText expiresAt)
                  HistoryProof = proof
                  Deletable = reasons.Count = 0
                  Reasons = reasons |> Seq.toList }
        with
        | HarnessException message ->
            { RunId = runId
              Status = "unlesbar"
              FinishedAtUtc = None
              ExpiresAtUtc = None
              HistoryProof = None
              Deletable = false
              Reasons = [ message ] }
        | error ->
            { RunId = runId
              Status = "unlesbar"
              FinishedAtUtc = None
              ExpiresAtUtc = None
              HistoryProof = None
              Deletable = false
              Reasons = [ error.Message ] }

    let private computePlan root (nowUtc: DateTimeOffset) =
        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore

        let candidates =
            RunStore.allRunIds root |> List.map (candidateFor root locations nowUtc)

        { GeneratedAtUtc = Internal.utcText nowUtc
          RetentionDays = RawRunRetentionDays
          Candidates = candidates }

    let private writeOptionalString (name: string) (value: string option) (writer: Utf8JsonWriter) =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull(name)

    let private planCoreBytes (plan: RetentionPlan) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            writer.WriteStartArray("candidates")

            for candidate in plan.Candidates do
                writer.WriteStartObject()
                writer.WriteBoolean("deletable", candidate.Deletable)

                writeOptionalString "expiresAtUtc" candidate.ExpiresAtUtc writer

                writeOptionalString "finishedAtUtc" candidate.FinishedAtUtc writer

                writeOptionalString "historyProof" candidate.HistoryProof writer

                writer.WriteStartArray("reasons")

                for reason in candidate.Reasons do
                    writer.WriteStringValue(reason)

                writer.WriteEndArray()
                writer.WriteString("runId", candidate.RunId)
                writer.WriteString("status", candidate.Status)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteNumber("deletableCount", (plan.Candidates |> List.filter (fun c -> c.Deletable)).Length)
            writer.WriteString("generatedAtUtc", plan.GeneratedAtUtc)
            writer.WriteNumber("retentionDays", plan.RetentionDays)
            writer.WriteEndObject())

    /// Erzeugt die read-only Vorschau-Plandatei; Rueckgabe: Dateibytes und Planhash.
    let planBytes root (nowUtc: DateTimeOffset) : byte array * string =
        let plan = computePlan root nowUtc
        let core = planCoreBytes plan
        let planHash = Internal.sha256Hex core

        let fileBytes =
            Internal.jsonBytes true (fun writer ->
                writer.WriteStartObject()
                writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
                writer.WriteString("planSha256", planHash)
                Internal.rawJson writer "plan" core
                writer.WriteEndObject())

        fileBytes, planHash

    let private requestedRunIds (coreElement: JsonElement) =
        let candidatesElement = Internal.requiredProperty "candidates" coreElement

        if candidatesElement.ValueKind <> JsonValueKind.Array then
            Internal.fail "Retention-Plan: candidates muss ein Array sein."

        candidatesElement.EnumerateArray()
        |> Seq.choose (fun item ->
            if item.ValueKind <> JsonValueKind.Object then
                Internal.fail "Retention-Plan: Kandidat muss ein Objekt sein."

            let deletable = (Internal.requiredProperty "deletable" item).GetBoolean()
            let runId = Internal.requiredString "runId" item

            if deletable then Some runId else None)
        |> Seq.toList

    let private parsePlanFile path =
        use document =
            JsonDocument.Parse(Internal.safeReadAllText path Constants.MaxPayloadBytes)

        let rootElement = document.RootElement

        if rootElement.ValueKind <> JsonValueKind.Object then
            Internal.fail "Retention-Plan muss ein JSON-Objekt sein."

        for property in rootElement.EnumerateObject() do
            match property.Name with
            | "plan"
            | "planSha256"
            | "schemaVersion" -> ()
            | other -> Internal.fail $"Retention-Plan enthaelt das unerlaubte Feld '{other}'."

        if Internal.requiredInt "schemaVersion" rootElement <> Constants.SchemaVersion then
            Internal.fail "Retention-Plan hat eine nicht unterstuetzte Schema-Version."

        let storedHash = Internal.requiredString "planSha256" rootElement

        if not (Internal.isSha256 storedHash) then
            Internal.fail "Retention-Plan enthaelt einen ungueltigen planSha256."

        let coreElement = Internal.requiredProperty "plan" rootElement

        let rawCore = Constants.Utf8NoBom.GetBytes(coreElement.GetRawText())

        if storedHash <> Internal.sha256Hex rawCore then
            Internal.fail "Retention-Plan: planSha256 passt nicht zum Planinhalt."

        // Innerhalb der Dokument-Lebensdauer materialisieren; JsonElemente duerfen
        // den use-Block nicht ueberleben.
        storedHash, requestedRunIds coreElement

    /// Fuehrt eine bestaetigte Bereinigung aus.
    /// Loeschung nur fuer Runs, die im gehashten Plan als loeschbar gefuehrt sind und
    /// zum Ausfuehrungszeitpunkt erneut alle Bedingungen erfuellen (transaktional).
    let execute root (planFilePath: string) (confirmHash: string) (nowUtc: DateTimeOffset) : RetentionExecutionReceipt =
        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore

        if not (Internal.isSha256 confirmHash) then
            Internal.fail "--confirm-plan-sha256 muss einen SHA-256-Hash enthalten."

        let storedHash, requested = parsePlanFile planFilePath

        if confirmHash <> storedHash then
            Internal.fail "Bestaetigungshash stimmt nicht mit dem Hash der Plandatei ueberein."

        let lockPath = Path.Combine(locations.Runtime, ".retention.lock")

        use lockHandle =
            new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

        // Frische Pruefung unter Sperre: Nur aktuell loeschbare Runs duerfen entfernt werden.
        let freshCandidates =
            RunStore.allRunIds root |> List.map (candidateFor root locations nowUtc)

        let freshDeletable =
            freshCandidates
            |> Seq.filter (fun candidate -> candidate.Deletable)
            |> Seq.map (fun candidate -> candidate.RunId)
            |> Set.ofSeq

        for runId in requested do
            if not (Set.contains runId freshDeletable) then
                Internal.fail $"Run {runId} ist nicht mehr loeschbar; Ausfuehrung wurde abgebrochen."

        // Transaktional: erst alle Verzeichnisse pruefen, dann alle entfernen.
        for runId in requested do
            let info = DirectoryInfo(Path.Combine(locations.Runs, runId))

            if not (isNull info.LinkTarget) then
                Internal.fail $"Run-Verzeichnis darf kein Symlink oder Junction sein: {runId}"

        for runId in requested do
            Directory.Delete(Path.Combine(locations.Runs, runId), true)

        // Bereinigungsnachweis anhaengen (nur wenn tatsaechlich geloescht wurde).
        let executedAtUtc = Internal.utcText nowUtc

        if not (List.isEmpty requested) then
            let entry =
                Internal.jsonBytes false (fun writer ->
                    writer.WriteStartObject()
                    writer.WriteStartArray("deletedRunIds")

                    for runId in requested do
                        writer.WriteStringValue(runId)

                    writer.WriteEndArray()
                    writer.WriteString("executedAtUtc", executedAtUtc)
                    writer.WriteString("planSha256", storedHash)
                    writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
                    writer.WriteEndObject())

            let logPath = Path.Combine(locations.Runtime, "retention-log.jsonl")

            use stream =
                new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read)

            stream.Write(entry, 0, entry.Length)
            stream.WriteByte(byte '\n')
            stream.Flush(true)

        { DeletedRunIds = requested
          ConsideredCount = freshCandidates.Length
          PlanSha256 = storedHash
          ExecutedAtUtc = executedAtUtc }

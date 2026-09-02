namespace RiftHarness

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json

[<RequireQualifiedAccess>]
module ResearchCli =
    let private takeOption name arguments =
        let rec loop found collected remaining =
            match remaining with
            | [] -> found, List.rev collected
            | option :: _ when option = name && Option.isSome found ->
                Internal.fail $"Option '{name}' was provided more than once."
            | option :: value :: tail when option = name -> loop (Some value) collected tail
            | [ option ] when option = name -> Internal.fail $"Option '{name}' requires a value."
            | head :: tail -> loop found (head :: collected) tail

        loop None [] arguments

    let private requireOption name arguments =
        let value, rest = takeOption name arguments
        value |> Option.defaultWith (fun () -> Internal.fail $"Required option is missing: {name}"), rest

    let private noArguments command arguments =
        if not (List.isEmpty arguments) then
            let joined = String.concat " " arguments
            Internal.fail $"Unexpected arguments for '{command}': {joined}"

    let private printJson (write: Utf8JsonWriter -> unit) =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
            write writer
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> Console.Out.WriteLine

    let private collectionText result =
        match result with
        | ResearchCollectionResult.Inactive -> "inactive"
        | ResearchCollectionResult.Recorded receipt -> receipt.EventId
        | ResearchCollectionResult.GapRecorded gapId -> gapId

    let private safeCliPath (root: string) (description: string) (allowMissing: bool) (path: string) =
        let locations = Workspace.requireInitialized root
        let candidate =
            if Path.IsPathRooted(path) then path else Path.Combine(locations.Root, path)
        Workspace.requireSafePath locations description allowMissing candidate

    let private writeFreshFile (root: string) (description: string) (path: string) (bytes: byte array) =
        let locations = Workspace.requireInitialized root
        let target = safeCliPath root description true path

        if File.Exists(target) || Directory.Exists(target) then
            Internal.fail $"RESEARCH_PATH_EXISTS: {description} must be absent."

        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        use stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, max 1 bytes.Length, FileOptions.WriteThrough)
        stream.Write(bytes, 0, bytes.Length)
        stream.Flush(true)
        Workspace.relativePath locations target, Internal.sha256Hex bytes

    let private statusJson status =
        printJson (fun writer ->
            writer.WriteBoolean("active", status.Active)
            writer.WriteNumber("collectorGapCount", status.CollectorGapCount)
            writer.WriteString("evidenceClass", status.EvidenceClass)
            writer.WriteNumber("eventCount", status.EventCount)
            writer.WriteString("lastEventHash", status.LastEventHash)
            writer.WriteString("ledgerStatus", status.LedgerStatus)
            writer.WriteString("observationId", status.ObservationId)
            writer.WriteNumber("openInterventionCount", status.OpenInterventionCount)
            writer.WriteNumber("openRunCount", status.OpenRunCount)
            writer.WriteString("state", status.State)
            writer.WriteString("targetTaskId", status.TargetTaskId)
            writer.WriteStartArray("issues")
            status.Issues |> List.iter writer.WriteStringValue
            writer.WriteEndArray())

    let private importHistory (root: string) (taskId: string) baseCommit headCommit output =
        if not (Text.RegularExpressions.Regex.IsMatch(taskId, "^T-[0-9]{3,}$")) then
            Internal.fail "GIT_IMPORT_INVALID: task ID is invalid."

        let history = ResearchGitImport.read root baseCommit headCommit
        let bytes =
            Internal.jsonBytes false (fun (writer: Utf8JsonWriter) ->
                writer.WriteStartObject()
                writer.WriteString("baseCommit", history.BaseCommit)
                writer.WriteStartArray("commits")

                for commit in history.Commits do
                    writer.WriteStartObject()
                    writer.WriteString("commitId", commit.CommitId)
                    writer.WriteString("commitObjectSha256", commit.CommitObjectSha256)
                    writer.WriteString("commitTimeUtc", commit.CommitTimeUtc)
                    writer.WriteStartArray("parentCommitIds")
                    commit.ParentCommitIds |> List.iter writer.WriteStringValue
                    writer.WriteEndArray()
                    writer.WriteString("treeId", commit.TreeId)
                    writer.WriteEndObject()

                writer.WriteEndArray()
                writer.WriteString("costAmount", ResearchContract.Unknown)
                writer.WriteString("costCurrency", ResearchContract.Unknown)
                writer.WriteString("evidenceClass", "retrospective-derived")
                writer.WriteString("headCommit", history.HeadCommit)
                writer.WriteString("humanActiveDurationMs", ResearchContract.Unknown)
                writer.WriteString("inputTokens", ResearchContract.Unknown)
                writer.WriteString("objectFormat", history.ObjectFormat)
                writer.WriteString("outputTokens", ResearchContract.Unknown)
                writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
                writer.WriteString("studyId", ResearchContract.StudyId)
                writer.WriteString("targetTaskId", taskId)
                writer.WriteEndObject())
            |> Constants.Utf8NoBom.GetString
            |> ResearchCanonical.canonicalizeJson

        let relative, sha256 = writeFreshFile root "Research Git history import" output bytes
        printJson (fun writer ->
            writer.WriteNumber("commitCount", history.Commits.Length)
            writer.WriteString("evidenceClass", "retrospective-derived")
            writer.WriteString("output", relative)
            writer.WriteString("sha256", sha256))
        0

    let execute root arguments =
        match arguments with
        | "begin" :: rest ->
            let manifest, rest = requireOption "--study-manifest" rest
            noArguments "research begin" rest
            let receipt = ResearchActivation.beginObservation root (safeCliPath root "Research study manifest" false manifest)
            printJson (fun writer ->
                writer.WriteString("activationEventHash", receipt.ActivationEventHash)
                writer.WriteString("headCommit", receipt.HeadCommit)
                writer.WriteString("headTreeId", receipt.HeadTreeId)
                writer.WriteBoolean("idempotent", receipt.Idempotent)
                writer.WriteString("ledgerSha256", receipt.LedgerSha256)
                writer.WriteString("markerSha256", receipt.MarkerSha256)
                writer.WriteString("observationId", receipt.ObservationId)
                writer.WriteString("protocolBundleSha256", receipt.ProtocolBundleSha256))
            0
        | "status" :: rest ->
            let study, rest = takeOption "--study" rest
            let observation, rest = takeOption "--observation" rest
            noArguments "research status" rest

            match study with
            | Some value when value <> ResearchContract.StudyId -> Internal.fail "RESEARCH_STUDY_INVALID: unsupported study ID."
            | _ -> ()

            let status = ResearchActivation.status root observation
            let issues = (status.Issues @ ResearchCollector.healthIssues root) |> List.distinct |> List.sort
            { status with Issues = issues } |> statusJson
            0
        | "verify" :: rest ->
            let manifestPath, rest = requireOption "--study-manifest" rest
            let recoveryPath, rest = takeOption "--recover-to" rest
            noArguments "research verify" rest
            let manifestPath = safeCliPath root "Research study manifest" false manifestPath
            let recoveryPath = recoveryPath |> Option.map (safeCliPath root "Research recovery ledger" true)
            let manifest = ResearchExport.loadStudyManifest root manifestPath
            let ledger = ResearchLedger.ledgerPath root manifest.ObservationId
            let initial = ResearchLedger.verify root ledger

            let result =
                match recoveryPath, initial.Status with
                | None, _ -> initial
                | Some _, ResearchLedgerStatus.Valid -> Internal.fail "RECOVERY_NOT_APPLICABLE: ledger is already valid."
                | Some _, ResearchLedgerStatus.Invalid -> Internal.fail "RECOVERY_NOT_APPLICABLE: invalid non-tail corruption cannot be recovered."
                | Some destination, ResearchLedgerStatus.TornTail ->
                    let identity = ResearchGitImport.currentIdentity root
                    let source = ResearchRuntime.sourceFromFile root "harness-evidence" (Workspace.relativePath (Workspace.paths root) ledger)
                    let relativeDestination = Workspace.relativePath (Workspace.paths root) destination
                    let payload =
                        ResearchRuntime.payload (fun writer ->
                            writer.WriteString("originalLedgerSha256", Option.get initial.OriginalSha256)
                            writer.WriteString("recoveredLedgerPath", relativeDestination)
                            writer.WriteString("tornTailSha256", Option.get initial.TornTailSha256)
                            writer.WriteString("verifiedPrefixSha256", initial.VerifiedPrefixSha256))
                    let draft = ResearchRuntime.createDraft manifest identity "ledger.recovery.recorded" [ source ] payload
                    ResearchLedger.recoverTo root ledger destination draft

            let baseStatus = ResearchActivation.status root (Some manifest.ObservationId)
            let status =
                { baseStatus with
                    Issues = (baseStatus.Issues @ ResearchCollector.healthIssues root) |> List.distinct |> List.sort }
            let valid = result.Status = ResearchLedgerStatus.Valid && List.isEmpty status.Issues
            printJson (fun writer ->
                writer.WriteNumber("eventCount", result.Events.Length)
                writer.WriteStartArray("errors")
                result.Errors |> List.iter writer.WriteStringValue
                status.Issues |> List.iter writer.WriteStringValue
                writer.WriteEndArray()
                writer.WriteString("ledgerStatus", string result.Status)
                writer.WriteString("observationId", manifest.ObservationId)
                writer.WriteBoolean("valid", valid)
                writer.WriteString("verifiedPrefixSha256", result.VerifiedPrefixSha256))
            if valid then 0 else 2
        | "export" :: rest ->
            let manifest, rest = requireOption "--study-manifest" rest
            let output, rest = requireOption "--output" rest
            noArguments "research export" rest
            let receipt = ResearchExport.export root (safeCliPath root "Research study manifest" false manifest) (safeCliPath root "Research export directory" true output)
            printJson (fun writer ->
                writer.WriteString("evidenceManifestSha256", receipt.EvidenceManifestSha256)
                writer.WriteNumber("fileCount", receipt.FileCount)
                writer.WriteString("observationId", receipt.ObservationId)
                writer.WriteString("outerManifestSha256", receipt.OuterManifestSha256)
                writer.WriteString("outputDirectory", receipt.OutputDirectory)
                writer.WriteString("studyManifestSha256", receipt.StudyManifestSha256)
                writer.WriteString("summarySha256", receipt.SummarySha256))
            0
        | "summarize" :: rest ->
            let exportManifest, rest = requireOption "--export-manifest" rest
            let output, rest = requireOption "--output" rest
            noArguments "research summarize" rest
            let locations = Workspace.requireInitialized root
            let manifestPath = safeCliPath root "Research export manifest" false exportManifest

            if Path.GetFileName(manifestPath) <> "EXPORT.SHA256" then
                Internal.fail "EXPORT_INVALID: --export-manifest must name EXPORT.SHA256."

            let exportDirectory = Path.GetDirectoryName(manifestPath)
            let outerHash = ResearchExport.verifyExport root exportDirectory
            let reportPath = Path.Combine(exportDirectory, "report.md")
            let reportBytes = File.ReadAllBytes(reportPath)
            let relative, reportHash = writeFreshFile root "Research summary" output reportBytes
            printJson (fun writer ->
                writer.WriteString("exportManifestSha256", outerHash)
                writer.WriteString("output", relative)
                writer.WriteString("reportSha256", reportHash))
            0
        | "intervention" :: "start" :: rest ->
            let observation, rest = requireOption "--observation" rest
            let category, rest = requireOption "--category" rest
            let sourceRef, rest = requireOption "--source-ref" rest
            let reason, rest = requireOption "--reason-code" rest
            noArguments "research intervention start" rest
            let interventionId, result = ResearchCollector.interventionStart root observation category sourceRef reason
            printJson (fun writer ->
                writer.WriteString("collection", collectionText result)
                writer.WriteString("interventionId", interventionId)
                writer.WriteString("observationId", observation))
            0
        | "intervention" :: "end" :: rest ->
            let observation, rest = requireOption "--observation" rest
            let interventionId, rest = requireOption "--intervention" rest
            let sourceRef, rest = requireOption "--source-ref" rest
            noArguments "research intervention end" rest
            let result = ResearchCollector.interventionEnd root observation interventionId sourceRef
            printJson (fun writer ->
                writer.WriteString("collection", collectionText result)
                writer.WriteString("interventionId", interventionId)
                writer.WriteString("observationId", observation))
            0
        | "intervention" :: "record" :: rest ->
            let observation, rest = requireOption "--observation" rest
            let category, rest = requireOption "--category" rest
            let sourceRef, rest = requireOption "--source-ref" rest
            let reason, rest = requireOption "--reason-code" rest
            noArguments "research intervention record" rest
            let interventionId, result = ResearchCollector.interventionRecord root observation category sourceRef reason
            printJson (fun writer ->
                writer.WriteString("collection", collectionText result)
                writer.WriteString("interventionId", interventionId)
                writer.WriteString("observationId", observation))
            0
        | "close" :: rest ->
            let observation, rest = requireOption "--observation" rest
            let outcomeReceipt, rest = requireOption "--outcome-receipt" rest
            noArguments "research close" rest
            let receipt = ResearchActivation.close root observation (safeCliPath root "Research outcome receipt" false outcomeReceipt)
            printJson (fun writer ->
                writer.WriteNumber("eventCount", receipt.EventCount)
                writer.WriteString("finalEventHash", receipt.FinalEventHash)
                writer.WriteBoolean("idempotent", receipt.Idempotent)
                writer.WriteString("ledgerSha256", receipt.LedgerSha256)
                writer.WriteBoolean("markerRemoved", receipt.MarkerRemoved)
                writer.WriteString("observationId", receipt.ObservationId))
            0
        | "import-git-history" :: rest ->
            let taskId, rest = requireOption "--task" rest
            let baseCommit, rest = requireOption "--base" rest
            let headCommit, rest = requireOption "--head" rest
            let output, rest = requireOption "--output" rest
            noArguments "research import-git-history" rest
            importHistory root taskId baseCommit headCommit (safeCliPath root "Research Git history output" true output)
        | _ -> Internal.fail "Unknown or incomplete research command."

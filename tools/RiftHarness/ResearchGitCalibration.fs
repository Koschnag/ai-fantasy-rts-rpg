namespace RiftHarness

open System
open System.IO
open System.Text.Json

type ResearchCalibrationEvidence =
    { Kind: string
      Path: string
      BlobOid: string
      Sha256: string }

type ResearchLaterLifecycle =
    { AcceptedManifest: ResearchCalibrationEvidence
      AcceptedManifestStatus: string
      AuditEvidence: ResearchCalibrationEvidence
      ReconciliationEvidence: ResearchCalibrationEvidence
      SupersededCommit: string
      SupersedingCommit: string
      SupersedingTreeId: string }

type ResearchGitCalibration =
    { BaseTreeId: string
      CalibrationId: string
      CalibrationSpecSha256: string
      HeadManifest: ResearchCalibrationEvidence
      HeadManifestStatus: string
      HeadReviewEvidence: ResearchCalibrationEvidence
      HeadTreeId: string
      HistoricalRoleSeparation: string
      LaterLifecycle: ResearchLaterLifecycle }

[<RequireQualifiedAccess>]
module ResearchGitCalibration =
    [<Literal>]
    let private MaxSpecBytes = 256 * 1024

    let private fail detail =
        Internal.fail $"GIT_CALIBRATION_INVALID: {detail}"

    let private properties (element: JsonElement) : JsonProperty list =
        if element.ValueKind <> JsonValueKind.Object then
            fail "expected an object."

        element.EnumerateObject() |> Seq.toList

    let private exactFields description expected (element: JsonElement) =
        let entries = properties element
        let names = entries |> List.map (fun property -> property.Name)

        if (names |> Set.ofList).Count <> names.Length then
            fail $"{description} contains duplicate fields."

        let actual = names |> Set.ofList

        if actual <> expected then
            fail $"{description} fields differ from the closed contract."

    let private requiredProperty name (element: JsonElement) =
        let matches =
            properties element |> List.filter (fun property -> property.Name = name)

        match matches with
        | [ property ] -> property.Value
        | [] -> fail $"required field {name} is missing."
        | _ -> fail $"field {name} is duplicated."

    let private requiredString name (element: JsonElement) =
        let value = requiredProperty name element

        if value.ValueKind <> JsonValueKind.String then
            fail $"field {name} must be a string."

        let text = value.GetString()

        if String.IsNullOrWhiteSpace(text) then
            fail $"field {name} must not be empty."

        text

    let private requiredInteger name (element: JsonElement) =
        let value = requiredProperty name element
        let mutable parsed = 0

        if value.ValueKind <> JsonValueKind.Number || not (value.TryGetInt32(&parsed)) then
            fail $"field {name} must be an integer."

        parsed

    let private requiredArray name (element: JsonElement) =
        let value = requiredProperty name element

        if value.ValueKind <> JsonValueKind.Array then
            fail $"field {name} must be an array."

        value.EnumerateArray() |> Seq.toList

    let private requireEqual description expected actual =
        if actual <> expected then
            fail $"{description} differs from the immutable calibration binding."

    let private parseEvidence expectedKind (element: JsonElement) =
        exactFields "evidence" (set [ "blobOid"; "kind"; "path"; "sha256" ]) element

        let evidence =
            { Kind = requiredString "kind" element
              Path = requiredString "path" element
              BlobOid = requiredString "blobOid" element
              Sha256 = requiredString "sha256" element }

        requireEqual "evidence kind" expectedKind evidence.Kind
        evidence

    let private verifyEvidence root commit evidence =
        let actual = ResearchGitImport.blobAtCommit root commit evidence.Path
        requireEqual $"blob OID for {evidence.Path}" evidence.BlobOid actual.BlobOid
        requireEqual $"SHA-256 for {evidence.Path}" evidence.Sha256 actual.Sha256
        actual.Bytes

    let private requiredStringItems description (items: JsonElement list) =
        items
        |> List.map (fun item ->
            if item.ValueKind <> JsonValueKind.String then
                fail $"{description} entries must be strings."

            let value = item.GetString()

            if String.IsNullOrWhiteSpace(value) then
                fail $"{description} entries must not be empty."

            value)

    let private statusFromManifest (bytes: byte array) =
        try
            use document = JsonDocument.Parse(bytes)
            requiredString "status" document.RootElement
        with :? JsonException ->
            fail "task manifest is not valid JSON."

    let private requireReceipt
        targetTaskId
        firstCommit
        headCommit
        headTreeId
        manifestBlobOid
        reviewBlobOid
        (bytes: byte array)
        =
        try
            use document = JsonDocument.Parse(bytes)
            let root = document.RootElement

            let matching =
                requiredArray "receipts" root
                |> List.filter (fun receipt ->
                    receipt.ValueKind = JsonValueKind.Object
                    && requiredString "taskId" receipt = targetTaskId)

            let receipt =
                match matching with
                | [ item ] -> item
                | _ -> fail "reconciliation must contain exactly one target-task receipt."

            requireEqual "reconciliation baseSha" firstCommit (requiredString "baseSha" receipt)
            requireEqual "reconciliation resultSha" headCommit (requiredString "resultSha" receipt)
            requireEqual "reconciliation mergeSha" headCommit (requiredString "mergeSha" receipt)
            requireEqual "reconciliation resultTree" headTreeId (requiredString "resultTree" receipt)

            requireEqual
                "reconciliation taskManifestBlobOid"
                manifestBlobOid
                (requiredString "taskManifestBlobOid" receipt)

            requireEqual
                "reconciliation reviewEvidenceBlobOid"
                reviewBlobOid
                (requiredString "reviewEvidenceBlobOid" receipt)

            requireEqual "reconciliation outcome" "success" (requiredString "outcome" receipt)
            requireEqual "historical role separation" "not-publicly-proven" (requiredString "roleSeparation" receipt)
        with :? JsonException ->
            fail "reconciliation evidence is not valid JSON."

    let private requireAudit targetTaskId (bytes: byte array) =
        try
            use document = JsonDocument.Parse(bytes)
            let root = document.RootElement

            let covered =
                requiredArray "coveredTaskIds" root |> requiredStringItems "coveredTaskIds"

            if covered |> List.filter ((=) targetTaskId) |> List.length <> 1 then
                fail "historical audit must cover the target task exactly once."

            requireEqual "historical audit criteria" "PASS" (requiredString "criteria" root)

            requireEqual
                "historical audit role separation"
                "not-publicly-proven"
                (requiredString "historicalRoleSeparation" root)
        with :? JsonException ->
            fail "historical audit evidence is not valid JSON."

    let loadAndVerify root taskId baseCommit headCommit specPath (history: ResearchGitHistory) =
        let specBytes = File.ReadAllBytes(specPath)

        if specBytes.Length = 0 || specBytes.Length > MaxSpecBytes then
            fail "calibration spec size is outside the allowed range."

        try
            use document = JsonDocument.Parse(specBytes)
            let spec = document.RootElement

            exactFields
                "calibration spec"
                (set
                    [ "baseCommit"
                      "baseTreeId"
                      "calibrationId"
                      "evidenceClass"
                      "expectedCommitIds"
                      "headCommit"
                      "headManifest"
                      "headManifestStatus"
                      "headReviewEvidence"
                      "headTreeId"
                      "laterLifecycle"
                      "schemaVersion"
                      "targetTaskId" ])
                spec

            requireEqual "schemaVersion" 1 (requiredInteger "schemaVersion" spec)
            requireEqual "calibrationId" "R-001" (requiredString "calibrationId" spec)
            requireEqual "targetTaskId" "T-037" (requiredString "targetTaskId" spec)
            requireEqual "CLI target task" "T-037" taskId
            requireEqual "evidenceClass" "retrospective-derived" (requiredString "evidenceClass" spec)
            requireEqual "baseCommit" baseCommit (requiredString "baseCommit" spec)
            requireEqual "headCommit" headCommit (requiredString "headCommit" spec)
            requireEqual "history baseCommit" baseCommit history.BaseCommit
            requireEqual "history headCommit" headCommit history.HeadCommit

            let baseTreeId = ResearchGitImport.treeAt root baseCommit
            let headTreeId = ResearchGitImport.treeAt root headCommit
            requireEqual "baseTreeId" baseTreeId (requiredString "baseTreeId" spec)
            requireEqual "headTreeId" headTreeId (requiredString "headTreeId" spec)

            let expectedCommitIds =
                requiredArray "expectedCommitIds" spec
                |> requiredStringItems "expectedCommitIds"

            let actualCommitIds = history.Commits |> List.map (fun commit -> commit.CommitId)
            requireEqual "ordered commit list" expectedCommitIds actualCommitIds

            if expectedCommitIds.Length <> 2 then
                fail "R-001 must bind exactly the ready and reviewed T-037 commits."

            let headManifest =
                requiredProperty "headManifest" spec |> parseEvidence "task-manifest"

            let headReview =
                requiredProperty "headReviewEvidence" spec |> parseEvidence "review-receipt"

            let headManifestBytes = verifyEvidence root headCommit headManifest
            verifyEvidence root headCommit headReview |> ignore
            let headManifestStatus = requiredString "headManifestStatus" spec
            requireEqual "head manifest status contract" "review" headManifestStatus
            requireEqual "head manifest status" headManifestStatus (statusFromManifest headManifestBytes)

            let later = requiredProperty "laterLifecycle" spec

            exactFields
                "later lifecycle"
                (set
                    [ "acceptedManifest"
                      "acceptedManifestStatus"
                      "auditEvidence"
                      "reconciliationEvidence"
                      "relation"
                      "supersededCommit"
                      "supersedingCommit"
                      "supersedingTreeId" ])
                later

            requireEqual "later lifecycle relation" "git.supersession.observed" (requiredString "relation" later)
            requireEqual "supersededCommit" headCommit (requiredString "supersededCommit" later)
            let supersedingCommit = requiredString "supersedingCommit" later

            if supersedingCommit = headCommit then
                fail "later acceptance cannot be dated at the review-state head."

            let laterHistory = ResearchGitImport.read root headCommit supersedingCommit

            if List.isEmpty laterHistory.Commits then
                fail "later acceptance must be a strict descendant of the review-state head."

            let supersedingTreeId = ResearchGitImport.treeAt root supersedingCommit
            requireEqual "supersedingTreeId" supersedingTreeId (requiredString "supersedingTreeId" later)

            let acceptedManifest =
                requiredProperty "acceptedManifest" later |> parseEvidence "task-manifest"

            let reconciliation =
                requiredProperty "reconciliationEvidence" later
                |> parseEvidence "review-receipt"

            let audit = requiredProperty "auditEvidence" later |> parseEvidence "review-receipt"
            let acceptedManifestBytes = verifyEvidence root supersedingCommit acceptedManifest
            let reconciliationBytes = verifyEvidence root supersedingCommit reconciliation
            let auditBytes = verifyEvidence root supersedingCommit audit
            let acceptedManifestStatus = requiredString "acceptedManifestStatus" later
            requireEqual "later manifest status contract" "accepted" acceptedManifestStatus
            requireEqual "later manifest status" acceptedManifestStatus (statusFromManifest acceptedManifestBytes)

            requireReceipt
                taskId
                expectedCommitIds.Head
                headCommit
                headTreeId
                headManifest.BlobOid
                headReview.BlobOid
                reconciliationBytes

            requireAudit taskId auditBytes

            { BaseTreeId = baseTreeId
              CalibrationId = "R-001"
              CalibrationSpecSha256 = Internal.sha256Hex specBytes
              HeadManifest = headManifest
              HeadManifestStatus = headManifestStatus
              HeadReviewEvidence = headReview
              HeadTreeId = headTreeId
              HistoricalRoleSeparation = "not-publicly-proven"
              LaterLifecycle =
                { AcceptedManifest = acceptedManifest
                  AcceptedManifestStatus = acceptedManifestStatus
                  AuditEvidence = audit
                  ReconciliationEvidence = reconciliation
                  SupersededCommit = headCommit
                  SupersedingCommit = supersedingCommit
                  SupersedingTreeId = supersedingTreeId } }
        with :? JsonException ->
            fail "calibration spec is not valid JSON."

    let writeJson (writer: Utf8JsonWriter) (calibration: ResearchGitCalibration) =
        let writeEvidence (name: string) (evidence: ResearchCalibrationEvidence) =
            writer.WriteStartObject(name)
            writer.WriteString("blobOid", evidence.BlobOid)
            writer.WriteString("kind", evidence.Kind)
            writer.WriteString("path", evidence.Path)
            writer.WriteString("sha256", evidence.Sha256)
            writer.WriteEndObject()

        writer.WriteStartObject("calibration")
        writer.WriteString("actorId", ResearchContract.Unknown)
        writer.WriteString("actorRole", ResearchContract.Unknown)
        writer.WriteString("agentActiveDurationMs", ResearchContract.Unknown)
        writer.WriteString("autonomousDurationMs", ResearchContract.Unknown)
        writer.WriteString("baseTreeId", calibration.BaseTreeId)
        writer.WriteString("cacheReadTokens", ResearchContract.Unknown)
        writer.WriteString("cacheWriteTokens", ResearchContract.Unknown)
        writer.WriteString("calibrationId", calibration.CalibrationId)
        writer.WriteString("calibrationSpecSha256", calibration.CalibrationSpecSha256)
        writer.WriteString("costProvenance", ResearchContract.Unknown)
        writer.WriteString("elapsedDurationMs", ResearchContract.Unknown)
        writeEvidence "headManifest" calibration.HeadManifest
        writer.WriteString("headManifestStatus", calibration.HeadManifestStatus)
        writeEvidence "headReviewEvidence" calibration.HeadReviewEvidence
        writer.WriteString("headTreeId", calibration.HeadTreeId)
        writer.WriteString("historicalRoleSeparation", calibration.HistoricalRoleSeparation)
        writer.WriteString("identityAssurance", ResearchContract.Unknown)
        writer.WriteString("interventionCount", ResearchContract.Unknown)
        writer.WriteString("interventionDurationMs", ResearchContract.Unknown)
        writer.WriteStartObject("laterLifecycle")
        writeEvidence "acceptedManifest" calibration.LaterLifecycle.AcceptedManifest
        writer.WriteString("acceptedManifestStatus", calibration.LaterLifecycle.AcceptedManifestStatus)
        writeEvidence "auditEvidence" calibration.LaterLifecycle.AuditEvidence
        writeEvidence "reconciliationEvidence" calibration.LaterLifecycle.ReconciliationEvidence
        writer.WriteString("relation", "git.supersession.observed")
        writer.WriteString("supersededCommit", calibration.LaterLifecycle.SupersededCommit)
        writer.WriteString("supersedingCommit", calibration.LaterLifecycle.SupersedingCommit)
        writer.WriteString("supersedingTreeId", calibration.LaterLifecycle.SupersedingTreeId)
        writer.WriteEndObject()
        writer.WriteString("modelId", ResearchContract.Unknown)
        writer.WriteString("modelVersion", ResearchContract.Unknown)
        writer.WriteString("providerId", ResearchContract.Unknown)
        writer.WriteString("requestCount", ResearchContract.Unknown)
        writer.WriteString("taskOutcome", ResearchContract.Unknown)
        writer.WriteString("usageProvenance", ResearchContract.Unknown)
        writer.WriteEndObject()

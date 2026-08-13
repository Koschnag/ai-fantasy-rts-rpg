namespace RiftHarness.Tests

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open RiftHarness
open global.Json.Schema

[<RequireQualifiedAccess>]
module AssetJobJournalTests =
    exception private InjectedCrash of string

    let private repositoryRoot =
        let rec find path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                let parent = Directory.GetParent(path)

                if isNull parent then
                    failwith "Repository root not found."

                find parent.FullName

        find Environment.CurrentDirectory

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private assertEqual expected actual message =
        if actual <> expected then
            failwith $"{message} Expected {expected}, got {actual}."

    let private expectConflict label action =
        let mutable rejected = false

        try
            action ()
        with AssetJobJournalConflict _ ->
            rejected <- true

        assertTrue rejected $"Journal conflict was not rejected: {label}."

    let private physicalTemporaryRoot () =
        let configured = Path.GetFullPath(Path.GetTempPath())

        if
            OperatingSystem.IsMacOS()
            && configured.StartsWith("/var/", StringComparison.Ordinal)
            && Directory.Exists("/private" + configured)
        then
            "/private" + configured
        else
            configured

    let private withWorkspace action =
        let root =
            Path.Combine(physicalTemporaryRoot (), "riftward-asset-journal-tests-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "assets/quarantine/3d")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "assets/receipts")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "assets/manifests")) |> ignore

        try
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private jobIdA = "01ARZ3NDEKTSV4RRFFQ69G5FAV"
    let private jobIdB = "01ARZ3NDEKTSV4RRFFQ69G5FAW"
    let private jobIdC = "01ARZ3NDEKTSV4RRFFQ69G5FAX"

    let private at second =
        DateTimeOffset(2026, 8, 13, 10, 0, second, TimeSpan.Zero)

    let private jobRelative jobId suffix =
        $".ai/runtime/asset-jobs/{jobId}/{suffix}"

    let private physical (root: string) (relativePath: string) =
        relativePath.Split('/')
        |> Array.fold (fun current segment -> Path.Combine(current, segment)) root

    let private writeRelative (root: string) (relativePath: string) (bytes: byte array) =
        let path = physical root relativePath
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllBytes(path, bytes)

    let private journalPath root jobId =
        Path.Combine(root, ".ai/runtime/asset-jobs", jobId, "journal.jsonl")

    type private PublicationFixture =
        { Inventory: AssetJobOwnedPath list
          StageQuarantine: string
          TargetQuarantine: string
          StageReceipt: string
          TargetReceipt: string
          StageManifest: string
          TargetManifest: string
          QuarantineSha256: string
          ReceiptSha256: string
          ManifestSha256: string }

    let private prepareProvenance root jobId =
        AssetJobJournal.withExclusiveJobLock root jobId (fun jobLock ->
            AssetJobJournal.appendTransition jobLock AssetJobState.Created [] (at 0) AssetJobJournal.noCrash
            |> ignore

            let stageQuarantine = jobRelative jobId "stage/quarantine"
            let targetQuarantine = "assets/quarantine/3d/CAL-STONEWOOD-V1-39FAAE34C4CD"
            Directory.CreateDirectory(physical root stageQuarantine) |> ignore
            writeRelative root (stageQuarantine + "/family.glb") (Encoding.ASCII.GetBytes("synthetic-glb"))
            writeRelative root (stageQuarantine + "/preview.png") (Encoding.ASCII.GetBytes("synthetic-png"))
            writeRelative root (stageQuarantine + "/technique.json") (Encoding.ASCII.GetBytes("{}\n"))

            let quarantineClaim =
                AssetJobJournal.hashOwnedPath jobLock stageQuarantine AssetJobOwnedPathKind.OwnedDirectory

            AssetJobJournal.appendTransition
                jobLock
                AssetJobState.Generated
                [ quarantineClaim ]
                (at 1)
                AssetJobJournal.noCrash
            |> ignore

            AssetJobJournal.appendTransition
                jobLock
                AssetJobState.Inspected
                [ quarantineClaim ]
                (at 2)
                AssetJobJournal.noCrash
            |> ignore

            let stageReceipt = jobRelative jobId "stage/receipt.json"
            let stageManifest = jobRelative jobId "stage/manifest.json"

            let targetReceipt =
                "assets/receipts/CAL-STONEWOOD-V1-39FAAE34C4CD/01ARZ3NDEKTSV4RRFFQ69G5FAV.json"

            let targetManifest = "assets/manifests/CAL-STONEWOOD-V1-39FAAE34C4CD.json"

            Directory.CreateDirectory(Path.GetDirectoryName(physical root targetReceipt))
            |> ignore

            writeRelative root stageReceipt (Encoding.ASCII.GetBytes("{\"kind\":\"receipt\"}\n"))
            writeRelative root stageManifest (Encoding.ASCII.GetBytes("{\"kind\":\"manifest\"}\n"))

            let receiptClaim =
                AssetJobJournal.hashOwnedPath jobLock stageReceipt AssetJobOwnedPathKind.OwnedFile

            let manifestClaim =
                AssetJobJournal.hashOwnedPath jobLock stageManifest AssetJobOwnedPathKind.OwnedFile

            let targetQuarantineClaim =
                AssetJobJournal.claimOwnedPath
                    jobLock
                    targetQuarantine
                    AssetJobOwnedPathKind.OwnedDirectory
                    quarantineClaim.Sha256

            let targetReceiptClaim =
                AssetJobJournal.claimOwnedPath
                    jobLock
                    targetReceipt
                    AssetJobOwnedPathKind.OwnedFile
                    receiptClaim.Sha256

            let targetReceiptTemporaryClaim =
                AssetJobJournal.claimOwnedPath
                    jobLock
                    (AssetJobJournal.publicationTemporaryPath jobLock targetReceipt)
                    AssetJobOwnedPathKind.OwnedFile
                    receiptClaim.Sha256

            let targetManifestClaim =
                AssetJobJournal.claimOwnedPath
                    jobLock
                    targetManifest
                    AssetJobOwnedPathKind.OwnedFile
                    manifestClaim.Sha256

            let targetManifestTemporaryClaim =
                AssetJobJournal.claimOwnedPath
                    jobLock
                    (AssetJobJournal.publicationTemporaryPath jobLock targetManifest)
                    AssetJobOwnedPathKind.OwnedFile
                    manifestClaim.Sha256

            let inventory =
                [ quarantineClaim
                  targetQuarantineClaim
                  receiptClaim
                  targetReceiptClaim
                  targetReceiptTemporaryClaim
                  manifestClaim
                  targetManifestClaim
                  targetManifestTemporaryClaim ]

            AssetJobJournal.appendTransition
                jobLock
                AssetJobState.ProvenancePrepared
                inventory
                (at 3)
                AssetJobJournal.noCrash
            |> ignore

            { Inventory = inventory
              StageQuarantine = stageQuarantine
              TargetQuarantine = targetQuarantine
              StageReceipt = stageReceipt
              TargetReceipt = targetReceipt
              StageManifest = stageManifest
              TargetManifest = targetManifest
              QuarantineSha256 = quarantineClaim.Sha256
              ReceiptSha256 = receiptClaim.Sha256
              ManifestSha256 = manifestClaim.Sha256 })

    let schemaIsClosedOfflineAndCanonicalEntryIsValid () =
        let schemaPath =
            Path.Combine(repositoryRoot, ".ai/schemas/asset-job-journal-entry.schema.json")

        let schemaNode = JsonNode.Parse(File.ReadAllBytes(schemaPath))

        let rec inspect (node: JsonNode) =
            match node with
            | :? JsonObject as item ->
                match item["$ref"] with
                | null -> ()
                | :? JsonValue as reference ->
                    assertTrue
                        (reference.GetValue<string>().StartsWith("#/", StringComparison.Ordinal))
                        "Asset journal schema must resolve references offline."
                | _ -> failwith "Journal schema $ref must be a string."

                match item["type"] with
                | null -> ()
                | :? JsonValue as value when value.GetValue<string>() = "object" ->
                    assertTrue
                        (not (isNull item["additionalProperties"])
                         && not (item["additionalProperties"].GetValue<bool>()))
                        "Every asset journal schema object must be closed."
                | _ -> ()

                for property in item do
                    if not (isNull property.Value) then
                        inspect property.Value
            | :? JsonArray as items ->
                items
                |> Seq.iter (fun item ->
                    if not (isNull item) then
                        inspect item)
            | _ -> ()

        inspect schemaNode
        let schema = JsonSchema.FromText(File.ReadAllText(schemaPath, Constants.Utf8NoBom))

        withWorkspace (fun root ->
            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                AssetJobJournal.appendTransition jobLock AssetJobState.Created [] (at 0) AssetJobJournal.noCrash
                |> ignore)

            let bytes = File.ReadAllBytes(journalPath root jobIdA)
            assertTrue (bytes[bytes.Length - 1] = byte '\n') "Journal entry is not LF-terminated."
            assertTrue (not (bytes.AsSpan().Contains(byte '\r'))) "Journal entry contains CR."
            use document = JsonDocument.Parse(bytes.AsMemory(0, bytes.Length - 1))

            let result =
                schema.Evaluate(document.RootElement, EvaluationOptions(OutputFormat = OutputFormat.List))

            assertTrue result.IsValid "Canonical CREATED entry does not satisfy the journal schema."

            let names =
                document.RootElement.EnumerateObject()
                |> Seq.map (fun property -> property.Name)
                |> Seq.toArray

            let expected =
                [| "atUtc"
                   "entrySha256"
                   "jobId"
                   "ownedPaths"
                   "previousEntrySha256"
                   "schemaVersion"
                   "sequence"
                   "state" |]

            assertTrue (names = expected) "Journal properties are not ordinal and closed.")

    let stateGraphHashChainAndCanonicalBytesAreStrict () =
        withWorkspace (fun root ->
            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                let created =
                    AssetJobJournal.appendTransition jobLock AssetJobState.Created [] (at 0) AssetJobJournal.noCrash

                let generatedPath = jobRelative jobIdA "stage/generated.bin"
                writeRelative root generatedPath (Encoding.ASCII.GetBytes("generated"))

                let generatedClaim =
                    AssetJobJournal.hashOwnedPath jobLock generatedPath AssetJobOwnedPathKind.OwnedFile

                let generated =
                    AssetJobJournal.appendTransition
                        jobLock
                        AssetJobState.Generated
                        [ generatedClaim ]
                        (at 1)
                        AssetJobJournal.noCrash

                let inspected =
                    AssetJobJournal.appendTransition
                        jobLock
                        AssetJobState.Inspected
                        [ generatedClaim ]
                        (at 2)
                        AssetJobJournal.noCrash

                assertEqual 1 created.Sequence "CREATED sequence changed."
                assertEqual (Some created.EntrySha256) generated.PreviousEntrySha256 "Hash chain changed."
                assertEqual (Some generated.EntrySha256) inspected.PreviousEntrySha256 "Hash chain changed."

                expectConflict "state skip" (fun () ->
                    AssetJobJournal.appendTransition jobLock AssetJobState.Verified [] (at 3) AssetJobJournal.noCrash
                    |> ignore)

                expectConflict "backwards timestamp" (fun () ->
                    AssetJobJournal.appendTransition
                        jobLock
                        AssetJobState.RolledBack
                        []
                        (at 1)
                        AssetJobJournal.noCrash
                    |> ignore)

                [ "../escape", AssetJobOwnedPathKind.OwnedFile
                  "/absolute", AssetJobOwnedPathKind.OwnedFile
                  "assets/manifests/a.json/child", AssetJobOwnedPathKind.OwnedFile
                  jobRelative jobIdB "stage/foreign", AssetJobOwnedPathKind.OwnedFile ]
                |> List.iter (fun (path, kind) ->
                    expectConflict path (fun () ->
                        AssetJobJournal.claimOwnedPath jobLock path kind (String.replicate 64 "0")
                        |> ignore)))

            let path = journalPath root jobIdA
            let original = File.ReadAllText(path, Constants.Utf8NoBom)
            File.WriteAllText(path, original.Replace("\"INSPECTED\"", "\"GENERATED\""), Constants.Utf8NoBom)

            expectConflict "tampered state/hash" (fun () ->
                AssetJobJournal.recover root jobIdA (at 4) AssetJobJournal.noCrash |> ignore))

    let exclusiveLockRejectsConcurrentRecovery () =
        withWorkspace (fun root ->
            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                AssetJobJournal.appendTransition jobLock AssetJobState.Created [] (at 0) AssetJobJournal.noCrash
                |> ignore

                expectConflict "concurrent lock" (fun () ->
                    AssetJobJournal.recover root jobIdA (at 1) AssetJobJournal.noCrash |> ignore)))

    let publicationAndRecoveryAreIdempotent () =
        withWorkspace (fun root ->
            let fixture = prepareProvenance root jobIdA

            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                AssetJobJournal.publishDirectoryByRename
                    jobLock
                    fixture.StageQuarantine
                    fixture.TargetQuarantine
                    fixture.QuarantineSha256
                    AssetJobJournal.noCrash

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.QuarantinePublished
                    fixture.Inventory
                    (at 4)
                    AssetJobJournal.noCrash
                |> ignore

                AssetJobJournal.publishFileAtomically
                    jobLock
                    fixture.StageReceipt
                    fixture.TargetReceipt
                    fixture.ReceiptSha256
                    AssetJobJournal.noCrash

                AssetJobJournal.publishFileAtomically
                    jobLock
                    fixture.StageManifest
                    fixture.TargetManifest
                    fixture.ManifestSha256
                    AssetJobJournal.noCrash

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.MetadataPublished
                    fixture.Inventory
                    (at 5)
                    AssetJobJournal.noCrash
                |> ignore

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Verified
                    fixture.Inventory
                    (at 6)
                    AssetJobJournal.noCrash
                |> ignore

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Committed
                    fixture.Inventory
                    (at 7)
                    AssetJobJournal.noCrash
                |> ignore)

            match AssetJobJournal.recover root jobIdA (at 8) AssetJobJournal.noCrash with
            | AssetJobRecoveryOutcome.AlreadyCommitted entry ->
                assertEqual AssetJobState.Committed entry.State "Committed recovery changed state."
            | _ -> failwith "Committed job was not idempotent."

            assertTrue (Directory.Exists(physical root fixture.TargetQuarantine)) "Committed output disappeared."
            assertTrue (File.Exists(physical root fixture.TargetReceipt)) "Committed receipt disappeared."
            assertTrue (File.Exists(physical root fixture.TargetManifest)) "Committed manifest disappeared.")

        withWorkspace (fun root ->
            let partial = prepareProvenance root jobIdB

            AssetJobJournal.withExclusiveJobLock root jobIdB (fun jobLock ->
                AssetJobJournal.publishDirectoryByRename
                    jobLock
                    partial.StageQuarantine
                    partial.TargetQuarantine
                    partial.QuarantineSha256
                    AssetJobJournal.noCrash

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.QuarantinePublished
                    partial.Inventory
                    (at 4)
                    AssetJobJournal.noCrash
                |> ignore)

            let foreign = jobRelative jobIdB "work/foreign.txt"
            writeRelative root foreign (Encoding.ASCII.GetBytes("foreign"))

            match AssetJobJournal.recover root jobIdB (at 5) AssetJobJournal.noCrash with
            | AssetJobRecoveryOutcome.RolledBack entry ->
                assertEqual AssetJobState.RolledBack entry.State "Partial job did not roll back."
            | _ -> failwith "Partial publication was not rolled back."

            assertTrue (not (Directory.Exists(physical root partial.TargetQuarantine))) "Partial quarantine remained."
            assertTrue (not (File.Exists(physical root partial.StageReceipt))) "Owned stage receipt remained."
            assertTrue (File.Exists(physical root foreign)) "Unowned foreign file was deleted."

            match AssetJobJournal.recover root jobIdB (at 6) AssetJobJournal.noCrash with
            | AssetJobRecoveryOutcome.AlreadyRolledBack entry ->
                assertEqual AssetJobState.RolledBack entry.State "Repeated recovery changed state."
            | _ -> failwith "Repeated recovery was not idempotent.")

    let crashPointsRemainRecoverable () =
        withWorkspace (fun root ->
            let fixture = prepareProvenance root jobIdA

            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                AssetJobJournal.publishDirectoryByRename
                    jobLock
                    fixture.StageQuarantine
                    fixture.TargetQuarantine
                    fixture.QuarantineSha256
                    AssetJobJournal.noCrash

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.QuarantinePublished
                    fixture.Inventory
                    (at 4)
                    AssetJobJournal.noCrash
                |> ignore

                try
                    AssetJobJournal.publishFileAtomically
                        jobLock
                        fixture.StageReceipt
                        fixture.TargetReceipt
                        fixture.ReceiptSha256
                        (fun point ->
                            if point = "after-receipt-temp-write" then
                                raise (InjectedCrash point))

                    failwith "Receipt temp-write crash point did not fire."
                with InjectedCrash "after-receipt-temp-write" ->
                    ())

            match AssetJobJournal.recover root jobIdA (at 5) AssetJobJournal.noCrash with
            | AssetJobRecoveryOutcome.RolledBack _ -> ()
            | _ -> failwith "Temp-write crash was not recoverable."

            assertTrue (not (Directory.Exists(physical root fixture.TargetQuarantine))) "Crash output remained."
            assertTrue (not (File.Exists(physical root fixture.TargetReceipt))) "Crash receipt remained."

            let mutable transitionCrashObserved = false

            try
                AssetJobJournal.withExclusiveJobLock root jobIdB (fun jobLock ->
                    AssetJobJournal.appendTransition jobLock AssetJobState.Created [] (at 0) (fun point ->
                        if point = "after-journal-CREATED" then
                            raise (InjectedCrash point))
                    |> ignore)
            with InjectedCrash "after-journal-CREATED" ->
                transitionCrashObserved <- true

            assertTrue transitionCrashObserved "Durable transition crash point did not fire."

            match AssetJobJournal.recover root jobIdB (at 1) AssetJobJournal.noCrash with
            | AssetJobRecoveryOutcome.RolledBack _ -> ()
            | _ -> failwith "Durable transition crash was not recoverable.")

    let recoveryRefusesChangedSymlinkAndCorruptOwnership () =
        withWorkspace (fun root ->
            let owned = jobRelative jobIdA "stage/owned.bin"
            let foreign = jobRelative jobIdA "work/foreign.bin"
            writeRelative root owned (Encoding.ASCII.GetBytes("owned"))
            writeRelative root foreign (Encoding.ASCII.GetBytes("foreign"))

            AssetJobJournal.withExclusiveJobLock root jobIdA (fun jobLock ->
                let claim =
                    AssetJobJournal.hashOwnedPath jobLock owned AssetJobOwnedPathKind.OwnedFile

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Created
                    [ claim ]
                    (at 0)
                    AssetJobJournal.noCrash
                |> ignore)

            writeRelative root owned (Encoding.ASCII.GetBytes("changed"))

            expectConflict "changed owned file" (fun () ->
                AssetJobJournal.recover root jobIdA (at 1) AssetJobJournal.noCrash |> ignore)

            assertEqual
                "changed"
                (File.ReadAllText(physical root owned, Encoding.ASCII))
                "Changed owned file was modified by recovery."

            assertEqual
                "foreign"
                (File.ReadAllText(physical root foreign, Encoding.ASCII))
                "Foreign file was modified by recovery."

            if not (OperatingSystem.IsWindows()) then
                let outside = Path.Combine(root, "outside.bin")
                File.WriteAllText(outside, "outside", Encoding.ASCII)
                File.Delete(physical root owned)
                File.CreateSymbolicLink(physical root owned, outside) |> ignore

                expectConflict "symlink swap" (fun () ->
                    AssetJobJournal.recover root jobIdA (at 2) AssetJobJournal.noCrash |> ignore)

                assertEqual "outside" (File.ReadAllText(outside, Encoding.ASCII)) "Symlink target was modified."
                assertTrue (not (isNull (FileInfo(physical root owned).LinkTarget))) "Symlink evidence disappeared."

            let cleanRootJob = prepareProvenance root jobIdB
            let corruptJournal = journalPath root jobIdB
            let bytes = File.ReadAllBytes(corruptJournal)
            bytes[bytes.Length / 2] <- bytes[bytes.Length / 2] ^^^ 1uy
            File.WriteAllBytes(corruptJournal, bytes)

            expectConflict "corrupt hash chain" (fun () ->
                AssetJobJournal.recover root jobIdB (at 9) AssetJobJournal.noCrash |> ignore)

            assertTrue
                (Directory.Exists(physical root cleanRootJob.StageQuarantine))
                "Corrupt journal recovery deleted owned evidence."

            let directoryFixture = prepareProvenance root jobIdC
            let foreignInside = directoryFixture.StageQuarantine + "/foreign-added.bin"
            writeRelative root foreignInside (Encoding.ASCII.GetBytes("foreign-added"))

            expectConflict "foreign directory entry" (fun () ->
                AssetJobJournal.recover root jobIdC (at 9) AssetJobJournal.noCrash |> ignore)

            assertEqual
                "foreign-added"
                (File.ReadAllText(physical root foreignInside, Encoding.ASCII))
                "Recovery deleted an unjournaled directory entry."

            assertTrue
                (File.Exists(physical root (directoryFixture.StageQuarantine + "/family.glb")))
                "Recovery partially deleted a directory before detecting a foreign entry.")

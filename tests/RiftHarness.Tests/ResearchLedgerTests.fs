namespace RiftHarness.Tests

open System
open System.IO
open System.Text.Json
open RiftHarness

module private T053 =
    let assertTrue condition message = if not condition then failwith message

    let assertEqual expected actual message =
        if not (Unchecked.equals expected actual) then failwith $"{message} Expected: {expected}; actual: {actual}"

    let expectFailure (code: string) (action: unit -> unit) =
        try
            action ()
            failwith $"Expected HarnessException containing {code}."
        with
        | HarnessException message when message.Contains(code, StringComparison.Ordinal) -> ()

    let workspace action =
        let root = Path.Combine(Path.GetTempPath(), "RiftHarness.T053-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(root) |> ignore

        try
            Workspace.initialize root |> ignore
            action root
        finally
            if Directory.Exists(root) then Directory.Delete(root, true)

    let json (text: string) =
        use document = JsonDocument.Parse(text)
        document.RootElement.Clone()

    let eventId (number: int) = "EV-" + number.ToString("D26")
    let observationId (number: int) = "OBS-" + number.ToString("D26")
    let sha character = String(character, 64)

    let source sourceKind =
        { SourceKind = sourceKind
          RepositoryCommit = ResearchValue.Unknown
          RepositoryPath = ResearchValue.Unknown
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = sha 'a'
          SourceEventId = ResearchValue.Unknown
          Resolvable = false }

    let payload toolClass =
        json $"{{\"toolClass\":\"{toolClass}\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":1,\"completedMonotonicNs\":2,\"resultSha256\":\"{sha 'c'}\"}}"

    let draft number observation evidence recorded payload =
        ResearchEventDraft.create (eventId number) observation evidence "tool.finished" recorded [ source "fixture" ] payload

    let synthetic number observation =
        draft number observation "synthetic-test-only" "2026-09-02T10:00:00.000Z" (payload "test")

    let append root ledger event = ResearchLedger.append root ledger event |> ignore

module ResearchLedgerTests =
    open T053

    let canonicalRoundTrip () =
        workspace (fun root ->
            let observation = observationId 1
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            let verified = ResearchLedger.verify root ledger
            assertEqual ResearchLedgerStatus.Valid verified.Status "Canonical ledger was rejected."
            assertEqual 1 verified.Events.Length "Roundtrip event count differs."
            let persisted = File.ReadAllBytes(ledger)
            assertEqual 0x0Auy persisted.[persisted.Length - 1] "JSONL lacks its final LF."
            let event = List.head verified.Events
            assertEqual (ResearchLedger.canonicalEventBytes event |> ResearchCanonical.appendLf) persisted "Roundtrip bytes differ.")

    let tamperIsNotRecoverableTail () =
        workspace (fun root ->
            let observation = observationId 2
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            append root ledger (synthetic 2 observation)
            let original = File.ReadAllText(ledger, Constants.Utf8NoBom)
            let needle = "\"toolClass\":\"test\""
            let index = original.IndexOf(needle, StringComparison.Ordinal)
            assertTrue (index >= 0) "Tamper fixture field is absent."
            let tampered = original.Substring(0, index) + "\"toolClass\":\"tampered\"" + original.Substring(index + needle.Length)
            File.WriteAllText(ledger, tampered, Constants.Utf8NoBom)
            let result = ResearchLedger.verify root ledger
            assertEqual ResearchLedgerStatus.Invalid result.Status "Changed non-final event was treated as recoverable tail.")

    let duplicateEventIdIsRejected () =
        workspace (fun root ->
            let observation = observationId 3
            let ledger = ResearchLedger.ledgerPath root observation
            let event = synthetic 1 observation
            append root ledger event
            expectFailure "DUPLICATE_EVENT_ID" (fun () -> append root ledger event))

    let unknownIsLiteralAndNullIsRejected () =
        workspace (fun root ->
            let observation = observationId 4
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            let persisted = File.ReadAllText(ledger, Constants.Utf8NoBom)
            assertTrue (persisted.Contains("\"providerId\":\"unknown\"", StringComparison.Ordinal)) "Unknown provider was not explicit."
            assertTrue (not (persisted.Contains(":null", StringComparison.Ordinal))) "Ledger contains JSON null."
            let invalid = draft 2 observation "synthetic-test-only" "2026-09-02T10:00:01.000Z" (json "{\"toolClass\":null,\"commandDigest\":\"unknown\",\"startedMonotonicNs\":\"unknown\",\"completedMonotonicNs\":\"unknown\",\"resultSha256\":\"unknown\"}")
            expectFailure "RESEARCH_JSON_INVALID" (fun () -> append root ledger invalid))

    let tornTailRequiresExplicitRecovery () =
        workspace (fun root ->
            let observation = observationId 5
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            use stream = new FileStream(ledger, FileMode.Append, FileAccess.Write, FileShare.None)
            let garbage = Constants.Utf8NoBom.GetBytes("{\"partial\":")
            stream.Write(garbage)
            stream.Flush(true)
            stream.Close()
            let originalHash = Internal.sha256File ledger
            let torn = ResearchLedger.verify root ledger
            assertEqual ResearchLedgerStatus.TornTail torn.Status "Torn tail was not classified."
            expectFailure "TORN_TAIL" (fun () -> append root ledger (synthetic 2 observation))
            let recovered = Path.Combine(Path.GetDirectoryName(ledger), "recovered.jsonl")
            let recoveredRelative = Workspace.relativePath (Workspace.paths root) recovered
            let recoveryPayload = json $"{{\"originalLedgerSha256\":\"{originalHash}\",\"verifiedPrefixSha256\":\"{torn.VerifiedPrefixSha256}\",\"tornTailSha256\":\"{Option.get torn.TornTailSha256}\",\"recoveredLedgerPath\":\"{recoveredRelative}\"}}"
            let recovery = ResearchEventDraft.create (eventId 2) observation "synthetic-test-only" "ledger.recovery.recorded" "2026-09-02T10:00:01.000Z" [ source "fixture" ] recoveryPayload
            let result = ResearchLedger.recoverTo root ledger recovered recovery
            assertEqual ResearchLedgerStatus.Valid result.Status "Recovered ledger did not verify."
            assertEqual originalHash (Internal.sha256File ledger) "Recovery modified the original ledger."
            assertEqual 2 result.Events.Length "Recovery event is missing.")

    let exclusiveWriterRejectsConcurrency () =
        workspace (fun root ->
            let observation = observationId 6
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            use held = new FileStream(ResearchLedger.lockPath ledger, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
            expectFailure "CONCURRENT_WRITER" (fun () -> append root ledger (synthetic 2 observation)))

    let monotonicClockAllowsWallClockSkewOnly () =
        workspace (fun root ->
            let observation = observationId 7
            let ledger = ResearchLedger.ledgerPath root observation
            let first = { synthetic 1 observation with OccurredAtUtc = ResearchValue.Known "2026-09-02T09:59:59.000Z"; MonotonicClockId = ResearchValue.Known "clock-a"; MonotonicTimeNs = ResearchValue.Known 10L }
            let skewed = { synthetic 2 observation with RecordedAtUtc = "2026-09-02T10:00:01.000Z"; OccurredAtUtc = ResearchValue.Known "2026-09-02T09:59:58.000Z"; MonotonicClockId = ResearchValue.Known "clock-a"; MonotonicTimeNs = ResearchValue.Known 11L }
            append root ledger first
            append root ledger skewed
            assertEqual ResearchLedgerStatus.Valid (ResearchLedger.verify root ledger).Status "Wall-clock skew invalidated monotonic evidence."
            let reversed = { synthetic 3 observation with RecordedAtUtc = "2026-09-02T10:00:02.000Z"; MonotonicClockId = ResearchValue.Known "clock-a"; MonotonicTimeNs = ResearchValue.Known 9L }
            expectFailure "RESEARCH_CLOCK_INVALID" (fun () -> append root ledger reversed))

    let secretsAreRedactedBeforePersistence () =
        workspace (fun root ->
            let observation = observationId 8
            let ledger = ResearchLedger.ledgerPath root observation
            let secretPayload = json $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":1,\"completedMonotonicNs\":2,\"resultSha256\":\"{sha 'c'}\",\"password\":\"hunter2\",\"contact\":\"alice@example.org\",\"localPath\":\"/Users/alice/private.txt\",\"message\":\"Bearer abc.def host=terra tailnet=terra.ts.net accountId=acct-42 billing=invoice-7 ipv6=2001:db8:85a3::8a2e:370:7334\",\"providerId\":\"provider-private-42\"}}"
            let redacted, changed = ResearchCanonical.redactAndCanonicalizePayload (HarnessConfig.load (Workspace.paths root)).Redaction secretPayload
            let persisted = Constants.Utf8NoBom.GetString(redacted)
            assertTrue changed "Secret fixture was not recognized by the redaction policy."
            for forbidden in [ "hunter2"; "alice@example.org"; "/Users/alice/private.txt"; "Bearer abc.def"; "terra.ts.net"; "acct-42"; "invoice-7"; "2001:db8:85a3::8a2e:370:7334"; "provider-private-42" ] do
                assertTrue (not (persisted.Contains(forbidden, StringComparison.Ordinal))) $"Secret persisted: {forbidden}"
            assertTrue (persisted.Contains("[REDACTED:", StringComparison.Ordinal)) "Typed redaction marker is absent."
            expectFailure "RESEARCH_SCHEMA_INVALID" (fun () -> append root ledger (draft 1 observation "synthetic-test-only" "2026-09-02T10:00:00.000Z" secretPayload))
            assertTrue (not (File.Exists ledger)) "Rejected secret-bearing payload created a ledger.")

    let evidenceClassesStaySeparated () =
        workspace (fun root ->
            let observation = observationId 9
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            let prospective = { draft 2 observation "prospective-observed" "2026-09-02T10:00:01.000Z" (payload "test") with SourceRefs = [ source "agent-event" ] }
            expectFailure "EVIDENCE_CLASS_INVALID" (fun () -> append root ledger prospective)
            let otherObservation = observationId 10
            let prospectiveLedger = ResearchLedger.ledgerPath root otherObservation
            append root prospectiveLedger { prospective with EventId = eventId 3; ObservationId = otherObservation }
            assertEqual "prospective-observed" (ResearchLedger.readVerified root prospectiveLedger |> List.head).Body.EvidenceClass "Evidence class changed in roundtrip.")

    let harnessEventReferencesAreStrictlyEarlierAndHashBound () =
        workspace (fun root ->
            let observation = observationId 11
            let ledger = ResearchLedger.ledgerPath root observation
            append root ledger (synthetic 1 observation)
            let earlier = ResearchLedger.readVerified root ledger |> List.head
            let reference eventId hash =
                { source "harness-event" with
                    RepositoryPath = ResearchValue.Unknown
                    SourceEventId = eventId
                    ArtifactSha256 = hash
                    Resolvable = true }
            let candidate number referenceValue =
                { synthetic number observation with SourceRefs = [ referenceValue ] }
            let cases =
                [ "missing", reference ResearchValue.Unknown (sha 'd')
                  "self", reference (ResearchValue.Known(eventId 3)) (sha 'd')
                  "future", reference (ResearchValue.Known(eventId 99)) (sha 'd')
                  "wrong-hash", reference (ResearchValue.Known earlier.Body.EventId) (sha 'd')
                  "cross-observation", reference (ResearchValue.Known earlier.Body.EventId) earlier.EventHash ]
            cases
            |> List.iteri (fun index (_, sourceValue) ->
                let target = if index = 4 then observationId 12 else observation
                let draft = { candidate (index + 2) sourceValue with ObservationId = target }
                let targetLedger = if index = 4 then ResearchLedger.ledgerPath root target else ledger
                expectFailure "RESEARCH_SOURCE_INVALID" (fun () -> append root targetLedger draft))
            let valid = candidate 20 (reference (ResearchValue.Known earlier.Body.EventId) earlier.EventHash)
            append root ledger valid
            assertEqual 2 (ResearchLedger.readVerified root ledger).Length "Valid earlier harness-event reference was rejected.")

    let collectorPayloadContractsRejectMalformedValues () =
        workspace (fun root ->
            let observation = observationId 13
            let ledger = ResearchLedger.ledgerPath root observation
            let validPayload = $"{{\"agentId\":\"agent-test\",\"agentRole\":\"builder\",\"promptSha256\":\"{sha 'a'}\",\"toolchainSha256\":\"{sha 'b'}\"}}"
            let draftWith payload =
                { ResearchEventDraft.create (eventId 1) observation "synthetic-test-only" "agent.run.started" "2026-09-02T10:00:00.000Z" [ source "fixture" ] (json payload) with
                    ActorId = ResearchValue.Known "agent-test"
                    ActorRole = ResearchValue.Known "agent" }
            let cases =
                [ "missing", validPayload.Replace(",\"toolchainSha256\":\"" + sha 'b' + "\"", "", StringComparison.Ordinal)
                  "null", validPayload.Replace("\"promptSha256\":\"" + sha 'a' + "\"", "\"promptSha256\":null", StringComparison.Ordinal)
                  "enum", validPayload.Replace("\"builder\"", "\"other\"", StringComparison.Ordinal)
                  "hash", validPayload.Replace(sha 'a', "not-a-hash", StringComparison.Ordinal)
                  "unexpected", validPayload.Substring(0, validPayload.Length - 1) + ",\"extra\":\"x\"}" ]
            cases
            |> List.iter (fun (_, payload) ->
                expectFailure "RESEARCH_" (fun () -> append root ledger (draftWith payload)))
            append root ledger (draftWith validPayload)
            assertEqual 1 (ResearchLedger.readVerified root ledger).Length "Valid strict collector payload was rejected.")

    let gateAndVerificationPayloadContractsAreTypedAndExact () =
        workspace (fun root ->
            let observation = observationId 14
            let ledger = ResearchLedger.ledgerPath root observation
            let tree = String('d', 64)
            let evidence = sha 'e'
            let gatePayload = $"{{\"attempt\":1,\"gateId\":\"gate-1\",\"targetTreeId\":\"{tree}\"}}"
            let verificationPayload = $"{{\"attempt\":1,\"evidenceSha256\":\"{evidence}\",\"stageId\":\"gate-1\",\"targetTreeId\":\"{tree}\"}}"
            let draft eventType number payload =
                ResearchEventDraft.create (eventId number) observation "synthetic-test-only" eventType "2026-09-02T10:00:00.000Z" [ source "fixture" ] (json payload)
            let gateCases =
                [ "RESEARCH_SCHEMA_INVALID", gatePayload.Replace("\"attempt\":1", "\"attempt\":\"1\"", StringComparison.Ordinal)
                  "RESEARCH_SCHEMA_INVALID", gatePayload.Replace("\"attempt\":1", "\"attempt\":0", StringComparison.Ordinal)
                  "RESEARCH_SCHEMA_INVALID", gatePayload.Replace(tree, "not-an-object-id", StringComparison.Ordinal)
                  "RESEARCH_JSON_INVALID", gatePayload.Replace("\"gateId\":\"gate-1\"", "\"gateId\":\"gate-1\",\"gateId\":\"gate-2\"", StringComparison.Ordinal)
                  "RESEARCH_SCHEMA_INVALID", gatePayload.Substring(0, gatePayload.Length - 1) + ",\"extra\":true}" ]
            gateCases
            |> List.iteri (fun index (failureCode, payload) -> expectFailure failureCode (fun () -> append root ledger (draft "gate.started" (index + 1) payload)))
            // The preregistered contract requires a positive integer attempt;
            // only the target tree may remain explicitly unknown.
            let unknownPayload = "{\"attempt\":1,\"gateId\":\"gate-unknown\",\"targetTreeId\":\"unknown\"}"
            append root ledger (draft "gate.started" 9 unknownPayload)
            append root ledger (draft "gate.started" 10 gatePayload)
            let failureCases =
                [ "missing", verificationPayload.Replace(",\"evidenceSha256\":\"" + evidence + "\"", "", StringComparison.Ordinal)
                  "string-attempt", verificationPayload.Replace("\"attempt\":1", "\"attempt\":\"1\"", StringComparison.Ordinal)
                  "bad-evidence", verificationPayload.Replace(evidence, "not-a-hash", StringComparison.Ordinal)
                  "extra", verificationPayload.Substring(0, verificationPayload.Length - 1) + ",\"extra\":true}" ]
            failureCases
            |> List.iteri (fun index (_, payload) -> expectFailure "RESEARCH_SCHEMA_INVALID" (fun () -> append root ledger (draft "verify.failed" (index + 11) payload)))
            append root ledger (draft "verify.failed" 20 verificationPayload)
            assertEqual 3 (ResearchLedger.readVerified root ledger).Length "Typed gate and verification payloads were rejected.")

    let closedLifecycleAndPrivacyAreFailClosed () =
        workspace (fun root ->
            let observation = observationId 15
            let ledger = ResearchLedger.ledgerPath root observation
            let appendEvent number eventType payload =
                ResearchLedger.append root ledger (ResearchEventDraft.create (eventId number) observation "synthetic-test-only" eventType "2026-09-02T10:00:00.000Z" [ source "fixture" ] (json payload)) |> ignore
            appendEvent 1 "protocol.frozen" $"{{\"protocolId\":\"p\",\"protocolVersion\":\"v1\",\"protocolBundleSha256\":\"{sha 'a'}\",\"freezeAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"
            appendEvent 2 "observation.started" $"{{\"targetTaskId\":\"T-053\",\"baselineCommit\":\"{String('b', 40)}\",\"collectorVersion\":\"test\",\"nonInterferenceSnapshotSha256\":\"{sha 'c'}\",\"activationGuardSha256\":\"{sha 'd'}\"}}"
            appendEvent 3 "activity.state.changed" "{\"fromActivityState\":\"idle\",\"toActivityState\":\"agent-active\",\"reasonCode\":\"test\"}"
            appendEvent 4 "outcome.observed" $"{{\"taskOutcome\":\"accepted\",\"hypothesisResult\":\"inconclusive\",\"resultCommit\":\"{String('e', 40)}\",\"resultTreeId\":\"{String('f', 40)}\",\"reasonCode\":\"test\"}}"
            appendEvent 5 "observation.closed" $"{{\"eventCount\":5,\"sourceManifestSha256\":\"{sha 'f'}\",\"outcomeEventId\":\"{eventId 4}\",\"closedAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"
            expectFailure "OBSERVATION_CLOSED" (fun () -> append root ledger (synthetic 6 observation))
            let privatePaths = { synthetic 7 (observationId 16) with ChangedFiles = ResearchValue.Known 1L; ChangedPaths = ResearchValue.Known [ "/Users/alice/secret.txt" ] }
            let privateLedger = ResearchLedger.ledgerPath root (observationId 16)
            append root privateLedger privatePaths
            let persisted = ResearchLedger.readVerified root privateLedger |> List.head
            assertEqual ResearchValue.Unknown persisted.Body.ChangedPaths "Unsafe changedPaths was not replaced by literal unknown.")

    let all =
        [ "research canonical roundtrip", canonicalRoundTrip
          "research tamper detection", tamperIsNotRecoverableTail
          "research duplicate event id", duplicateEventIdIsRejected
          "research literal unknown and null", unknownIsLiteralAndNullIsRejected
          "research torn tail recovery", tornTailRequiresExplicitRecovery
          "research concurrent writer", exclusiveWriterRejectsConcurrency
          "research clock skew", monotonicClockAllowsWallClockSkewOnly
          "research secret redaction", secretsAreRedactedBeforePersistence
          "research evidence separation", evidenceClassesStaySeparated
          "research harness event reference matrix", harnessEventReferencesAreStrictlyEarlierAndHashBound
          "research collector payload contracts", collectorPayloadContractsRejectMalformedValues
          "research gate and verification payload contracts", gateAndVerificationPayloadContractsAreTypedAndExact
          "research closed lifecycle and changed-path privacy", closedLifecycleAndPrivacyAreFailClosed ]

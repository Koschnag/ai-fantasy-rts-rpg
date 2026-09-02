namespace RiftHarness

open System
open System.Globalization
open System.Text.Json

/// A supplied classification rule.  The harness never guesses a component from a path.
type ArchitecturePathRule =
    { Prefix: string
      FileClass: string
      Component: string }

/// A file observation at the result tree, with an optional exact baseline name for a rename.
type ArchitectureFileObservation =
    { Path: string
      BaselinePath: string option
      ResultLines: int option
      BaselineLines: int option
      IsBinary: bool
      SourceSha256: string
      Changed: bool }

type ArchitectureProjectReference =
    { FromComponent: string
      ToComponent: string
      Direction: string }

type ArchitectureFinding =
    { FindingId: string
      Source: string
      Confirmed: bool }

/// Structured receipts are deliberately the only source for analyzer and test counts.
type ArchitectureStructuredReceipt =
    { Entries: (string * string list) list }

type ArchitectureComplexityReceipt =
    { Method: string
      Values: (string * int) list }

type ArchitectureCheckpointInput =
    { CheckpointId: string
      BaselineCommit: string
      ResultCommit: string
      AcceptedTaskId: string
      AcceptedTreeId: string
      PathMapVersion: string
      PathMap: ArchitecturePathRule list
      Files: ArchitectureFileObservation list
      BaselineReferences: ArchitectureProjectReference list
      ResultReferences: ArchitectureProjectReference list
      Findings: ArchitectureFinding list
      AnalyzerReceipt: ArchitectureStructuredReceipt option
      TestReceipt: ArchitectureStructuredReceipt option
      BaselineTestReceipt: ArchitectureStructuredReceipt option
      ComplexityReceipt: ArchitectureComplexityReceipt option }

type ArchitectureFileRow =
    { RepoRelativePath: string
      BaselinePath: string
      FileClass: string
      Component: string
      Lines: string
      BaselineLines: string
      LineDelta: string
      AnalyzerWarnings: string
      TestCount: string
      ComplexityMethod: string
      Complexity: string
      SourceSha256: string
      GateCoupled: bool }

type ArchitectureTrendRow =
    { Metric: string
      Value: string
      GateCoupled: bool }

type ArchitectureDependencyRow =
    { FromComponent: string
      ToComponent: string
      Direction: string
      Change: string
      GateCoupled: bool }

type ArchitectureIntegrationRow =
    { Name: string
      RepoRelativePath: string
      LineDelta: string
      GateCoupled: bool }

type ArchitectureCheckpointSnapshot =
    { FileRows: ArchitectureFileRow list
      TrendRows: ArchitectureTrendRow list
      DependencyRows: ArchitectureDependencyRow list
      IntegrationRows: ArchitectureIntegrationRow list
      TopFiles: string list
      TopGrowth: string list
      ConfirmedFindingIds: string list
      FileInventoryBytes: byte array
      DependencyInventoryBytes: byte array
      AnalyzerInventoryBytes: byte array
      TestInventoryBytes: byte array
      EventPayloadBytes: byte array
      GateCoupled: bool }

type BoundArchitectureCheckpoint =
    { CheckpointId: string
      AcceptedTaskId: string
      AcceptedTreeId: string
      BaselineCommit: string
      ResultCommit: string
      PathMapVersion: string
      FileRows: ArchitectureFileRow list
      DependencyRows: ArchitectureDependencyRow list
      AnalyzerInventoryBytes: byte array
      TestInventoryBytes: byte array
      ConfirmedFindingIds: string list
      GateCoupled: bool }

[<RequireQualifiedAccess>]
module ResearchArchitecture =
    let private invariant (value: int) = value.ToString(CultureInfo.InvariantCulture)

    let private isSafeRepoPath (path: string) =
        not (String.IsNullOrWhiteSpace(path))
        && not (path.StartsWith("/", StringComparison.Ordinal))
        && not (path.Contains('\\'))
        && path.Split('/')
           |> Array.forall (fun part -> not (String.IsNullOrWhiteSpace(part)) && part <> "." && part <> "..")

    let private requireSafePath label path =
        if not (isSafeRepoPath path) then
            Internal.fail $"ARCHITECTURE_UNSAFE_{label}: {path}"

    let private requireIdentifier label value =
        if String.IsNullOrWhiteSpace(value) then
            Internal.fail $"ARCHITECTURE_EMPTY_{label}"

    let private requireObjectId label value =
        requireIdentifier label value

        if (value.Length <> 40 && value.Length <> 64) || (value |> Seq.exists (fun character -> not (Uri.IsHexDigit(character)))) then
            Internal.fail $"ARCHITECTURE_INVALID_{label}"

    let private prefixMatches (prefix: string) (path: string) =
        path = prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal)

    let private classify rules path =
        match rules |> List.filter (fun rule -> prefixMatches rule.Prefix path) |> List.sortByDescending (fun rule -> rule.Prefix.Length) with
        | rule :: _ -> rule.FileClass, rule.Component
        | [] -> "unknown", "unknown"

    let private receiptMap (receipt: ArchitectureStructuredReceipt option) =
        receipt
        |> Option.map (fun supplied ->
            let rows =
                supplied.Entries
                |> List.map (fun (path, ids) ->
                    requireSafePath "RECEIPT_PATH" path
                    path, (ids |> List.distinct |> List.sort))

            if (rows |> List.map fst |> Set.ofList |> Set.count) <> rows.Length then
                Internal.fail "ARCHITECTURE_DUPLICATE_RECEIPT_PATH"

            rows |> Map.ofList)

    let private complexityMap (receipt: ArchitectureComplexityReceipt option) =
        receipt
        |> Option.map (fun supplied ->
            requireIdentifier "COMPLEXITY_METHOD" supplied.Method
            let rows =
                supplied.Values
                |> List.map (fun (path, value) ->
                    requireSafePath "COMPLEXITY_PATH" path
                    if value < 0 then Internal.fail "ARCHITECTURE_NEGATIVE_COMPLEXITY"
                    path, value)

            if (rows |> List.map fst |> Set.ofList |> Set.count) <> rows.Length then
                Internal.fail "ARCHITECTURE_DUPLICATE_COMPLEXITY_PATH"

            rows |> Map.ofList)

    let private canonicalBytes<'T> (value: 'T) =
        JsonSerializer.Serialize<'T>(value) |> ResearchCanonical.canonicalizeJson

    let private inventoryHash bytes = Internal.sha256Hex bytes

    let private valueOrUnknown value = value |> Option.map invariant |> Option.defaultValue "unknown"

    let private lineDelta observation =
        if observation.IsBinary then None
        else
            match observation.ResultLines, observation.BaselineLines with
            | Some result, Some baseline -> Some(result - baseline)
            | _ -> None

    let private knownSum values =
        if values |> List.exists Option.isNone then "unknown"
        else values |> List.choose id |> List.sum |> invariant

    let private referenceKey (reference: ArchitectureProjectReference) =
        reference.FromComponent, reference.ToComponent, reference.Direction

    let private specialIntegrations =
        [ "CommandLoopRunner", "src/Riftward.App/Command/CommandLoopRunner.cs"
          "CommandReportSchema", "src/Riftward.App/Command/CommandReportSchema.cs"
          "SessionEngine", "src/Riftward.Session/SessionEngine.cs" ]

    let create (input: ArchitectureCheckpointInput) =
        [ input.CheckpointId, "CHECKPOINT_ID"
          input.AcceptedTaskId, "ACCEPTED_TASK_ID"
          input.PathMapVersion, "PATH_MAP_VERSION" ]
        |> List.iter (fun (value, label) -> requireIdentifier label value)

        [ input.BaselineCommit, "BASELINE_COMMIT"
          input.ResultCommit, "RESULT_COMMIT"
          input.AcceptedTreeId, "ACCEPTED_TREE_ID" ]
        |> List.iter (fun (value, label) -> requireObjectId label value)

        input.PathMap
        |> List.iter (fun rule ->
            requireSafePath "PATH_MAP_PREFIX" rule.Prefix
            requireIdentifier "FILE_CLASS" rule.FileClass
            requireIdentifier "COMPONENT" rule.Component)

        input.Files
        |> List.iter (fun observation ->
            requireSafePath "FILE_PATH" observation.Path
            observation.BaselinePath |> Option.iter (requireSafePath "BASELINE_PATH")
            if observation.ResultLines |> Option.exists (fun value -> value < 0) then Internal.fail "ARCHITECTURE_NEGATIVE_RESULT_LINES"
            if observation.BaselineLines |> Option.exists (fun value -> value < 0) then Internal.fail "ARCHITECTURE_NEGATIVE_BASELINE_LINES")

        if (input.Files |> List.map (fun observation -> observation.Path) |> Set.ofList |> Set.count) <> input.Files.Length then
            Internal.fail "ARCHITECTURE_DUPLICATE_FILE_PATH"

        (input.BaselineReferences @ input.ResultReferences)
        |> List.iter (fun reference ->
            requireIdentifier "REFERENCE_FROM" reference.FromComponent
            requireIdentifier "REFERENCE_TO" reference.ToComponent
            requireIdentifier "REFERENCE_DIRECTION" reference.Direction)

        let analyzer = receiptMap input.AnalyzerReceipt
        let tests = receiptMap input.TestReceipt
        let baselineTests = receiptMap input.BaselineTestReceipt
        let complexity = complexityMap input.ComplexityReceipt

        let fileRows =
            input.Files
            |> List.sortBy (fun observation -> observation.Path)
            |> List.map (fun observation ->
                let fileClass, componentId = classify input.PathMap observation.Path
                let lines = if observation.IsBinary then None else observation.ResultLines
                let baselineLines = if observation.IsBinary then None else observation.BaselineLines
                let warnings = analyzer |> Option.bind (Map.tryFind observation.Path) |> Option.map (List.length >> invariant) |> Option.defaultValue "unknown"
                let testCount = tests |> Option.bind (Map.tryFind observation.Path) |> Option.map (List.length >> invariant) |> Option.defaultValue "unknown"
                let complexityValue = complexity |> Option.bind (Map.tryFind observation.Path) |> Option.map invariant |> Option.defaultValue "unknown"
                let methodName = input.ComplexityReceipt |> Option.map (fun value -> value.Method) |> Option.defaultValue "unknown"
                { RepoRelativePath = observation.Path
                  BaselinePath = observation.BaselinePath |> Option.defaultValue "unknown"
                  FileClass = fileClass
                  Component = componentId
                  Lines = valueOrUnknown lines
                  BaselineLines = valueOrUnknown baselineLines
                  LineDelta = lineDelta observation |> valueOrUnknown
                  AnalyzerWarnings = warnings
                  TestCount = testCount
                  ComplexityMethod = methodName
                  Complexity = complexityValue
                  SourceSha256 = observation.SourceSha256
                  GateCoupled = false })

        let productionRows = fileRows |> List.filter (fun row -> row.FileClass = "production")
        let testRows = fileRows |> List.filter (fun row -> row.FileClass = "test")
        let productionLines = if input.PathMap.IsEmpty then "unknown" else productionRows |> List.map (fun row -> if row.Lines = "unknown" then None else Some(Int32.Parse(row.Lines, CultureInfo.InvariantCulture))) |> knownSum
        let testLines = if input.PathMap.IsEmpty then "unknown" else testRows |> List.map (fun row -> if row.Lines = "unknown" then None else Some(Int32.Parse(row.Lines, CultureInfo.InvariantCulture))) |> knownSum
        let binaryChanged = input.Files |> List.filter (fun file -> file.Changed && file.IsBinary) |> List.length |> invariant
        let changedProduction = input.Files |> List.filter (fun file -> file.Changed && fst (classify input.PathMap file.Path) = "production")
        let changedModules = changedProduction |> List.map (fun file -> snd (classify input.PathMap file.Path)) |> List.distinct |> List.length |> invariant

        let confirmedFindingIds =
            input.Findings
            |> List.filter (fun finding -> finding.Confirmed && (finding.Source = "boundary-validator" || finding.Source = "review"))
            |> List.map (fun finding -> requireIdentifier "FINDING_ID" finding.FindingId; finding.FindingId)
            |> List.distinct
            |> List.sort

        let baselineReferences = input.BaselineReferences |> List.map referenceKey |> Set.ofList
        let resultReferences = input.ResultReferences |> List.map referenceKey |> Set.ofList
        let dependencyRows =
            Set.union baselineReferences resultReferences
            |> Set.toList
            |> List.sort
            |> List.map (fun (fromComponent, toComponent, direction) ->
                { FromComponent = fromComponent
                  ToComponent = toComponent
                  Direction = direction
                  Change = if resultReferences.Contains((fromComponent, toComponent, direction)) && not (baselineReferences.Contains((fromComponent, toComponent, direction))) then "added" elif baselineReferences.Contains((fromComponent, toComponent, direction)) && not (resultReferences.Contains((fromComponent, toComponent, direction))) then "removed" else "unchanged"
                  GateCoupled = false })

        let topFiles =
            fileRows
            |> List.filter (fun row -> (row.FileClass = "production" || row.FileClass = "test") && row.Lines <> "unknown")
            |> List.sortBy (fun row -> -Int32.Parse(row.Lines, CultureInfo.InvariantCulture), row.RepoRelativePath)
            |> List.truncate 10
            |> List.map (fun row -> row.RepoRelativePath)

        let topGrowth =
            fileRows
            |> List.filter (fun row -> row.LineDelta <> "unknown")
            |> List.sortBy (fun row -> -Int32.Parse(row.LineDelta, CultureInfo.InvariantCulture), row.RepoRelativePath)
            |> List.truncate 10
            |> List.map (fun row -> row.RepoRelativePath)

        let integrationRows =
            specialIntegrations
            |> List.map (fun (name, path) ->
                let delta = fileRows |> List.tryFind (fun row -> row.RepoRelativePath = path) |> Option.map (fun row -> row.LineDelta) |> Option.defaultValue "unknown"
                { Name = name; RepoRelativePath = path; LineDelta = delta; GateCoupled = false })

        let integrationConcentration =
            let allDeltas = productionRows |> List.map (fun row -> if row.LineDelta = "unknown" then None else Some(abs (Int32.Parse(row.LineDelta, CultureInfo.InvariantCulture))))
            let specialDeltas = integrationRows |> List.map (fun row -> if row.LineDelta = "unknown" then None else Some(abs (Int32.Parse(row.LineDelta, CultureInfo.InvariantCulture))))
            if (allDeltas |> List.exists Option.isNone) || (specialDeltas |> List.exists Option.isNone) then "unknown"
            else
                let denominator = allDeltas |> List.choose id |> List.sum
                if denominator = 0 then "unknown"
                else
                    let numerator = specialDeltas |> List.choose id |> List.sum
                    (decimal numerator / decimal denominator).ToString("0.######", CultureInfo.InvariantCulture)

        let componentShareRows =
            let components = productionRows |> List.map (fun row -> row.Component) |> List.distinct |> List.sort
            let productionValues = productionRows |> List.map (fun row -> if row.Lines = "unknown" then None else Some(Int32.Parse(row.Lines, CultureInfo.InvariantCulture)))
            let changeValues = productionRows |> List.map (fun row -> if row.LineDelta = "unknown" then None else Some(abs (Int32.Parse(row.LineDelta, CultureInfo.InvariantCulture))) )
            let totalProduction = if productionValues |> List.exists Option.isNone then None else Some(productionValues |> List.choose id |> List.sum)
            let totalChange = if changeValues |> List.exists Option.isNone then None else Some(changeValues |> List.choose id |> List.sum)
            components
            |> List.collect (fun componentId ->
                let componentProduction = productionRows |> List.filter (fun row -> row.Component = componentId) |> List.map (fun row -> if row.Lines = "unknown" then None else Some(Int32.Parse(row.Lines, CultureInfo.InvariantCulture)))
                let componentChange = productionRows |> List.filter (fun row -> row.Component = componentId) |> List.map (fun row -> if row.LineDelta = "unknown" then None else Some(abs (Int32.Parse(row.LineDelta, CultureInfo.InvariantCulture))))
                let share values total =
                    if values |> List.exists Option.isNone then "unknown"
                    else
                        match total with
                        | Some denominator when denominator > 0 ->
                            (decimal (values |> List.choose id |> List.sum) / decimal denominator).ToString("0.######", CultureInfo.InvariantCulture)
                        | _ -> "unknown"
                [ $"component-share/{componentId}", share componentProduction totalProduction
                  $"change-share/{componentId}", share componentChange totalChange ])

        let trendRows =
            let testInventoryCount receipt =
                receipt
                |> Option.map (fun supplied -> supplied.Entries |> List.collect snd |> List.distinct |> List.length)

            let testGrowth =
                match testInventoryCount input.TestReceipt, testInventoryCount input.BaselineTestReceipt with
                | Some current, Some baseline -> invariant (current - baseline)
                | _ -> "unknown"

            [ "production-lines", productionLines
              "test-lines", testLines
              "binary-files-changed", binaryChanged
              "production-files-changed", (changedProduction.Length |> invariant)
              "modules-touched", changedModules
              "reference-edges-added", (dependencyRows |> List.filter (fun row -> row.Change = "added") |> List.length |> invariant)
              "reference-edges-removed", (dependencyRows |> List.filter (fun row -> row.Change = "removed") |> List.length |> invariant)
              "boundary-findings-confirmed", (confirmedFindingIds.Length |> invariant)
              "analyzer-warnings", if input.AnalyzerReceipt.IsSome then fileRows |> List.map (fun row -> if row.AnalyzerWarnings = "unknown" then 0 else Int32.Parse(row.AnalyzerWarnings, CultureInfo.InvariantCulture)) |> List.sum |> invariant else "unknown"
              "test-count", testInventoryCount input.TestReceipt |> Option.map invariant |> Option.defaultValue "unknown"
              "test-growth", testGrowth
              "integration-concentration", integrationConcentration ]
            |> List.append componentShareRows
            |> List.map (fun (metric, value) -> { Metric = metric; Value = value; GateCoupled = false })

        let fileInventoryBytes = canonicalBytes fileRows
        let dependencyInventoryBytes = canonicalBytes dependencyRows
        let analyzerInventoryBytes =
            match input.AnalyzerReceipt with
            | Some receipt -> canonicalBytes receipt
            | None -> ResearchCanonical.canonicalizeJson "{\"availability\":\"unknown\"}"
        let testInventoryBytes =
            match input.TestReceipt with
            | Some receipt -> canonicalBytes receipt
            | None -> ResearchCanonical.canonicalizeJson "{\"availability\":\"unknown\"}"
        let eventPayloadBytes =
            Internal.jsonBytes false (fun writer ->
                writer.WriteStartObject()
                writer.WriteString("checkpointId", input.CheckpointId)
                writer.WriteString("baselineCommit", input.BaselineCommit)
                writer.WriteString("resultCommit", input.ResultCommit)
                writer.WriteString("pathMapVersion", input.PathMapVersion)
                writer.WriteString("fileInventorySha256", inventoryHash fileInventoryBytes)
                writer.WriteString("dependencyInventorySha256", inventoryHash dependencyInventoryBytes)
                writer.WriteString("analyzerInventorySha256", inventoryHash analyzerInventoryBytes)
                writer.WriteString("testInventorySha256", inventoryHash testInventoryBytes)
                writer.WriteString("acceptedTaskId", input.AcceptedTaskId)
                writer.WriteString("acceptedTreeId", input.AcceptedTreeId)
                writer.WriteStartArray("confirmedFindingIds")
                confirmedFindingIds |> List.iter writer.WriteStringValue
                writer.WriteEndArray()
                writer.WriteBoolean("gateCoupled", false)
                writer.WriteEndObject())
            |> fun bytes -> Constants.Utf8NoBom.GetString(bytes) |> ResearchCanonical.canonicalizeJson

        { FileRows = fileRows
          TrendRows = trendRows
          DependencyRows = dependencyRows
          IntegrationRows = integrationRows
          TopFiles = topFiles
          TopGrowth = topGrowth
          ConfirmedFindingIds = confirmedFindingIds
          FileInventoryBytes = fileInventoryBytes
          DependencyInventoryBytes = dependencyInventoryBytes
          AnalyzerInventoryBytes = analyzerInventoryBytes
          TestInventoryBytes = testInventoryBytes
          EventPayloadBytes = eventPayloadBytes
          GateCoupled = false }

    /// These are the only architecture artifacts an event may bind.  Callers must
    /// store them explicitly and put their exact hashes in sourceRefs; no export
    /// discovers files by directory scan.
    let artifactBytes (snapshot: ArchitectureCheckpointSnapshot) =
        [ "files", snapshot.FileInventoryBytes
          "dependencies", snapshot.DependencyInventoryBytes
          "analyzer", snapshot.AnalyzerInventoryBytes
          "tests", snapshot.TestInventoryBytes ]

    let private jsonString (name: string) (element: JsonElement) : string =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
        | _ -> Internal.fail $"ARCHITECTURE_INVENTORY_INVALID: {name} is missing."

    let private jsonBool (name: string) (element: JsonElement) : bool =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.True -> true
        | true, value when value.ValueKind = JsonValueKind.False -> false
        | _ -> Internal.fail $"ARCHITECTURE_INVENTORY_INVALID: {name} is missing."

    let private parseFileRows (bytes: byte array) =
        use document = JsonDocument.Parse(bytes)
        if document.RootElement.ValueKind <> JsonValueKind.Array then Internal.fail "ARCHITECTURE_INVENTORY_INVALID: files is not an array."
        document.RootElement.EnumerateArray()
        |> Seq.map (fun row ->
            { RepoRelativePath = jsonString "RepoRelativePath" row
              BaselinePath = jsonString "BaselinePath" row
              FileClass = jsonString "FileClass" row
              Component = jsonString "Component" row
              Lines = jsonString "Lines" row
              BaselineLines = jsonString "BaselineLines" row
              LineDelta = jsonString "LineDelta" row
              AnalyzerWarnings = jsonString "AnalyzerWarnings" row
              TestCount = jsonString "TestCount" row
              ComplexityMethod = jsonString "ComplexityMethod" row
              Complexity = jsonString "Complexity" row
              SourceSha256 = jsonString "SourceSha256" row
              GateCoupled = jsonBool "GateCoupled" row })
        |> Seq.toList

    let private parseDependencyRows (bytes: byte array) =
        use document = JsonDocument.Parse(bytes)
        if document.RootElement.ValueKind <> JsonValueKind.Array then Internal.fail "ARCHITECTURE_INVENTORY_INVALID: dependencies is not an array."
        document.RootElement.EnumerateArray()
        |> Seq.map (fun row ->
            { FromComponent = jsonString "FromComponent" row
              ToComponent = jsonString "ToComponent" row
              Direction = jsonString "Direction" row
              Change = jsonString "Change" row
              GateCoupled = jsonBool "GateCoupled" row })
        |> Seq.toList

    let bind (payload: JsonElement) (files: byte array) (dependencies: byte array) (analyzer: byte array) (tests: byte array) =
        let canonicalHash (bytes: byte array) = ResearchCanonical.canonicalizeJson (Constants.Utf8NoBom.GetString(bytes)) |> Internal.sha256Hex
        let requiredHash name = jsonString name payload
        if canonicalHash files <> requiredHash "fileInventorySha256"
           || canonicalHash dependencies <> requiredHash "dependencyInventorySha256"
           || canonicalHash analyzer <> requiredHash "analyzerInventorySha256"
           || canonicalHash tests <> requiredHash "testInventorySha256" then
            Internal.fail "ARCHITECTURE_BINDING_INVALID: inventory hash mismatch."

        let gateCoupled = jsonBool "gateCoupled" payload
        if gateCoupled then Internal.fail "ARCHITECTURE_BINDING_INVALID: checkpoint is gate-coupled."
        let rows = parseFileRows files
        let dependenciesRows = parseDependencyRows dependencies
        if rows |> List.exists (fun row -> row.GateCoupled) || dependenciesRows |> List.exists (fun row -> row.GateCoupled) then
            Internal.fail "ARCHITECTURE_BINDING_INVALID: diagnostic inventory is gate-coupled."
        let findings =
            match payload.TryGetProperty("confirmedFindingIds") with
            | true, value when value.ValueKind = JsonValueKind.Array -> value.EnumerateArray() |> Seq.map (fun item -> if item.ValueKind <> JsonValueKind.String then Internal.fail "ARCHITECTURE_BINDING_INVALID: finding ID is invalid." else item.GetString()) |> Seq.distinct |> Seq.sort |> Seq.toList
            | _ -> Internal.fail "ARCHITECTURE_BINDING_INVALID: confirmed finding IDs are missing."
        { CheckpointId = jsonString "checkpointId" payload
          AcceptedTaskId = jsonString "acceptedTaskId" payload
          AcceptedTreeId = jsonString "acceptedTreeId" payload
          BaselineCommit = jsonString "baselineCommit" payload
          ResultCommit = jsonString "resultCommit" payload
          PathMapVersion = jsonString "pathMapVersion" payload
          FileRows = rows
          DependencyRows = dependenciesRows
          AnalyzerInventoryBytes = analyzer
          TestInventoryBytes = tests
          ConfirmedFindingIds = findings
          GateCoupled = false }

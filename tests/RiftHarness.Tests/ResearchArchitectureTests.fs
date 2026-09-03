namespace RiftHarness.Tests

open System
open System.Text.Json
open RiftHarness

module ResearchArchitectureTests =
    let private require condition message =
        if not condition then
            failwith message

    let private baseInput files =
        { CheckpointId = "checkpoint-1"
          BaselineCommit = String.replicate 40 "a"
          ResultCommit = String.replicate 40 "b"
          AcceptedTaskId = "T-053"
          AcceptedTreeId = String.replicate 40 "c"
          PathMapVersion = "path-map-v1"
          PathMap =
            [ { Prefix = "src/Riftward.App"
                FileClass = "production"
                Component = "Riftward.App" }
              { Prefix = "src/Riftward.Session"
                FileClass = "production"
                Component = "Riftward.Session" }
              { Prefix = "tests"
                FileClass = "test"
                Component = "tests" } ]
          Files = files
          BaselineReferences = []
          ResultReferences = []
          Findings = []
          AnalyzerReceipt = None
          TestReceipt = None
          BaselineTestReceipt = None
          ComplexityReceipt = None }

    let binaryIsUnknown () =
        let snapshot =
            baseInput
                [ { Path = "src/Riftward.App/Asset.bin"
                    BaselinePath = None
                    ResultLines = Some 4
                    BaselineLines = Some 1
                    IsBinary = true
                    SourceSha256 = "binary"
                    Changed = true } ]
            |> ResearchArchitecture.create

        require (snapshot.FileRows.Head.Lines = "unknown") "binary line count must remain unknown"

        require
            (snapshot.TrendRows
             |> List.exists (fun row -> row.Metric = "binary-files-changed" && row.Value = "1"))
            "binary metric missing"

    let renamedFilePreservesBaselineName () =
        let snapshot =
            baseInput
                [ { Path = "src/Riftward.App/NewName.cs"
                    BaselinePath = Some "src/Riftward.App/OldName.cs"
                    ResultLines = Some 13
                    BaselineLines = Some 8
                    IsBinary = false
                    SourceSha256 = "source"
                    Changed = true } ]
            |> ResearchArchitecture.create

        require (snapshot.FileRows.Head.BaselinePath = "src/Riftward.App/OldName.cs") "rename baseline missing"
        require (snapshot.FileRows.Head.LineDelta = "5") "rename growth missing"

    let emptyPathMapAblatesClassification () =
        let input =
            baseInput
                [ { Path = "src/Riftward.App/File.cs"
                    BaselinePath = None
                    ResultLines = Some 2
                    BaselineLines = Some 1
                    IsBinary = false
                    SourceSha256 = "source"
                    Changed = true } ]

        let snapshot = { input with PathMap = [] } |> ResearchArchitecture.create
        require (snapshot.FileRows.Head.FileClass = "unknown") "empty path map must not infer a class"

        require
            (snapshot.TrendRows
             |> List.exists (fun row -> row.Metric = "production-lines" && row.Value = "unknown"))
            "ablated production lines must be unknown"

    let deterministicBytes () =
        let files =
            [ { Path = "tests/A.fs"
                BaselinePath = None
                ResultLines = Some 2
                BaselineLines = Some 1
                IsBinary = false
                SourceSha256 = "a"
                Changed = true }
              { Path = "src/Riftward.App/A.cs"
                BaselinePath = None
                ResultLines = Some 3
                BaselineLines = Some 2
                IsBinary = false
                SourceSha256 = "b"
                Changed = true } ]

        let first = baseInput files |> ResearchArchitecture.create
        let second = baseInput (List.rev files) |> ResearchArchitecture.create
        require (first.FileInventoryBytes = second.FileInventoryBytes) "file inventory is not deterministic"
        require (first.EventPayloadBytes = second.EventPayloadBytes) "event payload is not deterministic"

    let specialIntegrationPointsAreExplicit () =
        let files =
            [ "CommandLoopRunner"; "CommandReportSchema" ]
            |> List.map (fun name ->
                { Path = $"src/Riftward.App/Command/{name}.cs"
                  BaselinePath = None
                  ResultLines = Some 3
                  BaselineLines = Some 1
                  IsBinary = false
                  SourceSha256 = name
                  Changed = true })
            |> fun rows ->
                { Path = "src/Riftward.Session/SessionEngine.cs"
                  BaselinePath = None
                  ResultLines = Some 4
                  BaselineLines = Some 2
                  IsBinary = false
                  SourceSha256 = "session"
                  Changed = true }
                :: rows

        let snapshot = baseInput files |> ResearchArchitecture.create

        require
            (snapshot.IntegrationRows |> List.map (fun row -> row.Name) = [ "CommandLoopRunner"
                                                                            "CommandReportSchema"
                                                                            "SessionEngine" ])
            "special integration set changed"

    let neverGateCoupled () =
        let snapshot = baseInput [] |> ResearchArchitecture.create
        use eventDocument = JsonDocument.Parse(snapshot.EventPayloadBytes)
        require (not snapshot.GateCoupled) "snapshot became gate coupled"
        require (snapshot.FileRows |> List.forall (fun row -> not row.GateCoupled)) "file inventory became gate coupled"

        require
            (snapshot.TrendRows |> List.forall (fun row -> not row.GateCoupled))
            "trend inventory became gate coupled"

        require
            (not (eventDocument.RootElement.GetProperty("gateCoupled").GetBoolean()))
            "event payload became gate coupled"

    let hashBoundArtifactsRoundTrip () =
        let snapshot =
            baseInput
                [ { Path = "src/Riftward.App/A.cs"
                    BaselinePath = None
                    ResultLines = Some 3
                    BaselineLines = Some 2
                    IsBinary = false
                    SourceSha256 = "source"
                    Changed = true } ]
            |> ResearchArchitecture.create

        let artifacts = ResearchArchitecture.artifactBytes snapshot |> Map.ofList

        let bound =
            ResearchArchitecture.bind
                (JsonDocument.Parse(snapshot.EventPayloadBytes).RootElement.Clone())
                (artifacts["files"])
                (artifacts["dependencies"])
                (artifacts["analyzer"])
                (artifacts["tests"])

        require (bound.CheckpointId = "checkpoint-1") "bound checkpoint lost its identity"
        require (not bound.GateCoupled) "bound checkpoint became gate coupled"

    let all () =
        binaryIsUnknown ()
        renamedFilePreservesBaselineName ()
        emptyPathMapAblatesClassification ()
        deterministicBytes ()
        specialIntegrationPointsAreExplicit ()
        neverGateCoupled ()
        hashBoundArtifactsRoundTrip ()

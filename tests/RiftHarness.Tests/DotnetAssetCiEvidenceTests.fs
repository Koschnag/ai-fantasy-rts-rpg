namespace RiftHarness.Tests

open System
open System.IO
open System.Text.Json
open RiftHarness
open global.Json.Schema

[<RequireQualifiedAccess>]
module DotnetAssetCiEvidenceTests =
    let private root =
        let rec find path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                find (Directory.GetParent(path).FullName)

        find Environment.CurrentDirectory

    let private assertTrue condition message =
        if not condition then
            failwith message

    /// T-007 evidence is canonical, bounded and validates without network access.
    let deterministicEvidenceIsSchemaClosedAndSanitized () =
        let first = DotnetAssetCiEvidence.generate root
        let second = DotnetAssetCiEvidence.generate root
        assertTrue (first.CanonicalJson = second.CanonicalJson) "Equal in-process runs changed evidence bytes."
        assertTrue (first.CanonicalJson.Length <= DotnetAssetCiEvidence.MaxEvidenceBytes) "Evidence limit exceeded."
        assertTrue (first.CanonicalJson[first.CanonicalJson.Length - 1] = byte '\n') "Evidence lacks its single LF."
        let text = Constants.Utf8NoBom.GetString(first.CanonicalJson)
        assertTrue (not (text.Contains(root, StringComparison.Ordinal))) "Evidence leaked an absolute workspace path."

        assertTrue
            (not (text.Contains("family.glb", StringComparison.Ordinal)))
            "Evidence leaked a binary payload name."

        use schema =
            JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(root, ".ai/schemas/dotnet-asset-calibration-ci-evidence.schema.json"))
            )

        use evidence = JsonDocument.Parse(first.CanonicalJson)

        let lockFileSha256 =
            evidence.RootElement.GetProperty("toolchain").GetProperty("lockFileSha256").GetString()

        assertTrue
            (lockFileSha256 = "e1115c5484a8df29fd25f2a96ee77de8f5561088a869b4192b5cc8f791f4afa8")
            "Evidence did not bind the complete toolchain lock bytes."

        let evaluation =
            JsonSchema
                .FromText(schema.RootElement.GetRawText())
                .Evaluate(evidence.RootElement, EvaluationOptions(OutputFormat = OutputFormat.List))

        assertTrue evaluation.IsValid "Evidence failed its closed offline schema."

    /// Unsafe roots are rejected before a run can create an output workspace.
    let unsafeWorkspaceFailsClosed () =
        try
            DotnetAssetCiEvidence.generate "relative-output" |> ignore
            failwith "Relative workspace was accepted."
        with DotnetAssetCiEvidenceError "UNSAFE_PATH" ->
            ()

        try
            DotnetAssetCiEvidence.generateWithSuiteReport root (String.replicate 64 "١")
            |> ignore

            failwith "A non-ASCII report hash was accepted."
        with DotnetAssetCiEvidenceError "INVALID_ARGUMENT" ->
            ()

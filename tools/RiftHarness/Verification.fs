namespace RiftHarness

open System
open System.IO
open System.Text.Json

type VerificationReport =
    { Valid: bool
      RunsChecked: int
      IndexChecked: bool
      Errors: string list }

[<RequireQualifiedAccess>]
module Verification =
    let verify root requestedRun =
        let locations = Workspace.paths root
        let errors = ResizeArray<string>()

        if not (Directory.Exists(locations.Runs)) then
            errors.Add($"Run-Verzeichnis fehlt: {locations.Runs}")

        if not (Directory.Exists(locations.Index)) then
            errors.Add($"Index-Verzeichnis fehlt: {locations.Index}")

        if not (File.Exists(locations.Config)) then
            errors.Add($"Konfiguration fehlt: {locations.Config}")

        let runIds =
            match requestedRun with
            | Some runId -> [ runId ]
            | None -> RunStore.allRunIds root

        if Directory.Exists(locations.Runs) then
            for runId in runIds do
                for error in RunStore.verifyRun root runId do
                    errors.Add($"Run {runId}: {error}")

        let indexChecked = requestedRun.IsNone && File.Exists(locations.IndexFile)

        if requestedRun.IsNone && File.Exists(locations.Config) then
            for error in RagIndex.verify root do
                errors.Add($"RAG: {error}")

        { Valid = errors.Count = 0
          RunsChecked =
            if Directory.Exists(locations.Runs) then
                runIds.Length
            else
                0
          IndexChecked = indexChecked
          Errors = errors |> Seq.toList }

    let reportJson report =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteBoolean("valid", report.Valid)
            writer.WriteNumber("runsChecked", report.RunsChecked)
            writer.WriteBoolean("indexChecked", report.IndexChecked)
            writer.WriteStartArray("errors")

            for error in report.Errors do
                writer.WriteStringValue(error)

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString

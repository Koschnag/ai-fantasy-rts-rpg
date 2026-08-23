module PlatformInteropTests

open System
open System.Collections.Generic
open System.IO
open Riftward.Platform
open Riftward.Platform.Interop

/// <summary>Einfacher zaehlender SDL-Fake fuer Besitz- und Reihenfolgeregeln.</summary>
type SdlApiFake() =
    member val InitCalls: int = 0 with get, set
    member val QuitCalls: int = 0 with get, set
    member val CreatedWindows: nativeint list = [] with get, set
    member val DestroyedWindows: nativeint list = [] with get, set

    interface ISdlApi with
        member this.Init(_) =
            this.InitCalls <- this.InitCalls + 1
            true

        member this.Quit() = this.QuitCalls <- this.QuitCalls + 1

        member this.CreateWindow(_, _, _, _) =
            this.CreatedWindows <- nativeint 4242 :: this.CreatedWindows
            nativeint 4242

        member this.DestroyWindow(window) =
            this.DestroyedWindows <- window :: this.DestroyedWindows

        member _.PollEvent(_: SdlEventBuffer byref) = false

        member _.GetWindowProperties(_) = 0u

        member _.GetNumberProperty(_, _, defaultValue) = defaultValue

        member _.GetPointerProperty(_, _, defaultValue) = defaultValue

        member _.GetError() = "fixture-error"

/// <summary>Aufzeichnender Fake der bgfx-Shim-Grenze mit steuerbaren Fehlern.</summary>
type BgfxApiFake(initResult: int, rendererType: int) =
    let mutable callsRev: string list = []
    let record name = callsRev <- name :: callsRev

    member _.Calls = List.rev callsRev
    member val InitResult: int = initResult with get, set
    member val RendererTypeValue: int = rendererType with get, set

    interface IBgfxApi with
        member _.ApiVersion() = 154u

        member this.Init(parameters) =
            record "init"
            this.InitResult

        member this.RendererType() = this.RendererTypeValue

        member _.GlStrings() =
            ("GL 3.3 fixture", "fixture-gpu", "3.30")

        member _.GpuIds() = (0x1002u <<< 16) ||| 0x6958u

        member _.Shutdown() = record "shutdown"

        member _.Frame() =
            record "frame"
            1u

        member _.DrawCalls() = 1u

        member _.ViewSetup(_, _, _, _) = record "viewsetup"

        member _.CreateVertexBuffer(_, size) =
            record $"createvb{size}"
            7us

        member _.CreateShader(_, size) =
            record $"createshader{size}"

            if size = 0u then 0xFFFFus else 11us

        member _.ShaderIsValid(_) = true

        member _.CreateProgram(_, _) =
            record "createprogram"
            21us

        member _.DestroyProgram(program) = record $"destroyprogram{program}"

        member _.DestroyShader(shader) = record $"destroyshader{shader}"

        member _.DestroyVertexBuffer(buffer) = record $"destroyvb{buffer}"

        member _.Submit(viewId, program, buffer) =
            record $"submit{viewId}:{program}:{buffer}"

let private expectPlatformException (code: PlatformErrorCode) (action: unit -> unit) (message: string) =
    try
        action ()
    with
    | :? PlatformException as error when error.Error.Code = code -> ()
    | :? PlatformException as error -> failwith $"{message}: falscher Fehlercode {error.Error.Code} statt {code}."
    | error -> failwith $"{message}: unerwarteter Fehler {error.GetType().Name}: {error.Message}"

/// Liest den ersten Prueffehler als kontrollierte Ausnahme.
let private firstFailureOrNone (report: ArtifactCatalogReport) =
    match report.FirstFailure() with
    | null -> None
    | failure -> Some failure

/// Legt ein Artefakt-Fixture-Verzeichnis mit Manifest an; Rueckgabe (root, manifestPath).
let private makeArtifactFixture (files: (string * byte array) list) =
    let root =
        Path.Combine(Path.GetTempPath(), "RiftArtifacts-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(Path.Combine(root, "lib")) |> ignore
    Directory.CreateDirectory(Path.Combine(root, "shaders")) |> ignore

    let manifestEntries = Dictionary<string, string * int>()

    for relative, content in files do
        let fullPath = Path.Combine(root, relative)
        File.WriteAllBytes(fullPath, content)

        let hash =
            System.Security.Cryptography.SHA256.HashData(content)
            |> Convert.ToHexString
            |> (fun text -> text.ToLowerInvariant())

        manifestEntries[relative.Replace('\\', '/')] <- (hash, content.Length)

    let entryTexts =
        manifestEntries
        |> Seq.map (fun pair ->
            let hash, bytes = pair.Value
            $"\"{pair.Key}\": {{\"sha256\": \"{hash}\", \"bytes\": {bytes}}}")

    File.WriteAllText(Path.Combine(root, "artifact-hashes.json"), "{" + String.Join(",", entryTexts) + "}")

    (root, Path.Combine(root, "artifact-hashes.json"))

let artifactValidationAcceptsIntactFixtures () =
    let root, manifestPath =
        makeArtifactFixture [ ("lib/a.so", [| 1uy; 2uy; 3uy |]); ("shaders/s.bin", [| 9uy |]) ]

    try
        let report = NativeArtifacts.Validate(root, manifestPath)

        if not report.Valid then
            failwith "Intaktes Artefakt-Fixture wurde abgelehnt."

        if report.Checks.Count <> 2 then
            failwith "Artefaktpruefung deckte nicht alle Eintraege ab."
    finally
        Directory.Delete(root, true)

let artifactFaultClassesFailControlled () =
    // Fehlendes Artefakt.
    do
        let root, manifestPath = makeArtifactFixture [ ("lib/missing.so", [| 1uy |]) ]

        try
            File.Delete(Path.Combine(root, "lib", "missing.so"))

            let report = NativeArtifacts.Validate(root, manifestPath)
            let failure = firstFailureOrNone report

            expectPlatformException
                PlatformErrorCode.ArtifactMissing
                (fun () ->
                    match failure with
                    | Some error -> raise (PlatformException error)
                    | None -> failwith "kein Fehler gemeldet")
                "Fehlende Datei wurde nicht als ARTIFACT_MISSING gemeldet"
        finally
            Directory.Delete(root, true)

    // Unvollstaendiges Artefakt (Groesse weicht ab).
    do
        let root, manifestPath =
            makeArtifactFixture [ ("lib/truncated.so", [| 1uy .. 32uy |]) ]

        try
            File.WriteAllBytes(Path.Combine(root, "lib", "truncated.so"), [| 1uy .. 8uy |])

            let report = NativeArtifacts.Validate(root, manifestPath)
            let failure = firstFailureOrNone report

            expectPlatformException
                PlatformErrorCode.ArtifactIncomplete
                (fun () ->
                    match failure with
                    | Some error -> raise (PlatformException error)
                    | None -> failwith "kein Fehler gemeldet")
                "Unvollstaendige Datei wurde nicht erkannt"
        finally
            Directory.Delete(root, true)

    // Hashbeschaedigtes Artefakt.
    do
        let root, manifestPath =
            makeArtifactFixture [ ("lib/damaged.so", [| 1uy .. 64uy |]) ]

        try
            let damaged = Array.copy [| 1uy .. 64uy |]
            damaged[10] <- damaged[10] ^^^ 0xFFuy
            File.WriteAllBytes(Path.Combine(root, "lib", "damaged.so"), damaged)

            let report = NativeArtifacts.Validate(root, manifestPath)
            let failure = firstFailureOrNone report

            expectPlatformException
                PlatformErrorCode.ArtifactHashMismatch
                (fun () ->
                    match failure with
                    | Some error -> raise (PlatformException error)
                    | None -> failwith "kein Fehler gemeldet")
                "Beschaedigte Datei wurde nicht erkannt"
        finally
            Directory.Delete(root, true)

    // Ungueltiges Manifest.
    do
        let root, manifestPath = makeArtifactFixture [ ("lib/x.so", [| 5uy |]) ]

        try
            File.WriteAllText(manifestPath, "{nicht-json")

            expectPlatformException
                PlatformErrorCode.ArtifactManifestInvalid
                (fun () -> NativeArtifacts.Validate(root, manifestPath) |> ignore)
                "Ungueltiges Manifest wurde akzeptiert"
        finally
            Directory.Delete(root, true)

let artifactValidationNeverWrites () =
    let root, manifestPath = makeArtifactFixture [ ("lib/a.so", [| 7uy; 8uy |]) ]

    // Manipulationsmanifest vor der Referenzaufnahme anlegen, damit die
    // Vergleichsaufnahme nur die Effekte der Pruefung selbst misst.
    let tamperedManifest = Path.Combine(root, "tampered.json")

    do
        let original = File.ReadAllText(manifestPath)
        File.WriteAllText(tamperedManifest, original.Replace("sha256", "sha256x"))

    let snapshot () =
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        |> Array.map (fun path -> Path.GetRelativePath(root, path), FileInfo(path).Length)
        |> Array.sortBy fst

    let before = snapshot ()
    NativeArtifacts.Validate(root, manifestPath) |> ignore

    try
        NativeArtifacts.Validate(root, tamperedManifest) |> ignore
    with
    | :? PlatformException -> ()
    | error -> raise error

    let after = snapshot ()

    if before <> after then
        failwith "Artefaktpruefung hat Dateien verändert oder neu angelegt."

    Directory.Delete(root, true)

let exitCodeMappingIsStableAndDocumented () =
    let expectations =
        [ PlatformErrorCode.Internal, 1
          PlatformErrorCode.ArtifactIncomplete, 15
          PlatformErrorCode.ArtifactMissing, 16
          PlatformErrorCode.ArtifactHashMismatch, 17
          PlatformErrorCode.BackendInitFailed, 18
          PlatformErrorCode.WindowFailed, 19
          PlatformErrorCode.WrongShutdownOrder, 20
          PlatformErrorCode.InvalidHandle, 21
          PlatformErrorCode.UnsupportedPlatform, 22
          PlatformErrorCode.SmokeNoFrame, 23
          PlatformErrorCode.EfficiencyBudgetViolated, 24 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Exitcode fuer {code} ist {ExitCodes.Map(code)}, dokumentiert ist {expected}."

let sdlSessionOwnershipRulesAreExplicit () =
    let api = SdlApiFake()
    let session = SdlSession.Start(api)

    // Fensterlebenszyklus inklusive Doppel-Freigabe.
    let window = session.CreateWindow("fixture", 320, 240)
    window.Dispose()
    window.Dispose()

    if api.DestroyedWindows.Length <> 1 then
        failwith "Doppeltes Fenster-Dispose fuehrte zu mehreren DestroyWindow-Aufrufen."

    expectPlatformException
        PlatformErrorCode.InvalidHandle
        (fun () -> window.Handle |> ignore)
        "Zugriff nach Fensterfreigabe wurde nicht kontrolliert abgelehnt"

    // Sitzung verweigert Shutdown bei offenen Fenstern.
    let openWindow = session.CreateWindow("offen", 320, 240)

    expectPlatformException
        PlatformErrorCode.WrongShutdownOrder
        (fun () -> session.Dispose())
        "Sitzungs-Shutdown vor Fenstern wurde nicht verweigert"

    openWindow.Dispose()
    session.Dispose()

    // Doppeltes Sitzungs-Dispose ist No-op; SDL_Quit genau einmal.
    session.Dispose()

    if api.InitCalls <> 1 || api.QuitCalls <> 1 then
        failwith $"SDL-Lebenszyklus fehlerhaft: init={api.InitCalls}, quit={api.QuitCalls}"

let bgfxDeviceTranslatesInitializationErrors () =
    // Shim meldet Initialisierungsfehler.
    let failing = BgfxApiFake(-2, BgfxDevice.RendererOpenGL)

    expectPlatformException
        PlatformErrorCode.BackendInitFailed
        (fun () ->
            BgfxDevice.Initialize(BgfxInitRequest(nativeint 1, nativeint 2, 640, 480, 0u, 0u), failing)
            |> ignore)
        "Shim-Initialisierungsfehler wurde nicht uebersetzt"

    // Falsches aktives Backend wird kontrolliert abgelehnt (kein stiller Fallback).
    let wrongBackend = BgfxApiFake(0, 9)

    expectPlatformException
        PlatformErrorCode.BackendInitFailed
        (fun () ->
            BgfxDevice.Initialize(BgfxInitRequest(nativeint 1, nativeint 2, 640, 480, 0u, 0u), wrongBackend)
            |> ignore)
        "Nicht-OpenGL-Backend wurde nicht abgelehnt"

    if not ((wrongBackend.Calls) |> List.contains "shutdown") then
        failwith "Falsches Backend wurde nicht sauber heruntergefahren."

let bgfxHandleOwnershipAndShutdownOrderAreEnforced () =
    let api = BgfxApiFake(0, BgfxDevice.RendererOpenGL)

    let device =
        BgfxDevice.Initialize(BgfxInitRequest(nativeint 1, nativeint 2, 640, 480, 0u, 0u), api)

    let vertexData = [| 0uy .. 47uy |]
    let resources = device.CreateTriangleResources(vertexData, [| 1uy |], [| 2uy |])

    resources.Submit()

    if not (api.Calls |> List.contains "submit0:21:7") then
        failwith "Submit hat nicht das Programm-/Bufferhandlepaar verwendet."

    // Shutdown vor Ressourcenfreigabe wird verweigert.
    expectPlatformException
        PlatformErrorCode.WrongShutdownOrder
        (fun () -> device.Dispose())
        "bgfx-Shutdown vor Ressourcen wurde nicht verweigert"

    // Kontrollierte Freigabe in fester Reihenfolge; doppelte Freigabe bleibt No-op.
    resources.Dispose()
    resources.Dispose()

    let calls = api.Calls

    let indexOf marker =
        calls |> List.tryFindIndex (fun candidate -> candidate = marker)

    let programIndex = indexOf "destroyprogram21"
    let shaderIndex = indexOf "destroyshader11"
    let bufferIndex = indexOf "destroyvb7"

    match programIndex, shaderIndex, bufferIndex with
    | Some p, Some s, Some b when p < s && s < b -> ()
    | _ -> failwith $"Freigabereihenfolge verletzt: {calls}"

    device.Dispose()

    if not (api.Calls |> List.contains "shutdown") then
        failwith "bgfx-Shutdown fehlt im Lebenszyklus."

let invalidNativeHandlesAreTranslated () =
    let api = BgfxApiFake(0, BgfxDevice.RendererOpenGL)

    let device =
        BgfxDevice.Initialize(BgfxInitRequest(nativeint 1, nativeint 2, 640, 480, 0u, 0u), api)

    expectPlatformException
        PlatformErrorCode.InvalidHandle
        (fun () ->
            let vertexData: byte[] = [| 0uy .. 15uy |]
            let emptyShader: byte[] = [||]
            device.CreateTriangleResources(vertexData, emptyShader, emptyShader) |> ignore)
        "Leerer Shader-Datenblock ergab kein kontrolliertes Handle-Fehlerobjekt"

let architectureKeepsNativeImportsInsidePlatformLayer () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let root = findRoot Environment.CurrentDirectory
    let violations = ResizeArray<string>()

    for projectDirectoryName in [ "src"; "tools"; "tests" ] do
        let directory = Path.Combine(root, projectDirectoryName)

        if Directory.Exists(directory) then
            for file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories) do
                let normalized = file.Replace('\\', '/')

                if normalized.Contains("/bin/") || normalized.Contains("/obj/") then
                    ()
                else
                    let insidePlatformLayer = normalized.Contains("/src/Riftward.Platform/")
                    let mutable lineNumber = 0

                    for line in File.ReadLines(file) do
                        lineNumber <- lineNumber + 1

                        let declaresImport =
                            line.Contains("[LibraryImport", StringComparison.Ordinal)
                            || line.Contains("[DllImport(", StringComparison.Ordinal)

                        if declaresImport && not insidePlatformLayer then
                            violations.Add($"{normalized}:{lineNumber}")

    for file in Directory.EnumerateFiles(Path.Combine(root, "src", "Riftward.App"), "*.cs", SearchOption.AllDirectories) do
        let normalized = file.Replace('\\', '/')

        if not (normalized.Contains("/bin/") || normalized.Contains("/obj/")) then
            let content = File.ReadAllText(file)

            for symbol in [ "Sdl3Native"; "BgfxShimNative" ] do
                if content.Contains(symbol, StringComparison.Ordinal) then
                    violations.Add($"{normalized}: referenziert internes Natursymbol {symbol}")

    if violations.Count > 0 then
        failwith $"Architekturverletzungen: {violations}"

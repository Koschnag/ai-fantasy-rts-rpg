module BenchEmptyTests

open System
open System.IO
open Riftward.App
open Riftward.App.Bench
open Riftward.Platform

let private goldenReport =
    """{"schemaVersion":1,"mode":"bench","command":"./scripts/rift.sh bench --scenario bench-empty --report <PFAD>","scenario":{"id":"bench-empty","seed":20260824,"resolution":{"width":1920,"height":1080},"displayProfile":"low","vsync":true,"content":"clear-pass-plus-technical-test-pattern"},"cameraPath":{"algorithm":"xorshift64star-fixedpoint-v1","samples":1080,"hash":"fixturehash","firstSample":{"frameIndex":0,"yawDegrees":"53.853","pitchDegrees":"0.187","radiusMeters":"3.653"}},"startedAtUtc":"2026-08-24T10:00:00Z","finishedAtUtc":"2026-08-24T10:00:30Z","environment":{"os":{"type":"Linux","kernelRelease":"6.8.0-fixture"},"cpu":{"model":"fixture-cpu"},"gpu":{"renderer":"fixture-gpu","vendorId":4098,"deviceId":26968},"gl":{"version":"GL 3.3 fixture"},"backend":{"name":"OpenGL","id":8,"profile":"3.3 Core","vsync":true},"rid":"linux-x64","commit":"0123456789abcdef0123456789abcdef01234567","buildMode":"Release","pins":[{"id":"sdl3","refType":"tag","ref":"release-3.4.14","commit":"a1","sourceSha256":"h1","licenseSpdx":"zlib"},{"id":"bgfx","refType":"commit","ref":"b2","commit":"b2","sourceSha256":"h2","licenseSpdx":"BSD-2-Clause"},{"id":"bx","refType":"commit","ref":"c3","commit":"c3","sourceSha256":"h3","licenseSpdx":"BSD-2-Clause"},{"id":"bimg","refType":"commit","ref":"d4","commit":"d4","sourceSha256":"h4","licenseSpdx":"BSD-2-Clause"}]},"measurement":{"warmupFrames":180,"sampleFrames":900,"framesRendered":1080,"rssSampleIntervalFrames":30},"metrics":{"frameTimeMs":{"unit":"ms","method":"stopwatch-frame-delta","p50":16.5,"p95":16.7,"p99":16.8},"managedAllocationsBytes":{"unit":"bytes","method":"gc-total-allocated-bytes-precise-delta","perWarmFrame":0.9},"gcPauseSumMs":{"unit":"ms","method":"gc-get-total-pause-duration-delta","value":0.123},"gcPauseCount":{"unit":"count","method":"gc-collection-count-gen0-to2-delta","value":0},"workingSetKiB":{"unit":"KiB","method":"proc-self-status-vmrss-samples","min":140000,"max":150000,"end":145000},"drawSubmitCallsPerFrame":{"unit":"count","method":"bgfx-stats-numdraw-max","value":1},"visibleTrianglesPerFrame":{"unit":"count","method":"bgfx-stats-numprims-trilist-max","value":1},"gpuTimeMs":{"measured":true,"unit":"ms","method":"bgfx-stats-gpu-timer-p99","p99":1.2,"timerFreqHz":1000000000},"vramBytes":{"measured":true,"unit":"bytes","method":"bgfx-managed-memory-texture-rt-transient-end","value":4096,"textureMemoryUsed":4096},"runtimeShaderCompilation":{"unit":"bool","method":"offline-shaderc-binaries-only","value":false}},"gate":{"limits":{"p99FrameTimeMsMax":33.3,"managedAllocationsPerWarmFrameBytesMax":1024.0,"drawSubmitCallsPerFrameMax":8,"runtimeShaderCompilationAllowed":false,"rssTargetMiB":300,"rssHardLimitMiB":450},"pass":true,"rssTargetMet":true,"violations":[]},"profiles":[{"id":"hw-pc-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"reference-hardware-unnamed-qops001"},{"id":"hw-mac-min","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"reference-hardware-unnamed-qops001"},{"id":"hw-pc-high","status":"NOT-MEASURED","boundReferenceClass":null,"reason":"reference-hardware-unnamed-qops001"}],"baseline":{"classification":"diagnostic-developer-workstation","protocol":"qops001-2026-08-24"},"exitCode":0}"""

let private assertHasError (fragment: string) (reportJson: string) (message: string) =
    let errors = BenchReportSchema.Validate(reportJson)

    if errors.Count = 0 then
        failwith $"{message}: Schemapruefung akzeptierte den Report unerwartet."

    let joined = String.concat "; " errors

    if not (joined.Contains(fragment, StringComparison.Ordinal)) then
        failwith $"{message}: Fehler {joined} enthaelt nicht '{fragment}'."

let private budgetInputs p99 allocations draws shaderObserved minRss maxRss endRss =
    BenchBudgetInputs(
        P99FrameTimeMs = p99,
        ManagedAllocationsPerWarmFrameBytes = allocations,
        DrawSubmitCallsPerFrameMax = draws,
        RuntimeShaderCompilationObserved = shaderObserved,
        RssMinKiB = (minRss |> Option.map int64 |> Option.toNullable),
        RssMaxKiB = (maxRss |> Option.map int64 |> Option.toNullable),
        RssEndKiB = (endRss |> Option.map int64 |> Option.toNullable)
    )

/// Golden-Fixtures fuer p50/p95/p99 je Bestehens- und Verletzungsklasse (AC-T020-04).
let percentileBandMatchesGoldenFixtures () =
    let ascending = [ for value in 1.0 .. 100.0 -> value ]
    let band = TelemetryMath.Band(ascending)

    if band.P50Ms <> 50.0 || band.P95Ms <> 95.0 || band.P99Ms <> 99.0 then
        failwith $"Percentilverfahren weicht ab: {band.P50Ms}/{band.P95Ms}/{band.P99Ms}"

    let violating = TelemetryMath.Band([ 33.4 ])

    let verdict =
        BudgetGate.Evaluate(
            BenchBudgetLimits.Documented,
            budgetInputs violating.P99Ms 0.0 1L false (Some 100000) (Some 110000) (Some 105000)
        )

    if verdict.Pass then
        failwith "p99-Verletzung wurde nicht erkannt."

    if
        not (
            verdict.Violations
            |> Seq.exists (fun violation -> violation.Contains("p99-frame-time-ms", StringComparison.Ordinal))
        )
    then
        failwith "Verletzungsklasse p99 fehlt im Bericht."

    // Unsortierte Eingaben fuehren zu denselben Werten.
    let shuffled = TelemetryMath.Band([ 95.0; 1.0; 50.0; 99.0 ])

    if shuffled.P50Ms <> 50.0 || shuffled.P99Ms <> 99.0 then
        failwith "Sortierung der Eingabereihe ist nicht stabil."

/// Gate-Evaluator-Fixtures je Bestehens- und Verletzungsklasse (AC-T020-04).
let budgetGateGoldenFixturesCoverEveryClass () =
    let evaluate inputs =
        BudgetGate.Evaluate(BenchBudgetLimits.Documented, inputs)

    let pass =
        evaluate (budgetInputs 16.7 512.0 1L false (Some 140000) (Some 150000) (Some 145000))

    if not pass.Pass then
        failwith "Bestehensklasse des Budgetgates schlug fehl."

    if not pass.RssTargetMet then
        failwith "RSS-Ziel wurde als verfehlt gemeldet obwohl eingehalten."

    if pass.Violations.Count <> 0 then
        failwith "Bestehensklasse meldete Verletzungen."

    let allocViolation =
        evaluate (budgetInputs 16.7 2048.0 1L false (Some 140000) (Some 150000) (Some 145000))

    if
        allocViolation.Pass
        || not (
            allocViolation.Violations
            |> Seq.exists (fun v -> v.Contains("managed-allocations-per-warm-frame-bytes", StringComparison.Ordinal))
        )
    then
        failwith "Allokationsverletzung wurde nicht erkannt."

    let drawViolation =
        evaluate (budgetInputs 16.7 512.0 9L false (Some 140000) (Some 150000) (Some 145000))

    if
        drawViolation.Pass
        || not (
            drawViolation.Violations
            |> Seq.exists (fun v -> v.Contains("draw-submit-per-frame", StringComparison.Ordinal))
        )
    then
        failwith "Draw-/Submitverletzung wurde nicht erkannt."

    let shaderViolation =
        evaluate (budgetInputs 16.7 512.0 1L true (Some 140000) (Some 150000) (Some 145000))

    if
        shaderViolation.Pass
        || not (
            shaderViolation.Violations
            |> Seq.exists (fun v -> v.Contains("runtime-shader-compilation", StringComparison.Ordinal))
        )
    then
        failwith "Laufzeitshaderkompilierung wurde nicht verweigert."

    let hardRss =
        evaluate (budgetInputs 16.7 512.0 1L false (Some 140000) (Some 500000) (Some 145000))

    if
        hardRss.Pass
        || not (
            hardRss.Violations
            |> Seq.exists (fun v -> v.Contains("working-set-max-mib", StringComparison.Ordinal))
        )
    then
        failwith "Harte RSS-Grenze wurde nicht durchgesetzt."

    let targetMissOnly =
        evaluate (budgetInputs 16.7 512.0 1L false (Some 140000) (Some 400000) (Some 145000))

    if not targetMissOnly.Pass then
        failwith "Zielverfehlung unter der harten Grenze darf das Gate nicht allein falten."

    if targetMissOnly.RssTargetMet then
        failwith "RSS-Zielverfehlung wurde nicht ausgewiesen."

    let notMeasurable = evaluate (budgetInputs 16.7 512.0 1L false None None None)

    if
        notMeasurable.Pass
        || not (
            notMeasurable.Violations
            |> Seq.exists (fun v -> v.Contains("not-measurable", StringComparison.Ordinal))
        )
    then
        failwith "Nicht messbare Groessen muessen fail-closed als Verletzung gelten."

    let nanP99 =
        evaluate (budgetInputs Double.NaN 512.0 1L false (Some 140000) (Some 150000) (Some 145000))

    if nanP99.Pass then
        failwith "NaN-Messwert wurde nicht als fail-closed erkannt."

/// Reportvertrag akzeptiert das Goldendokument und lehnt Faelschungen ab (AC-T020-02).
let reportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    let goldenErrors = BenchReportSchema.Validate(goldenReport)

    if goldenErrors.Count > 0 then
        let joined = String.Join("; ", goldenErrors)
        failwith $"Goldenreport wurde abgelehnt: {joined}"

    // Erfundene Kennzahl ohne Methodenkennung.
    assertHasError
        "gcPauseSumMs.method"
        (goldenReport.Replace("\"method\":\"gc-get-total-pause-duration-delta\",", ""))
        "Kennzahl ohne Methodenkennung wurde akzeptiert"

    // Typenfremder Wert.
    assertHasError
        "numerischer"
        (goldenReport.Replace("\"p50\":16.5", "\"p50\":\"16.5\""))
        "Typenfremder Messwert wurde akzeptiert"

    // Grundlos fehlender Pflichtwert.
    assertHasError "frameTimeMs.p99" (goldenReport.Replace(",\"p99\":16.8", "")) "Fehlendes p99 wurde akzeptiert"

    // Unavailable ohne maschinenlesbaren Grund.
    assertHasError
        "reason"
        (goldenReport.Replace(
            "\"gpuTimeMs\":{\"measured\":true,\"unit\":\"ms\",\"method\":\"bgfx-stats-gpu-timer-p99\",\"p99\":1.2,\"timerFreqHz\":1000000000}",
            "\"gpuTimeMs\":{\"measured\":false}"
        ))
        "Unavailable ohne Grund wurde akzeptiert"

    // Unbekanntes Feld.
    assertHasError
        "unbekanntes Feld"
        (goldenReport.Replace("\"schemaVersion\":1,", "\"schemaVersion\":1,\"extraField\":1,"))
        "Unbekanntes Feld wurde akzeptiert"

    // Fremde Schemaversion.
    assertHasError
        "schemaVersion"
        (goldenReport.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"))
        "Fremde Schemaversion wurde akzeptiert"

    // Beschaedigtes Zwischenartefakt.
    assertHasError "gueltiges JSON" "{beschädigt" "Beschaedigtes Dokument wurde akzeptiert"

/// Kamerapfad-Determinismus mit Hashfixture ohne Uhrzufall (AC-T020-03).
let cameraPathIsDeterministicWithHashFixture () =
    let first = CameraFlight.Samples(CameraFlight.DefaultSeed, 256)
    let second = CameraFlight.Samples(CameraFlight.DefaultSeed, 256)
    let hashFirst = CameraFlight.HashHex(first)
    let hashSecond = CameraFlight.HashHex(second)

    if hashFirst <> hashSecond then
        failwith "Identische Konfiguration lieferte unterschiedlichen Kamerapfad-Hash."

    let expected = "6be4bc2366d0540b54e48539502d33c8a1ec9058f4abe1271a2cfa16f2ca98e0"

    if hashFirst <> expected then
        failwith $"Kamerapfad-Hash weicht von der Fixture ab: {hashFirst}"

    let otherSeed =
        CameraFlight.HashHex(CameraFlight.Samples(CameraFlight.DefaultSeed + 1u, 256))

    if otherSeed = hashFirst then
        failwith "Unterschiedliche Seeds ergaben denselben Pfad."

    for sample in first do
        if sample.YawDegrees < -360.0 || sample.YawDegrees >= 360.0 then
            failwith $"Gierwert ausserhalb des quantisierten Bereichs: {sample.YawDegrees}"

        if sample.PitchDegrees < -15.0 || sample.PitchDegrees >= 45.0 then
            failwith $"Nickwert ausserhalb des quantisierten Bereichs: {sample.PitchDegrees}"

        if sample.RadiusMeters < 2.5 || sample.RadiusMeters > 4.0 then
            failwith $"Orbitradius ausserhalb des Bereichs: {sample.RadiusMeters}"

/// Kameramatrizen bleiben endlich und projizieren den Szenenursprung ins Bild.
let cameraMatricesStayFiniteAndSane () =
    let sample = CameraFlight.Samples(CameraFlight.DefaultSeed, 8)[0]
    let pose = CameraFlight.Pose(sample)
    let view = CameraMath.LookAt(pose.Eye, pose.Center, CameraMath.Vec3(0.0, 1.0, 0.0))
    let projection = CameraMath.PerspectiveFov(60.0, 1920.0 / 1080.0, 0.1, 100.0)

    if view.Length <> 16 || projection.Length <> 16 then
        failwith "Matrizen besitzen nicht 16 Elemente."

    for value in Array.append view projection do
        if Double.IsNaN(value) || Double.IsInfinity(value) then
            failwith "Matrix enthaelt einen nicht-endlichen Wert."

    if
        projection[11] <> 1.0
        || projection[14] >= 0.0
        || projection[0] <= 0.0
        || projection[5] <= 0.0
    then
        failwith "Projektion entspricht nicht der bx-Konvention."

    // Ursprung projizieren (Spaltenvektorkonvention wie bgfx/GL).
    let transform (matrix: float[]) (x: float) (y: float) (z: float) =
        matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
        matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
        matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14],
        matrix[3] * x + matrix[7] * y + matrix[11] * z + matrix[15]

    let vx, vy, vz, vw = transform view 0.0 0.0 0.0
    let cx, cy, _, cw = transform projection vx vy vz

    if vw <= 0.0 || cw <= 0.0 then
        failwith "Ursprung liegt hinter der Kamera oder im Nulldivisionsbereich."

    let ndcX = cx / cw
    let ndcY = cy / cw

    if abs ndcX > 1.0 || abs ndcY > 1.0 then
        failwith $"Ursprung verlaesst das Frustum unmittelbar vor der Kamera: {ndcX}, {ndcY}"

/// Ehrlichkeitsregel der Profilbindung ueber synthetische Hardwarebeschreibungen (AC-T020-05).
let profileBindingHonestyMatrixIsEnforced () =
    let developerRx570 =
        HardwareDescriptor("AMD Radeon RX 570 Series", "Intel i7-3770", true)

    let syntheticGtx660 =
        HardwareDescriptor("NVIDIA GeForce GTX 660", "Intel i7-3770", false)

    let syntheticRx570 =
        HardwareDescriptor("AMD Radeon RX 570 Series", "Ryzen 5", false)

    let syntheticM1 = HardwareDescriptor("Apple M1", "Apple M1", false)

    // Entwickler-PC bleibt auch bei passender Klasse diagnostische Baseline.
    let devClaim =
        ProfileBinding.EvaluateClaim(HardwareProfiles.PcHigh, developerRx570, "rx 570", true)

    if
        devClaim.Status <> ProfileStatus.NotMeasured
        || devClaim.Reason <> ProfileBinding.DeveloperWorkstationDiagnosticReason
    then
        failwith "Entwickler-PC-Bindung wurde nicht als Diagnose abgewiesen."

    // Ohne benannte Referenzrechner wird jede Behauptung abgewiesen.
    let unnamed =
        ProfileBinding.EvaluateClaim(HardwareProfiles.PcMinimum, syntheticGtx660, "gtx 660", false)

    if
        unnamed.Status <> ProfileStatus.NotMeasured
        || unnamed.Reason <> ProfileBinding.ReferenceMachinesUnnamedReason
    then
        failwith "Bindung ohne benannte Referenzrechner wurde nicht abgewiesen."

    // Passende Klasse plus benannte Rechner erzeugt den einzigen Bestehenspfad.
    let validMin =
        ProfileBinding.EvaluateClaim(HardwareProfiles.PcMinimum, syntheticGtx660, "gtx660", true)

    if
        validMin.Status <> ProfileStatus.Pass
        || validMin.BoundReferenceClass <> "gtx660"
    then
        failwith "Gueltige Referenzbindung wurde nicht anerkannt."

    let validMac =
        ProfileBinding.EvaluateClaim(HardwareProfiles.MacMinimum, syntheticM1, "apple m1", true)

    if validMac.Status <> ProfileStatus.Pass then
        failwith "M1-Bindung wurde nicht anerkannt."

    let validHigh =
        ProfileBinding.EvaluateClaim(HardwareProfiles.PcHigh, syntheticRx570, "rx 570", true)

    if validHigh.Status <> ProfileStatus.Pass then
        failwith "HIGH-Klassenbindung wurde nicht anerkannt."

    // Klassenfremde Behauptung.
    let mismatch =
        ProfileBinding.EvaluateClaim(HardwareProfiles.PcMinimum, syntheticGtx660, "rx 580", true)

    if
        mismatch.Status <> ProfileStatus.NotMeasured
        || mismatch.Reason <> ProfileBinding.BindingMismatchReason
    then
        failwith "Klassenfremde Bindung wurde nicht erkannt."

    // Pflichtprofile bleiben ohne Referenzhardware NOT-MEASURED.
    let mandatory = ProfileBinding.MandatoryWithoutReferenceHardware()

    if
        mandatory.Count <> 3
        || (mandatory
            |> Seq.exists (fun profile ->
                profile.Status <> ProfileStatus.NotMeasured
                || profile.Reason <> ProfileBinding.ReferenceHardwareUnavailableReason))
    then
        failwith "Pflichtprofile sind nicht konsistent NOT-MEASURED."

/// Szenarioregistry: nur bench-empty ist implementiert; alles andere bricht definiert (AC-T020-01).
let scenarioRegistryRejectsUnknownAndUnimplemented () =
    if
        BenchScenarios.Classify(BenchScenarios.Empty)
        <> BenchScenarios.Support.Implemented
    then
        failwith "bench-empty ist nicht als implementiert klassifiziert."

    for pending in
        [ BenchScenarios.Army
          BenchScenarios.Battle
          BenchScenarios.Base
          BenchScenarios.Path
          BenchScenarios.Load ] do
        if
            BenchScenarios.Classify(pending)
            <> BenchScenarios.Support.RegisteredNotImplemented
        then
            failwith $"Szenario {pending} ist nicht als registriert-unimplementiert klassifiziert."

    if BenchScenarios.Classify(null) <> BenchScenarios.Support.Unknown then
        failwith "Fehlende ID wurde nicht als unbekannt behandelt."

    if BenchScenarios.Classify("") <> BenchScenarios.Support.Unknown then
        failwith "Leere ID wurde nicht als unbekannt behandelt."

    if BenchScenarios.Classify("bench-nope") <> BenchScenarios.Support.Unknown then
        failwith "Fremde ID wurde nicht abgewiesen."

/// Neue Exitcodes bleiben stabil und dokumentiert (AC-T020-06, NATIVE_UNTERBAU.md).
let exitCodesTwentyFiveToTwentyEightAreStableAndDocumented () =
    let expectations =
        [ PlatformErrorCode.BenchScenarioUnavailable, 25
          PlatformErrorCode.BenchBudgetViolated, 26
          PlatformErrorCode.TelemetryInvalid, 27
          PlatformErrorCode.ReportNotWritable, 28 ]

    for code, expected in expectations do
        if ExitCodes.Map(code) <> expected then
            failwith $"Exitcode fuer {code} ist {ExitCodes.Map(code)}, dokumentiert ist {expected}."

/// Fault-Injection am CLI-Vertrag ohne Fensteroeffnung (AC-T020-01/06).
let cliContractFailsControlledWithoutReports () =
    let temporary =
        Path.Combine(Path.GetTempPath(), "rift-t020-" + Guid.NewGuid().ToString("N"))

    try
        // Unbekanntes Szenario: Exitcode 25, kein Report.
        let unknownArguments =
            CommandLineArgs([| "bench"; "--scenario"; "bench-nope"; "--report"; temporary |])

        if
            BenchRunner.Run(unknownArguments)
            <> ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable)
        then
            failwith "Unbekanntes Szenario ergab nicht Exitcode 25."

        // Registriertes, aber nicht implementiertes Szenario: ebenfalls 25.
        let pendingArguments =
            CommandLineArgs([| "bench"; "--scenario"; BenchScenarios.Army; "--report"; temporary |])

        if
            BenchRunner.Run(pendingArguments)
            <> ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable)
        then
            failwith "Nicht implementiertes Szenario ergab nicht Exitcode 25."

        // Fehlender Reportpfad: Usagefehler 2.
        let usageArguments =
            CommandLineArgs([| "bench"; "--scenario"; BenchScenarios.Empty |])

        if BenchRunner.Run(usageArguments) <> ExitCodes.Usage then
            failwith "Fehlender Reportpfad ergab keinen Usagefehler."

        if File.Exists(temporary) then
            failwith "Abgebrochene Laeufe duerfen keinen Report schreiben."
    finally
        if File.Exists(temporary) then
            File.Delete(temporary)

/// Fault-Injection: nicht schreibbarer Reportpfad liefert definierten Code ohne Absturz (AC-T020-06).
let unwritableReportPathFailsControlled () =
    let blockedDirectory =
        Path.Combine(Path.GetTempPath(), "rift-t020-missing-" + Guid.NewGuid().ToString("N"), "unter")

    let blockedPath = Path.Combine(blockedDirectory, "bench.json")

    if BenchRunner.WriteReportOrDiagnose(blockedPath, "{}") then
        failwith "Schreibvorgang in fehlendes Verzeichnis meldete Erfolg."
    else
        try
            File.WriteAllText(blockedPath, "{}")
            failwith "Unerwartet schreibbarer Pfad: Fixture ungueltig."
        with
        | :? DirectoryNotFoundException
        | :? IOException -> ()

/// Strukturgleichheit zweiter Reports trotz variierender Messwerte (AC-T020-03).
let structureEqualityIgnoresMeasurementDriftButDetectsShapeChanges () =
    let drifted =
        goldenReport.Replace("\"p99\":16.8", "\"p99\":17.4").Replace("\"min\":140000", "\"min\":141000")

    let driftDifferences = BenchReportSchema.StructureDifferences(goldenReport, drifted)

    if driftDifferences.Count > 0 then
        let joined = String.Join("; ", driftDifferences)
        failwith $"Strukturgleichheit wurde faelschlich verworfen: {joined}"

    if
        BenchReportSchema
            .StructureDifferences(goldenReport, goldenReport.Replace(",\"rssTargetMet\"", ",\"rssTargetMetX\""))
            .Count = 0
    then
        failwith "Feldumbenennung wurde nicht erkannt."

/// Der rift.sh-Befehlsvertrag bindet bench an den App-Build-Guard (AC-T020-06/07).
let riftScriptBenchContractKeepsAppBuildGuard () =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    let script =
        File.ReadAllText(Path.Combine(findRoot (Environment.CurrentDirectory), "scripts", "rift.sh"))

    let benchIndex = script.IndexOf("  bench)", StringComparison.Ordinal)

    if benchIndex < 0 then
        failwith "rift.sh besitzt keinen bench-Zweig."

    // Naechste Zweigmarke auf gleicher Einrueckungstiefe finden (genau zwei
    // Leerzeichen, danach kein weiteres Leerzeichen).
    let rec nextTopLevel index =
        let found = script.IndexOf("\n  ", index, StringComparison.Ordinal)

        if found < 0 then
            -1
        elif
            found + 2 < script.Length
            && script[found + 2] <> ' '
            && script[found + 2] <> '\n'
        then
            found
        else
            nextTopLevel (found + 1)

    let nextBranch = nextTopLevel (benchIndex + 1)

    let branchLength =
        (if nextBranch < 0 then script.Length else nextBranch + 1) - benchIndex

    let branch = script.Substring(benchIndex, branchLength)

    if not (branch.Contains("rift_need_app_output", StringComparison.Ordinal)) then
        failwith "bench-Zweig umgeht den App-Build-Guard (fehlender Build muss Exitcode 4 liefern)."

    if not (branch.Contains("Riftward.App.dll\" bench", StringComparison.Ordinal)) then
        failwith "bench-Zweig ruft nicht den Hostmodus 'bench' auf."

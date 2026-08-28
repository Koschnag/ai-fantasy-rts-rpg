using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Platform.Interop;
using Riftward.Session;
using Riftward.Simulation;

namespace Riftward.App.Command;

/// <summary>
/// KOMMANDO-GRAYBOX (T-032): erste interaktive Graybox-Kommandoschleife als
/// Sitzungsmodus des bestehenden Hosts. Headless laeuft der Befehl nativ auf
/// linux-x64 rein CPU-seitig ohne Fenster/Renderer/Netzwerk und ohne Laden
/// der nativen SDL3-/bgfx-Artefakte; der Report ist die pruefbare Evidenz
/// nach NF-007 mit fail-closed Gate gegen docs/KOMMANDOVERTRAG.md,
/// Simulationsvertrag V1 und PERFORMANCE_BUDGET.md. Der Interaktivmodus
/// bedient denselben Pipelinepfad mit T-010-Fenstereingaben, Graybox-
/// darstellung nach T-023-Mustern und vertraglicher Zweikanal-Rueckmeldung.
/// Budgetverletzungen ergeben definierte Exitcodes; der Report wird
/// trotzdem geschrieben und klar markiert.
/// </summary>
internal static class CommandLoopRunner
{
    public const string CommandName = "./scripts/rift.sh kommandoschleife";

    public const int DefaultSeed = unchecked((int)CameraFlight.DefaultSeed);

    private const int EdgePanMarginPixels = 8;

    private const int BoxDragThresholdPixels = 6;

    private const int MaxCatchUpTicksPerFrame = 3;

    public static int Run(CommandLineArgs arguments)
    {
        var interactive = arguments.HasFlag("--interactive");
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("kommandoschleife: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        // Opt-in Aktivierung der Erkundung (T-034, Vertrag Abschnitt 6):
        // reiner Schalter ohne Wert; ohne Flag bleibt Verhalten und Report
        // byteidentisch zum Bestandsstand.
        var explorationEnabled = arguments.HasFlag("--exploration");
        var autoExitAtHorizon = arguments.HasFlag("--auto-exit-at-horizon");

        if (autoExitAtHorizon && !interactive)
        {
            Console.Error.WriteLine(
                "kommandoschleife: --auto-exit-at-horizon ist nur zusammen mit --interactive erlaubt.");
            return ExitCodes.Usage;
        }

        // Szenario- und Skriptpruefung vor jedem teuren Schritt: unbekanntes
        // Szenario oder unlesbares/malformiertes Skript bricht ohne Report ab
        // (Code 37), statt einen Scheinbericht zu erzeugen.
        var scenarioId = arguments.Option("--scenario");

        if (!string.Equals(scenarioId, SessionContract.ScenarioId, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"kommandoschleife: unbekanntes Szenario '{scenarioId ?? "<fehlt>"}'. Implementiert: {SessionContract.ScenarioId}.");
            return ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable);
        }

        var scriptPath = arguments.Option("--input-script");

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            Console.Error.WriteLine("kommandoschleife: --input-script PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var seedValue = arguments.NumberOption("--seed", DefaultSeed);

        if (arguments.Option("--seed") is { } rawSeed && rawSeed.TrimStart('-').Length > 0 && seedValue < 0)
        {
            Console.Error.WriteLine("kommandoschleife: --seed erwartet eine nichtnegative Ganzzahl.");
            return ExitCodes.Usage;
        }

        var seed = unchecked((uint)seedValue);
        var horizonTicks = (int)Math.Clamp(
            arguments.NumberOption("--horizon-ticks", SessionContract.DefaultHorizonTicks),
            SessionContract.DefaultWarmupTicks + 60,
            SessionContract.HorizonTicksMax);
        var warmupTicks = (int)Math.Clamp(
            arguments.NumberOption("--warmup-ticks", SessionContract.DefaultWarmupTicks),
            30,
            Math.Min(SessionContract.WarmupTicksMax, horizonTicks - 60));

        ParsedInputScript parsed;

        try
        {
            parsed = InputScriptParser.Parse(
                ReadInputScriptBytes(scriptPath),
                new ScriptWindowRules(WarmupTicks: warmupTicks, HorizonTicks: horizonTicks));
        }
        catch (InputScriptException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"kommandoschleife: Eingabeskript nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.CommandScenarioUnavailable);
        }

        return interactive
            ? RunInteractive(
                arguments, reportPath!, seed, parsed, warmupTicks, horizonTicks,
                explorationEnabled, autoExitAtHorizon)
            : RunHeadless(arguments, reportPath!, seed, parsed, warmupTicks, horizonTicks, explorationEnabled);
    }

    /* ------------------------------------------------------------- Headless */

    private static int RunHeadless(
        CommandLineArgs arguments,
        string reportPath,
        uint seed,
        ParsedInputScript parsed,
        int warmupTicks,
        int horizonTicks,
        bool explorationEnabled)
    {
        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();
        IReadOnlyList<ToolchainPin> pins;

        try
        {
            pins = ToolchainLockReader.ReadNativeComponents(arguments.Option("--lock") ?? "toolchain.lock.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"kommandoschleife: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }

        SessionRunResult result;

        try
        {
            result = SessionEngine.Run(new SessionRunRequest(
                Seed: seed,
                ScriptedIntents: parsed.Intents,
                WarmupTicks: warmupTicks,
                HorizonTicks: horizonTicks,
                RunSelfConsistencyPass: true,
                ExplorationEnabled: explorationEnabled));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"kommandoschleife: Lauf vorzeitig beendet: {exception.Message}");
            return WriteIncompleteReport(
                reportPath, CommandReportSchema.ExecutionHeadless, seed, parsed, warmupTicks, horizonTicks,
                commit, buildMode, environment, explorationEnabled, exploration: null);
        }

        var verdict = CommandGate.Evaluate(CommandGateLimits.Documented, new CommandGateInputs(
            P99TickTimeMs: result.Metrics.P99TickTimeMs,
            ManagedAllocationsPerWarmTickBytes: result.Metrics.AllocationsPerWarmTickBytes,
            MaxReactionTicks: result.Metrics.MaxReactionTicks,
            ReactionSampleCount: result.Metrics.ReactionSampleCount,
            RuntimeShaderCompilationObserved: false,
            StateChainSelfConsistent: result.StateChainSelfConsistent,
            MaxSwitchReactionTicks: result.Telemetry.MaxSwitchReactionTicks,
            SwitchReactionSampleCount: result.Telemetry.SwitchReactionSampleCount));

        var gateExitCode = verdict.Pass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.CommandGateViolated);
        var reportJson = JsonSerializer.Serialize(BuildReport(new ReportContext(
            ExecutionMode: CommandReportSchema.ExecutionHeadless,
            Seed: seed,
            Parsed: parsed,
            WarmupTicks: warmupTicks,
            HorizonTicks: horizonTicks,
            ProcessStart: processStart,
            Commit: commit,
            BuildMode: buildMode,
            Environment: environment,
            Pins: pins,
            Metrics: result.Metrics,
            StartHash: result.StartStateHash,
            EndHash: result.EndStateHash,
            IntervalSampleTicks: result.IntervalSampleTicks,
            IntervalHashes: result.IntervalHashes,
            AppliedIntents: result.AppliedIntents,
            RejectedIntents: result.RejectedIntents,
            EmptyPointDeselects: result.EmptyPointDeselects,
            MoveWithoutSelectionRejects: result.MoveWithoutSelectionRejects,
            NoZoneRejects: 0,
            KernelCommandsTotal: result.KernelCommandsTotal,
            Verdict: verdict,
            WindowCompleted: true,
            Capture: NotRequestedCapture(),
            Display: null,
            WorkingSet: WorkingSetFrom(result),
            ExitCode: gateExitCode,
            Telemetry: result.Telemetry,
            Exploration: result.Exploration)), BenchRunner.ReportJsonOptions) + "\n";

        return FinishReport(reportPath, reportJson, gateExitCode);
    }

    /// <summary>
    /// Liest das untrusted Eingabeskript als exakt begrenzten Rohbytepuffer
    /// (Vertrag Abschnitt 5): Die vertragliche Bytegrenze wird waehrend des
    /// Lesens durchgesetzt, sodass uebergrosse Dateien und endlos liefernde
    /// Spezialdateien kontrolliert mit der Klasse <c>ScriptTooLarge</c>
    /// abgewiesen werden, bevor beliebig viele Bytes materialisiert werden.
    /// Der Schreibzugriff bleibt auf den Reportpfad beschraenkt; es wird nie
    /// aus dem Skript ausgefuehrt. Nicht existierende oder nicht lesbare
    /// Quellen propagieren ihre ueblichen IO-Ausnahmen an den kontrollierten
    /// Code-37-Abbruch des Runners.
    /// </summary>
    internal static byte[] ReadInputScriptBytes(string path)
    {
        var maxBytes = checked((int)SessionContract.ScriptBytesMax);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[maxBytes + 1];
        var total = 0;

        int chunk;
        while (total <= maxBytes && (chunk = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += chunk;
        }

        if (total > maxBytes)
        {
            throw new InputScriptException(
                InputScriptRejectReason.ScriptTooLarge,
                0,
                $"Skript ist groesser als die erlaubten {maxBytes} Bytes; die Bytegrenze wird am Rohmaterial durchgesetzt.");
        }

        return total == buffer.Length ? buffer : buffer.AsSpan(0, total).ToArray();
    }

    internal static WorkingSetSamples WorkingSetFrom(SessionRunResult result)
    {
        // Der headless Lauf erhebt den Arbeitssatz als Stichprobenreihe im
        // Engine-Durchlauf nicht; er bleibt hier unavailable mit wahrheits-
        // treuem Grund, damit kein behaupteter Messwert ohne Quelle entsteht.
        // Der Grund ist Bestandteil des vertraglichen Reportinhalts und wird
        // durch einen Suiteeintrag gegen die Kennung gebunden (T-032).
        return new WorkingSetSamples(false, null, null, null, "headless-session-does-not-sample-rss");
    }

    /* ----------------------------------------------------------- Interaktiv */

    private sealed class InputState
    {
        public bool QuitRequested;

        public readonly HashSet<int> HeldScancodes = new();

        public double CursorX = BenchRunner.DefaultWidth / 2.0;

        public double CursorY = BenchRunner.DefaultHeight / 2.0;

        public bool MiddleDragging;

        public double LastMiddleX;

        public double LastMiddleY;

        public bool LeftDragging;

        public double BoxStartX;

        public double BoxStartY;

        public long NoZoneRejects;

        /// <summary>Interaktive, sichtbare Kontextabweisungen (T-033, Modevertrag Abschnitt 5).</summary>
        public long InteractiveContextRejections;

        /// <summary>Tick der letzten interaktiven Lenkanwendung (ein Lenkimpuls je Tick).</summary>
        public long LastSteerTick = -1;
    }

    private sealed class InteractiveMeasurement
    {
        public readonly List<double> TickTimes = new();
        public readonly List<double> FrameTimes = new();
        public readonly List<double> GpuTimes = new();
        public readonly List<long> ReactionTimes = new();
        public readonly List<long> RssSamples = new();
        public readonly long[] IntervalSampleTicks;
        public readonly ulong[] IntervalHashes;
        public int HashCursor = 1;
        public long MaxReactionTicks;
        public long PeakMarkers;
        public bool WindowStarted;
        public bool WindowCompleted;
        public long DrawCallsMax;

        public long TrianglesMax;

        public long GpuTimerFrequencyHz;

        public long AllocationSumBytes;

        public double GcPauseSumMs;
        public long GcPauseCount;

        public InteractiveMeasurement(int windowTicks)
        {
            var capacity = (windowTicks / SessionContract.HashSampleIntervalTicks) + 2;
            IntervalSampleTicks = new long[capacity];
            IntervalHashes = new ulong[capacity];
        }
    }

    private static int RunInteractive(
        CommandLineArgs arguments,
        string reportPath,
        uint seed,
        ParsedInputScript parsed,
        int warmupTicks,
        int horizonTicks,
        bool explorationEnabled,
        bool autoExitAtHorizon)
    {
        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();
        IReadOnlyList<ToolchainPin> pins;

        try
        {
            pins = ToolchainLockReader.ReadNativeComponents(arguments.Option("--lock") ?? "toolchain.lock.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"kommandoschleife: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }

        var capturePath = arguments.Option("--capture-frame");
        HostBootstrap.Context? context = null;
        InteractiveSceneResources? resources = null;
        InteractiveView? view = null;
        ExplorationSession? exploration = null;
        string glVersion;
        string glRenderer;
        uint gpuIds;

        try
        {
            // Der normale Spielpfad bleibt praesentationssynchron. Ein
            // explizites Auto-Exit-Display-Gate darf dagegen nicht von der
            // 5-Hz-Drossel eines gesperrten/verdeckt dargestellten Wayland-
            // Surfaces ausgebremst werden: Die Simulation bleibt weiterhin
            // wanduhrgebunden bei 20 Hz, nur das fuer die Evidenz unnoetige
            // Present-Warten wird abgeschaltet.
            context = HostBootstrap.Start(
                arguments,
                BenchRunner.DefaultWidth,
                BenchRunner.DefaultHeight,
                vsync: UsesVsyncForInteractiveRun(autoExitAtHorizon));
            (glVersion, glRenderer, _) = NativeApi.Instance.GlStrings();
            gpuIds = NativeApi.Instance.GpuIds();

            resources = InteractiveSceneResources.Build(context.Device, arguments);
            resources.ConfigureViews(context.Device);

            var world = new SimWorld(seed);
            var selectionGroups = SessionEngine.ReadAgentGroups(world);
            var selection = new SelectionModel(selectionGroups);
            // Opt-in Erkundung (T-034): die Beobachtung lebt ausschließlich
            // in Riftward.Session, liest schreibgeschützt und erzeugt
            // niemals einen Kernbefehl; ohne Aktivierung bleibt der Pfad
            // byteidentisch zum Bestandsstand.
            exploration = explorationEnabled ? new ExplorationSession() : null;
            var pipeline = new SessionPipeline(world, selection, parsed.Intents, exploration);
            view = new InteractiveView();
            view.BindAgentGroups(selectionGroups);
            view.BindSelection(selection);
            view.BindExploration(exploration);

            var projection = InteractiveCameraMath.Projection(BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
            var camera = new GrayboxCamera();
            var heroCamera = new HeroChaseCamera();
            var input = new InputState();
            SdlEventBuffer eventBuffer = default;

            var measurement = RunInteractiveLoop(
                context.Device, resources, view, world, pipeline, camera, heroCamera, input,
                context.Window, ref eventBuffer, NativeApi.Instance, projection, warmupTicks, horizonTicks,
                exploration, autoExitAtHorizon);

            // Laufende ausgewertete, aber nicht mehr wirksame Wechsel sind
            // ausdrücklich im Protokoll gebunden statt still zu verschwinden.
            pipeline.FlushPendingSwitches();

            CaptureOutcome capture;

            if (capturePath is null)
            {
                capture = NotRequestedCapture();
            }
            else if (!measurement.WindowCompleted)
            {
                capture = new CaptureOutcome(true, false, true, "window-not-completed-no-capture");
            }
            else if (!context.Device.IsReadBackSupported() || !context.Device.IsBlitSupported())
            {
                capture = new CaptureOutcome(true, false, true, "readback-or-blit-unsupported-by-backend");
            }
            else
            {
                capture = ExecuteCapturePair(
                    context.Device, resources, view, world, camera, heroCamera,
                    projection, capturePath!);
            }

            var allocationsPerWarmTick = AllocationsPerWarmTick(measurement);
            var modeTelemetry = SessionEngine.BuildModeTelemetry(pipeline);

            var verdict = CommandGate.Evaluate(CommandGateLimits.Documented, new CommandGateInputs(
                P99TickTimeMs: PercentileOrDefault(measurement.TickTimes, 0.99),
                ManagedAllocationsPerWarmTickBytes: allocationsPerWarmTick,
                MaxReactionTicks: measurement.MaxReactionTicks,
                ReactionSampleCount: measurement.ReactionTimes.Count,
                RuntimeShaderCompilationObserved: false,
                StateChainSelfConsistent: null,
                MaxSwitchReactionTicks: modeTelemetry.MaxSwitchReactionTicks,
                SwitchReactionSampleCount: modeTelemetry.SwitchReactionSampleCount));

            var gateExitCode = verdict.Pass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.CommandGateViolated);

            // Vorzeitiger Abbruch vor Fensterabschluss ist vertraglich ein
            // unvollstaendiger Lauf (Code 36), niemals ein Erfolgscodes 0 —
            // auch wenn Teilmetriken zufaellig innerhalb der Grenzen liegen.
            // Der Report traegt bereits gate.pass=false mit der Verletzung
            // run-incomplete-no-evidence und gilt damit nicht als Evidenz.
            var exitCode = ResolveInteractiveExitCode(
                measurement.WindowCompleted, capture.Failed, gateExitCode);

            var displayBinding = new
            {
                measured = true,
                renderer = glRenderer,
                vendorId = gpuIds >> 16,
                deviceId = gpuIds & 0xFFFFu,
                glVersion,
            };

            var workingSet = measurement.RssSamples.Count >= 2
                ? new WorkingSetSamples(true, measurement.RssSamples.Min(), measurement.RssSamples.Max(), measurement.RssSamples[^1], null)
                : new WorkingSetSamples(false, null, null, null, "rss-sampler-unavailable");

            var reportJson = JsonSerializer.Serialize(BuildReport(new ReportContext(
                ExecutionMode: CommandReportSchema.ExecutionInteractive,
                Seed: seed,
                Parsed: parsed,
                WarmupTicks: warmupTicks,
                HorizonTicks: horizonTicks,
                ProcessStart: processStart,
                Commit: commit,
                BuildMode: buildMode,
                Environment: environment,
                Pins: pins,
                Metrics: new SessionMetrics(
                    P50TickTimeMs: PercentileOrDefault(measurement.TickTimes, 0.50),
                    P95TickTimeMs: PercentileOrDefault(measurement.TickTimes, 0.95),
                    P99TickTimeMs: PercentileOrDefault(measurement.TickTimes, 0.99),
                    AllocationsPerWarmTickBytes: allocationsPerWarmTick,
                    GcPauseSumMs: measurement.GcPauseSumMs,
                    GcPauseCount: measurement.GcPauseCount,
                    MaxReactionTicks: measurement.MaxReactionTicks,
                    ReactionP50Ticks: SessionMath.Percentile(measurement.ReactionTimes, 0.50),
                    ReactionP95Ticks: SessionMath.Percentile(measurement.ReactionTimes, 0.95),
                    ReactionP99Ticks: SessionMath.Percentile(measurement.ReactionTimes, 0.99),
                    ReactionSampleCount: measurement.ReactionTimes.Count),
                StartHash: measurement.IntervalHashes.Length > 0 ? measurement.IntervalHashes[0] : 0UL,
                EndHash: world.ComputeStateHash(),
                IntervalSampleTicks: measurement.IntervalSampleTicks.AsSpan(0, Math.Max(1, measurement.HashCursor)).ToArray(),
                IntervalHashes: measurement.IntervalHashes.AsSpan(0, Math.Max(1, measurement.HashCursor)).ToArray(),
                AppliedIntents: (int)pipeline.AppliedIntentsTotal,
                RejectedIntents: (int)pipeline.RejectedIntentsTotal,
                EmptyPointDeselects: (int)pipeline.EmptyPointDeselectTotal,
                MoveWithoutSelectionRejects: (int)pipeline.MoveWithoutSelectionTotal,
                NoZoneRejects: (int)input.NoZoneRejects,
                KernelCommandsTotal: (int)pipeline.AppliedCommandsTotal,
                Verdict: verdict,
                WindowCompleted: measurement.WindowCompleted,
                Capture: capture,
                Display: displayBinding,
                WorkingSet: workingSet,
                ExitCode: exitCode,
                InteractiveContextRejections: input.InteractiveContextRejections,
                Hud: measurement.WindowCompleted
                    ? (object)new
                    {
                        measured = true,
                        kind = ModeContract.HudModelId,
                        fields = new
                        {
                            mode = ModeName(pipeline.CurrentEffectiveMode),
                            heroZone = HeroTracker.ZoneIndexOf(world),
                        },
                    }
                    : new
                    {
                        measured = false,
                        kind = ModeContract.HudModelId,
                        reason = "run-incomplete-hud-not-asserted",
                    },
                InteractiveExtras: new InteractiveExtras(
                    FrameBand: new FrameBandValues(
                        PercentileOrDefault(measurement.FrameTimes, 0.50),
                        PercentileOrDefault(measurement.FrameTimes, 0.95),
                        PercentileOrDefault(measurement.FrameTimes, 0.99)),
                    GpuTimeMeasured: measurement.GpuTimes.Count > 0,
                    GpuTimeP99Ms: measurement.GpuTimes.Count > 0 ? SessionMath.Percentile(measurement.GpuTimes, 0.99) : 0.0,
                    GpuTimerFrequencyHz: measurement.GpuTimerFrequencyHz,
                    DrawCallsMax: measurement.DrawCallsMax,
                    TrianglesMax: measurement.TrianglesMax,
                    PeakMarkers: measurement.PeakMarkers),
                Telemetry: modeTelemetry,
                Exploration: exploration?.ToTelemetry())), BenchRunner.ReportJsonOptions) + "\n";

            return FinishReport(reportPath, reportJson, exitCode);
        }
        catch (PlatformException exception)
        {
            // Enthaelt insbesondere den dokumentierten Code-19-Abbruch ohne
            // nutzbares Display statt simulierter Interaktion.
            Console.Error.WriteLine(exception.Error.ToString());
            return ExitCodes.Map(exception.Error.Code);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"kommandoschleife: Lauf vorzeitig beendet: {exception.Message}");
            return WriteIncompleteReport(
                reportPath, CommandReportSchema.ExecutionInteractive, seed, parsed, warmupTicks, horizonTicks,
                commit, buildMode, environment, explorationEnabled, exploration?.ToTelemetry());
        }
        finally
        {
            view?.Dispose();

            if (context is not null)
            {
                resources?.Dispose();
                HostBootstrap.Stop(context);
            }
        }
    }

    private static double PercentileOrDefault(List<double> values, double fraction) =>
        values.Count == 0 ? 0.0 : SessionMath.Percentile(values, fraction);

    private static double AllocationsPerWarmTick(InteractiveMeasurement measurement) =>
        measurement.TickTimes.Count == 0
            ? 0.0
            : measurement.AllocationSumBytes / (double)measurement.TickTimes.Count;

    /// <summary>
    /// Interaktive Hauptschleife: Ereignispumpe, Intent-Uebersetzung,
    /// wanduhrgebundene 20-Hz-Tickfolge mit Messfenster [warmup, horizon),
    /// Zweikanaldarstellung und kontrolliertem Beenden. Der Sitzungsmodus der
    /// Pipeline (T-033) waehlt den Eingabekontext, die aktive Kamera und den
    /// Badge-Kanal; der Titel-HUD traegt Modus und Heldenzone (Modevertrag
    /// Abschnitt 8).
    /// </summary>
    private static InteractiveMeasurement RunInteractiveLoop(
        BgfxDevice device,
        InteractiveSceneResources resources,
        InteractiveView view,
        SimWorld world,
        SessionPipeline pipeline,
        GrayboxCamera camera,
        HeroChaseCamera heroCamera,
        InputState input,
        Window window,
        ref SdlEventBuffer eventBuffer,
        NativeApi api,
        float[] projection,
        int warmupTicks,
        int horizonTicks,
        ExplorationSession? exploration,
        bool autoExitAtHorizon)
    {
        var windowTicks = horizonTicks - warmupTicks;
        var measurement = new InteractiveMeasurement(windowTicks);
        var pauseSumBeforeMs = GC.GetTotalPauseDuration().TotalMilliseconds;
        var collectionCountBefore = GcCollectionTotal();
        var lastTitle = string.Empty;

        measurement.IntervalSampleTicks[0] = world.TickIndex;
        measurement.IntervalHashes[0] = world.ComputeStateHash();

        using var rssSampler = RssSampler.TryCreate();
        var lastTickTimestamp = Stopwatch.GetTimestamp();
        var tickInterval = Stopwatch.Frequency / SimulationContract.TickRateHz;

        while (ShouldContinueInteractiveLoop(
            input.QuitRequested, measurement.WindowCompleted, autoExitAtHorizon))
        {
            PumpEvents(input, ref eventBuffer, api, camera, heroCamera, pipeline, world);

            if (input.QuitRequested)
            {
                break;
            }

            ApplyHeldKeys(input, camera, heroCamera, pipeline, world);

            if (pipeline.CurrentEffectiveMode == SessionMode.Strategic)
            {
                ApplyEdgePan(input, camera);
            }
            else
            {
                // Verfolgungskamera: Blickpunkt folgt schreibgeschützt dem
                // Vertragshelden; Strategie-Kamerasemantik (Rand-Schwenken,
                // Mittelklick-Schwenken) ist im persönlichen Modus nicht
                // gebunden (KOMMANDOVERTRAG Abschnitt 12).
                heroCamera.Follow(world);
            }

            UpdateTitleHud(window, pipeline, world, exploration, ref lastTitle);

            var catchUp = 0;
            var now = Stopwatch.GetTimestamp();

            while (now - lastTickTimestamp >= tickInterval
                && catchUp < MaxCatchUpTicksPerFrame
                && !input.QuitRequested)
            {
                lastTickTimestamp += tickInterval;
                var tick = world.TickIndex;
                var outcome = pipeline.ProcessBoundary(tick);
                var consumed = outcome.AppliedCount > 0;

                if (outcome.RejectedMoveWithoutSelection > 0)
                {
                    // UF-001-Fehlerzeile mit dem vertraglichen Grund
                    // (Kommandovertrag Abschnitt 2): die Abweisung ist
                    // sichtbar, nicht nur als Zaehler gebunden.
                    Console.Error.WriteLine(
                        $"kommandoschleife: Befehl abgewiesen - {SessionContract.RejectReasonMoveWithoutSelection} bei Tick {tick}.");
                }

                if (outcome.RejectedStrategyInPersonal > 0 || outcome.RejectedSteerInStrategy > 0)
                {
                    // Kontextabweisungen, die die Pipeline erreicht haben
                    // (T-033, Modevertrag Abschnitt 5), sind ebenfalls
                    // sichtbar mit ihrer vertraglichen Kennung.
                    if (outcome.RejectedStrategyInPersonal > 0)
                    {
                        Console.Error.WriteLine(
                            $"kommandoschleife: Befehl abgewiesen - {ModeContract.RejectReasonStrategyIntentInPersonalMode} bei Tick {tick}.");
                    }

                    if (outcome.RejectedSteerInStrategy > 0)
                    {
                        Console.Error.WriteLine(
                            $"kommandoschleife: Befehl abgewiesen - {ModeContract.RejectReasonSteerIntentInStrategyMode} bei Tick {tick}.");
                    }
                }

                if (consumed)
                {
                    // Vertragliche Zweikanalrueckmeldung (Kommandovertrag
                    // Abschnitt 3, NF-005): je angewendetem Bewegungsintent
                    // erhaelt die Darstellung einen Befehlspuls; abgewiesene
                    // Intents erscheinen nie.
                    foreach (var issuedZone in pipeline.DispatchedMoveZonesOfLastBoundary)
                    {
                        view.NotifyCommandIssued(tick, issuedZone);
                    }
                }

                var frameStart = Stopwatch.GetTimestamp();
                var windowed = tick >= warmupTicks && tick < horizonTicks;
                var allocationBefore = windowed ? GC.GetTotalAllocatedBytes(precise: true) : 0L;
                var tickStart = Stopwatch.GetTimestamp();
                world.Tick();
                var tickEnd = Stopwatch.GetTimestamp();
                var allocationAfter = windowed ? GC.GetTotalAllocatedBytes(precise: true) : 0L;

                if (windowed)
                {
                    measurement.WindowStarted |= tick == warmupTicks;
                    measurement.AllocationSumBytes += allocationAfter - allocationBefore;
                    measurement.TickTimes.Add(SessionMath.TimestampDeltaToMilliseconds(tickStart, tickEnd));
                    measurement.FrameTimes.Add(SessionMath.TimestampDeltaToMilliseconds(frameStart, Stopwatch.GetTimestamp()));

                    if (consumed)
                    {
                        var reactionTicks = world.TickIndex - tick;
                        measurement.MaxReactionTicks = Math.Max(measurement.MaxReactionTicks, reactionTicks);

                        for (var slot = 0; slot < outcome.AppliedCount; slot++)
                        {
                            measurement.ReactionTimes.Add(reactionTicks);
                        }
                    }

                    if (world.TickIndex % SessionContract.HashSampleIntervalTicks == 0
                        && measurement.HashCursor < measurement.IntervalSampleTicks.Length)
                    {
                        measurement.IntervalSampleTicks[measurement.HashCursor] = world.TickIndex;
                        measurement.IntervalHashes[measurement.HashCursor] = world.ComputeStateHash();
                        measurement.HashCursor++;
                    }

                    if (rssSampler is not null
                        && measurement.TickTimes.Count % SessionContract.RssSampleIntervalTicks == 0)
                    {
                        rssSampler.Sample();
                    }
                }

                catchUp++;
                now = Stopwatch.GetTimestamp();

                if (!measurement.WindowCompleted && world.TickIndex >= horizonTicks)
                {
                    measurement.WindowCompleted = true;
                }
            }

            var activeCamera = pipeline.CurrentEffectiveMode == SessionMode.Personal
                ? InteractiveCameraMath.ActiveCamera.From(heroCamera)
                : InteractiveCameraMath.ActiveCamera.From(camera);
            var markerCount = RenderFrame(
                device, resources, view, world, activeCamera, projection, pipeline.CurrentEffectiveMode);
            measurement.PeakMarkers = Math.Max(measurement.PeakMarkers, markerCount);

            if (device.TryReadStats(out var stats))
            {
                measurement.DrawCallsMax = Math.Max(measurement.DrawCallsMax, stats.NumDraw);
                measurement.TrianglesMax = Math.Max(measurement.TrianglesMax, stats.TrianglesRendered);

                if (stats.GpuTimerFrequency > 0)
                {
                    measurement.GpuTimerFrequencyHz = stats.GpuTimerFrequency;
                    measurement.GpuTimes.Add(
                        (stats.GpuTimeEndTicks - stats.GpuTimeBeginTicks) * 1000.0 / stats.GpuTimerFrequency);
                }
            }
        }

        measurement.GcPauseSumMs = GC.GetTotalPauseDuration().TotalMilliseconds - pauseSumBeforeMs;
        measurement.GcPauseCount = GcCollectionTotal() - collectionCountBefore;
        return measurement;
    }

    /// <summary>
    /// Hält den normalen interaktiven Spielpfad offen, erlaubt aber einem
    /// explizit begrenzten, unbeaufsichtigten Display-Gate, nach dem letzten
    /// vollständigen Messframe kontrolliert in Report und Capture zu laufen.
    /// </summary>
    internal static bool ShouldContinueInteractiveLoop(
        bool quitRequested,
        bool windowCompleted,
        bool autoExitAtHorizon) =>
        !quitRequested && (!autoExitAtHorizon || !windowCompleted);

    /// <summary>
    /// Normale Spielsitzungen praesentieren mit VSync. Ausschliesslich das
    /// explizit begrenzte Auto-Exit-Display-Gate rendert ohne Present-Warten;
    /// dessen Simulationstakt bleibt unabhaengig davon wanduhrgebunden.
    /// </summary>
    internal static bool UsesVsyncForInteractiveRun(bool autoExitAtHorizon) =>
        !autoExitAtHorizon;

    private static int RenderFrame(
        BgfxDevice device,
        InteractiveSceneResources resources,
        InteractiveView view,
        SimWorld world,
        InteractiveCameraMath.ActiveCamera camera,
        float[] projection,
        SessionMode visualMode)
    {
        var markerCount = view.WriteFrameState(world, world.TickIndex, visualMode);

        device.UpdateTexture2DRgba32F(
            resources.PaletteTexture,
            0,
            0,
            RepresentativeScenario.BonesPerNormalUnit * 3,
            SimulationContract.AgentCount,
            view.Palette);

        resources.SubmitShadowPasses(device, view.UnitsPointer, SimulationContract.AgentCount);

        var view16 = InteractiveCameraMath.View16(camera);
        var basis = InteractiveCameraMath.BillboardBasis(camera);

        resources.SubmitCompositePass(
            device,
            InteractiveViews.ViewMain,
            view16,
            projection,
            basis,
            view.UnitsPointer,
            SimulationContract.AgentCount,
            view.MarkersPointer,
            (uint)markerCount);

        device.RenderFrame();
        return markerCount;
    }

    private static void PumpEvents(
        InputState input,
        ref SdlEventBuffer eventBuffer,
        NativeApi api,
        GrayboxCamera camera,
        HeroChaseCamera heroCamera,
        SessionPipeline pipeline,
        SimWorld world)
    {
        while (api.PollEvent(ref eventBuffer))
        {
            var inputView = SdlInputEventView.FromBuffer(ref eventBuffer);

            switch (inputView.Type)
            {
                case SdlEventCodes.Quit:
                case SdlEventCodes.WindowCloseRequested:
                    input.QuitRequested = true;
                    return;

                case SdlEventCodes.KeyDown:
                case SdlEventCodes.KeyUp:
                    HandleKey(input, camera, heroCamera, pipeline, world, inputView);
                    break;

                case SdlEventCodes.MouseMotion:
                    HandleMotion(input, camera, pipeline, inputView);
                    break;

                case SdlEventCodes.MouseButtonDown:
                case SdlEventCodes.MouseButtonUp:
                    HandleButton(input, camera, pipeline, world, inputView);
                    break;

                case SdlEventCodes.MouseWheel:
                    // Rad nach vorn (SDL: WheelY > 0) zoomt heran (+1),
                    // konsistent zur Keymap; im persönlichen Modus belegt
                    // Zoom die Distanz der Verfolgungskamera (KOMMANDO-
                    // VERTRAG Abschnitt 12), sonst die der Graybox-Kamera.
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        heroCamera.ZoomSteps(inputView.WheelY > 0 ? +1 : -1);
                    }
                    else
                    {
                        camera.ZoomSteps(inputView.WheelY > 0 ? +1 : -1);
                    }

                    break;

                default:
                    break;
            }
        }
    }

    private static void HandleKey(
        InputState input,
        GrayboxCamera camera,
        HeroChaseCamera heroCamera,
        SessionPipeline pipeline,
        SimWorld world,
        SdlInputEventView inputView)
    {
        var action = Keymap.Resolve(inputView.Scancode);

        if (action is null)
        {
            return;
        }

        if (action == "quit")
        {
            if (inputView.Type == SdlEventCodes.KeyDown && !inputView.KeyIsRepeat)
            {
                input.QuitRequested = true;
            }

            return;
        }

        if (inputView.Type == SdlEventCodes.KeyDown)
        {
            if (inputView.KeyIsRepeat)
            {
                return;
            }

            switch (action)
            {
                case "zoom-in":
                    // Vertraglich getestete Richtung: +1 verkleinert die
                    // Anzeigedistanz (heranzoomen); im persönlichen Modus
                    // die Distanz der Verfolgungskamera.
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        heroCamera.ZoomSteps(+1);
                    }
                    else
                    {
                        camera.ZoomSteps(+1);
                    }

                    return;

                case "zoom-out":
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        heroCamera.ZoomSteps(-1);
                    }
                    else
                    {
                        camera.ZoomSteps(-1);
                    }

                    return;

                case ModeContract.SwitchActionName:
                    // Frei belegbare Umschaltaktion (T-033, Modevertrag
                    // Abschnitt 4): erzeugt an der laufenden Vorgrenze einen
                    // Live-Wechsel-Intent; kein Kernbefehl, kein
                    // Simulationszustand. In beiden Modi gültig.
                    pipeline.EnqueueLiveIntent(new GrayboxIntent(
                        (int)world.TickIndex,
                        GrayboxIntentKind.SwitchMode));
                    return;

                default:
                    input.HeldScancodes.Add(inputView.Scancode);
                    return;
            }
        }

        input.HeldScancodes.Remove(inputView.Scancode);
    }

    private static void ApplyHeldKeys(
        InputState input,
        GrayboxCamera camera,
        HeroChaseCamera heroCamera,
        SessionPipeline pipeline,
        SimWorld world)
    {
        foreach (var scancode in input.HeldScancodes)
        {
            switch (Keymap.Resolve(scancode))
            {
                case "pan-up":
                    // Vertraglich nordaufwaerts (Kommandovertrag §4, feste
                    // Nordausrichtung): Bildschirm oben ist Norden (-Z),
                    // konsistent zum Rand-Schwenken am oberen Fensterrand.
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        EnqueueDirectionalSteering(input, pipeline, world, 0L, -1L);
                    }
                    else
                    {
                        camera.PanSteps(0, -1);
                    }

                    break;

                case "pan-down":
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        EnqueueDirectionalSteering(input, pipeline, world, 0L, +1L);
                    }
                    else
                    {
                        camera.PanSteps(0, +1);
                    }

                    break;

                case "pan-left":
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        EnqueueDirectionalSteering(input, pipeline, world, -1L, 0L);
                    }
                    else
                    {
                        camera.PanSteps(-1, 0);
                    }

                    break;

                case "pan-right":
                    if (pipeline.CurrentEffectiveMode == SessionMode.Personal)
                    {
                        EnqueueDirectionalSteering(input, pipeline, world, +1L, 0L);
                    }
                    else
                    {
                        camera.PanSteps(+1, 0);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Interaktive Lenkung im persönlichen Modus (T-033, Modevertrag
    /// Abschnitt 3, hero-direction-steering-zones-v1): löst die
    /// kamerarelative Himmelsrichtung deterministisch gegen die sechs
    /// Zonenzentren auf und erzeugt höchstens einen Lenk-Intent je Tick;
    /// ohne Richtungstreue-Kandidat kontrollierte, sichtbare Abweisung mit
    /// der vertraglichen Kennung statt stiller Wirkung. Die Lenkung ist der
    /// einzige Befehlskanal des persönlichen Modus; ein strategischer Intent
    /// kann an dieser Stelle strukturell nicht entstehen.
    /// </summary>
    private static void EnqueueDirectionalSteering(
        InputState input,
        SessionPipeline pipeline,
        SimWorld world,
        long directionX,
        long directionZ)
    {
        if (input.LastSteerTick == world.TickIndex)
        {
            // Höchstens ein Lenkimpuls je Vorgrenze; gehaltene Tasten
            // wirken am jeweils nächsten Tick erneut.
            return;
        }

        var zone = HeroDirectionSteering.ResolveZone(world, directionX, directionZ);

        if (zone < 0)
        {
            // Kein richtungstreuer Kandidat (Modevertrag Abschnitt 3):
            // kontrollierte, sichtbare Abweisung statt stiller Wirkung; kein
            // Kernbefehl. Keine Kontextabweisung, daher kein Eintrag in den
            // interaktiven Kontextabweisungszaehler.
            Console.Error.WriteLine(
                $"kommandoschleife: Befehl abgewiesen - {ModeContract.RejectReasonSteerDirectionWithoutZone} bei Tick {world.TickIndex}.");
            input.LastSteerTick = world.TickIndex;
            return;
        }

        pipeline.EnqueueLiveIntent(new GrayboxIntent(
            (int)world.TickIndex,
            GrayboxIntentKind.SteerGroupToZone,
            zone));
        input.LastSteerTick = world.TickIndex;
    }

    private static void ApplyEdgePan(InputState input, GrayboxCamera camera)
    {
        var stepsX = 0.0;
        var stepsY = 0.0;

        if (input.CursorX <= EdgePanMarginPixels)
        {
            stepsX -= 1;
        }
        else if (input.CursorX >= BenchRunner.DefaultWidth - EdgePanMarginPixels)
        {
            stepsX += 1;
        }

        if (input.CursorY <= EdgePanMarginPixels)
        {
            stepsY -= 1;
        }
        else if (input.CursorY >= BenchRunner.DefaultHeight - EdgePanMarginPixels)
        {
            stepsY += 1;
        }

        if (stepsX != 0.0 || stepsY != 0.0)
        {
            camera.PanSteps(stepsX * 0.25, stepsY * 0.25);
        }
    }

    /// <summary>
    /// Mindest-HUD in der Fenstertitelzeile (T-033, Modevertrag Abschnitt 8,
    /// title-hud-mode-herozone-v1): aktueller Modus und Heldenzone in der
    /// festen Form `Riftward Graybox — Modus: Strategisch|Persönlich —
    /// Heldenzone: <Zone|–>`; bei Opt-in Aktivierung (T-034) ausschließlich
    /// der additive, unterscheidbare Segment ` — Erkundung: <n>/<m>`, ohne
    /// Aktivierung bleibt die Titelzeile byteidentisch zum T-033-Stand;
    /// rein darstellseitig, nur bei Änderung gesetzt.
    /// </summary>
    private static void UpdateTitleHud(
        Window window,
        SessionPipeline pipeline,
        SimWorld world,
        ExplorationSession? exploration,
        ref string lastTitle)
    {
        var modeText = pipeline.CurrentEffectiveMode == SessionMode.Personal ? "Persönlich" : "Strategisch";
        var heroZone = HeroTracker.ZoneIndexOf(world);
        var explorationProgress = exploration is null
            ? string.Empty
            : $" — Erkundung: {exploration.VisitedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{exploration.LandmarkCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var title = $"Riftward Graybox — Modus: {modeText} — Heldenzone: {(heroZone < 0 ? "–" : heroZone.ToString(System.Globalization.CultureInfo.InvariantCulture))}{explorationProgress}";

        if (title == lastTitle)
        {
            return;
        }

        window.SetTitle(title);
        lastTitle = title;
    }

    private static void HandleMotion(
        InputState input,
        GrayboxCamera camera,
        SessionPipeline pipeline,
        SdlInputEventView inputView)
    {
        input.CursorX = inputView.PositionX;
        input.CursorY = inputView.PositionY;

        if (input.MiddleDragging
            && pipeline.CurrentEffectiveMode == SessionMode.Strategic)
        {
            // Ziehen bewegt die Welt mit dem Zeiger: Kameramittelpunkt
            // entgegen. Im persönlichen Modus ist Zieh-Schwenken ohne
            // Wirkung (KOMMANDOVERTRAG Abschnitt 12): die Kamera folgt
            // dem Helden; ein bodenverankerter Schwenk existiert dort nicht.
            var metersPerPixel = camera.DistanceMeters / 700.0;
            camera.Pan(
                -(inputView.PositionX - input.LastMiddleX) * metersPerPixel,
                -(inputView.PositionY - input.LastMiddleY) * metersPerPixel);
        }

        input.LastMiddleX = inputView.PositionX;
        input.LastMiddleY = inputView.PositionY;
    }

    private static void HandleButton(
        InputState input,
        GrayboxCamera camera,
        SessionPipeline pipeline,
        SimWorld world,
        SdlInputEventView inputView)
    {
        var pressed = inputView.Type == SdlEventCodes.MouseButtonDown;

        // Kontexttrennung am Live-Pfad (T-033, KOMMANDOVERTRAG Abschnitt 12,
        // context-visible-rejection-v1): Strategische Maussemantik
        // (Auswahl, Rahmen, Befehl) ist im persönlichen Modus nicht gebunden;
        // ein kontextfalscher Impuls erhält eine kontextierte, maschinen-
        // lesbare Abweisung mit der vertraglichen Kennung und erhöht den
        // Reportzähler, erzeugt aber niemals einen Kernübergabebefehl und
        // keine Auswahlwirkung.
        if (pressed
            && pipeline.CurrentEffectiveMode == SessionMode.Personal
            && inputView.ButtonIndex is SdlMouseButtons.Left or SdlMouseButtons.Right)
        {
            Console.Error.WriteLine(
                $"kommandoschleife: Befehl abgewiesen - {ModeContract.RejectReasonStrategyIntentInPersonalMode} bei ({inputView.PositionX:F0}, {inputView.PositionY:F0}).");
            input.InteractiveContextRejections++;
            return;
        }

        switch (inputView.ButtonIndex)
        {
            case SdlMouseButtons.Left:
                if (pressed)
                {
                    input.LeftDragging = true;
                    input.BoxStartX = inputView.PositionX;
                    input.BoxStartY = inputView.PositionY;
                }
                else if (input.LeftDragging)
                {
                    input.LeftDragging = false;
                    EnqueueBoxOrPoint(input, camera, pipeline, world, inputView.PositionX, inputView.PositionY);
                }

                break;

            case SdlMouseButtons.Middle:
                input.MiddleDragging = pressed;
                input.LastMiddleX = inputView.PositionX;
                input.LastMiddleY = inputView.PositionY;
                break;

            case SdlMouseButtons.Right:
                if (pressed)
                {
                    EnqueueMoveAtCursor(input, camera, pipeline, world, inputView.PositionX, inputView.PositionY);
                }

                break;
        }
    }

    private static void EnqueueBoxOrPoint(
        InputState input,
        GrayboxCamera camera,
        SessionPipeline pipeline,
        SimWorld world,
        double endX,
        double endY)
    {
        var draggedPixels = Math.Sqrt(
            ((endX - input.BoxStartX) * (endX - input.BoxStartX))
            + ((endY - input.BoxStartY) * (endY - input.BoxStartY)));
        var startGround = ScreenToGroundOrNothing(camera, input.BoxStartX, input.BoxStartY);
        var endGround = ScreenToGroundOrNothing(camera, endX, endY);

        if (startGround is null || endGround is null)
        {
            return;
        }

        if (draggedPixels < BoxDragThresholdPixels)
        {
            pipeline.EnqueueLiveIntent(new GrayboxIntent(
                (int)world.TickIndex,
                GrayboxIntentKind.PointSelect,
                ToMillimeters(startGround.Value.SimX, SessionContract.WorldWidthMillimeters),
                ToMillimeters(startGround.Value.SimZ, SessionContract.WorldHeightMillimeters)));
            return;
        }

        pipeline.EnqueueLiveIntent(new GrayboxIntent(
            (int)world.TickIndex,
            GrayboxIntentKind.BoxSelect,
            ToMillimeters(Math.Min(startGround.Value.SimX, endGround.Value.SimX), SessionContract.WorldWidthMillimeters),
            ToMillimeters(Math.Min(startGround.Value.SimZ, endGround.Value.SimZ), SessionContract.WorldHeightMillimeters),
            ToMillimeters(Math.Max(startGround.Value.SimX, endGround.Value.SimX), SessionContract.WorldWidthMillimeters),
            ToMillimeters(Math.Max(startGround.Value.SimZ, endGround.Value.SimZ), SessionContract.WorldHeightMillimeters)));
    }

    private static void EnqueueMoveAtCursor(
        InputState input,
        GrayboxCamera camera,
        SessionPipeline pipeline,
        SimWorld world,
        double pixelX,
        double pixelY)
    {
        var ground = ScreenToGroundOrNothing(camera, pixelX, pixelY);

        if (ground is null)
        {
            return;
        }

        var zone = InteractiveCameraMath.ZoneAtGroundPoint(ground.Value.SimX, ground.Value.SimZ);

        if (zone < 0)
        {
            // Kontrollierte fachliche Abweisung statt stiller Annahme;
            // die vertragliche Kennung (Kommandovertrag Abschnitt 9) wird
            // als UF-001-Fehlerzeile sichtbar ausgegeben.
            Console.Error.WriteLine(
                $"kommandoschleife: Befehl abgewiesen - {SessionContract.RejectReasonTargetNotInZone} bei ({pixelX:F0}, {pixelY:F0}).");
            input.NoZoneRejects++;
            return;
        }

        pipeline.EnqueueLiveIntent(new GrayboxIntent(
            (int)world.TickIndex,
            GrayboxIntentKind.GroupMoveToZone,
            zone));
    }

    private static InteractiveCameraMath.GroundPoint? ScreenToGroundOrNothing(GrayboxCamera camera, double pixelX, double pixelY) =>
        InteractiveCameraMath.ScreenToGround(camera, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight, pixelX, pixelY);

    private static long GcCollectionTotal() =>
        GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    private static long ToMillimeters(double meters, long axisMaximumMillimeters)
    {
        var millimeters = (long)Math.Round(meters * 1000.0, MidpointRounding.AwayFromZero);
        return Math.Clamp(millimeters, 0, axisMaximumMillimeters);
    }

    /* -------------------------------------------------------------- Abgriff */

    /// <summary>
    /// Opt-in Abgriffpaar (T-033, Modevertrag Abschnitt 8): höchstens zwei
    /// Einzelabgriffe — je einer pro Modus über DEMSELBEN Weltzustand am
    /// selben Tick. Der gebundene Zustand (Tick und Zustands-Hash) wird
    /// einmal gelesen; die Modusumschaltung zwischen beiden Abgriffen ist
    /// rein darstellseitig (Kamera- und Badgekanal) und verändert denselben
    /// Weltzustand nicht. Jede Datei ist ein unkomprimiertes 32-Bit-BMP nach
    /// dem T-023-/T-032-Muster mit der maschinenlesbaren Aussagegrenze
    /// Graybox-Zustandsbelegung. Fail-closed: Der Abgriff-View wird vor dem
    /// Rendern explizit an das Renderziel gebunden (T-023-Präzedenz,
    /// RepBenchRunner), das Paar wird vor dem Schreiben gegen identische und
    /// uniforme Frames geprüft, und eine fremde Pfadendung ist kein
    /// vertraglicher Abgriffpfad — jede Verletzung ergibt captured=false mit
    /// Grund (Code 38) statt eines falschen Erfolgs.
    /// </summary>
    private static CaptureOutcome ExecuteCapturePair(
        BgfxDevice device,
        InteractiveSceneResources resources,
        InteractiveView view,
        SimWorld world,
        GrayboxCamera strategicCamera,
        HeroChaseCamera heroCamera,
        float[] projection,
        string artifactPath)
    {
        if (!TrySuffixArtifactPath(artifactPath, out var strategicPath, out var personalPath, out var extensionReason))
        {
            Console.Error.WriteLine($"kommandoschleife: Frameabgriff abgewiesen: {extensionReason}");
            return new CaptureOutcome(true, false, true, extensionReason ?? "capture-path-extension-must-be-bmp");
        }

        var boundTick = world.TickIndex;
        var boundStateHash = FormatHash(world.ComputeStateHash());

        try
        {
            var rtTexture = device.CreateTexture2D(
                BenchRunner.DefaultWidth,
                BenchRunner.DefaultHeight,
                BgfxSceneApi.TextureFormatRgba8,
                BgfxSceneApi.TextureFlagRt,
                initialData: default);
            var frameBuffer = device.CreateFrameBufferFromTexture(rtTexture);
            var readBackTexture = device.CreateTexture2D(
                BenchRunner.DefaultWidth,
                BenchRunner.DefaultHeight,
                BgfxSceneApi.TextureFormatRgba8,
                BgfxSceneApi.TextureFlagBlitDst | BgfxSceneApi.TextureFlagReadBack,
                initialData: default);

            try
            {
                // Beide Abgriffe entstehen in einem eigenen Renderziel-View;
                // ohne explizite Bindung bliebe das Renderziel leer und das
                // Paar wäre byteidentisch schwarz (T-023-Präzedenz).
                device.SetViewFrameBuffer(InteractiveViews.ViewCapture, frameBuffer);
                device.ConfigureRenderTargetView(
                    InteractiveViews.ViewCapture,
                    HostBootstrap.ClearColorRgba,
                    BenchRunner.DefaultWidth,
                    BenchRunner.DefaultHeight);

                // Abgriff 1: strategische Darstellung über den gebundenen
                // Weltzustand. Nur der opt-in Evidenzabgriff zentriert den
                // unveränderten Zoomstand auf den Vertragshelden, damit ein
                // autonomer Skriptlauf am Weltrand keinen ehrlosen Leerblick
                // als Modusbeleg erzeugt; die Sitzungskamera bleibt unberührt.
                var strategicBmp = RenderCaptureFrame(
                    device, resources, view, world,
                    StrategicCaptureCamera(strategicCamera, world),
                    projection, SessionMode.Strategic, readBackTexture, rtTexture);

                // Abgriff 2: persönliche Darstellung über denselben
                // Weltzustand (Verfolgungskamera hinter dem Vertragshelden).
                heroCamera.Follow(world);
                var personalBmp = RenderCaptureFrame(
                    device, resources, view, world,
                    InteractiveCameraMath.ActiveCamera.From(heroCamera),
                    projection, SessionMode.Personal, readBackTexture, rtTexture);

                // Fail-closed Paarprüfung vor dem Schreiben: identische oder
                // uniforme Frames sind kein belegbarer Graybox-Zustand.
                var pairFailure = CommandFrameEvidence.AnalyzeCapturePair(strategicBmp, personalBmp);

                if (pairFailure is not null)
                {
                    Console.Error.WriteLine($"kommandoschleife: Frameabgriff fehlgeschlagen: {pairFailure}");
                    return new CaptureOutcome(true, false, true, pairFailure);
                }

                File.WriteAllBytes(strategicPath, strategicBmp);
                File.WriteAllBytes(personalPath, personalBmp);
                Console.WriteLine($"frame-artifact={strategicPath}");
                Console.WriteLine($"frame-artifact={personalPath}");

                var artifacts = new List<CaptureArtifact>(2)
                {
                    new(
                        Mode: ModeContract.ModeStrategicId,
                        Sha256Hex: FrameEvidence.Sha256Hex(strategicBmp),
                        Width: BenchRunner.DefaultWidth,
                        Height: BenchRunner.DefaultHeight,
                        FormatId: FrameEvidence.FormatId,
                        StatementLimit: CommandFrameEvidence.StatementLimit),
                    new(
                        Mode: ModeContract.ModePersonalId,
                        Sha256Hex: FrameEvidence.Sha256Hex(personalBmp),
                        Width: BenchRunner.DefaultWidth,
                        Height: BenchRunner.DefaultHeight,
                        FormatId: FrameEvidence.FormatId,
                        StatementLimit: CommandFrameEvidence.StatementLimit),
                };

                return new CaptureOutcome(
                    Requested: true,
                    Captured: true,
                    Failed: false,
                    Reason: string.Empty,
                    Artifacts: artifacts,
                    BoundTick: boundTick,
                    BoundStateHashHex: boundStateHash);
            }
            finally
            {
                device.SetViewFrameBuffer(InteractiveViews.ViewCapture, BgfxDevice.InvalidIndex);
                device.DestroyFrameBuffer(frameBuffer);
                device.DestroyTexture(readBackTexture);
                device.DestroyTexture(rtTexture);
            }
        }
        catch (PlatformException exception)
        {
            Console.Error.WriteLine($"kommandoschleife: Frameabgriff fehlgeschlagen: {exception.Error}");
            return new CaptureOutcome(true, false, true, "capture-failed-controlled");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"kommandoschleife: Frameabgriff fehlgeschlagen: {exception.Message}");
            return new CaptureOutcome(true, false, true, "artifact-not-writable");
        }
    }

    /// <summary>
    /// Rein lokale Kamera des opt-in Abgriffs: gleicher strategischer
    /// Nickwinkel und hoechstens der Sitzungszoom, mit dem Vertragshelden im
    /// weltrandbegrenzten Frustum. Mutiert weder Sitzungskamera noch Welt und
    /// ist deshalb kein Eingabe-/Gameplaypfad.
    /// </summary>
    internal static InteractiveCameraMath.ActiveCamera StrategicCaptureCamera(
        GrayboxCamera sessionCamera,
        SimWorld world) =>
        InteractiveCameraMath.ClampToWorldFootprint(
            InteractiveCameraMath.FitHorizontalWorld(
                new(
                    Math.Clamp(
                        world.PositionXOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One,
                        0.0,
                        NavWorld.TilesX),
                    Math.Clamp(
                        world.PositionYOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One,
                        0.0,
                        NavWorld.TilesY),
                    sessionCamera.DistanceMeters,
                    InteractiveCameraMath.PitchRadians),
                InteractiveCameraMath.DefaultViewportAspectRatio,
                GrayboxCamera.DistanceMinMeters),
            InteractiveCameraMath.DefaultViewportAspectRatio);

    /// <summary>Rendert und liest einen einzelnen Abgriff des Paars zurück.</summary>
    private static byte[] RenderCaptureFrame(
        BgfxDevice device,
        InteractiveSceneResources resources,
        InteractiveView view,
        SimWorld world,
        InteractiveCameraMath.ActiveCamera camera,
        float[] projection,
        SessionMode visualMode,
        ushort readBackTexture,
        ushort rtTexture)
    {
        var view16 = InteractiveCameraMath.View16(camera);
        var basis = InteractiveCameraMath.BillboardBasis(camera);
        var markerCount = view.WriteFrameState(world, world.TickIndex, visualMode);

        resources.SubmitCompositePass(
            device,
            InteractiveViews.ViewCapture,
            view16,
            projection,
            basis,
            view.UnitsPointer,
            SimulationContract.AgentCount,
            view.MarkersPointer,
            (uint)markerCount);

        device.RenderFrame();
        device.BlitFull(InteractiveViews.ViewBlit, readBackTexture, rtTexture, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
        device.RenderFrame();

        var captureBytes = new byte[BenchRunner.DefaultWidth * BenchRunner.DefaultHeight * 4];
        var captureHandle = GCHandle.Alloc(captureBytes, GCHandleType.Pinned);

        try
        {
            var readyFrame = device.ReadTextureBegin(readBackTexture, captureHandle.AddrOfPinnedObject(), (uint)captureBytes.Length);
            uint currentFrame;

            do
            {
                currentFrame = device.RenderFrame();
            }
            while (currentFrame < readyFrame);
        }
        finally
        {
            captureHandle.Free();
        }

        return FrameEvidence.EncodeBmpFromRgbaTopDown(captureBytes, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
    }

    /// <summary>
    /// Vertragliche Paarbenennung (Modevertrag Abschnitt 8): vor der Endung
    /// von PFAD wird das Suffix eingefügt; ohne Endung wird suffigiert. Die
    /// Abgriffe sind stets BMP — eine fremde Endung ist kein vertraglicher
    /// Pfad und wird fail-closed mit Grund abgewiesen statt BMP-Bytes unter
    /// falscher Endung zu schreiben.
    /// </summary>
    internal static bool TrySuffixArtifactPath(
        string path,
        out string strategicPath,
        out string personalPath,
        out string? reason)
    {
        var extension = Path.GetExtension(path);

        if (string.IsNullOrEmpty(extension))
        {
            strategicPath = path + "-strategisch";
            personalPath = path + "-persoenlich";
            reason = null;
            return true;
        }

        if (!string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            strategicPath = string.Empty;
            personalPath = string.Empty;
            reason = "capture-path-extension-must-be-bmp";
            return false;
        }

        strategicPath = path[..^extension.Length] + "-strategisch" + extension;
        personalPath = path[..^extension.Length] + "-persoenlich" + extension;
        reason = null;
        return true;
    }

    /* --------------------------------------------------------------- Report */

    internal sealed record CaptureArtifact(
        string Mode,
        string Sha256Hex,
        int Width,
        int Height,
        string FormatId,
        string StatementLimit);

    internal sealed record CaptureOutcome(
        bool Requested,
        bool Captured,
        bool Failed,
        string Reason,
        IReadOnlyList<CaptureArtifact>? Artifacts = null,
        long BoundTick = -1,
        string? BoundStateHashHex = null);

    internal static CaptureOutcome NotRequestedCapture() =>
        new(false, false, false, CommandFrameEvidence.ReasonNotRequested);

    internal sealed record WorkingSetSamples(
        bool Measured,
        long? MinKiB,
        long? MaxKiB,
        long? EndKiB,
        string? Reason);

    internal sealed record FrameBandValues(double P50, double P95, double P99);

    internal sealed record InteractiveExtras(
        FrameBandValues FrameBand,
        bool GpuTimeMeasured,
        double GpuTimeP99Ms,
        long GpuTimerFrequencyHz,
        long DrawCallsMax,
        long TrianglesMax,
        long PeakMarkers);

    internal sealed record ReportContext(
        string ExecutionMode,
        uint Seed,
        ParsedInputScript Parsed,
        int WarmupTicks,
        int HorizonTicks,
        DateTime ProcessStart,
        string Commit,
        string BuildMode,
        SystemInfo.Environment Environment,
        IReadOnlyList<ToolchainPin> Pins,
        SessionMetrics Metrics,
        ulong StartHash,
        ulong EndHash,
        long[] IntervalSampleTicks,
        ulong[] IntervalHashes,
        int AppliedIntents,
        int RejectedIntents,
        int EmptyPointDeselects,
        int MoveWithoutSelectionRejects,
        int NoZoneRejects,
        int KernelCommandsTotal,
        CommandGateVerdict Verdict,
        bool WindowCompleted,
        CaptureOutcome Capture,
        object? Display,
        WorkingSetSamples WorkingSet,
        int ExitCode,
        InteractiveExtras? InteractiveExtras = null,
        ModeTelemetry? Telemetry = null,
        long InteractiveContextRejections = 0,
        object? Hud = null,
        ExplorationTelemetry? Exploration = null);

    /// <summary>
    /// Baut den Report: ohne Opt-in Aktivierung exakt den Bestandsreport
    /// (Schemaversion 2, byteidentisch zum T-033-Stand); bei Aktivierung
    /// rein additiv die Schemaversion 3 mit dem vollstaendigen
    /// explorationSession-Block (T-034, Vertrag Abschnitte 6 und 7). Die
    /// Feldreihenfolge ist fixiert, damit die Schemaversion 2-Reports
    /// byteidentisch bleiben.
    /// </summary>
    internal static object BuildReport(ReportContext ctx)
    {
        var report = new Dictionary<string, object>
        {
            ["schemaVersion"] = ctx.Exploration is null
                ? CommandReportSchema.VersionWithoutExploration
                : CommandReportSchema.CurrentVersion,
            ["mode"] = CommandReportSchema.ModeCommandLoop,
            ["executionMode"] = ctx.ExecutionMode,
            ["command"] = $"{CommandName} --scenario {SessionContract.ScenarioId} --input-script <PFAD> --seed N --report <PFAD>",
            ["scenario"] = new
            {
                id = SessionContract.ScenarioId,
                seed = ctx.Seed,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = SimulationContract.AgentCount,
                worldId = SimulationContract.WorldId,
                content = SessionContract.ContentId,
            },
            ["commandContract"] = new
            {
                document = SessionContract.DocumentPath,
                version = SessionContract.ContractVersion,
                scriptFormat = ctx.Parsed.FormatId,
                selectionModel = SessionContract.SelectionModelId,
                cameraModel = SessionContract.CameraModelId,
                diagnosticOnlyReplayDisclaimer = true,
                modeContract = new
                {
                    document = ModeContract.DocumentPath,
                    version = ModeContract.ContractVersion,
                },
            },
            ["modeSession"] = BuildModeSession(ctx),
        };

        if (ctx.Exploration is { } exploration)
        {
            report["explorationSession"] = BuildExplorationSession(
                ctx.ExecutionMode, ctx.WindowCompleted, exploration);
        }

        report["simulationContract"] = new
        {
            document = SimulationContract.DocumentPath,
            version = SimulationContract.ContractVersion,
            numericModel = SimulationContract.NumericModelId,
            hashAlgorithm = SimulationContract.HashAlgorithmId,
            allocationLimitBytesPerWarmTick = SimulationContract.AllocationLimitBytesPerWarmTick,
        };
        report["inputScript"] = new
        {
            scriptSha256 = ctx.Parsed.ScriptSha256Hex,
            intentPlanHash = ctx.Parsed.IntentPlanHashHex,
            horizonTicks = ctx.HorizonTicks,
            warmupTicks = ctx.WarmupTicks,
            intentsTotal = ctx.Parsed.Intents.Length,
            appliedTotal = ctx.AppliedIntents,
            rejectedTotal = ctx.RejectedIntents,
            emptyPointDeselects = ctx.EmptyPointDeselects,
            moveWithoutSelectionRejects = ctx.MoveWithoutSelectionRejects,
            noZoneRejects = ctx.NoZoneRejects,
            kernelCommandsTotal = ctx.KernelCommandsTotal,
        };
        report["startedAtUtc"] = ctx.ProcessStart;
        report["finishedAtUtc"] = DateTime.UtcNow;
        report["environment"] = BuildEnvironment(ctx);
        report["measurement"] = new
        {
            warmupTicks = (long)ctx.WarmupTicks,
            sampleTicks = (long)(ctx.HorizonTicks - ctx.WarmupTicks),
            ticksExecuted = (long)ctx.HorizonTicks,
            hashSampleIntervalTicks = (long)SessionContract.HashSampleIntervalTicks,
            rssSampleIntervalTicks = (long)SessionContract.RssSampleIntervalTicks,
            windowCompleted = ctx.WindowCompleted,
        };
        report["metrics"] = BuildMetrics(ctx);
        report["stateHashChain"] = new
        {
            unit = "hex64",
            method = SimulationContract.HashAlgorithmId,
            start = FormatHash(ctx.StartHash),
            intervalSampleTicks = ctx.IntervalSampleTicks,
            intervalHashes = ctx.IntervalHashes.Select(FormatHash).ToArray(),
            end = FormatHash(ctx.EndHash),
        };
        report["gate"] = new
        {
            limits = new
            {
                p99TickTimeHardLimitMs = CommandGateLimits.Documented.P99TickTimeHardLimitMs,
                p99TickTimeTargetMs = CommandGateLimits.Documented.P99TickTimeTargetMs,
                allocationsPerWarmTickBytesMax = CommandGateLimits.Documented.AllocationsPerWarmTickLimitBytes,
                reactionHardLimitTicks = CommandGateLimits.Documented.ReactionHardLimitTicks,
                reactionTargetTicks = CommandGateLimits.Documented.ReactionTargetTicks,
                runtimeShaderCompilationAllowed = false,
                switchReactionHardLimitTicks = CommandGateLimits.Documented.SwitchReactionHardLimitTicks,
                switchReactionTargetTicks = CommandGateLimits.Documented.SwitchReactionTargetTicks,
            },
            stateChainSelfConsistency = ctx.ExecutionMode == CommandReportSchema.ExecutionInteractive
                ? ChainCriterion.NotEvaluated()
                : ChainCriterion.Evaluated(),
            switchReaction = ctx.Verdict.SwitchReactionEvaluated
                ? (object)new
                {
                    evaluated = true,
                    max = (ctx.Telemetry ?? ModeTelemetry.Empty).MaxSwitchReactionTicks,
                    targetMet = ctx.Verdict.SwitchReactionTargetMet,
                }
                : new
                {
                    evaluated = false,
                    reason = "no-effective-mode-switch-in-run",
                },
            pass = ctx.WindowCompleted ? ctx.Verdict.Pass : false,
            tickTimeTargetMet = ctx.Verdict.TickTimeTargetMet,
            reactionTargetMet = ctx.Verdict.ReactionTargetMet,
            violations = ctx.WindowCompleted
                ? ctx.Verdict.Violations
                : ctx.Verdict.Violations.Append("run-incomplete-no-evidence").ToArray(),
        };
        report["openQuestions"] = OpenQuestions();
        report["profiles"] = ProfileBinding.MandatoryWithoutReferenceHardware()
            .Select(status => new
            {
                id = status.ProfileId,
                status = status.Status,
                boundReferenceClass = status.BoundReferenceClass,
                reason = status.Reason,
            })
            .ToArray();
        report["baseline"] = new
        {
            classification = "diagnostic-developer-workstation",
            protocol = "qops001-2026-08-24",
        };
        report["frameEvidence"] = BuildFrameEvidence(ctx.Capture);
        report["exitCode"] = ctx.ExitCode;

        return report;
    }

    /// <summary>
    /// Erkundungssitzungsblock des Reports (T-034, Vertrag Abschnitt 7): bei
    /// Aktivierung vertraglich gebunden — Vertragsbindung, Landmarkenmenge
    /// in fester Zonenordnung, Aufsuchprotokoll in kanonischer
    /// Registrierungsfolge, Fortschritt/Abschluss, versionierte
    /// Nichtpersistenzaussage und die fensterpflichtigen Ausweise
    /// (headless ausdruecklich nicht gemessen mit maschinenlesbarem Grund).
    /// Rein diagnostisch (gateCoupled=false); kein Gate, kein Budgetwert und
    /// keine Exitcodebedeutung.
    /// </summary>
    internal static Dictionary<string, object> BuildExplorationSession(
        string executionMode,
        bool windowCompleted,
        ExplorationTelemetry exploration)
    {
        var presentationMeasured = executionMode == CommandReportSchema.ExecutionInteractive
            && windowCompleted;
        var unavailableReason = executionMode == CommandReportSchema.ExecutionHeadless
            ? ExplorationContract.HeadlessMeasurementReason
            : "run-incomplete-exploration-presentation-not-asserted";

        return new Dictionary<string, object>
        {
            ["contract"] = new
            {
                document = ExplorationContract.DocumentPath,
                version = ExplorationContract.ContractVersion,
            },
            ["activationId"] = ExplorationContract.ActivationId,
            ["landmarkModel"] = ExplorationContract.LandmarkModelId,
            ["visitRule"] = ExplorationContract.VisitRuleId,
            ["counterModel"] = ExplorationContract.CounterModelId,
            ["landmarks"] = exploration.Landmarks.Select(landmark => new Dictionary<string, object>
            {
                ["zoneIndex"] = landmark.ZoneIndex,
                ["anchorTileX"] = landmark.AnchorTileX,
                ["anchorTileY"] = landmark.AnchorTileY,
                ["walkable"] = landmark.Walkable,
            }).ToArray(),
            ["visitProtocol"] = exploration.VisitProtocol.Select(visit => new Dictionary<string, object>
            {
                ["evaluationBoundaryTick"] = visit.EvaluationBoundaryTick,
                ["zoneIndex"] = visit.ZoneIndex,
                ["mode"] = visit.Mode,
                ["visitOrder"] = visit.VisitOrder,
                ["gateCoupled"] = false,
            }).ToArray(),
            ["progress"] = new Dictionary<string, object>
            {
                ["visitedCount"] = exploration.VisitedCount,
                ["landmarkCount"] = exploration.LandmarkCount,
                ["completed"] = exploration.Completed,
                ["gateCoupled"] = false,
            },
            ["persistence"] = new Dictionary<string, object>
            {
                ["statementId"] = ExplorationContract.NotPersistedStatementId,
                ["persisted"] = ExplorationContract.Persisted,
                ["saveLoad"] = "not-continued",
                ["replay"] = "not-continued",
                ["gateCoupled"] = false,
            },
            ["gateCoupled"] = false,
            ["hud"] = presentationMeasured
                ? (object)new Dictionary<string, object>
                {
                    ["measured"] = true,
                    ["kind"] = ExplorationContract.HudModelId,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["visitedCount"] = exploration.VisitedCount,
                        ["landmarkCount"] = exploration.LandmarkCount,
                        ["completed"] = exploration.Completed,
                    },
                }
                : new Dictionary<string, object>
                {
                    ["measured"] = false,
                    ["kind"] = ExplorationContract.HudModelId,
                    ["reason"] = unavailableReason,
                },
            ["landmarkChannel"] = presentationMeasured
                ? (object)new Dictionary<string, object>
                {
                    ["measured"] = true,
                    ["kind"] = ExplorationContract.LandmarkChannelModelId,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["landmarkCount"] = exploration.LandmarkCount,
                        ["registeredCount"] = exploration.VisitedCount,
                    },
                }
                : new Dictionary<string, object>
                {
                    ["measured"] = false,
                    ["kind"] = ExplorationContract.LandmarkChannelModelId,
                    ["reason"] = unavailableReason,
                },
        };
    }

    private static Dictionary<string, object> BuildEnvironment(ReportContext ctx)
    {
        var environment = new Dictionary<string, object>
        {
            ["os"] = new { type = ctx.Environment.OsType, kernelRelease = ctx.Environment.KernelRelease },
            ["cpu"] = new { model = ctx.Environment.CpuModel },
            ["rid"] = BenchEnvironment.Rid(),
            ["commit"] = ctx.Commit,
            ["buildMode"] = ctx.BuildMode,
            ["display"] = ctx.Display
                ?? (object)new
                {
                    measured = false,
                    reason = "headless-mode-native-artifacts-not-loaded",
                },
            ["pins"] = ctx.Pins.Select(pin => new Dictionary<string, string>
            {
                ["id"] = pin.Id,
                ["refType"] = pin.RefType,
                ["ref"] = pin.Ref,
                ["commit"] = pin.Commit,
                ["sourceSha256"] = pin.SourceSha256,
                ["licenseSpdx"] = pin.LicenseSpdx,
            }).ToArray(),
        };

        return environment;
    }

    /// <summary>
    /// Modussitzungsblock des Reports (T-033, Modevertrag Abschnitt 7):
    /// Wechselprotokoll je Grenze inklusive Heldenstatus von Agentenindex 0,
    /// Kontextabweisungszähler (Pipelinezähler plus der Live-Pfadzähler
    /// context-visible-rejection-v1), Lenk-Dedupe, Titel-HUD-Bindung und die
    /// diagnostische Wechselreaktionsverteilung. Der Modus ist
    /// Sitzungszustand; dieser Block ist rein diagnostisch
    /// (gateCoupled=false), die fail-closed Koppelung von Kriterium 6 erfolgt
    /// ausschließlich über gate.switchReaction.
    /// </summary>
    private static Dictionary<string, object> BuildModeSession(ReportContext ctx)
    {
        var telemetry = ctx.Telemetry ?? ModeTelemetry.Empty;

        return new Dictionary<string, object>
        {
            ["contract"] = new
            {
                document = ModeContract.DocumentPath,
                version = ModeContract.ContractVersion,
            },
            ["initialMode"] = ModeName(telemetry.InitialMode),
            ["finalMode"] = ModeName(telemetry.FinalMode),
            ["switchProtocol"] = telemetry.SwitchProtocol.Select(entry => new Dictionary<string, object>
            {
                ["intentTick"] = entry.IntentTick,
                ["evaluatedBoundaryTick"] = entry.EvaluatedBoundaryTick,
                ["effectiveBoundaryTick"] = entry.EffectiveBoundaryTick,
                ["previousMode"] = ModeName(entry.PreviousMode),
                ["newMode"] = ModeName(entry.NewMode),
                ["effectiveInRun"] = entry.EffectiveInRun,
                ["switchReactionTicks"] = entry.SwitchReactionTicks,
                ["heroPositionXMm"] = entry.HeroPositionXMm,
                ["heroPositionYMm"] = entry.HeroPositionYMm,
                ["heroZoneIndex"] = entry.HeroZoneIndex,
                ["heroPathState"] = (int)entry.HeroPathState,
            }).ToArray(),
            ["strategyIntentsRejectedInPersonalMode"] = telemetry.StrategyIntentsRejectedInPersonalMode,
            ["steerIntentsRejectedInStrategyMode"] = telemetry.SteerIntentsRejectedInStrategyMode,
            ["steerIdleDedupes"] = telemetry.SteerIdleDedupes,
            ["interactiveContextRejections"] = ctx.InteractiveContextRejections,
            ["hud"] = ctx.Hud ?? (object)new
            {
                measured = false,
                kind = ModeContract.HudModelId,
                reason = "headless-run-without-window",
            },
            ["switchReactionTicks"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "ticks",
                ["method"] = "mode-switch-intent-tick-to-first-validity-boundary-in-new-mode",
                ["p50"] = telemetry.SwitchReactionP50Ticks,
                ["p95"] = telemetry.SwitchReactionP95Ticks,
                ["p99"] = telemetry.SwitchReactionP99Ticks,
                ["max"] = telemetry.MaxSwitchReactionTicks,
                ["count"] = telemetry.SwitchReactionSampleCount,
                ["target"] = ModeContract.SwitchReactionTargetTicks,
                ["hardLimit"] = ModeContract.SwitchReactionHardLimitTicks,
            }),
        };
    }

    private static string ModeName(SessionMode mode) =>
        mode == SessionMode.Personal ? ModeContract.ModePersonalId : ModeContract.ModeStrategicId;

    private static Dictionary<string, object> BuildMetrics(ReportContext ctx)
    {
        var metrics = ctx.Metrics;
        var interactive = ctx.ExecutionMode == CommandReportSchema.ExecutionInteractive;

        var result = new Dictionary<string, object>
        {
            ["tickTimeMs"] = new
            {
                unit = "ms",
                method = "stopwatch-tick-delta",
                p50 = Math.Round(metrics.P50TickTimeMs, 3),
                p95 = Math.Round(metrics.P95TickTimeMs, 3),
                p99 = Math.Round(metrics.P99TickTimeMs, 3),
            },
            ["managedAllocationsBytes"] = new
            {
                unit = "bytes",
                method = "gc-total-allocated-bytes-precise-delta-per-tick-sum",
                perWarmTick = Math.Round(metrics.AllocationsPerWarmTickBytes, 3),
            },
            ["reactionTicks"] = new
            {
                unit = "ticks",
                method = "command-submission-tick-to-first-effect-state-hash-delta",
                p50 = metrics.ReactionP50Ticks,
                p95 = metrics.ReactionP95Ticks,
                p99 = metrics.ReactionP99Ticks,
                max = metrics.MaxReactionTicks,
                count = metrics.ReactionSampleCount,
                target = SessionContract.ReactionTargetTicks,
                hardLimit = SessionContract.ReactionHardLimitTicks,
            },
            ["runtimeShaderCompilation"] = new
            {
                unit = "bool",
                method = "offline-shaderc-binaries-only",
                value = false,
            },
            ["gcPauseSumMs"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "ms",
                ["method"] = "gc-get-total-pause-duration-delta",
                ["value"] = Math.Round(metrics.GcPauseSumMs, 3),
            }),
            ["gcPauseCount"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "count",
                ["method"] = "gc-collection-count-gen0-to2-delta",
                ["value"] = metrics.GcPauseCount,
            }),
            ["activeAgents"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "count",
                ["method"] = "soa-agent-count-fixed",
                ["value"] = SimulationContract.AgentCount,
            }),
            ["workingSetKiB"] = ctx.WorkingSet.Measured
                ? DiagnosticEnvelope(new Dictionary<string, object>
                {
                    ["measured"] = true,
                    ["unit"] = "KiB",
                    ["method"] = "proc-self-status-vmrss-samples",
                    ["min"] = ctx.WorkingSet.MinKiB!.Value,
                    ["max"] = ctx.WorkingSet.MaxKiB!.Value,
                    ["end"] = ctx.WorkingSet.EndKiB!.Value,
                })
                : (object)new
                {
                    measured = false,
                    reason = ctx.WorkingSet.Reason ?? "rss-sampler-unavailable",
                },
        };

        if (interactive && ctx.InteractiveExtras is { } extras)
        {
            result["frameTimeMs"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "ms",
                ["method"] = "stopwatch-delta-around-windowed-simulation-tick-including-allocation-probes",
                ["p50"] = Math.Round(extras.FrameBand.P50, 3),
                ["p95"] = Math.Round(extras.FrameBand.P95, 3),
                ["p99"] = Math.Round(extras.FrameBand.P99, 3),
            });

            if (extras.GpuTimeMeasured)
            {
                result["gpuTimeMs"] = DiagnosticEnvelope(new Dictionary<string, object>
                {
                    ["measured"] = true,
                    ["unit"] = "ms",
                    ["method"] = "bgfx-stats-gpu-timer-p99",
                    ["p99"] = Math.Round(extras.GpuTimeP99Ms, 3),
                    ["timerFreqHz"] = extras.GpuTimerFrequencyHz,
                });
            }
            else
            {
                result["gpuTimeMs"] = new
                {
                    measured = false,
                    reason = "backend-gpu-timer-unavailable",
                };
            }

            result["drawSubmitCallsPerFrame"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "count",
                ["method"] = "bgfx-stats-numdraw-max-including-shadow-passes",
                ["value"] = extras.DrawCallsMax,
            });
            result["visibleTrianglesPerFrame"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "count",
                ["method"] = "bgfx-stats-numprims-trilist-max-including-shadow-passes",
                ["value"] = extras.TrianglesMax,
            });
            result["concurrentMarkers"] = DiagnosticEnvelope(new Dictionary<string, object>
            {
                ["unit"] = "count",
                ["method"] = "marker-instance-count-max-per-frame",
                ["peak"] = extras.PeakMarkers,
            });
        }
        else
        {
            var unavailableReason = ctx.WindowCompleted
                ? "headless-cpu-scenario-no-renderer"
                : "run-incomplete-no-evidence";

            result["frameTimeMs"] = Unavailable(unavailableReason);
            result["gpuTimeMs"] = Unavailable(unavailableReason);
            result["drawSubmitCallsPerFrame"] = Unavailable(unavailableReason);
            result["visibleTrianglesPerFrame"] = Unavailable(unavailableReason);
            result["concurrentMarkers"] = Unavailable(unavailableReason);
        }

        return result;
    }

    private static object Unavailable(string reason) => new
    {
        measured = false,
        reason,
    };

    private static Dictionary<string, object> DiagnosticEnvelope(Dictionary<string, object> payload)
    {
        payload["gateCoupled"] = false;
        return payload;
    }

    /// <summary>
    /// Ausweis des Ketten-Selbstkonsistenzkriteriums (Kommandovertrag §7):
    /// headless ausgewertet (Ergebnis über pass/violations), im
    /// Interaktivmodus ausdrücklich nicht auswertbar mit maschinenlesbarem
    /// Grund statt stiller Behauptung.
    /// </summary>
    internal static class ChainCriterion
    {
        public const string InteractiveReason = "live-inputs-nondeterministic-criterion-not-asserted";

        public static object Evaluated() => new { evaluated = true };

        public static object NotEvaluated() => new { evaluated = false, reason = InteractiveReason };
    }

    /// <summary>
    /// Vertragliche Exitcodepraezedenz des Interaktivmodus (Kommandovertrag §8,
    /// NATIVE_UNTERBAU.md): Ein unvollstaendiger Lauf (windowCompleted=false)
    /// ist niemals Evidenz und dominiert deshalb stets mit Code 36 — auch wenn
    /// ein Abgriff angefordert war, der wegen der Unvollstaendigkeit unterbleiben
    /// musste; sein Grund bleibt im Report gebunden (captured=false). Bei
    /// abgeschlossenem Fenster entscheidet ein fehlgeschlagener opt-in Abgriff
    /// mit Code 38, sonst das Gateverdict.
    /// </summary>
    internal static int ResolveInteractiveExitCode(bool windowCompleted, bool captureFailed, int gateExitCode)
    {
        if (!windowCompleted)
        {
            return ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete);
        }

        return captureFailed
            ? ExitCodes.Map(PlatformErrorCode.CommandCaptureFailed)
            : gateExitCode;
    }

    private static Dictionary<string, string> OpenQuestions() => new()
    {
        ["qtec004"] = "open",
        ["qtec006"] = "open",
        ["qtec010"] = "open",
        ["qgam001"] = "open",
        ["qgam002"] = "open",
        ["qgam003"] = "open",
        ["qgam004"] = "open",
        ["qgam005"] = "open",
        ["qgam006"] = "open",
        ["qgam007"] = "open",
        ["qgam010"] = "open",
        ["qnar002"] = "open",
    };

    private static object BuildFrameEvidence(CaptureOutcome capture) =>
        !capture.Requested
            ? new { captured = false, reason = CommandFrameEvidence.ReasonNotRequested }
            : capture.Captured
                ? (object)new
                {
                    captured = true,
                    afterMeasurementWindow = true,
                    boundTick = capture.BoundTick,
                    boundStateHash = capture.BoundStateHashHex!,
                    captures = capture.Artifacts!.Select(artifact => new
                    {
                        mode = artifact.Mode,
                        sha256 = artifact.Sha256Hex,
                        width = artifact.Width,
                        height = artifact.Height,
                        format = artifact.FormatId,
                        statementLimit = artifact.StatementLimit,
                    }).ToArray(),
                }
                : new { captured = false, reason = capture.Reason };

    private static int FinishReport(string reportPath, string reportJson, int successExitCode)
    {
        var schemaErrors = CommandReportSchema.Validate(reportJson);

        if (schemaErrors.Count > 0)
        {
            Console.Error.WriteLine($"kommandoschleife: Report widerspricht dem Schemavertrag: {string.Join("; ", schemaErrors)}");
            BenchRunner.WriteReportOrDiagnose(reportPath, reportJson);
            return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
        }

        if (!BenchRunner.WriteReportOrDiagnose(reportPath, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        return successExitCode;
    }

    /// <summary>Schreibt einen als keine Evidenz markierten Teilreport (Exitcode 36).</summary>
    private static int WriteIncompleteReport(
        string reportPath,
        string executionMode,
        uint seed,
        ParsedInputScript parsed,
        int warmupTicks,
        int horizonTicks,
        string commit,
        string buildMode,
        SystemInfo.Environment environment,
        bool explorationEnabled,
        ExplorationTelemetry? exploration)
    {
        var verdict = new CommandGateVerdict(
            Pass: false,
            TickTimeTargetMet: false,
            ReactionTargetMet: false,
            Violations: ["run-incomplete-no-evidence"]);

        var zeroMetrics = new SessionMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var reportJson = JsonSerializer.Serialize(BuildReport(new ReportContext(
            ExecutionMode: executionMode,
            Seed: seed,
            Parsed: parsed,
            WarmupTicks: warmupTicks,
            HorizonTicks: horizonTicks,
            ProcessStart: DateTime.UtcNow,
            Commit: commit,
            BuildMode: buildMode,
            Environment: environment,
            Pins: Array.Empty<ToolchainPin>(),
            Metrics: zeroMetrics,
            StartHash: 0,
            EndHash: 0,
            IntervalSampleTicks: [0],
            IntervalHashes: [0],
            AppliedIntents: 0,
            RejectedIntents: 0,
            EmptyPointDeselects: 0,
            MoveWithoutSelectionRejects: 0,
            NoZoneRejects: 0,
            KernelCommandsTotal: 0,
            Verdict: verdict,
            WindowCompleted: false,
            Capture: NotRequestedCapture(),
            Display: null,
            WorkingSet: new WorkingSetSamples(false, null, null, null, "run-incomplete"),
            ExitCode: ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete),
            Hud: new
            {
                measured = false,
                kind = ModeContract.HudModelId,
                reason = "run-incomplete-hud-not-asserted",
            },
            Exploration: ResolveIncompleteExploration(
                explorationEnabled, exploration))), BenchRunner.ReportJsonOptions) + "\n";

        Console.Error.WriteLine("kommandoschleife: Teilreport gilt ausdruecklich nicht als Evidenz.");
        return FinishReport(reportPath, reportJson, ExitCodes.Map(PlatformErrorCode.CommandRunIncomplete));
    }

    /// <summary>
    /// Erhaelt die explizit angeforderte T-034-Aktivierung auch in
    /// Exception-Teilreports. Bereits beobachtete Telemetrie wird bewahrt;
    /// vor der Sitzungserzeugung abgebrochene Laeufe tragen den kanonischen
    /// leeren, aber vollstaendigen Schemaversion-3-Block.
    /// </summary>
    internal static ExplorationTelemetry? ResolveIncompleteExploration(
        bool explorationEnabled,
        ExplorationTelemetry? observed) =>
        explorationEnabled
            ? observed ?? new ExplorationSession().ToTelemetry()
            : null;

    private static string FormatHash(ulong hash) =>
        hash.ToString(SimReportSchema.HashFormat, CultureInfo.InvariantCulture);
}

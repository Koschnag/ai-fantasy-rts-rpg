using System.Globalization;
using Riftward.App.Bench;
using Riftward.Simulation;

namespace Riftward.App.Soak;

/// <summary>Optionen eines Engine-Laufs (reine Werte, keine Umgebung).</summary>
internal sealed record SoakEngineOptions(
    uint Seed,
    long TotalTicks,
    bool Paced,
    int WarmupTicks,
    int WindowSeconds,
    double WatchdogWindowSeconds,
    long HashSampleIntervalTicks,
    int StrictAllocationVerificationTicks);

/// <summary>
/// Ergebnis eines Engine-Laufs: rohe Stichproben ohne Gateentscheidung.
/// Die Auswertung gegen dokumentierte Grenzwerte geschieht ausschliesslich
/// im fail-closed Soakgate; die Engine selbst entscheidet nichts.
/// </summary>
internal sealed class SoakExecutionResult
{
    public required SimWorld World { get; init; }

    public required long MeasuredTicksExecuted { get; init; }

    public required double WallSeconds { get; init; }

    public required bool StallDetected { get; init; }

    public required ulong StartStateHash { get; init; }

    public required ulong EndStateHash { get; init; }

    public required long[] ChainSampleTicks { get; init; }

    public required ulong[] ChainSampleHashes { get; init; }

    public required int ChainSampleCount { get; init; }

    public required SoakWindowSeries Series { get; init; }

    public required ProgressWatchdog Watchdog { get; init; }

    public required long GcPauseCount { get; init; }

    public required double GcPauseSumMs { get; init; }

    public required double AllocationsPerWarmTickBytes { get; init; }

    public required long StrictVerificationTickCount { get; init; }

    public required int StrictVerificationBurstCount { get; init; }

    public required double StrictAllocationsPerTickBytes { get; init; }

    public required bool RssMeasured { get; init; }

    public required long RssFirstKiB { get; init; }

    public required long RssMinKiB { get; init; }

    public required long RssMaxKiB { get; init; }

    public required long RssEndKiB { get; init; }

    public required string? RssReason { get; init; }

    public required int CommandCount { get; init; }

    public required string PlanHashHex { get; init; }
}

/// <summary>
/// Gemeinsamer deterministischer Treiber aller Soakszenarien: fuehrt den
/// skriptierten Befehlsplan gemaess Simulationsvertrag V1 ueber die
/// unveraenderte Welt mit genau 250 vollstaendig simulierten Agenten aus.
/// Der Zustand haengt ausschliesslich an der Tickzahl und der kanonischen
/// Befehlsordnung; die Taktquelle entscheidet nur, wann wie viele Ticks
/// ausgefuehrt werden (Nachholmechanik ohne Zustandsabhaengigkeit von der
/// Wanduhr). Alle Erfassungspuffer sind vorallokiert; die Fensterklammerung
/// des Allokationszaehlers schliesst Telemetrie bewusst aus.
/// </summary>
internal static class SoakEngine
{
    public static SoakExecutionResult Run(SoakEngineOptions options, ITickPacingClock clock)
    {
        if (options.TotalTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Soakhorizont benoetigt mindestens einen Messstick.");
        }

        if (options.WarmupTicks <= CommandPlan.FirstCommandTick)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Warm-up muss hinter dem ersten Planbefehl liegen.");
        }

        if (options.HashSampleIntervalTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Kettenintervall benoetigt mindestens einen Tick.");
        }

        var world = new SimWorld(options.Seed);
        var totalPlanTicks = checked((int)Math.Min(int.MaxValue - 1L, options.WarmupTicks + options.TotalTicks));
        var plan = CommandPlan.Generate(options.Seed, totalPlanTicks);
        var planHash = CommandPlan.Hash(plan);

        var startStateHash = world.ComputeStateHash();
        var totalSimulationTick = options.WarmupTicks + options.TotalTicks;
        var schedule = SoakPlan.ChainSchedule(totalSimulationTick, options.HashSampleIntervalTicks);
        var chainTicks = new long[schedule.Length + 1];
        var chainHashes = new ulong[schedule.Length + 1];
        var chainCursor = 0;

        chainTicks[0] = world.TickIndex;
        chainHashes[0] = startStateHash;
        chainCursor = 1;
        var scheduleCursor = 0;

        var planIndex = 0;

        void ApplyDueCommands()
        {
            var firstDue = planIndex;
            var tick = world.TickIndex;

            while (planIndex < plan.Length && plan[planIndex].Tick <= tick)
            {
                planIndex++;
            }

            if (planIndex > firstDue)
            {
                world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue));
            }
        }

        // Warmphase ohne Messung (Praezedenz T-021).
        for (var tick = 0; tick < options.WarmupTicks; tick++)
        {
            ApplyDueCommands();
            world.Tick();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var pauseSumBefore = GC.GetTotalPauseDuration();
        var collectionCountBefore = GcCollectionCount();

        clock.Start();

        var windowStride = SoakPlan.WindowTickStride(options.WindowSeconds);
        var windowCount = SoakPlan.WindowCount(options.TotalTicks, options.WindowSeconds);
        var series = new SoakWindowSeries(windowCount, windowStride);
        var tickBuffer = new double[windowStride];
        var watchdog = new ProgressWatchdog(options.WatchdogWindowSeconds, windowCount + 2);

        watchdog.Reset(clock.ElapsedSeconds, world.TickIndex);

        using var rssSampler = RssSampler.TryCreate();
        long rssFirst = 0;
        long rssMin = long.MaxValue;
        long rssMax = long.MinValue;
        long rssEnd = 0;
        var rssMeasured = false;
        string? rssReason = rssSampler?.Reason;

        var allocatedSumBytes = 0L;
        var measuredTicks = 0L;
        var stallDetected = false;
        var tickRate = SimulationContract.TickRateHz;
        var tickRateSeconds = 1.0 / tickRate;

        // Strenge Per-Tick-Allokationspruefung (Methode des Simulations-
        // vertrags V1 Abschnitt 5): erste StrictAllocationVerificationTicks
        // Messsticks sowie je Stundenbeginn dieselbe Burstlaenge.
        var strictLength = Math.Max(0, options.StrictAllocationVerificationTicks);
        var strictRanges = new List<(long Start, long End)>();

        if (strictLength > 0)
        {
            strictRanges.Add((0, strictLength - 1));

            for (var hourStart = SimulationContract.TickRateHz * 3600L;
                 hourStart + strictLength <= options.TotalTicks;
                 hourStart += SimulationContract.TickRateHz * 3600L)
            {
                strictRanges.Add((hourStart, hourStart + strictLength - 1));
            }
        }

        var strictCursor = 0;
        var strictTickCount = 0L;
        var strictAllocatedSumBytes = 0L;

        for (var window = 0; window < windowCount && measuredTicks < options.TotalTicks && !stallDetected; window++)
        {
            var remaining = options.TotalTicks - measuredTicks;
            var windowTicks = (int)Math.Min(windowStride, remaining);

            // Allokationsklammern: nur die Tickarbeit liegt zwischen den
            // Zaehlerlesen; jede Telemetrie erfolgt danach und zaehlt nicht
            // in das Warm-tick-Budget. Diese groberen Fensterdeltas sind
            // Telemetrie; das Gate entscheidet ueber die strengen Per-Tick-
            // Bursts mit der Vertragsmethode.
            var allocationBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stallInWindow = false;
            var executedInWindow = 0;

            for (var index = 0; index < windowTicks; index++)
            {
                ApplyDueCommands();

                var globalIndex = measuredTicks + index;
                long strictBefore = 0;
                var strictActive = false;

                if (strictCursor < strictRanges.Count)
                {
                    var range = strictRanges[strictCursor];

                    if (globalIndex >= range.Start && globalIndex <= range.End)
                    {
                        strictActive = true;
                        strictBefore = GC.GetTotalAllocatedBytes(precise: true);
                    }
                }

                var startTimestamp = TickTiming.Timestamp();
                world.Tick();
                var endTimestamp = TickTiming.Timestamp();
                tickBuffer[index] = TickTiming.DeltaMs(startTimestamp, endTimestamp);

                // Kettenstichprobe bei Kreuzung eines geplanten absoluten
                // Simulationsticks (intervallunabhaengig vom Warm-up-Offset).
                if (scheduleCursor < schedule.Length && world.TickIndex == schedule[scheduleCursor])
                {
                    if (chainCursor >= chainTicks.Length)
                    {
                        throw new InvalidOperationException("Kettenstichprobenkapazitaet ueberschritten.");
                    }

                    chainTicks[chainCursor] = world.TickIndex;
                    chainHashes[chainCursor] = world.ComputeStateHash();
                    chainCursor++;
                    scheduleCursor++;
                }

                if (strictActive)
                {
                    strictAllocatedSumBytes += GC.GetTotalAllocatedBytes(precise: true) - strictBefore;
                    strictTickCount++;

                    if (globalIndex == strictRanges[strictCursor].End)
                    {
                        strictCursor++;
                    }
                }

                executedInWindow = index + 1;

                if (!options.Paced)
                {
                    continue;
                }

                clock.WaitUntil((globalIndex + 1) * tickRateSeconds);
                var nowForStallCheck = clock.ElapsedSeconds;

                watchdog.Observe(nowForStallCheck, world.TickIndex);

                if (watchdog.IsStalled(nowForStallCheck))
                {
                    stallInWindow = true;
                    break;
                }
            }

            var allocationAfter = GC.GetTotalAllocatedBytes(precise: true);
            allocatedSumBytes += allocationAfter - allocationBefore;

            // Buchhaltung zaehlt ausschliesslich tatsaechlich ausgefuehrte
            // Ticks; ein Stallabbruch darf den Horizont nicht kosmetisch
            // vollstaendig melden (fail-closed Ehrlichkeit des Teilreports).
            measuredTicks += executedInWindow;

            // Buchhaltung ausserhalb der Klammern (allokationsfrei).
            SampleRss(rssSampler, ref rssMeasured, ref rssFirst, ref rssMin, ref rssMax, ref rssEnd, ref rssReason);
            series.CloseWindow(window, rssMeasured ? rssEnd : 0, allocationAfter - allocationBefore, tickBuffer.AsSpan(0, executedInWindow));

            var nowSeconds = clock.ElapsedSeconds;
            watchdog.Observe(nowSeconds, world.TickIndex);

            stallDetected = stallInWindow || watchdog.IsStalled(nowSeconds);
        }

        // Autoritative Realzeitlaeufe halten die vereinbarte Wanduhrdauer
        // strikt ein, auch wenn der letzte Tick kurz vor dem Sollzeitpunkt
        // abgeschlossen wurde. Die Haltedauer haengt nur an der Monotonuhr,
        // niemals am Zustand.
        if (options.Paced && !stallDetected)
        {
            var requiredSeconds = options.TotalTicks * tickRateSeconds;

            while (clock.ElapsedSeconds < requiredSeconds)
            {
                clock.WaitUntil(requiredSeconds);
            }
        }

        var wallSeconds = clock.ElapsedSeconds;
        watchdog.Observe(wallSeconds, world.TickIndex);

        var pauseSumAfter = GC.GetTotalPauseDuration();

        // Endstichprobe stets am tatsaechlichen Endtick verankern; bei einem
        // vorzeitigen Abbruch traegt sie den abgebrochenen Tick.
        if (chainTicks[chainCursor - 1] != world.TickIndex)
        {
            chainTicks[chainCursor] = world.TickIndex;
            chainHashes[chainCursor] = world.ComputeStateHash();
            chainCursor++;
        }

        return new SoakExecutionResult
        {
            World = world,
            MeasuredTicksExecuted = measuredTicks,
            WallSeconds = wallSeconds,
            StallDetected = stallDetected,
            StartStateHash = startStateHash,
            EndStateHash = world.ComputeStateHash(),
            ChainSampleTicks = chainTicks,
            ChainSampleHashes = chainHashes,
            ChainSampleCount = chainCursor,
            Series = series,
            Watchdog = watchdog,
            GcPauseCount = GcCollectionCount() - collectionCountBefore,
            GcPauseSumMs = (pauseSumAfter - pauseSumBefore).TotalMilliseconds,
            AllocationsPerWarmTickBytes = allocatedSumBytes / (double)measuredTicks,
            StrictVerificationTickCount = strictTickCount,
            StrictVerificationBurstCount = strictRanges.Count,
            StrictAllocationsPerTickBytes = strictTickCount > 0
                ? strictAllocatedSumBytes / (double)strictTickCount
                : double.NaN,
            RssMeasured = rssMeasured,
            RssFirstKiB = rssFirst,
            RssMinKiB = rssMeasured ? rssMin : 0,
            RssMaxKiB = rssMeasured ? rssMax : 0,
            RssEndKiB = rssEnd,
            RssReason = rssReason,
            CommandCount = plan.Length,
            PlanHashHex = planHash.ToString("x16", CultureInfo.InvariantCulture),
        };
    }

    private static void SampleRss(
        RssSampler? sampler,
        ref bool measured,
        ref long first,
        ref long minimum,
        ref long maximum,
        ref long end,
        ref string? reason)
    {
        if (sampler is null)
        {
            return;
        }

        sampler.Sample();

        if (sampler.Measured && sampler.EndKiB is { } currentRss)
        {
            if (!measured)
            {
                first = currentRss;
                measured = true;
            }

            minimum = Math.Min(minimum, currentRss);
            maximum = Math.Max(maximum, currentRss);
            end = currentRss;
        }
        else if (sampler.Reason is { } samplerReason)
        {
            reason = samplerReason;
        }
    }

    private static long GcCollectionCount() =>
        GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
}

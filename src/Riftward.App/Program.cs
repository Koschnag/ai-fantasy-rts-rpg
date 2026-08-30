using Riftward.App;
using Riftward.App.Package;
using Riftward.App.Soak;
using Riftward.Platform;
using CommandLoopRunner = Riftward.App.Command.CommandLoopRunner;

if (OperatingSystem.IsLinux()
    && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
{
    Console.Error.WriteLine("plattformsmoke/effizienzbaseline/bench/soak/savecheck/kommandoschleife/package: nur linux-x64 im T-010-/T-020-/T-022-/T-031-/T-032-/T-038-Scope.");
    return ExitCodes.Map(PlatformErrorCode.UnsupportedPlatform);
}

var arguments = new CommandLineArgs(args);

try
{
    var mode = arguments.Next();
    return mode switch
    {
        "plattformsmoke" => SmokeRunner.Run(arguments),
        "effizienzbaseline" => EfficiencyRunner.Run(arguments),
        "bench" => BenchRunner.Run(arguments),
        "soak" => SoakRunner.Run(arguments),
        "savecheck" => SavecheckRunner.Run(arguments),
        "kommandoschleife" => CommandLoopRunner.Run(arguments),
        "package" => PackageRunner.Run(arguments),
        _ => PrintUsage($"Unbekannter Modus '{mode ?? "<fehlt>"}'."),
    };
}
catch (PlatformException exception)
{
    Console.Error.WriteLine(exception.Error.ToString());
    return ExitCodes.Map(exception.Error.Code);
}
catch (FileNotFoundException exception)
{
    Console.Error.WriteLine($"Erforderliche Datei fehlt: {exception.FileName ?? exception.Message}");
    return ExitCodes.Map(PlatformErrorCode.ArtifactMissing);
}

static int PrintUsage(string message)
{
    Console.Error.WriteLine(
        message
        + Environment.NewLine
        + "Verwendung:"
        + Environment.NewLine
        + "  Riftward.App plattformsmoke [--report PFAD] [--time-limit-ms N] [--width W] [--height H]"
        + Environment.NewLine
        + "      [--artifacts-dir VERZ] [--manifest DATEI] [--lock DATEI]"
        + Environment.NewLine
        + "  Riftward.App effizienzbaseline --report PFAD [--idle-window-seconds N] [--warmup-frames N]"
        + Environment.NewLine
        + "      [--sample-frames N] [--artifacts-dir VERZ] [--manifest DATEI] [--lock DATEI]"
        + Environment.NewLine
    + "  Riftward.App bench --scenario bench-empty --report PFAD [--seed N] [--warmup-frames N]"
    + Environment.NewLine
    + "      [--sample-frames N] [--bind-profile PROFIL=KLASSE] [--artifacts-dir VERZ] [--manifest DATEI]"
    + Environment.NewLine
    + "      [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App bench --scenario bench-sim --report PFAD [--seed N] [--warmup-ticks N]"
    + Environment.NewLine
    + "      [--sample-ticks N] [--bind-profile PROFIL=KLASSE] [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App bench --scenario bench-representative --report PFAD [--seed N]"
    + Environment.NewLine
    + "      [--warmup-frames N] [--sample-frames N] [--capture-frame PFAD]"
    + Environment.NewLine
    + "      [--bind-profile PROFIL=KLASSE] [--artifacts-dir VERZ] [--manifest DATEI] [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App soak --scenario soak-replay --report PFAD [--seed N]"
    + Environment.NewLine
    + "      [--diagnostic-accelerated [--horizon-ticks N]] [--reference-out PFAD]"
    + Environment.NewLine
    + "      [--bind-profile PROFIL=KLASSE] [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App savecheck --report PFAD [--work VERZ] [--seed N] [--plan-ticks N]"
    + Environment.NewLine
    + "      [--safe-tick N] [--sample-interval-ticks N] [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App kommandoschleife --scenario kommando-graybox --input-script PFAD"
    + Environment.NewLine
    + "      --seed N --report PFAD [--interactive [--auto-exit-at-horizon]] [--capture-frame PFAD]"
    + Environment.NewLine
    + "      [--warmup-ticks N] [--horizon-ticks N] [--lock DATEI]"
    + Environment.NewLine
    + "  Riftward.App package [--output-dir VERZ] [--work VERZ] [--rid linux-x64]"
    + Environment.NewLine
    + "  Riftward.App package --verify ARCHIV.tar.gz [--work VERZ]");
    return ExitCodes.Usage;
}

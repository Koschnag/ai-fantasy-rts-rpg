using Riftward.App;
using Riftward.Platform;

if (OperatingSystem.IsLinux()
    && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
{
    Console.Error.WriteLine("plattformsmoke/effizienzbaseline/bench: nur linux-x64 im T-010-/T-020-Scope.");
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
        + "      [--lock DATEI]");
    return ExitCodes.Usage;
}

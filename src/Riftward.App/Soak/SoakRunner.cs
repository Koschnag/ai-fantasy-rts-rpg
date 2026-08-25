using Riftward.App.Bench;
using Riftward.Platform;

namespace Riftward.App.Soak;

/// <summary>
/// Oeffentliche Eingangsweiche des soak-Befehls (T-022). Sie folgt dem
/// Szenarioregistry-Muster von bench: unbekannte oder noch nicht
/// implementierte Szenarien brechen mit definiertem Exitcode ab, ohne einen
/// Report vorzutaeuschen.
/// </summary>
internal static class SoakRunner
{
    public static int Run(CommandLineArgs arguments)
    {
        var scenarioId = arguments.Option("--scenario");

        switch (SoakScenarios.Classify(scenarioId))
        {
            case SoakScenarios.Support.Implemented:
                if (string.Equals(scenarioId, SoakScenarios.Calibration, StringComparison.Ordinal))
                {
                    // Abschnitt-0-Spike: rein diagnostische Kalibrierung.
                    return SoakCalibrationRunner.Run(arguments);
                }

                if (string.Equals(scenarioId, SoakScenarios.Replay, StringComparison.Ordinal))
                {
                    return SoakReplayRunner.Run(arguments);
                }

                break;

            case SoakScenarios.Support.RegisteredNotImplemented:
                Console.Error.WriteLine(
                    $"soak: Szenario '{scenarioId}' ist bekannt, aber in diesem Auftrag nicht implementiert (kein Report).");
                return ExitCodes.Map(PlatformErrorCode.SoakScenarioUnavailable);

            default:
                Console.Error.WriteLine(
                    $"soak: unbekanntes Szenario '{scenarioId ?? "<fehlt>"}'. Bekannte Szenarien: {string.Join(", ", SoakScenarios.Known)}.");
                return ExitCodes.Map(PlatformErrorCode.SoakScenarioUnavailable);
        }

        Console.Error.WriteLine("soak: Szenario konnte nicht ausgefuehrt werden (kein Report).");
        return ExitCodes.Map(PlatformErrorCode.SoakScenarioUnavailable);
    }
}

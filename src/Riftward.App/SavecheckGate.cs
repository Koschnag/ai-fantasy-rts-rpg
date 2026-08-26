using Riftward.Save;

namespace Riftward.App;

/// <summary>Eine Kettenstichprobe der Fortsetzung (Tick und Zustands-Hash).</summary>
internal sealed record SavecheckChainSample(long Tick, ulong Hash);

/// <summary>
/// Eine Prüfklassenentscheidung des savecheck-Laufs (AC-T031-06-Muster):
/// Klasse, Ergebnis und optional verständliches Detail ohne interne Pfade.
/// </summary>
internal sealed record SavecheckCheck(string Class, bool Pass, string? Detail = null)
{
    public object ToJson() => new
    {
        @class = Class,
        pass = Pass,
        detail = Detail,
    };
}

/// <summary>Fail-closed-Gesamtentscheidung über alle Prüfklassen.</summary>
internal sealed record SavecheckVerdict(bool Pass, IReadOnlyList<string> Violations);

/// <summary>
/// Gate-Evaluator des savecheck-Laufs: entscheidet ausschließlich gegen die
/// Savevertragsgarantien (Prüfklassenmatrix, Größen-Sanity-Schwellwert aus
/// Kalibrierläufen, Fortsetzungshorizont). Diagnostische Dauerfelder gehen
/// zu keinem Zeitpunkt ein; eine leere Klassenliste ist selbst Verletzung.
/// </summary>
internal static class SavecheckGate
{
    public static SavecheckVerdict Evaluate(IReadOnlyList<SavecheckCheck> checks)
    {
        var violations = new List<string>();

        if (checks.Count == 0)
        {
            violations.Add("keine Prüfklassen ausgewertet (fail-closed)");
        }

        foreach (var check in checks)
        {
            if (!check.Pass)
            {
                violations.Add($"{check.Class}: {check.Detail ?? "fehlgeschlagen"}");
            }
        }

        return new SavecheckVerdict(Pass: violations.Count == 0, violations);
    }

    /// <summary>
    /// Größen-Sanity-Schwellwert gemäß Savevertrag Abschnitt 6: mindestens
    /// zwei übereinstimmende Kalibrierläufe, Faktor im Band 2 bis 16,
    /// Grenzwert als Vielfaches der Messung, zusätzlich absolutes Vorablimit.
    /// </summary>
    public static (bool Pass, string? Detail, long LimitBytes) EvaluateSizeSanity(
        long firstCalibrationBytes,
        long secondCalibrationBytes,
        int factor,
        long absoluteMaxSaveBytes)
    {
        if (firstCalibrationBytes <= 0 || secondCalibrationBytes <= 0)
        {
            return (false, "kalibrierläufe-nicht-positiv", 0L);
        }

        if (firstCalibrationBytes != secondCalibrationBytes)
        {
            return (false, "kalibrierläufe-weichen-ab", 0L);
        }

        if (factor < SaveContract.SizeSanityFactorMinimum || factor > SaveContract.SizeSanityFactorMaximum)
        {
            return (false, "faktor-verlaesst-auftragsband", 0L);
        }

        try
        {
            var limit = checked(firstCalibrationBytes * factor);

            if (limit > absoluteMaxSaveBytes)
            {
                return (false, "abgeleiteter-grenzwert-oberhalb-des-absoluten-limits", limit);
            }

            return (true, null, limit);
        }
        catch (OverflowException)
        {
            return (false, "grenzwert-ueberlauf", 0L);
        }
    }

    /// <summary>Der Fortsetzungshorizont muss mindestens den Vertragsanteil am Planhorizont erreichen.</summary>
    public static bool ContinuationMeetsContractMinimum(long planTicks, long safeTick, out long continuationTicks)
    {
        continuationTicks = planTicks - safeTick;
        return planTicks > 0
            && safeTick >= 0
            && safeTick < planTicks
            && (continuationTicks * SaveContract.MinContinuationFractionDenominator)
                >= (planTicks * SaveContract.MinContinuationFractionNumerator);
    }
}

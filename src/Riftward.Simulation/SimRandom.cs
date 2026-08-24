namespace Riftward.Simulation;

/// <summary>
/// Deterministischer Ganzzahl-Zufall fuer Seedableitung und Streuung
/// (Xorshift64* mit SplitMix64-Ableitung, Vertrag Abschnitt 0c). Kein Uhr-,
/// Umwelt- oder Threadbeitrag; identische Seeds erzeugen identische Folgen.
/// </summary>
public struct SimRandom
{
    private ulong _state;

    public SimRandom(ulong seed)
    {
        // SplitMix64-Streuung: auch nahe beieinanderliegende Seeds liefern
        // weit entfernte Startzustände.
        ulong mixed = seed + 0x9E3779B97F4A7C15UL;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
        _state = mixed ^ (mixed >> 31);

        if (_state == 0UL)
        {
            _state = 0x1UL;
        }
    }

    public ulong NextULong()
    {
        var state = _state;
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        _state = state;
        return state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Gleichverteilt in [0, bound) ohne Modulo-Bias via Restablehnung.</summary>
    public int NextInt(int bound)
    {
        var range = (ulong)bound;
        var threshold = (0UL - range) % range;

        ulong value;

        do
        {
            value = NextULong();
        }
        while (value < threshold);

        return (int)(value % range);
    }
}

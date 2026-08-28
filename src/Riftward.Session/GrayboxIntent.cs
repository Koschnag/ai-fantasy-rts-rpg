namespace Riftward.Session;

/// <summary>
/// Intentarten der Graybox-Kommandoschleife (Kommandovertrag Abschnitt 2,
/// erweitert um die T-033-Obermenge gemäß Modevertrag Abschnitt 6). Die
/// numerische Reihenfolge ist vertraglich: Innerhalb eines Ticks werden
/// Intents in aufsteigender Kindreihenfolge ausgefuehrt, bei Gleichstand nach
/// den Parametern als numerisches Tupel. <see cref="GroupMoveToZone"/> und
/// <see cref="SteerGroupToZone"/> erzeugen Kernbefehle
/// (SimCommandKind.GroupMoveToZone); <see cref="SwitchMode"/> wird kanonisch
/// zuletzt ausgewertet, ist nie Kontextbildner seines eigenen Ticks und
/// erzeugt niemals einen Kernbefehl; alle uebrigen Arten sind rein
/// darstellseitig.
/// </summary>
public enum GrayboxIntentKind : byte
{
    /// <summary>Hebt die Auswahl auf.</summary>
    Clear = 0,

    /// <summary>Punktwahl: Gruppe des naechstgelegenen Agenten im Radius; ein Klick ins Leere hebt die Auswahl auf.</summary>
    PointSelect = 1,

    /// <summary>Rahmenwahl: Vereinigung der Gruppen aller Agenten im Rechteck.</summary>
    BoxSelect = 2,

    /// <summary>Gruppenbewegung: je ausgewaehlter Gruppe ein Kernbefehl GroupMoveToZone.</summary>
    GroupMoveToZone = 3,

    /// <summary>Persoenliche Lenkung: genau ein Kernbefehl GroupMoveToZone auf der Vertragsgruppe des Helden (T-033).</summary>
    SteerGroupToZone = 4,

    /// <summary>Moduswechsel an der Tickgrenze: kein Kernbefehl, kein Simulationszustand (T-033).</summary>
    SwitchMode = 5,
}

/// <summary>
/// Ein tickbezogener Sitzungsintent mit Ganzzahlparametern in Millimetern
/// (Skripteinheit). Struktur ist unveränderlich und kanonisch vergleichbar;
/// die Eingabereihenfolge bestimmt niemals das Ergebnis (Vertrag Abschnitt 2).
/// </summary>
public readonly struct GrayboxIntent : IComparable<GrayboxIntent>, IEquatable<GrayboxIntent>
{
    public GrayboxIntent(int tick, GrayboxIntentKind kind, long a = 0, long b = 0, long c = 0, long d = 0)
    {
        Tick = tick;
        Kind = kind;
        A = a;
        B = b;
        C = c;
        D = d;
    }

    /// <summary>Absendettick S des Intents (Vertrag Abschnitt 6).</summary>
    public int Tick { get; }

    public GrayboxIntentKind Kind { get; }

    /// <summary>Erster Parameter: point x / box x0 / move zoneIndex / steer zoneIndex.</summary>
    public long A { get; }

    /// <summary>Zweiter Parameter: point y / box y0.</summary>
    public long B { get; }

    /// <summary>Dritter Parameter: box x1.</summary>
    public long C { get; }

    /// <summary>Vierter Parameter: box y1.</summary>
    public long D { get; }

    public int CompareTo(GrayboxIntent other)
    {
        if (Tick != other.Tick)
        {
            return Tick.CompareTo(other.Tick);
        }

        if (Kind != other.Kind)
        {
            return ((byte)Kind).CompareTo((byte)other.Kind);
        }

        if (A != other.A)
        {
            return A.CompareTo(other.A);
        }

        if (B != other.B)
        {
            return B.CompareTo(other.B);
        }

        if (C != other.C)
        {
            return C.CompareTo(other.C);
        }

        return D.CompareTo(other.D);
    }

    public bool Equals(GrayboxIntent other) =>
        Tick == other.Tick
        && Kind == other.Kind
        && A == other.A
        && B == other.B
        && C == other.C
        && D == other.D;

    public override bool Equals(object? obj) => obj is GrayboxIntent other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Tick, Kind, A, B, C, D);

    public static bool operator ==(GrayboxIntent left, GrayboxIntent right) => left.Equals(right);

    public static bool operator !=(GrayboxIntent left, GrayboxIntent right) => !left.Equals(right);

    public static bool operator <(GrayboxIntent left, GrayboxIntent right) => left.CompareTo(right) < 0;

    public static bool operator <=(GrayboxIntent left, GrayboxIntent right) => left.CompareTo(right) <= 0;

    public static bool operator >(GrayboxIntent left, GrayboxIntent right) => left.CompareTo(right) > 0;

    public static bool operator >=(GrayboxIntent left, GrayboxIntent right) => left.CompareTo(right) >= 0;

    /// <summary>Konvertiert Skriptmillimeter deterministisch nach Q16.16 (kaufmaennisch half-up).</summary>
    public static long MillimetersToQ16(long millimeters)
    {
        // 65536/1000 laesst sich nicht exakt abbilden; die Vertragskonvention
        // rundet half-up, sodass dieselbe Eingabe immer denselben Q16-Wert liefert.
        var product = millimeters * 65536L;
        var quotient = Math.DivRem(product, 1000L, out var remainder);

        if (remainder * 2 >= 1000L)
        {
            quotient++;
        }

        return quotient;
    }
}

/// <summary>
/// Kanonische Festbreitenkodierung eines Intents fuer den Planhash
/// (Kommandovertrag Abschnitt 5): Little-Endian, 21 Bytes fest
/// (tick int32, kind byte, vier int32-Parameterslots; ungenutzte Slots null).
/// Hash ist FNV-1a-64 ueber die Kodierungsfolge — dasselbe Schema wie der
/// Befehlsplanhash des Simulationskerns. Festbreite 4 + 1 + (4 * 4) Bytes.
/// </summary>
public static class IntentCodec
{
    public const int EncodedSize = 21;

    private const ulong FnvPrime = 0x100000001B3UL;
    private const ulong FnvOffset = 0xCBF29CE484222325UL;

    public static void Encode(GrayboxIntent intent, Span<byte> target)
    {
        if (target.Length < EncodedSize)
        {
            throw new ArgumentException("Zielpuffer benoetigt 21 Bytes.", nameof(target));
        }

        target.Clear();
        WriteInt32(target, 0, intent.Tick);
        target[4] = (byte)intent.Kind;

        switch (intent.Kind)
        {
            case GrayboxIntentKind.PointSelect:
                WriteInt32(target, 5, checked((int)intent.A));
                WriteInt32(target, 9, checked((int)intent.B));
                break;

            case GrayboxIntentKind.BoxSelect:
                WriteInt32(target, 5, checked((int)intent.A));
                WriteInt32(target, 9, checked((int)intent.B));
                WriteInt32(target, 13, checked((int)intent.C));
                WriteInt32(target, 17, checked((int)intent.D));
                break;

            case GrayboxIntentKind.GroupMoveToZone:
            case GrayboxIntentKind.SteerGroupToZone:
                WriteInt32(target, 5, checked((int)intent.A));
                break;

            case GrayboxIntentKind.Clear:
            case GrayboxIntentKind.SwitchMode:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(intent), "Unbekannte Intentart.");
        }
    }

    /// <summary>Feldfreundliche Variante fuer Tests und Diagnosewerkzeuge.</summary>
    public static byte[] EncodeToArray(GrayboxIntent intent)
    {
        var buffer = new byte[EncodedSize];
        Encode(intent, buffer);
        return buffer;
    }

    /// <summary>Feldfreundliche Hashvariante fuer Tests und Diagnosewerkzeuge.</summary>
    public static ulong HashOf(System.Collections.Generic.IReadOnlyList<GrayboxIntent> intents)
    {
        Span<byte> buffer = stackalloc byte[EncodedSize];
        var hash = FnvOffset;

        for (var index = 0; index < intents.Count; index++)
        {
            Encode(intents[index], buffer);

            foreach (var value in buffer)
            {
                hash = (hash ^ value) * FnvPrime;
            }
        }

        return hash;
    }

    public static ulong Hash(ReadOnlySpan<GrayboxIntent> intents)
    {
        Span<byte> buffer = stackalloc byte[EncodedSize];
        var hash = FnvOffset;

        foreach (var intent in intents)
        {
            Encode(intent, buffer);

            foreach (var value in buffer)
            {
                hash = (hash ^ value) * FnvPrime;
            }
        }

        return hash;
    }

    private static void WriteInt32(Span<byte> target, int offset, int value)
    {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)(value >> 16);
        target[offset + 3] = (byte)(value >> 24);
    }
}

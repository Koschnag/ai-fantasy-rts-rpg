namespace Riftward.App;

/// <summary>Kleine Positions-/Optionsleser fuer die Befehlszeile.</summary>
public sealed class CommandLineArgs
{
    private readonly string[] _arguments;
    private int _position;

    public CommandLineArgs(string[] arguments) => _arguments = arguments;

    public string? Next() => _position < _arguments.Length ? _arguments[_position++] : null;

    /// <summary>Prueft, ob ein Schalter ohne Wert gesetzt wurde.</summary>
    public bool HasFlag(string name)
    {
        foreach (var argument in _arguments)
        {
            if (string.Equals(argument, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public string? Option(string name)
    {
        for (var index = 0; index < _arguments.Length - 1; index++)
        {
            if (string.Equals(_arguments[index], name, StringComparison.Ordinal))
            {
                return _arguments[index + 1];
            }
        }

        return null;
    }

    /// <summary>Alle Werte eines wiederholbaren Optionsnamens in Reihenfolge.</summary>
    public IReadOnlyList<string> AllOptions(string name)
    {
        var values = new List<string>();

        for (var index = 0; index < _arguments.Length - 1; index++)
        {
            if (string.Equals(_arguments[index], name, StringComparison.Ordinal))
            {
                values.Add(_arguments[index + 1]);
            }
        }

        return values;
    }

    public long NumberOption(string name, long fallback) =>
        long.TryParse(Option(name), out var value) && value >= 0 ? value : fallback;
}

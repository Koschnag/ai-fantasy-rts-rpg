namespace Riftward.App;

/// <summary>Kleine Positions-/Optionsleser fuer die Befehlszeile.</summary>
public sealed class CommandLineArgs
{
    private readonly string[] _arguments;
    private int _position;

    public CommandLineArgs(string[] arguments) => _arguments = arguments;

    public string? Next() => _position < _arguments.Length ? _arguments[_position++] : null;

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

    public long NumberOption(string name, long fallback) =>
        long.TryParse(Option(name), out var value) && value >= 0 ? value : fallback;
}

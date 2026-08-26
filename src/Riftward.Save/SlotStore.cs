using System.Diagnostics.CodeAnalysis;

namespace Riftward.Save;

/// <summary>
/// Ergebnis eines atomaren Slot-Schreibvorgangs. Ein Fehlschlag lässt den
/// letzten gültigen Stand unangetastet und benennt die Phase kontrolliert.
/// </summary>
public sealed record SlotWriteResult
{
    public required bool Success { get; init; }

    /// <summary>Verständliche Fehlermeldung ohne interne Pfade über den Diagnosefall hinaus.</summary>
    public string? Error { get; init; }

    /// <summary>Phase des Protokolls, in der der Fehler auftrat (Diagnose).</summary>
    public string? Phase { get; init; }

    /// <summary>Diagnostischer Hinweis ohne Gatebezug; ein grünes Ergebnis trägt niemals eine Warnung, damit kein teilweise ausgeführter Schritt als Erfolg maskiert wird.</summary>
    public string? Warning { get; init; }

    public required string SlotName { get; init; }

    public static SlotWriteResult Ok(string slotName, string? warning = null) =>
        new() { Success = true, SlotName = slotName, Warning = warning };

    public static SlotWriteResult Failed(string slotName, string phase, string error) =>
        new() { Success = false, Phase = phase, Error = error, SlotName = slotName };
}

/// <summary>Ergebnis eines Slot-Lesevorgangs.</summary>
public sealed record SlotReadResult
{
    public required bool Success { get; init; }

    public SaveRejection? Rejection { get; init; }

    public byte[]? Bytes { get; init; }

    public static SlotReadResult Ok(byte[] bytes) => new() { Success = true, Bytes = bytes };

    public static SlotReadResult Rejected(SaveRejection rejection) =>
        new() { Success = false, Rejection = rejection };
}

/// <summary>
/// Dateisystemnahtstelle des Atomarprotokolls (Savevertrag Abschnitt 5).
/// Die Produktimplementierung arbeitet über die BCL; Tests injizieren
/// Ausfälle je Schreibphase (temporärer Pfad, Validierung, atomare
/// Ersetzung), ohne echte Umgebungsbedingungen zu benötigen.
/// </summary>
internal interface ISaveFilePort
{
    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    string ResolveFullPath(string path);

    bool EntryExists(string path);

    bool IsReparsePoint(string path);

    void WriteAllBytesSynced(string path, byte[] bytes);

    byte[] ReadAllBytes(string path);

    void AtomicReplace(string sourcePath, string targetPath);

    void DeleteQuiet(string path);
}

internal sealed class SystemIoSaveFilePort : ISaveFilePort
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string ResolveFullPath(string path) => Path.GetFullPath(path);

    public bool EntryExists(string path) => File.Exists(path) || Directory.Exists(path);

    public bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    public void WriteAllBytesSynced(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void AtomicReplace(string sourcePath, string targetPath) =>
        File.Move(sourcePath, targetPath, overwrite: true);

    public void DeleteQuiet(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort: eine nicht löschbare Tempdatei darf den letzten
            // gültigen Stand niemals berühren; sie bleibt liegen und wird
            // beim nächsten Lauf unter neuem Namen umgangen.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Atomares Slotprotokoll (Savevertrag Abschnitt 5): temporärer Schreibpfad
/// im Zielverzeichnis, Sync der Dateiinhalte, vollständige Validierung der
/// geschriebenen Datei vor Ersetzung, atomare Ersetzung. Jeder Abbruch
/// entfernt die Tempdatei und lässt den letzten gültigen Stand unangetastet.
/// Schreibvorgänge erfolgen ausschließlich unterhalb des Erlaubnisverzeichnisses;
/// Pfadaustritte und symbolische Komponenten werden kontrolliert abgewiesen.
/// </summary>
public sealed class SlotStore
{
    private readonly ISaveFilePort _port;
    private readonly string _allowedRoot;

    public SlotStore(string allowedRoot)
        : this(allowedRoot, new SystemIoSaveFilePort())
    {
    }

    internal SlotStore(string allowedRoot, ISaveFilePort port)
    {
        _port = port;
        _allowedRoot = port.ResolveFullPath(allowedRoot);

        if (!port.DirectoryExists(_allowedRoot))
        {
            port.CreateDirectory(_allowedRoot);
        }
    }

    public string AllowedRoot => _allowedRoot;

    /// <summary>Schreibt einen Slot atomar; Validierung erfolgt vor der Ersetzung.</summary>
    public SlotWriteResult WriteSlotAtomic(string slotName, byte[] documentBytes)
    {
        ArgumentNullException.ThrowIfNull(documentBytes);

        if (!TryResolveTarget(slotName, out var target, out var resolutionError))
        {
            return SlotWriteResult.Failed(slotName, "resolve", resolutionError!);
        }

        var temporaryPath = target + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            try
            {
                _port.WriteAllBytesSynced(temporaryPath, documentBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return SlotWriteResult.Failed(
                    slotName,
                    "temp-write",
                    $"Temporäre Savedatei konnte nicht geschrieben werden: {exception.Message}");
            }

            byte[] writtenBytes;

            try
            {
                writtenBytes = _port.ReadAllBytes(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _port.DeleteQuiet(temporaryPath);
                return SlotWriteResult.Failed(
                    slotName,
                    "validation-read",
                    $"Geschriebene Savedatei war zur Validierung nicht lesbar: {exception.Message}");
            }

            if (!writtenBytes.AsSpan().SequenceEqual(documentBytes))
            {
                _port.DeleteQuiet(temporaryPath);
                return SlotWriteResult.Failed(
                    slotName,
                    "validation",
                    "Geschriebene Savedatei weicht von den erzeugten Bytes ab.");
            }

            var (rejection, _) = SaveDocumentValidator.Validate(writtenBytes);

            if (rejection is not null)
            {
                _port.DeleteQuiet(temporaryPath);
                return SlotWriteResult.Failed(
                    slotName,
                    "validation",
                    $"Geschriebene Savedatei verletzt den Savevertrag ({rejection.Class}).");
            }

            try
            {
                _port.AtomicReplace(temporaryPath, target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _port.DeleteQuiet(temporaryPath);
                return SlotWriteResult.Failed(
                    slotName,
                    "atomic-replace",
                    $"Atomare Ersetzung des Slots schlug fehl: {exception.Message}");
            }

            // Bewusst kein Sync des Verzeichniseintrags: Die BCL besitzt kein
            // Primitive dafür (FileStream darf Verzeichnisse nicht öffnen) und
            // der akzeptierte Architekturvertrag hält Native-Imports in der
            // Plattformschicht. Das Restrisiko ist im Savevertrag Abschnitt 5
            // mit Rückrollweg dokumentiert; ein grünes Ergebnis behauptet
            // daher genau die ausgeführten Schritte und nichts zusätzlich.

            return SlotWriteResult.Ok(slotName);
        }
        finally
        {
            _port.DeleteQuiet(temporaryPath);
        }
    }

    /// <summary>Liest einen Slot vollständig; Größenprüfung erfolgt vor der Zuweisung.</summary>
    public SlotReadResult ReadSlot(string slotName)
    {
        if (!TryResolveTarget(slotName, out var target, out var resolutionError))
        {
            return SlotReadResult.Rejected(new SaveRejection(SaveRejectionClass.ReferenceInvalid, resolutionError!));
        }

        try
        {
            var length = _port.EntryExists(target)
                ? new FileInfo(target).Length
                : throw new FileNotFoundException();

            if (length > SaveContract.AbsoluteMaxSaveBytes)
            {
                return SlotReadResult.Rejected(
                    new SaveRejection(SaveRejectionClass.SizeLimitExceeded, "Slot überschreitet das absolute Größenlimit."));
            }

            return SlotReadResult.Ok(_port.ReadAllBytes(target));
        }
        catch (FileNotFoundException)
        {
            return SlotReadResult.Rejected(
                new SaveRejection(SaveRejectionClass.TruncatedFile, "Slot existiert nicht."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SlotReadResult.Rejected(
                new SaveRejection(SaveRejectionClass.TruncatedFile, $"Slot ist nicht lesbar: {exception.Message}"));
        }
    }

    private bool TryResolveTarget(string slotName, [NotNullWhen(true)] out string? target, [NotNullWhen(false)] out string? error)
    {
        target = null;
        error = null;

        if (string.IsNullOrWhiteSpace(slotName)
            || slotName.Contains('/')
            || slotName.Contains('\\')
            || slotName.Contains("..", StringComparison.Ordinal)
            || slotName.StartsWith('.'))
        {
            error = "Slotname enthält vertragswidrige Pfadbestandteile.";
            return false;
        }

        string resolved;

        try
        {
            resolved = _port.ResolveFullPath(Path.Combine(_allowedRoot, slotName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Slotpfad konnte nicht aufgelöst werden: {exception.Message}";
            return false;
        }

        if (!resolved.StartsWith(_allowedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            error = "Slotpfad verlässt das erlaubte Verzeichnis.";
            return false;
        }

        if (RejectsSymbolicComponent(resolved, out var componentError))
        {
            error = componentError;
            return false;
        }

        target = resolved;
        return true;
    }

    private bool RejectsSymbolicComponent(string targetPath, [NotNullWhen(true)] out string? error)
    {
        error = null;
        var cursor = targetPath;

        while (cursor.Length > _allowedRoot.Length)
        {
            if (_port.EntryExists(cursor) && _port.IsReparsePoint(cursor))
            {
                error = "Slotpfad enthält eine symbolische Komponente.";
                return true;
            }

            var parent = Path.GetDirectoryName(cursor);

            if (parent is null || parent.Length >= cursor.Length || parent.Length < _allowedRoot.Length)
            {
                break;
            }

            cursor = parent;
        }

        return false;
    }
}

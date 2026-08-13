namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

exception AssetJobJournalConflict of string

[<RequireQualifiedAccess>]
type AssetJobState =
    | Created
    | Generated
    | Inspected
    | ProvenancePrepared
    | QuarantinePublished
    | MetadataPublished
    | Verified
    | Committed
    | RolledBack

[<RequireQualifiedAccess>]
type AssetJobOwnedPathKind =
    | OwnedFile
    | OwnedDirectory

type AssetJobOwnedPath =
    { Path: string
      Kind: AssetJobOwnedPathKind
      Sha256: string }

type AssetJobJournalEntry =
    { SchemaVersion: int
      Sequence: int
      JobId: string
      State: AssetJobState
      PreviousEntrySha256: string option
      AtUtc: string
      OwnedPaths: AssetJobOwnedPath list
      EntrySha256: string }

type AssetJobLock =
    private
        { WorkspaceRoot: string
          JobId: string
          JobRoot: string
          LockHandle: FileStream }

[<RequireQualifiedAccess>]
type AssetJobRecoveryOutcome =
    | AlreadyCommitted of AssetJobJournalEntry
    | AlreadyRolledBack of AssetJobJournalEntry
    | RolledBack of AssetJobJournalEntry

[<RequireQualifiedAccess>]
module AssetJobJournal =
    [<Literal>]
    let private MaxJournalBytes = 1_048_576L

    [<Literal>]
    let private MaxJournalEntries = 64

    [<Literal>]
    let private MaxOwnedPaths = 64

    [<Literal>]
    let private MaxRelativePathBytes = 240

    [<Literal>]
    let private MaxSegmentBytes = 80

    [<Literal>]
    let private MaxOwnedFileBytes = 16_777_216L

    [<Literal>]
    let private MaxOwnedDirectoryBytes = 25_165_824L

    [<Literal>]
    let private MaxOwnedDirectoryFiles = 64

    [<Literal>]
    let private MaxOwnedDirectoryEntries = 128

    [<Literal>]
    let private MaxOwnedDirectoryDepth = 16

    let private strictUtf8 = UTF8Encoding(false, true)

    let private jobIdPattern =
        Regex("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.CultureInvariant)

    let private sha256Pattern = Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)

    let private conflict message = raise (AssetJobJournalConflict message)

    let private utf8ByteCount description (value: string) =
        try
            strictUtf8.GetByteCount(value)
        with
        | :? EncoderFallbackException
        | :? ArgumentException -> conflict $"{description} ist kein gueltiger Unicode-Text."

    let private stateText state =
        match state with
        | AssetJobState.Created -> "CREATED"
        | AssetJobState.Generated -> "GENERATED"
        | AssetJobState.Inspected -> "INSPECTED"
        | AssetJobState.ProvenancePrepared -> "PROVENANCE_PREPARED"
        | AssetJobState.QuarantinePublished -> "QUARANTINE_PUBLISHED"
        | AssetJobState.MetadataPublished -> "METADATA_PUBLISHED"
        | AssetJobState.Verified -> "VERIFIED"
        | AssetJobState.Committed -> "COMMITTED"
        | AssetJobState.RolledBack -> "ROLLED_BACK"

    let private parseState value =
        match value with
        | "CREATED" -> AssetJobState.Created
        | "GENERATED" -> AssetJobState.Generated
        | "INSPECTED" -> AssetJobState.Inspected
        | "PROVENANCE_PREPARED" -> AssetJobState.ProvenancePrepared
        | "QUARANTINE_PUBLISHED" -> AssetJobState.QuarantinePublished
        | "METADATA_PUBLISHED" -> AssetJobState.MetadataPublished
        | "VERIFIED" -> AssetJobState.Verified
        | "COMMITTED" -> AssetJobState.Committed
        | "ROLLED_BACK" -> AssetJobState.RolledBack
        | _ -> conflict "Das Jobjournal enthaelt einen unbekannten Zustand."

    let private kindText kind =
        match kind with
        | AssetJobOwnedPathKind.OwnedFile -> "file"
        | AssetJobOwnedPathKind.OwnedDirectory -> "directory"

    let private parseKind value =
        match value with
        | "file" -> AssetJobOwnedPathKind.OwnedFile
        | "directory" -> AssetJobOwnedPathKind.OwnedDirectory
        | _ -> conflict "Das Jobjournal enthaelt einen unbekannten Pfadtyp."

    let private validateJobId (jobId: string) =
        if isNull jobId || jobId.Length <> 26 || not (jobIdPattern.IsMatch(jobId)) then
            conflict "Die Job-ID ist keine kanonische ULID."

    let private validateSha256 description (value: string) =
        if isNull value || value.Length <> 64 || not (sha256Pattern.IsMatch(value)) then
            conflict $"{description} ist kein kanonischer SHA-256."

    let private pathComparison =
        if OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() then
            StringComparison.OrdinalIgnoreCase
        else
            StringComparison.Ordinal

    let private pathComparer =
        if OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() then
            StringComparer.OrdinalIgnoreCase
        else
            StringComparer.Ordinal

    let private trimDirectorySeparators (path: string) =
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

    let private isInsideRoot root candidate =
        let normalizedRoot = trimDirectorySeparators root
        let normalizedCandidate = Path.GetFullPath(candidate)
        let prefix = normalizedRoot + string Path.DirectorySeparatorChar

        normalizedCandidate.Equals(normalizedRoot, pathComparison)
        || normalizedCandidate.StartsWith(prefix, pathComparison)

    let private attributesIndicateLink (path: string) =
        try
            if File.Exists(path) || Directory.Exists(path) then
                File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)
            else
                false
        with
        | :? IOException
        | :? UnauthorizedAccessException -> conflict "Ein Pfadtyp konnte nicht sicher bestimmt werden."

    let private hasLinkTarget (path: string) =
        try
            not (isNull (FileInfo(path).LinkTarget))
            || not (isNull (DirectoryInfo(path).LinkTarget))
            || attributesIndicateLink path
        with
        | :? IOException
        | :? UnauthorizedAccessException -> conflict "Ein Pfad konnte nicht symlink-sicher geprueft werden."

    let private requireNoLink path =
        if hasLinkTarget path then
            conflict "Ein Jobjournal-Pfad darf kein Symlink oder ReparsePoint sein."

    let private requireSafeWorkspaceRoot workspaceRoot =
        if String.IsNullOrWhiteSpace(workspaceRoot) then
            conflict "Der Workspace-Pfad fehlt."

        let root = Path.GetFullPath(workspaceRoot) |> trimDirectorySeparators

        if not (Directory.Exists(root)) then
            conflict "Der Workspace existiert nicht als Verzeichnis."

        let mutable current = DirectoryInfo(root)

        while not (isNull current) do
            requireNoLink current.FullName
            current <- current.Parent

        root

    let private ensureFixedDirectory parent name =
        let path = Path.Combine(parent, name)
        requireNoLink path

        if File.Exists(path) then
            conflict "Ein fester Jobjournal-Verzeichnispfad ist als Datei belegt."

        if not (Directory.Exists(path)) then
            Directory.CreateDirectory(path) |> ignore

        requireNoLink path

        if not (Directory.Exists(path)) then
            conflict "Ein festes Jobjournal-Verzeichnis konnte nicht sicher angelegt werden."

        path

    let private validateRelativePath jobId (relativePath: string) =
        let isNormalized =
            try
                relativePath.IsNormalized(NormalizationForm.FormC)
            with :? ArgumentException ->
                false

        if
            String.IsNullOrEmpty(relativePath)
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.Contains('\\')
            || relativePath.Contains(':')
            || not isNormalized
            || utf8ByteCount "Ein beanspruchter Pfad" relativePath > MaxRelativePathBytes
            || relativePath |> Seq.exists (fun character -> Char.IsControl(character))
        then
            conflict "Ein beanspruchter Pfad ist nicht kanonisch."

        let segments = relativePath.Split('/')

        if
            segments.Length = 0
            || segments
               |> Array.exists (fun segment ->
                   String.IsNullOrEmpty(segment)
                   || segment = "."
                   || segment = ".."
                   || utf8ByteCount "Ein Pfadsegment" segment > MaxSegmentBytes)
        then
            conflict "Ein beanspruchter Pfad verletzt die Segmentgrenzen."

        let isOwnJobPath =
            segments.Length >= 5
            && segments[0] = ".ai"
            && segments[1] = "runtime"
            && segments[2] = "asset-jobs"
            && segments[3] = jobId
            && (segments[4] = "stage" || segments[4] = "work")

        let isQuarantinePath =
            segments.Length >= 4
            && segments[0] = "assets"
            && segments[1] = "quarantine"
            && segments[2] = "3d"

        let isReceiptPath =
            segments.Length = 4
            && segments[0] = "assets"
            && segments[1] = "receipts"
            && segments[2].Length > 0
            && (segments[3].EndsWith(".json", StringComparison.Ordinal)
                || segments[3].EndsWith($".json.{jobId}.tmp", StringComparison.Ordinal))

        let isManifestPath =
            segments.Length = 3 && segments[0] = "assets" && segments[1] = "manifests"

        if not (isOwnJobPath || isQuarantinePath || isReceiptPath || isManifestPath) then
            conflict "Ein beanspruchter Pfad liegt ausserhalb der festen Jobjournal-Wurzeln."

        segments

    let private physicalPath workspaceRoot jobId relativePath =
        let segments = validateRelativePath jobId relativePath

        let combined =
            segments
            |> Array.fold (fun current segment -> Path.Combine(current, segment)) workspaceRoot
            |> Path.GetFullPath

        if not (isInsideRoot workspaceRoot combined) then
            conflict "Ein beanspruchter Pfad verlaesst den Workspace."

        combined

    let private ensureExistingComponentsSafe workspaceRoot jobId relativePath =
        let segments = validateRelativePath jobId relativePath
        let mutable current = workspaceRoot
        let mutable missing = false

        for index = 0 to segments.Length - 1 do
            current <- Path.Combine(current, segments[index])
            requireNoLink current

            if not missing then
                if File.Exists(current) then
                    if index <> segments.Length - 1 then
                        conflict "Eine Pfadkomponente ist unerwartet eine Datei."
                elif Directory.Exists(current) then
                    ()
                else
                    missing <- true

        Path.GetFullPath(current)

    let private tryPhysicalKind workspaceRoot jobId relativePath =
        let path = ensureExistingComponentsSafe workspaceRoot jobId relativePath
        requireNoLink path

        if File.Exists(path) then
            Some AssetJobOwnedPathKind.OwnedFile
        elif Directory.Exists(path) then
            Some AssetJobOwnedPathKind.OwnedDirectory
        else
            None

    let private hashRegularFile path =
        requireNoLink path

        if not (File.Exists(path)) || Directory.Exists(path) then
            conflict "Ein beanspruchter Dateipfad ist keine regulaere Datei."

        let before = FileInfo(path)

        if before.Length > MaxOwnedFileBytes then
            conflict "Eine beanspruchte Datei ueberschreitet die Groessengrenze."

        let hash =
            use stream =
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan)

            System.Security.Cryptography.SHA256.HashData(stream)
            |> Convert.ToHexString
            |> fun value -> value.ToLowerInvariant()

        requireNoLink path
        let after = FileInfo(path)

        if
            before.Length <> after.Length
            || before.LastWriteTimeUtc <> after.LastWriteTimeUtc
        then
            conflict "Eine beanspruchte Datei wurde waehrend der Hashpruefung veraendert."

        hash, after.Length

    type private DirectoryInventoryEntry =
        | InventoryDirectory of string
        | InventoryFile of string * int64 * string

    let private inventoryEntryPath entry =
        match entry with
        | InventoryDirectory value -> value
        | InventoryFile(value, _, _) -> value

    let private inventoryBytes entries =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartArray()

            entries
            |> List.iter (fun entry ->
                writer.WriteStartObject()

                match entry with
                | InventoryDirectory relative ->
                    writer.WriteString("path", relative)
                    writer.WriteString("type", "directory")
                | InventoryFile(relative, length, sha256) ->
                    writer.WriteNumber("bytes", length)
                    writer.WriteString("path", relative)
                    writer.WriteString("sha256", sha256)
                    writer.WriteString("type", "file")

                writer.WriteEndObject())

            writer.WriteEndArray())

    let private readDirectoryInventory path =
        requireNoLink path

        if not (Directory.Exists(path)) || File.Exists(path) then
            conflict "Ein beanspruchter Verzeichnispfad ist kein regulaeres Verzeichnis."

        let entries = ResizeArray<DirectoryInventoryEntry>()
        let mutable fileCount = 0
        let mutable totalBytes = 0L

        let rec walk current relative depth =
            if depth > MaxOwnedDirectoryDepth then
                conflict "Ein beanspruchtes Verzeichnis ist zu tief verschachtelt."

            requireNoLink current

            let children =
                try
                    Directory.EnumerateFileSystemEntries(current)
                    |> Seq.sortWith (fun left right -> pathComparer.Compare(left, right))
                    |> Seq.toArray
                with
                | :? IOException
                | :? UnauthorizedAccessException ->
                    conflict "Ein beanspruchtes Verzeichnis konnte nicht sicher inventarisiert werden."

            for child in children do
                requireNoLink child
                let name = Path.GetFileName(child)

                let normalizedName =
                    try
                        name.IsNormalized(NormalizationForm.FormC)
                    with :? ArgumentException ->
                        false

                if
                    String.IsNullOrEmpty(name)
                    || not normalizedName
                    || name |> Seq.exists Char.IsControl
                    || utf8ByteCount "Ein Verzeichniseintrag" name > MaxSegmentBytes
                then
                    conflict "Ein beanspruchtes Verzeichnis enthaelt einen nicht kanonischen Namen."

                let childRelative =
                    if String.IsNullOrEmpty(relative) then
                        name
                    else
                        relative + "/" + name

                if utf8ByteCount "Ein Verzeichnisinventarpfad" childRelative > MaxRelativePathBytes then
                    conflict "Ein beanspruchtes Verzeichnis enthaelt einen zu langen Pfad."

                if entries.Count >= MaxOwnedDirectoryEntries then
                    conflict "Ein beanspruchtes Verzeichnis enthaelt zu viele Eintraege."

                if Directory.Exists(child) && not (File.Exists(child)) then
                    entries.Add(InventoryDirectory childRelative)
                    walk child childRelative (depth + 1)
                elif File.Exists(child) && not (Directory.Exists(child)) then
                    fileCount <- fileCount + 1

                    if fileCount > MaxOwnedDirectoryFiles then
                        conflict "Ein beanspruchtes Verzeichnis enthaelt zu viele Dateien."

                    let hash, length = hashRegularFile child
                    totalBytes <- totalBytes + length

                    if totalBytes > MaxOwnedDirectoryBytes then
                        conflict "Ein beanspruchtes Verzeichnis ueberschreitet die Gesamtgroessengrenze."

                    entries.Add(InventoryFile(childRelative, length, hash))
                else
                    conflict "Ein beanspruchtes Verzeichnis enthaelt einen unsicheren Pfadtyp."

        walk path "" 0
        requireNoLink path

        let sorted =
            entries
            |> Seq.sortWith (fun left right ->
                StringComparer.Ordinal.Compare(inventoryEntryPath left, inventoryEntryPath right))
            |> Seq.toList

        sorted, sorted |> inventoryBytes |> Internal.sha256Hex

    let private inventoryPhysicalPath (root: string) (relative: string) =
        let candidate =
            relative.Split('/')
            |> Array.fold (fun current segment -> Path.Combine(current, segment)) root
            |> Path.GetFullPath

        if not (isInsideRoot root candidate) then
            conflict "Ein Verzeichnisinventarpfad verlaesst seinen beanspruchten Root."

        candidate

    let private hashDirectory path = readDirectoryInventory path |> snd

    let private flushFileToDisk path =
        requireNoLink path

        try
            use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            stream.Flush(true)
        with
        | :? IOException
        | :? UnauthorizedAccessException ->
            conflict "Eine eigene Datei konnte nicht auf Datentraeger synchronisiert werden."

        requireNoLink path

    let private flushDirectoryFilesToDisk path =
        let entries, _ = readDirectoryInventory path

        entries
        |> List.iter (function
            | InventoryDirectory _ -> ()
            | InventoryFile(relative, _, _) -> inventoryPhysicalPath path relative |> flushFileToDisk)

    let private deleteOwnedDirectorySafely (path: string) (expectedSha256: string) =
        let entries, actualSha256 = readDirectoryInventory path

        if actualSha256 <> expectedSha256 then
            conflict "Ein Recovery-Verzeichnis besitzt nicht mehr seinen Journalhash."

        let files =
            entries
            |> List.choose (function
                | InventoryFile(relative, length, sha256) -> Some(relative, length, sha256)
                | InventoryDirectory _ -> None)
            |> List.sortWith (fun (left, _, _) (right, _, _) ->
                let depthResult = compare (right.Split('/').Length) (left.Split('/').Length)

                if depthResult <> 0 then
                    depthResult
                else
                    StringComparer.Ordinal.Compare(right, left))

        for relative, expectedLength, expectedHash in files do
            let filePath = inventoryPhysicalPath path relative
            requireNoLink filePath

            if not (File.Exists(filePath)) || Directory.Exists(filePath) then
                conflict "Eine Recovery-Datei fehlt oder wechselte ihren Typ."

            let actualHash, actualLength = hashRegularFile filePath

            if actualHash <> expectedHash || actualLength <> expectedLength then
                conflict "Eine Recovery-Datei wurde vor dem Entfernen veraendert."

            try
                File.Delete(filePath)
            with
            | :? IOException
            | :? UnauthorizedAccessException -> conflict "Eine eigene Recovery-Datei konnte nicht entfernt werden."

        let directories =
            entries
            |> List.choose (function
                | InventoryDirectory relative -> Some relative
                | InventoryFile _ -> None)
            |> List.sortWith (fun left right ->
                let depthResult = compare (right.Split('/').Length) (left.Split('/').Length)

                if depthResult <> 0 then
                    depthResult
                else
                    StringComparer.Ordinal.Compare(right, left))

        for relative in directories do
            let directoryPath = inventoryPhysicalPath path relative
            requireNoLink directoryPath

            try
                Directory.Delete(directoryPath, false)
            with
            | :? IOException
            | :? UnauthorizedAccessException ->
                conflict "Ein Recovery-Verzeichnis enthaelt einen unbekannten oder veraenderten Eintrag."

        requireNoLink path

        try
            Directory.Delete(path, false)
        with
        | :? IOException
        | :? UnauthorizedAccessException ->
            conflict "Ein Recovery-Verzeichnis enthaelt einen unbekannten oder veraenderten Eintrag."

    let private hashExistingPath workspaceRoot jobId relativePath kind =
        let path = ensureExistingComponentsSafe workspaceRoot jobId relativePath

        match tryPhysicalKind workspaceRoot jobId relativePath, kind with
        | Some AssetJobOwnedPathKind.OwnedFile, AssetJobOwnedPathKind.OwnedFile -> hashRegularFile path |> fst
        | Some AssetJobOwnedPathKind.OwnedDirectory, AssetJobOwnedPathKind.OwnedDirectory -> hashDirectory path
        | None, _ -> conflict "Ein zu hashender beanspruchter Pfad fehlt."
        | _ -> conflict "Der Typ eines beanspruchten Pfads stimmt nicht."

    let private validateOwnedPath jobId ownedPath =
        validateRelativePath jobId ownedPath.Path |> ignore
        validateSha256 "Pfadhash" ownedPath.Sha256

    let private sortOwnedPaths ownedPaths =
        ownedPaths
        |> List.sortWith (fun left right ->
            let pathResult = StringComparer.Ordinal.Compare(left.Path, right.Path)

            if pathResult <> 0 then
                pathResult
            else
                StringComparer.Ordinal.Compare(kindText left.Kind, kindText right.Kind))

    let private pathsOverlap (left: string) (right: string) =
        left.Equals(right, pathComparison)
        || left.StartsWith(right + "/", pathComparison)
        || right.StartsWith(left + "/", pathComparison)

    let private requireNonOverlappingPaths message ownedPaths =
        ownedPaths
        |> List.iteri (fun index left ->
            ownedPaths
            |> List.skip (index + 1)
            |> List.iter (fun right ->
                if pathsOverlap left.Path right.Path then
                    conflict message))

    let private normalizeOwnedPaths jobId ownedPaths =
        if List.length ownedPaths > MaxOwnedPaths then
            conflict "Ein Jobjournal-Eintrag beansprucht zu viele Pfade."

        ownedPaths |> List.iter (validateOwnedPath jobId)
        let sorted = sortOwnedPaths ownedPaths

        requireNonOverlappingPaths "Beanspruchte Pfade duerfen weder doppelt noch ueberlappend sein." sorted

        sorted

    let private validateHistoricalOwnership entries =
        let claims = Dictionary<string, AssetJobOwnedPath>(pathComparer)

        for entry in entries do
            for ownedPath in entry.OwnedPaths do
                match claims.TryGetValue(ownedPath.Path) with
                | true, previous when previous.Kind <> ownedPath.Kind || previous.Sha256 <> ownedPath.Sha256 ->
                    conflict "Ein beanspruchter Pfad aendert Typ oder Hash innerhalb desselben Jobs."
                | true, _ -> ()
                | false, _ -> claims.Add(ownedPath.Path, ownedPath)

        let allClaims = claims.Values |> Seq.toList |> sortOwnedPaths

        requireNonOverlappingPaths "Historische Pfadbeanspruchungen duerfen nicht ueberlappen." allClaims

        allClaims

    let private writeOwnedPath (writer: Utf8JsonWriter) (ownedPath: AssetJobOwnedPath) =
        writer.WriteStartObject()
        writer.WriteString("path", ownedPath.Path)
        writer.WriteString("sha256", ownedPath.Sha256)
        writer.WriteString("type", kindText ownedPath.Kind)
        writer.WriteEndObject()

    let private coreBytes
        (schemaVersion: int)
        (sequence: int)
        (jobId: string)
        (state: AssetJobState)
        (previousEntrySha256: string option)
        (atUtc: string)
        (ownedPaths: AssetJobOwnedPath list)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("atUtc", atUtc)
            writer.WriteString("jobId", jobId)
            writer.WritePropertyName("ownedPaths")
            writer.WriteStartArray()
            ownedPaths |> List.iter (writeOwnedPath writer)
            writer.WriteEndArray()
            writer.WritePropertyName("previousEntrySha256")

            match previousEntrySha256 with
            | Some hash -> writer.WriteStringValue(hash)
            | None -> writer.WriteNullValue()

            writer.WriteNumber("schemaVersion", schemaVersion)
            writer.WriteNumber("sequence", sequence)
            writer.WriteString("state", stateText state)
            writer.WriteEndObject())

    let private lineBytes (entry: AssetJobJournalEntry) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("atUtc", entry.AtUtc)
            writer.WriteString("entrySha256", entry.EntrySha256)
            writer.WriteString("jobId", entry.JobId)
            writer.WritePropertyName("ownedPaths")
            writer.WriteStartArray()
            entry.OwnedPaths |> List.iter (writeOwnedPath writer)
            writer.WriteEndArray()
            writer.WritePropertyName("previousEntrySha256")

            match entry.PreviousEntrySha256 with
            | Some hash -> writer.WriteStringValue(hash)
            | None -> writer.WriteNullValue()

            writer.WriteNumber("schemaVersion", entry.SchemaVersion)
            writer.WriteNumber("sequence", entry.Sequence)
            writer.WriteString("state", stateText entry.State)
            writer.WriteEndObject())

    let private requireObjectProperties description expected (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            conflict $"{description} muss ein JSON-Objekt sein."

        let actual =
            element.EnumerateObject()
            |> Seq.map (fun property -> property.Name)
            |> Set.ofSeq

        if actual <> expected then
            conflict $"{description} besitzt keine geschlossene Feldmenge."

    let private requiredString (name: string) (element: JsonElement) =
        let property = element.GetProperty(name)

        if property.ValueKind <> JsonValueKind.String then
            conflict $"Jobjournal-Feld {name} muss eine Zeichenfolge sein."

        property.GetString()

    let private requiredInt (name: string) (element: JsonElement) =
        let property = element.GetProperty(name)

        match property.TryGetInt32() with
        | true, value -> value
        | _ -> conflict $"Jobjournal-Feld {name} muss eine Ganzzahl sein."

    let private parseOwnedPath jobId (element: JsonElement) =
        requireObjectProperties "ownedPaths-Eintrag" (set [ "path"; "sha256"; "type" ]) element

        let ownedPath =
            { Path = requiredString "path" element
              Kind = requiredString "type" element |> parseKind
              Sha256 = requiredString "sha256" element }

        validateOwnedPath jobId ownedPath
        ownedPath

    let private parseEntry (line: byte array) =
        if line.Length = 0 || line.Length > 131_072 then
            conflict "Eine Jobjournal-Zeile verletzt die Groessengrenze."

        try
            use document =
                JsonDocument.Parse(
                    ReadOnlyMemory<byte>(line),
                    JsonDocumentOptions(
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 5
                    )
                )

            let root = document.RootElement

            requireObjectProperties
                "Jobjournal-Eintrag"
                (set
                    [ "atUtc"
                      "entrySha256"
                      "jobId"
                      "ownedPaths"
                      "previousEntrySha256"
                      "schemaVersion"
                      "sequence"
                      "state" ])
                root

            let schemaVersion = requiredInt "schemaVersion" root

            if schemaVersion <> 1 then
                conflict "Das Jobjournal besitzt eine unbekannte Schemaversion."

            let sequence = requiredInt "sequence" root

            if sequence < 1 || sequence > MaxJournalEntries then
                conflict "Die Jobjournal-Sequenz liegt ausserhalb der Grenzen."

            let jobId = requiredString "jobId" root
            validateJobId jobId
            let state = requiredString "state" root |> parseState
            let atUtc = requiredString "atUtc" root

            let parsedAtUtc =
                Internal.tryParseUtc atUtc
                |> Option.defaultWith (fun () -> conflict "Der Jobjournal-Zeitstempel ist nicht UTC.")

            if Internal.utcText parsedAtUtc <> atUtc then
                conflict "Der Jobjournal-Zeitstempel ist nicht kanonisch."

            let previousProperty = root.GetProperty("previousEntrySha256")

            let previous =
                match previousProperty.ValueKind with
                | JsonValueKind.Null -> None
                | JsonValueKind.String ->
                    let value = previousProperty.GetString()
                    validateSha256 "Vorheriger Eintragshash" value
                    Some value
                | _ -> conflict "Der vorherige Eintragshash ist ungueltig."

            let ownedProperty = root.GetProperty("ownedPaths")

            if ownedProperty.ValueKind <> JsonValueKind.Array then
                conflict "ownedPaths muss ein Array sein."

            let ownedPaths =
                ownedProperty.EnumerateArray() |> Seq.map (parseOwnedPath jobId) |> Seq.toList

            let normalizedOwnedPaths = normalizeOwnedPaths jobId ownedPaths

            if normalizedOwnedPaths <> ownedPaths then
                conflict "ownedPaths ist nicht ordinal sortiert."

            if state = AssetJobState.RolledBack && not (List.isEmpty ownedPaths) then
                conflict "ROLLED_BACK darf keine Pfade mehr beanspruchen."

            let entrySha256 = requiredString "entrySha256" root
            validateSha256 "Eintragshash" entrySha256

            let core = coreBytes schemaVersion sequence jobId state previous atUtc ownedPaths

            if Internal.sha256Hex core <> entrySha256 then
                conflict "Der Jobjournal-Eintragshash stimmt nicht."

            let entry =
                { SchemaVersion = schemaVersion
                  Sequence = sequence
                  JobId = jobId
                  State = state
                  PreviousEntrySha256 = previous
                  AtUtc = atUtc
                  OwnedPaths = ownedPaths
                  EntrySha256 = entrySha256 }

            let canonical = lineBytes entry

            if not (canonical.AsSpan().SequenceEqual(line.AsSpan())) then
                conflict "Eine Jobjournal-Zeile ist nicht bytekanonisch."

            entry
        with
        | :? JsonException -> conflict "Das Jobjournal enthaelt ungueltiges JSON."
        | :? DecoderFallbackException -> conflict "Das Jobjournal ist nicht gueltiges UTF-8."

    let private validTransition previous next =
        match previous, next with
        | AssetJobState.Created, AssetJobState.Generated
        | AssetJobState.Generated, AssetJobState.Inspected
        | AssetJobState.Inspected, AssetJobState.ProvenancePrepared
        | AssetJobState.ProvenancePrepared, AssetJobState.QuarantinePublished
        | AssetJobState.QuarantinePublished, AssetJobState.MetadataPublished
        | AssetJobState.MetadataPublished, AssetJobState.Verified
        | AssetJobState.Verified, AssetJobState.Committed -> true
        | state, AssetJobState.RolledBack when state <> AssetJobState.Committed && state <> AssetJobState.RolledBack ->
            true
        | _ -> false

    let private validateChain (expectedJobId: string) (entries: AssetJobJournalEntry list) =
        if List.length entries > MaxJournalEntries then
            conflict "Das Jobjournal enthaelt zu viele Eintraege."

        let mutable previous: AssetJobJournalEntry option = None

        entries
        |> List.iteri (fun index (entry: AssetJobJournalEntry) ->
            if entry.JobId <> expectedJobId then
                conflict "Das Jobjournal gehoert zu einer anderen Job-ID."

            if entry.Sequence <> index + 1 then
                conflict "Die Jobjournal-Sequenz ist nicht lueckenlos."

            match previous with
            | None ->
                if entry.State <> AssetJobState.Created || entry.PreviousEntrySha256.IsSome then
                    conflict "Das Jobjournal beginnt nicht mit CREATED."
            | Some prior ->
                if entry.PreviousEntrySha256 <> Some prior.EntrySha256 then
                    conflict "Die Jobjournal-Hashkette ist unterbrochen."

                if not (validTransition prior.State entry.State) then
                    conflict "Das Jobjournal enthaelt einen ungueltigen Zustandsuebergang."

                let priorTime =
                    Internal.tryParseUtc prior.AtUtc
                    |> Option.defaultWith (fun () -> conflict "Ein Jobjournal-Zeitstempel ist ungueltig.")

                let currentTime =
                    Internal.tryParseUtc entry.AtUtc
                    |> Option.defaultWith (fun () -> conflict "Ein Jobjournal-Zeitstempel ist ungueltig.")

                if currentTime < priorTime then
                    conflict "Jobjournal-Zeitstempel duerfen nicht rueckwaerts laufen."

            previous <- Some entry)

        validateHistoricalOwnership entries |> ignore
        entries

    let private parseJournalBytes jobId allowEmpty (bytes: byte array) =
        if int64 bytes.Length > MaxJournalBytes then
            conflict "Das Jobjournal ueberschreitet die Groessengrenze."

        if bytes.Length = 0 then
            if allowEmpty then
                []
            else
                conflict "Das Jobjournal ist leer."
        else
            if bytes[bytes.Length - 1] <> byte '\n' then
                conflict "Das Jobjournal endet nicht mit LF."

            if bytes |> Array.exists (fun value -> value = byte '\r' || value = 0uy) then
                conflict "Das Jobjournal enthaelt unzulaessige Bytes."

            let text =
                try
                    strictUtf8.GetString(bytes)
                with :? DecoderFallbackException ->
                    conflict "Das Jobjournal ist nicht gueltiges UTF-8."

            let lines = text.Split('\n')

            if lines[lines.Length - 1] <> "" then
                conflict "Das Jobjournal besitzt nachlaufende Bytes."

            let entries =
                lines
                |> Array.take (lines.Length - 1)
                |> Array.map (fun line -> line |> strictUtf8.GetBytes |> parseEntry)
                |> Array.toList

            if List.isEmpty entries && not allowEmpty then
                conflict "Das Jobjournal besitzt keinen Eintrag."

            validateChain jobId entries

    let private readBoundedStream (stream: FileStream) =
        if stream.Length > MaxJournalBytes then
            conflict "Das Jobjournal ueberschreitet die Groessengrenze."

        if stream.Length > int64 Int32.MaxValue then
            conflict "Das Jobjournal ist nicht sicher allokierbar."

        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        let bytes = Array.zeroCreate<byte> (int stream.Length)
        stream.ReadExactly(bytes)
        bytes

    let private journalPath jobLock =
        Path.Combine(jobLock.JobRoot, "journal.jsonl")

    let private requireExistingDirectory path =
        requireNoLink path

        if not (Directory.Exists(path)) || File.Exists(path) then
            conflict "Eine feste Jobjournal-Pfadkomponente ist kein sicheres Verzeichnis."

    let private requireJobInfrastructureSafe jobLock =
        let aiRoot = Path.Combine(jobLock.WorkspaceRoot, ".ai")
        let runtimeRoot = Path.Combine(aiRoot, "runtime")
        let assetJobsRoot = Path.Combine(runtimeRoot, "asset-jobs")
        let expectedJobRoot = Path.Combine(assetJobsRoot, jobLock.JobId) |> Path.GetFullPath

        [ jobLock.WorkspaceRoot; aiRoot; runtimeRoot; assetJobsRoot; expectedJobRoot ]
        |> List.iter requireExistingDirectory

        if not (expectedJobRoot.Equals(jobLock.JobRoot, pathComparison)) then
            conflict "Der feste Jobroot hat sich veraendert."

        let lockPath = Path.Combine(expectedJobRoot, ".job.lock")
        requireNoLink lockPath

        if not (File.Exists(lockPath)) || Directory.Exists(lockPath) then
            conflict "Der Job-Lock ist keine regulaere Datei."

    let private requireUsableLock jobLock =
        let usable =
            try
                not (isNull (box jobLock)) && jobLock.LockHandle.CanRead
            with :? ObjectDisposedException ->
                false

        if not usable then
            conflict "Der exklusive Job-Lock ist nicht mehr gueltig."

        requireJobInfrastructureSafe jobLock

    let private loadLocked allowMissing jobLock =
        requireUsableLock jobLock
        let path = journalPath jobLock
        requireNoLink path

        if not (File.Exists(path)) then
            if allowMissing then
                []
            else
                conflict "Das Jobjournal fehlt."
        elif Directory.Exists(path) then
            conflict "Das Jobjournal ist keine regulaere Datei."
        else
            use stream =
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan)

            requireNoLink path
            let entries = readBoundedStream stream |> parseJournalBytes jobLock.JobId false
            requireNoLink path
            entries

    let private invokeCrashHook crashHook point =
        if not (obj.ReferenceEquals(crashHook, null)) then
            crashHook point

    let noCrash: string -> unit = ignore

    let withExclusiveJobLock (workspaceRoot: string) (jobId: string) (action: AssetJobLock -> 'T) =
        validateJobId jobId
        let root = requireSafeWorkspaceRoot workspaceRoot
        let aiRoot = ensureFixedDirectory root ".ai"
        let runtimeRoot = ensureFixedDirectory aiRoot "runtime"
        let assetJobsRoot = ensureFixedDirectory runtimeRoot "asset-jobs"
        let jobRoot = ensureFixedDirectory assetJobsRoot jobId
        let lockPath = Path.Combine(jobRoot, ".job.lock")
        requireNoLink lockPath

        let handle =
            try
                new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.WriteThrough
                )
            with :? IOException ->
                conflict "Der Job besitzt bereits einen exklusiven Lock."

        use lockHandle = handle

        try
            requireNoLink lockPath

            if Directory.Exists(lockPath) || not (File.Exists(lockPath)) then
                conflict "Der Job-Lock ist keine regulaere Datei."

            let jobLock =
                { WorkspaceRoot = root
                  JobId = jobId
                  JobRoot = jobRoot
                  LockHandle = lockHandle }

            action jobLock
        finally
            requireNoLink lockPath

    let load (jobLock: AssetJobLock) = loadLocked true jobLock

    let current (jobLock: AssetJobLock) = load jobLock |> List.tryLast

    let claimOwnedPath (jobLock: AssetJobLock) (relativePath: string) (kind: AssetJobOwnedPathKind) (sha256: string) =
        requireUsableLock jobLock

        let claim =
            { Path = relativePath
              Kind = kind
              Sha256 = sha256 }

        validateOwnedPath jobLock.JobId claim
        claim

    let hashOwnedPath (jobLock: AssetJobLock) (relativePath: string) (kind: AssetJobOwnedPathKind) =
        requireUsableLock jobLock
        let hash = hashExistingPath jobLock.WorkspaceRoot jobLock.JobId relativePath kind
        claimOwnedPath jobLock relativePath kind hash

    let private pathCategory (relativePath: string) =
        if relativePath.StartsWith("assets/manifests/", StringComparison.Ordinal) then
            "manifest"
        elif relativePath.StartsWith("assets/receipts/", StringComparison.Ordinal) then
            "receipt"
        elif relativePath.StartsWith("assets/quarantine/3d/", StringComparison.Ordinal) then
            "quarantine"
        else
            "job"

    let publicationTemporaryPath (jobLock: AssetJobLock) (targetRelativePath: string) =
        requireUsableLock jobLock
        validateRelativePath jobLock.JobId targetRelativePath |> ignore

        match pathCategory targetRelativePath with
        | "manifest"
        | "receipt" -> targetRelativePath + "." + jobLock.JobId + ".tmp"
        | _ -> conflict "Nur Receipt und Manifest besitzen atomare Tempfile-Pfade."

    let private requireClaim currentEntry relativePath kind sha256 =
        currentEntry.OwnedPaths
        |> List.tryFind (fun claim -> claim.Path = relativePath)
        |> function
            | Some claim when claim.Kind = kind && claim.Sha256 = sha256 -> ()
            | _ -> conflict "Die aktuelle Journalstufe beansprucht einen Publikationspfad nicht exakt."

    let private requireExistingClaim workspaceRoot jobId claim =
        match tryPhysicalKind workspaceRoot jobId claim.Path with
        | None -> conflict "Ein fuer diesen Zustand erforderlicher beanspruchter Pfad fehlt."
        | Some kind when kind <> claim.Kind -> conflict "Ein beanspruchter Pfad besitzt den falschen Typ."
        | Some _ ->
            let actual = hashExistingPath workspaceRoot jobId claim.Path claim.Kind

            if actual <> claim.Sha256 then
                conflict "Ein beanspruchter Pfad besitzt nicht mehr seinen Journalhash."

            let path = physicalPath workspaceRoot jobId claim.Path

            match claim.Kind with
            | AssetJobOwnedPathKind.OwnedFile -> flushFileToDisk path
            | AssetJobOwnedPathKind.OwnedDirectory -> flushDirectoryFilesToDisk path

            let afterFlush = hashExistingPath workspaceRoot jobId claim.Path claim.Kind

            if afterFlush <> claim.Sha256 then
                conflict "Ein beanspruchter Pfad wurde waehrend der Synchronisierung veraendert."

    let private requireCategoryExists workspaceRoot jobId category ownedPaths =
        ownedPaths
        |> List.tryFind (fun claim -> pathCategory claim.Path = category)
        |> Option.defaultWith (fun () -> conflict "Dem Journalzustand fehlt eine erforderliche Pfadklasse.")
        |> requireExistingClaim workspaceRoot jobId

    let private requireCategoryClaim category ownedPaths =
        if not (ownedPaths |> List.exists (fun claim -> pathCategory claim.Path = category)) then
            conflict "Dem Journalzustand fehlt eine erforderliche Pfadklasse."

    let private validateStateInventory workspaceRoot jobId state ownedPaths =
        if state = AssetJobState.RolledBack then
            if not (List.isEmpty ownedPaths) then
                conflict "ROLLED_BACK darf keine Pfade beanspruchen."
        else
            for claim in ownedPaths do
                match tryPhysicalKind workspaceRoot jobId claim.Path with
                | None -> ()
                | Some actualKind when actualKind <> claim.Kind ->
                    conflict "Ein bereits vorhandener beanspruchter Pfad besitzt den falschen Typ."
                | Some _ -> requireExistingClaim workspaceRoot jobId claim

            match state with
            | AssetJobState.Generated
            | AssetJobState.Inspected -> requireCategoryExists workspaceRoot jobId "job" ownedPaths
            | AssetJobState.ProvenancePrepared ->
                requireCategoryExists workspaceRoot jobId "job" ownedPaths
                requireCategoryClaim "quarantine" ownedPaths
                requireCategoryClaim "receipt" ownedPaths
                requireCategoryClaim "manifest" ownedPaths
            | AssetJobState.QuarantinePublished ->
                requireCategoryExists workspaceRoot jobId "job" ownedPaths
                requireCategoryExists workspaceRoot jobId "quarantine" ownedPaths
            | AssetJobState.MetadataPublished
            | AssetJobState.Verified
            | AssetJobState.Committed ->
                requireCategoryExists workspaceRoot jobId "quarantine" ownedPaths
                requireCategoryExists workspaceRoot jobId "receipt" ownedPaths
                requireCategoryExists workspaceRoot jobId "manifest" ownedPaths
            | _ -> ()

    let appendTransition
        (jobLock: AssetJobLock)
        (state: AssetJobState)
        (ownedPaths: AssetJobOwnedPath list)
        (atUtc: DateTimeOffset)
        (crashHook: string -> unit)
        =
        requireUsableLock jobLock
        let normalized = normalizeOwnedPaths jobLock.JobId ownedPaths
        validateStateInventory jobLock.WorkspaceRoot jobLock.JobId state normalized
        let path = journalPath jobLock
        requireNoLink path

        use stream =
            new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                65_536,
                FileOptions.WriteThrough
            )

        requireNoLink path
        let existingBytes = readBoundedStream stream
        let entries = parseJournalBytes jobLock.JobId true existingBytes
        let previous = entries |> List.tryLast

        match previous with
        | None when state <> AssetJobState.Created -> conflict "Der erste Jobjournal-Zustand muss CREATED sein."
        | Some prior when not (validTransition prior.State state) ->
            conflict "Der angeforderte Jobjournal-Zustandsuebergang ist ungueltig."
        | _ -> ()

        let timestamp = atUtc.ToUniversalTime() |> Internal.utcText

        match previous with
        | Some prior ->
            let priorTimestamp =
                Internal.tryParseUtc prior.AtUtc
                |> Option.defaultWith (fun () -> conflict "Der letzte Jobjournal-Zeitstempel ist ungueltig.")

            if atUtc.ToUniversalTime() < priorTimestamp then
                conflict "Der neue Jobjournal-Zeitstempel laeuft rueckwaerts."
        | None -> ()

        let sequence = entries.Length + 1

        if sequence > MaxJournalEntries then
            conflict "Das Jobjournal kann keinen weiteren Eintrag aufnehmen."

        let previousHash = previous |> Option.map (fun entry -> entry.EntrySha256)

        let core =
            coreBytes 1 sequence jobLock.JobId state previousHash timestamp normalized

        let hash = Internal.sha256Hex core

        let entry =
            { SchemaVersion = 1
              Sequence = sequence
              JobId = jobLock.JobId
              State = state
              PreviousEntrySha256 = previousHash
              AtUtc = timestamp
              OwnedPaths = normalized
              EntrySha256 = hash }

        let serialized = lineBytes entry

        if int64 existingBytes.Length + int64 serialized.Length + 1L > MaxJournalBytes then
            conflict "Das Jobjournal kann die Groessengrenze nicht einhalten."

        let prospective = Array.concat [ existingBytes; serialized; [| byte '\n' |] ]
        parseJournalBytes jobLock.JobId false prospective |> ignore
        stream.Seek(0L, SeekOrigin.End) |> ignore
        let durableLine = Array.append serialized [| byte '\n' |]
        stream.Write(durableLine)
        stream.Flush(true)
        requireNoLink path
        invokeCrashHook crashHook ("after-journal-" + stateText state)
        entry

    let publishDirectoryByRename
        (jobLock: AssetJobLock)
        (stagedRelativePath: string)
        (targetRelativePath: string)
        (expectedSha256: string)
        (crashHook: string -> unit)
        =
        requireUsableLock jobLock
        validateSha256 "Publikationshash" expectedSha256

        if
            not (
                stagedRelativePath.StartsWith(
                    $".ai/runtime/asset-jobs/{jobLock.JobId}/stage/",
                    StringComparison.Ordinal
                )
            )
        then
            conflict "Ein Quarantaene-Rename muss aus dem eigenen Stagebereich stammen."

        if not (targetRelativePath.StartsWith("assets/quarantine/3d/", StringComparison.Ordinal)) then
            conflict "Ein Quarantaene-Rename muss in die feste 3D-Quarantaene zielen."

        let latest =
            current jobLock
            |> Option.defaultWith (fun () -> conflict "Eine Publikation benoetigt einen Jobjournal-Eintrag.")

        if latest.State <> AssetJobState.ProvenancePrepared then
            conflict "Quarantaene darf nur aus PROVENANCE_PREPARED publiziert werden."

        requireClaim latest stagedRelativePath AssetJobOwnedPathKind.OwnedDirectory expectedSha256
        requireClaim latest targetRelativePath AssetJobOwnedPathKind.OwnedDirectory expectedSha256

        let sourcePath =
            ensureExistingComponentsSafe jobLock.WorkspaceRoot jobLock.JobId stagedRelativePath

        let targetPath =
            ensureExistingComponentsSafe jobLock.WorkspaceRoot jobLock.JobId targetRelativePath

        requireExistingClaim
            jobLock.WorkspaceRoot
            jobLock.JobId
            { Path = stagedRelativePath
              Kind = AssetJobOwnedPathKind.OwnedDirectory
              Sha256 = expectedSha256 }

        if
            tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId targetRelativePath
            |> Option.isSome
        then
            conflict "Das Quarantaeneziel ist bereits belegt."

        let parent = Path.GetDirectoryName(targetPath)

        if String.IsNullOrEmpty(parent) || not (Directory.Exists(parent)) then
            conflict "Das feste Quarantaene-Zielverzeichnis fehlt."

        requireNoLink parent

        try
            Directory.Move(sourcePath, targetPath)
        with :? IOException ->
            conflict "Der atomare Quarantaene-Rename ist fehlgeschlagen."

        let publishedHash =
            hashExistingPath jobLock.WorkspaceRoot jobLock.JobId targetRelativePath AssetJobOwnedPathKind.OwnedDirectory

        if publishedHash <> expectedSha256 then
            conflict "Der publizierte Quarantaene-Hash stimmt nicht."

        invokeCrashHook crashHook "after-quarantine-rename"

    let publishFileAtomically
        (jobLock: AssetJobLock)
        (stagedRelativePath: string)
        (targetRelativePath: string)
        (expectedSha256: string)
        (crashHook: string -> unit)
        =
        requireUsableLock jobLock
        validateSha256 "Publikationshash" expectedSha256

        if
            not (
                stagedRelativePath.StartsWith(
                    $".ai/runtime/asset-jobs/{jobLock.JobId}/stage/",
                    StringComparison.Ordinal
                )
            )
        then
            conflict "Eine Metadatenpublikation muss aus dem eigenen Stagebereich stammen."

        let category = pathCategory targetRelativePath

        if category <> "receipt" && category <> "manifest" then
            conflict "Atomare Metadatenpublikation ist nur fuer Receipt und Manifest erlaubt."

        let temporaryRelativePath = publicationTemporaryPath jobLock targetRelativePath

        let latest =
            current jobLock
            |> Option.defaultWith (fun () -> conflict "Eine Publikation benoetigt einen Jobjournal-Eintrag.")

        if latest.State <> AssetJobState.QuarantinePublished then
            conflict "Metadaten duerfen nur aus QUARANTINE_PUBLISHED publiziert werden."

        requireCategoryExists jobLock.WorkspaceRoot jobLock.JobId "quarantine" latest.OwnedPaths

        if category = "manifest" then
            requireCategoryExists jobLock.WorkspaceRoot jobLock.JobId "receipt" latest.OwnedPaths

        requireClaim latest stagedRelativePath AssetJobOwnedPathKind.OwnedFile expectedSha256
        requireClaim latest targetRelativePath AssetJobOwnedPathKind.OwnedFile expectedSha256
        requireClaim latest temporaryRelativePath AssetJobOwnedPathKind.OwnedFile expectedSha256

        requireExistingClaim
            jobLock.WorkspaceRoot
            jobLock.JobId
            { Path = stagedRelativePath
              Kind = AssetJobOwnedPathKind.OwnedFile
              Sha256 = expectedSha256 }

        if
            tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId targetRelativePath
            |> Option.isSome
        then
            conflict "Das Metadatenziel ist bereits belegt."

        if
            tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId temporaryRelativePath
            |> Option.isSome
        then
            conflict "Der atomare Metadaten-Temppfad ist bereits belegt."

        let sourcePath = physicalPath jobLock.WorkspaceRoot jobLock.JobId stagedRelativePath
        let targetPath = physicalPath jobLock.WorkspaceRoot jobLock.JobId targetRelativePath

        let temporaryPath =
            physicalPath jobLock.WorkspaceRoot jobLock.JobId temporaryRelativePath

        let parent = Path.GetDirectoryName(targetPath)

        if String.IsNullOrEmpty(parent) || not (Directory.Exists(parent)) then
            conflict "Das feste Metadaten-Zielverzeichnis fehlt."

        requireNoLink parent

        try
            use source =
                new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    65_536,
                    FileOptions.SequentialScan
                )

            use destination =
                new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    65_536,
                    FileOptions.WriteThrough
                )

            source.CopyTo(destination)
            destination.Flush(true)
        with :? IOException ->
            conflict "Der atomare Metadaten-Tempwrite ist fehlgeschlagen."

        let temporaryHash =
            hashExistingPath jobLock.WorkspaceRoot jobLock.JobId temporaryRelativePath AssetJobOwnedPathKind.OwnedFile

        if temporaryHash <> expectedSha256 then
            conflict "Der atomare Metadaten-Temphash stimmt nicht."

        invokeCrashHook crashHook ("after-" + category + "-temp-write")

        if
            tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId targetRelativePath
            |> Option.isSome
        then
            conflict "Das Metadatenziel wurde konkurrierend belegt."

        try
            File.Move(temporaryPath, targetPath, false)
        with :? IOException ->
            conflict "Der atomare Metadaten-Rename ist fehlgeschlagen."

        let targetHash =
            hashExistingPath jobLock.WorkspaceRoot jobLock.JobId targetRelativePath AssetJobOwnedPathKind.OwnedFile

        if targetHash <> expectedSha256 then
            conflict "Der publizierte Metadatenhash stimmt nicht."

        invokeCrashHook crashHook ("after-" + category + "-rename")

    let private recoveryOrder claim =
        let category = pathCategory claim.Path

        let rank =
            match category with
            | "manifest" -> 0
            | "receipt" -> 1
            | "quarantine" -> 2
            | _ -> 3

        rank, -claim.Path.Length, claim.Path

    let private preflightPresentClaims jobLock claims =
        claims
        |> List.choose (fun claim ->
            match tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId claim.Path with
            | None -> None
            | Some actualKind when actualKind <> claim.Kind ->
                conflict "Recovery fand einen beanspruchten Pfad mit fremdem Typ."
            | Some _ ->
                let currentHash =
                    hashExistingPath jobLock.WorkspaceRoot jobLock.JobId claim.Path claim.Kind

                if currentHash <> claim.Sha256 then
                    conflict "Recovery fand einen fremden oder veraenderten Pfad."

                Some claim)

    let private rollbackLocked jobLock atUtc crashHook =
        let entries = loadLocked false jobLock

        let latest =
            entries
            |> List.tryLast
            |> Option.defaultWith (fun () -> conflict "Das Jobjournal besitzt keinen Zustand.")

        match latest.State with
        | AssetJobState.RolledBack -> AssetJobRecoveryOutcome.AlreadyRolledBack latest
        | AssetJobState.Committed ->
            let claims = validateHistoricalOwnership entries
            preflightPresentClaims jobLock claims |> ignore
            requireCategoryExists jobLock.WorkspaceRoot jobLock.JobId "quarantine" latest.OwnedPaths
            requireCategoryExists jobLock.WorkspaceRoot jobLock.JobId "receipt" latest.OwnedPaths
            requireCategoryExists jobLock.WorkspaceRoot jobLock.JobId "manifest" latest.OwnedPaths
            AssetJobRecoveryOutcome.AlreadyCommitted latest
        | _ ->
            let claims = validateHistoricalOwnership entries
            let presentClaims = preflightPresentClaims jobLock claims

            let ordered = presentClaims |> List.sortBy recoveryOrder

            for index, claim in ordered |> List.indexed do
                match tryPhysicalKind jobLock.WorkspaceRoot jobLock.JobId claim.Path with
                | None -> ()
                | Some actualKind when actualKind <> claim.Kind ->
                    conflict "Ein Recovery-Pfad wechselte vor dem Entfernen seinen Typ."
                | Some _ ->
                    let currentHash =
                        hashExistingPath jobLock.WorkspaceRoot jobLock.JobId claim.Path claim.Kind

                    if currentHash <> claim.Sha256 then
                        conflict "Ein Recovery-Pfad wurde vor dem Entfernen veraendert."

                    let path = physicalPath jobLock.WorkspaceRoot jobLock.JobId claim.Path

                    match claim.Kind with
                    | AssetJobOwnedPathKind.OwnedFile ->
                        try
                            File.Delete(path)
                        with
                        | :? IOException
                        | :? UnauthorizedAccessException ->
                            conflict "Eine eigene Recovery-Datei konnte nicht entfernt werden."
                    | AssetJobOwnedPathKind.OwnedDirectory -> deleteOwnedDirectorySafely path claim.Sha256

                    invokeCrashHook crashHook $"after-recovery-delete-{index + 1}"

            let rolledBack =
                appendTransition jobLock AssetJobState.RolledBack [] atUtc crashHook

            AssetJobRecoveryOutcome.RolledBack rolledBack

    let recover (workspaceRoot: string) (jobId: string) (atUtc: DateTimeOffset) (crashHook: string -> unit) =
        withExclusiveJobLock workspaceRoot jobId (fun jobLock -> rollbackLocked jobLock atUtc crashHook)

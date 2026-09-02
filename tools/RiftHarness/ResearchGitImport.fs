namespace RiftHarness

open System
open System.Diagnostics
open System.Globalization
open System.IO

type ResearchGitCommit =
    { CommitId: string
      TreeId: string
      ParentCommitIds: string list
      CommitTimeUtc: string
      CommitObjectSha256: string }

type ResearchGitHistory =
    { BaseCommit: string
      HeadCommit: string
      ObjectFormat: string
      Commits: ResearchGitCommit list }

type ResearchGitIdentity =
    { ObjectFormat: string
      HeadCommit: string
      HeadTreeId: string
      WorktreeClean: bool
      BranchRef: ResearchValue<string> }

[<RequireQualifiedAccess>]
module ResearchGitImport =
    [<Literal>]
    let private MaxGitOutputBytes = 8 * 1024 * 1024

    [<Literal>]
    let private MaxImportedCommits = 10000

    let private isLowerHexLength length (value: string) =
        not (isNull value)
        && value.Length = length
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9')
               || (character >= 'a' && character <= 'f'))

    let private requireExactObjectId expectedLength description value =
        if not (isLowerHexLength expectedLength value) then
            Internal.fail $"{description} muss eine exakte kleingeschriebene Git-Objekt-ID mit {expectedLength} Zeichen sein."

        value

    let private startGit root arguments =
        let startInfo = ProcessStartInfo("git")
        startInfo.WorkingDirectory <- root
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.Environment["LC_ALL"] <- "C"
        startInfo.Environment["LANG"] <- "C"
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] <- "1"
        startInfo.Environment["GIT_TERMINAL_PROMPT"] <- "0"
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] <- "0"

        if not (OperatingSystem.IsWindows()) then
            startInfo.Environment["GIT_CONFIG_GLOBAL"] <- "/dev/null"

        startInfo.ArgumentList.Add("--no-optional-locks")
        startInfo.ArgumentList.Add("-c")
        startInfo.ArgumentList.Add("core.hooksPath=/dev/null")

        for argument in arguments do
            startInfo.ArgumentList.Add(argument)

        match Process.Start(startInfo) with
        | null -> Internal.fail "Git-Prozess konnte nicht gestartet werden."
        | child -> child

    let private readBounded (stream: Stream) =
        use output = new MemoryStream()
        let buffer = Array.zeroCreate<byte> 16384
        let mutable total = 0
        let mutable reading = true

        while reading do
            let count = stream.Read(buffer, 0, buffer.Length)

            if count = 0 then
                reading <- false
            else
                total <- total + count

                if total > MaxGitOutputBytes then
                    Internal.fail "Git-Ausgabe ueberschreitet das Importlimit."

                output.Write(buffer, 0, count)

        output.ToArray()

    let private runBytes root arguments acceptedExitCodes =
        use child = startGit root arguments

        // Drain both redirected pipes concurrently. Reading one pipe fully before
        // the other can deadlock when Git fills the second OS pipe buffer.
        let stdoutTask =
            Threading.Tasks.Task.Run(fun () -> readBounded child.StandardOutput.BaseStream)

        let stderrTask =
            Threading.Tasks.Task.Run(fun () -> readBounded child.StandardError.BaseStream)

        child.WaitForExit()
        let stdout = stdoutTask.GetAwaiter().GetResult()
        let stderr = stderrTask.GetAwaiter().GetResult()

        if not (Set.contains child.ExitCode acceptedExitCodes) then
            let digest = Internal.sha256Hex stderr
            Internal.fail $"Git-Import fehlgeschlagen (Exit {child.ExitCode}, stderr-sha256 {digest})."

        child.ExitCode, stdout

    let private runText root arguments acceptedExitCodes =
        let exitCode, bytes = runBytes root arguments acceptedExitCodes

        let text =
            try
                Constants.Utf8NoBom.GetString(bytes)
            with :? Text.DecoderFallbackException ->
                Internal.fail "Git-Ausgabe ist kein gueltiges UTF-8."

        exitCode, text.TrimEnd('\r', '\n')

    let private oneLine root arguments =
        let _, text = runText root arguments (set [ 0 ])

        if String.IsNullOrWhiteSpace(text) || text.Contains('\n') || text.Contains('\r') then
            Internal.fail "Git lieferte keine eindeutige einzeilige Antwort."

        text

    let private requireRepositoryRoot root =
        let locations = Workspace.requireInitialized root
        Workspace.requireSafePath locations "Workspace marker" false locations.Config |> ignore
        let _, prefix = runText locations.Root [ "rev-parse"; "--show-prefix" ] (set [ 0 ])

        if prefix <> "" then
            Internal.fail "Git-Repositorywurzel stimmt nicht mit dem initialisierten Workspace ueberein."

        locations.Root

    let private objectLength objectFormat =
        match objectFormat with
        | "sha1" -> 40
        | "sha256" -> 64
        | value -> Internal.fail $"Nicht unterstuetztes Git-Objektformat '{value}'."

    let private verifyCommit root expectedLength commit =
        requireExactObjectId expectedLength "Commitgrenze" commit |> ignore
        let resolved = oneLine root [ "rev-parse"; "--verify"; commit + "^{commit}" ]
        requireExactObjectId expectedLength "Aufgeloester Commit" resolved |> ignore

        if resolved <> commit then
            Internal.fail "Git-Commitgrenze wurde nicht exakt aufgeloest."

    let private safeRepositoryPath (value: string) =
        if
            String.IsNullOrWhiteSpace(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Contains(':')
            || (value.Split('/') |> Array.exists (fun segment -> segment = "" || segment = "." || segment = ".."))
        then
            Internal.fail "Git-Pfad ist nicht sicher repo-relativ."

        value

    let currentIdentity root =
        let repositoryRoot = requireRepositoryRoot root
        let objectFormat = oneLine repositoryRoot [ "rev-parse"; "--show-object-format" ]
        let expectedLength = objectLength objectFormat
        let head = oneLine repositoryRoot [ "rev-parse"; "--verify"; "HEAD^{commit}" ]
        let tree = oneLine repositoryRoot [ "rev-parse"; "--verify"; "HEAD^{tree}" ]
        requireExactObjectId expectedLength "HEAD-Commit" head |> ignore
        requireExactObjectId expectedLength "HEAD-Tree" tree |> ignore
        let branchExit, branch = runText repositoryRoot [ "symbolic-ref"; "--quiet"; "--short"; "HEAD" ] (set [ 0; 1 ])
        let _, status =
            runText repositoryRoot [ "status"; "--porcelain=v1"; "--untracked-files=all" ] (set [ 0 ])

        { ObjectFormat = objectFormat
          HeadCommit = head
          HeadTreeId = tree
          WorktreeClean = String.IsNullOrEmpty(status)
          BranchRef =
            if branchExit = 0 && not (String.IsNullOrWhiteSpace(branch)) then
                ResearchValue.Known branch
            else
                ResearchValue.Unknown }

    let treeAt root commit =
        let repositoryRoot = requireRepositoryRoot root
        let objectFormat = oneLine repositoryRoot [ "rev-parse"; "--show-object-format" ]
        let expectedLength = objectLength objectFormat
        verifyCommit repositoryRoot expectedLength commit
        let tree = oneLine repositoryRoot [ "rev-parse"; "--verify"; commit + "^{tree}" ]
        requireExactObjectId expectedLength "Commit-Tree" tree

    let fileAtCommit root commit relativePath =
        let repositoryRoot = requireRepositoryRoot root
        let objectFormat = oneLine repositoryRoot [ "rev-parse"; "--show-object-format" ]
        let expectedLength = objectLength objectFormat
        verifyCommit repositoryRoot expectedLength commit
        let safePath = safeRepositoryPath relativePath
        let _, bytes = runBytes repositoryRoot [ "show"; commit + ":" + safePath ] (set [ 0 ])
        bytes

    let requirePathsClean root relativePaths =
        let repositoryRoot = requireRepositoryRoot root
        let safePaths = relativePaths |> List.map safeRepositoryPath
        let _, status = runText repositoryRoot ([ "status"; "--porcelain=v1"; "--untracked-files=all"; "--" ] @ safePaths) (set [ 0 ])

        if not (String.IsNullOrEmpty(status)) then
            Internal.fail "RESEARCH_WORKTREE_DIRTY: frozen research inputs differ from HEAD."

    let requireWorktreeClean root =
        if not (currentIdentity root).WorktreeClean then
            Internal.fail "RESEARCH_WORKTREE_DIRTY: prospective begin requires an exact clean input tree."

    let private parseCommit root expectedLength commit =
        let format = "%H%x00%T%x00%P%x00%cI"
        let _, metadata = runText root [ "show"; "-s"; "--format=" + format; commit ] (set [ 0 ])
        let fields = metadata.Split('\000')

        if fields.Length <> 4 then
            Internal.fail $"Git-Metadaten fuer {commit} sind mehrdeutig."

        let commitId = requireExactObjectId expectedLength "Commit-ID" (fields[0])
        let treeId = requireExactObjectId expectedLength "Tree-ID" (fields[1])

        if commitId <> commit then
            Internal.fail "Git-Show lieferte einen anderen Commit als angefordert."

        let parents =
            if String.IsNullOrEmpty(fields[2]) then
                []
            else
                fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (requireExactObjectId expectedLength "Parent-Commit-ID")
                |> Array.toList

        let commitTime =
            match DateTimeOffset.TryParse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
            | true, parsed -> Internal.utcText parsed
            | _ -> Internal.fail $"Git-Commitzeit fuer {commit} ist ungueltig."

        let _, objectBytes = runBytes root [ "cat-file"; "commit"; commit ] (set [ 0 ])

        { CommitId = commitId
          TreeId = treeId
          ParentCommitIds = parents
          CommitTimeUtc = commitTime
          CommitObjectSha256 = Internal.sha256Hex objectBytes }

    let read root baseCommit headCommit =
        let repositoryRoot = requireRepositoryRoot root
        let objectFormat = oneLine repositoryRoot [ "rev-parse"; "--show-object-format" ]
        let expectedLength = objectLength objectFormat
        verifyCommit repositoryRoot expectedLength baseCommit
        verifyCommit repositoryRoot expectedLength headCommit

        let ancestryExit, _ =
            runBytes
                repositoryRoot
                [ "merge-base"; "--is-ancestor"; baseCommit; headCommit ]
                (set [ 0; 1 ])

        if ancestryExit <> 0 then
            Internal.fail "Die Importgrenzen bilden keine vorwaerts gerichtete Ancestry-Kette."

        let _, revisionText =
            runText
                repositoryRoot
                [ "rev-list"; "--reverse"; "--ancestry-path"; baseCommit + ".." + headCommit ]
                (set [ 0 ])

        let commits =
            if String.IsNullOrEmpty(revisionText) then
                []
            else
                revisionText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                |> Array.toList

        if commits.Length > MaxImportedCommits then
            Internal.fail $"Git-Import umfasst mehr als {MaxImportedCommits} Commits."

        commits
        |> List.iter (fun commit -> requireExactObjectId expectedLength "Rev-list-Commit" commit |> ignore)

        { BaseCommit = baseCommit
          HeadCommit = headCommit
          ObjectFormat = objectFormat
          Commits = commits |> List.map (parseCommit repositoryRoot expectedLength) }

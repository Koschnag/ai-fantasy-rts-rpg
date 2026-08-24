namespace RiftHarness

module ToolchainCheck =

    open System
    open System.IO
    open System.Text.Json

    /// <summary>
    /// Maschinenpruefung der T-010-Native-Pins (AC-T010-01) und des ISA-Vertrags
    /// (AC-T010-08). Rein offline: liest toolchain.lock.json, THIRD_PARTY_NOTICES.md
    /// und die Buildkonfiguration unter src/; es wird nichts geschrieben.
    ///
    /// Vertragsregeln:
    /// - Genau die vier Komponenten sdl3/bgfx/bx/bimg mit vollstaendigen Pins.
    /// - Lizenzen am gepinnten Stand: SDL3=zlib, bgfx/bx/bimg=BSD-2-Clause.
    /// - bgfx-Familie stammt aus einem gemeinsamen Kohortenschluessel.
    /// - Notices enthalten jeden Commit.
    /// - Kein Buildkonfigurationsflag oberhalb der x86-64-v2-Basis
    ///   (-march=native, AVX/AVX2/AVX512/FMA sind verboten; SSE4.2 ist Basis).
    /// </summary>

    type Finding = { Code: string; Detail: string }

    type Report = { Valid: bool; Findings: Finding list }

    let private expectedLicense id =
        match id with
        | "sdl3" -> "zlib"
        | "bgfx"
        | "bx"
        | "bimg" -> "BSD-2-Clause"
        | other -> other

    let private officialUpstream id =
        match id with
        | "sdl3" -> "https://github.com/libsdl-org/SDL"
        | "bgfx" -> "https://github.com/bkaradzic/bgfx"
        | "bx" -> "https://github.com/bkaradzic/bx"
        | "bimg" -> "https://github.com/bkaradzic/bimg"
        | other -> other

    let private requiredIds = [ "bgfx"; "bimg"; "bx"; "sdl3" ]

    let private isLowerHex64 (value: string) =
        value.Length = 64
        && Seq.forall
            (fun character -> (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))
            value

    let private isLowerHex40 (value: string) =
        value.Length = 40
        && Seq.forall
            (fun character -> (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))
            value

    let private parseIsoUtc (text: string) =
        match
            DateTimeOffset.TryParse(
                text,
                Globalization.CultureInfo.InvariantCulture,
                Globalization.DateTimeStyles.RoundtripKind
            )
        with
        | true, parsed when parsed.Offset = TimeSpan.Zero -> Some parsed
        | _ -> None

    /// <summary>Verbotene ISA-anhebende Muster; -msse4.2/x86-64-v2 ist die dokumentierte Basis.</summary>
    let private forbiddenIsaPatterns =
        [| "-march=native"
           "-mavx2"
           "-mavx512"
           "-mavx "
           "-mavx,"
           "-mfma"
           "/arch:AVX" |]

    let private scannedSourceExtensions =
        set [| ".cpp"; ".cc"; ".c"; ".h"; ".hpp"; ".inl"; ".sc"; ".shd" |]

    let private scanRootNames = [| "src" |]

    let private getString (element: JsonElement) (name: string) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if
            element.TryGetProperty(name, &property)
            && property.ValueKind = JsonValueKind.String
        then
            Some(property.GetString())
        else
            None

    let private getBooleanTrue (element: JsonElement) (name: string) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if
            element.TryGetProperty(name, &property)
            && property.ValueKind = JsonValueKind.True
        then
            Some true
        else
            None

    /// <summary>Prueft die Native-Pins gegen den Lockfile-/Notices-/Kohortenvertrag.</summary>
    let check root =
        let findings = ResizeArray<Finding>()
        let lockPath = Path.Combine(root, "toolchain.lock.json")
        let noticesPath = Path.Combine(root, "THIRD_PARTY_NOTICES.md")

        if not (File.Exists(lockPath)) then
            findings.Add(
                { Code = "TOOLCHAIN_LOCK_MISSING"
                  Detail = "toolchain.lock.json fehlt." }
            )
        else
            try
                use document = JsonDocument.Parse(File.ReadAllText(lockPath))
                let mutable componentsElement = Unchecked.defaultof<JsonElement>

                let hasComponents =
                    document.RootElement.TryGetProperty("nativeComponents", &componentsElement)
                    && componentsElement.ValueKind = JsonValueKind.Array

                if not hasComponents then
                    findings.Add(
                        { Code = "NATIVE_COMPONENTS_MISSING"
                          Detail = "toolchain.lock.json enthaelt kein nativeComponents-Array." }
                    )
                else
                    let entries =
                        componentsElement.EnumerateArray()
                        |> Seq.map (fun element ->
                            {| Id = getString element "id" |> Option.defaultValue "<fehlt>"
                               UpstreamUrl = getString element "upstreamUrl"
                               RefType = getString element "refType"
                               Ref = getString element "ref"
                               Commit = getString element "commit"
                               FetchedAtUtc = getString element "fetchedAtUtc"
                               SourceSha256 = getString element "sourceSha256"
                               LicenseSpdx = getString element "licenseSpdx"
                               LicenseVerified = getBooleanTrue element "licenseVerifiedAtRef"
                               NoticesRecorded = getBooleanTrue element "thirdPartyNoticesRecorded"
                               CompatibilityGroup = getString element "compatibilityGroup"
                               CompatibilityKey = getString element "compatibilityKey" |})
                        |> Array.ofSeq

                    let ids = entries |> Array.map (fun entry -> entry.Id) |> Array.sort

                    if List.ofArray ids <> requiredIds then
                        let expected = String.Join(", ", requiredIds)
                        let found = String.Join(", ", ids)

                        findings.Add(
                            { Code = "NATIVE_COMPONENT_IDS_INVALID"
                              Detail = $"nativeComponents muessen genau {expected} enthalten; gefunden: {found}." }
                        )

                    for entry in entries do
                        let fail code detail =
                            findings.Add(
                                { Code = $"{code}_{entry.Id.ToUpperInvariant()}"
                                  Detail = detail }
                            )

                        if entry.Id <> "<fehlt>" then
                            match entry.UpstreamUrl with
                            | Some url when url = officialUpstream entry.Id -> ()
                            | _ -> fail "UPSTREAM_URL_INVALID" "Upstream-URL weicht von der offiziellen Quelle ab."

                            match entry.Commit with
                            | Some commit when isLowerHex40 commit -> ()
                            | _ -> fail "COMMIT_PIN_INVALID" "Commit-Pin fehlt oder ist kein 40-stelliger Hexwert."

                            match entry.RefType with
                            | Some "commit" -> ()
                            | Some "tag" ->
                                match entry.Ref with
                                | Some reference when not (String.IsNullOrWhiteSpace(reference)) -> ()
                                | _ -> fail "REF_PIN_INVALID" "Tag-Pin ohne Tag-Referenz."
                            | _ -> fail "REF_TYPE_INVALID" "refType muss 'tag' oder 'commit' sein."

                            match entry.FetchedAtUtc with
                            | Some fetched when parseIsoUtc fetched |> Option.isSome -> ()
                            | _ -> fail "FETCHED_AT_INVALID" "Abrufdatum fehlt oder ist kein UTC-Zeitstempel."

                            match entry.SourceSha256 with
                            | Some sha256 when isLowerHex64 sha256 -> ()
                            | _ -> fail "SOURCE_SHA256_INVALID" "SHA-256 der Quelle fehlt oder ist malformed."

                            match entry.LicenseSpdx with
                            | Some license when license = expectedLicense entry.Id -> ()
                            | _ ->
                                fail "LICENSE_SPDX_INVALID" "SPDX-Lizenz entspricht nicht der am Pin erwarteten Lizenz."

                            if entry.LicenseVerified <> Some true then
                                fail "LICENSE_UNVERIFIED" "Lizenz wurde nicht als am Pin verifiziert markiert."

                            if entry.NoticesRecorded <> Some true then
                                fail "NOTICES_UNRECORDED" "Drittanbieterhinweise wurden nicht verzeichnet."

                            // Kohortenzugehoerigkeit der bgfx-Familie.
                            if entry.Id = "bgfx" || entry.Id = "bx" || entry.Id = "bimg" then
                                if entry.CompatibilityGroup <> Some "bgfx-family" then
                                    fail
                                        "COHORT_GROUP_INVALID"
                                        "bgfx-Familie muss compatibilityGroup 'bgfx-family' tragen."

                                if String.IsNullOrWhiteSpace(Option.defaultValue "" entry.CompatibilityKey) then
                                    fail "COHORT_KEY_MISSING" "Kompatibilitaetsschluessel fehlt."

                    // Quellhashes gegen den lokalen Native-Cache kreuzpruefen,
                    // wenn dieser vorhanden ist (Entwicklermaschine). Fehlt der
                    // Cache (z. B. Fresh-Checkout), entfaellt die Kreuzpruefung;
                    // der Native-Build prueft die Hashes dort verbindlich selbst.
                    let cacheDirectory = Path.Combine(root, ".ai", "runtime", "cache", "native", "src")

                    if Directory.Exists(cacheDirectory) then
                        for entry in entries do
                            match entry.Commit, entry.SourceSha256 with
                            | Some commit, Some expected when isLowerHex64 expected ->
                                let pattern = entry.Id + "-" + commit + ".tar.gz"
                                let archive = Directory.EnumerateFiles(cacheDirectory, pattern) |> Seq.tryHead

                                match archive with
                                | Some archivePath ->
                                    let actual =
                                        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(archivePath))
                                        |> Convert.ToHexString
                                        |> (fun text -> text.ToLowerInvariant())

                                    if actual <> expected then
                                        findings.Add(
                                            { Code = $"SOURCE_CACHE_MISMATCH_{entry.Id.ToUpperInvariant()}"
                                              Detail =
                                                $"Cache-Archiv {entry.Id} hat SHA-256 {actual}, gepinnt ist {expected}." }
                                        )
                                | None -> ()
                            | _ -> ()

                    // Ein gemeinsamer Kohortenschluessel fuer die Familie.
                    let familyKeys =
                        entries
                        |> Array.filter (fun entry -> entry.Id = "bgfx" || entry.Id = "bx" || entry.Id = "bimg")
                        |> Array.map (fun entry -> entry.CompatibilityKey)
                        |> Array.distinct

                    if familyKeys.Length > 1 then
                        findings.Add(
                            { Code = "BGFX_COHORT_INCONSISTENT"
                              Detail =
                                $"bgfx/bx/bimg stammen aus unterschiedlichen Kohorten ({familyKeys.Length} Schluessel)." }
                        )

                    // Notices enthalten jeden Commit.
                    if File.Exists(noticesPath) then
                        let notices = File.ReadAllText(noticesPath)

                        for entry in entries do
                            match entry.Commit with
                            | Some commit when notices.Contains(commit, StringComparison.Ordinal) -> ()
                            | Some commit ->
                                findings.Add(
                                    { Code = "NOTICES_COMMIT_MISSING"
                                      Detail = $"Commit {commit} fehlt in THIRD_PARTY_NOTICES.md." }
                                )
                            | None -> ()
                    else
                        findings.Add(
                            { Code = "THIRD_PARTY_NOTICES_MISSING"
                              Detail = "THIRD_PARTY_NOTICES.md fehlt." }
                        )
            with :? JsonException as error ->
                findings.Add(
                    { Code = "TOOLCHAIN_LOCK_INVALID"
                      Detail = $"toolchain.lock.json unlesbar: {error.Message}" }
                )

        // ISA-Buildkonfiguration unter src/.
        for scanRootName in scanRootNames do
            let scanRoot = Path.Combine(root, scanRootName)

            if Directory.Exists(scanRoot) then
                for file in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories) do
                    let extension = Path.GetExtension(file).ToLowerInvariant()

                    if Set.contains extension scannedSourceExtensions then
                        let mutable lineNumber = 0

                        try
                            for line in File.ReadLines(file) do
                                lineNumber <- lineNumber + 1

                                for pattern in forbiddenIsaPatterns do
                                    if line.Contains(pattern, StringComparison.Ordinal) then
                                        findings.Add(
                                            { Code = "ISA_FLAG_FORBIDDEN"
                                              Detail = $"{Path.GetRelativePath(root, file)}:{lineNumber}: {pattern}" }
                                        )
                        with :? IOException as error ->
                            findings.Add(
                                { Code = "ISA_SCAN_IO_ERROR"
                                  Detail = $"{file}: {error.Message}" }
                            )
            else
                findings.Add(
                    { Code = "ISA_SCAN_ROOT_MISSING"
                      Detail = $"{scanRootName}/ fehlt im Workspace." }
                )

        let findingList = List.ofSeq findings

        { Valid = findingList.IsEmpty
          Findings = findingList }

    let reportJson report =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteBoolean("valid", report.Valid)
            writer.WriteNumber("findingCount", report.Findings.Length)
            writer.WriteStartArray("findings")

            for finding in report.Findings do
                writer.WriteStartObject()
                writer.WriteString("code", finding.Code)
                writer.WriteString("detail", finding.Detail)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString

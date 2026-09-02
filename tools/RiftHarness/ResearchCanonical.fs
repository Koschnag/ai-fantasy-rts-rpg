namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
module ResearchCanonical =
    let private compareCodePoints (left: string) (right: string) =
        let mutable leftIndex = 0
        let mutable rightIndex = 0
        let mutable result = 0

        while result = 0 && leftIndex < left.Length && rightIndex < right.Length do
            let leftCodePoint = Char.ConvertToUtf32(left, leftIndex)
            let rightCodePoint = Char.ConvertToUtf32(right, rightIndex)
            result <- compare leftCodePoint rightCodePoint
            leftIndex <- leftIndex + if Char.IsSurrogatePair(left, leftIndex) then 2 else 1
            rightIndex <- rightIndex + if Char.IsSurrogatePair(right, rightIndex) then 2 else 1

        if result <> 0 then result else compare (left.Length - leftIndex) (right.Length - rightIndex)

    let private propertyComparer =
        { new IComparer<JsonProperty> with
            member _.Compare(left, right) = compareCodePoints left.Name right.Name }

    let private failInvalid message = Internal.fail $"RESEARCH_JSON_INVALID: {message}"

    let rec private validateElement (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Object ->
            let names = HashSet<string>(StringComparer.Ordinal)

            for property in element.EnumerateObject() do
                if not (names.Add(property.Name)) then
                    failInvalid $"Duplicate property '{property.Name}'."

                validateElement property.Value
        | JsonValueKind.Array ->
            for item in element.EnumerateArray() do
                validateElement item
        | JsonValueKind.Null
        | JsonValueKind.Undefined -> failInvalid "JSON null and undefined are forbidden; use literal 'unknown'."
        | _ -> ()

    let private canonicalNumber (element: JsonElement) =
        let mutable signed = 0L
        let mutable unsigned = 0UL
        let mutable decimalValue = 0M

        if element.TryGetInt64(&signed) then
            signed.ToString(CultureInfo.InvariantCulture)
        elif element.TryGetUInt64(&unsigned) then
            unsigned.ToString(CultureInfo.InvariantCulture)
        elif element.TryGetDecimal(&decimalValue) then
            if decimalValue = 0M then
                "0"
            else
                decimalValue.ToString("G29", CultureInfo.InvariantCulture).Replace("E+", "e").Replace("E", "e")
        else
            failInvalid $"Number is outside the supported exact decimal range: {element.GetRawText()}"

    let rec private writeCanonical (writer: Utf8JsonWriter) (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Object ->
            writer.WriteStartObject()

            let properties = element.EnumerateObject() |> Seq.toArray
            Array.sortInPlaceWith (fun left right -> propertyComparer.Compare(left, right)) properties

            for property in properties do
                writer.WritePropertyName(property.Name)
                writeCanonical writer property.Value

            writer.WriteEndObject()
        | JsonValueKind.Array ->
            writer.WriteStartArray()

            for item in element.EnumerateArray() do
                writeCanonical writer item

            writer.WriteEndArray()
        | JsonValueKind.String -> writer.WriteStringValue(element.GetString())
        | JsonValueKind.Number -> writer.WriteRawValue(canonicalNumber element, true)
        | JsonValueKind.True -> writer.WriteBooleanValue(true)
        | JsonValueKind.False -> writer.WriteBooleanValue(false)
        | JsonValueKind.Null
        | JsonValueKind.Undefined -> failInvalid "JSON null and undefined are forbidden; use literal 'unknown'."
        | kind -> failInvalid $"Unsupported JSON kind: {kind}."

    let canonicalizeElement (element: JsonElement) =
        validateElement element
        Internal.jsonBytes false (fun writer -> writeCanonical writer element)

    let canonicalizeJson (text: string) =
        try
            use document = JsonDocument.Parse(text)
            canonicalizeElement document.RootElement
        with :? JsonException as error ->
            failInvalid error.Message

    let canonicalizeWithoutTopLevelProperty propertyName (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            failInvalid "The event envelope must be an object."

        validateElement element

        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            let properties =
                element.EnumerateObject()
                |> Seq.filter (fun property -> property.Name <> propertyName)
                |> Seq.toArray

            Array.sortInPlaceWith (fun left right -> propertyComparer.Compare(left, right)) properties

            for property in properties do
                writer.WritePropertyName(property.Name)
                writeCanonical writer property.Value

            writer.WriteEndObject())

    let eventHash (element: JsonElement) =
        canonicalizeWithoutTopLevelProperty "eventHash" element |> Internal.sha256Hex

    let appendLf (canonicalBytes: byte array) =
        Array.append canonicalBytes [| 0x0Auy |]

    let private emailPattern =
        Regex(
            "(?i)[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100.0)
        )

    let private ipv4Pattern =
        Regex(
            "(?:[0-9]{1,3}\\.){3}[0-9]{1,3}",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100.0)
        )

    let private absoluteUnixPathPattern =
        Regex(
            "/(?:Users|home)/[^\\s\"']+",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100.0)
        )

    let private absoluteWindowsPathPattern =
        Regex(
            "(?i)[A-Z]:\\\\(?:Users\\\\)?[^\\s\"']+",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100.0)
        )

    let private redactString (policy: RedactionPolicy) (value: string) =
        let mutable changed = false
        let mutable result = Internal.redactText policy value

        if result <> value then
            changed <- true
            result <- result.Replace("[REDACTED]", "[REDACTED:secret]")

        let replace (pattern: Regex) (replacement: string) =
            let next = pattern.Replace(result, replacement)

            if next <> result then
                changed <- true
                result <- next

        replace emailPattern "[REDACTED:email]"
        replace ipv4Pattern "[REDACTED:ip]"
        replace absoluteUnixPathPattern "[REDACTED:path]"
        replace absoluteWindowsPathPattern "[REDACTED:path]"
        result, changed

    let redactScalar policy value = redactString policy value

    let rec private writeRedacted policy (writer: Utf8JsonWriter) (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Object ->
            writer.WriteStartObject()
            let properties = element.EnumerateObject() |> Seq.toArray
            Array.sortInPlaceWith (fun left right -> propertyComparer.Compare(left, right)) properties
            let mutable changed = false

            for property in properties do
                writer.WritePropertyName(property.Name)

                if Internal.isSensitivePropertyWithPolicy policy property.Name then
                    writer.WriteStringValue("[REDACTED:secret]")
                    changed <- true
                else
                    changed <- writeRedacted policy writer property.Value || changed

            writer.WriteEndObject()
            changed
        | JsonValueKind.Array ->
            writer.WriteStartArray()
            let mutable changed = false

            for item in element.EnumerateArray() do
                changed <- writeRedacted policy writer item || changed

            writer.WriteEndArray()
            changed
        | JsonValueKind.String ->
            let redacted, changed = redactString policy (element.GetString())
            writer.WriteStringValue(redacted)
            changed
        | JsonValueKind.Number ->
            writer.WriteRawValue(canonicalNumber element, true)
            false
        | JsonValueKind.True ->
            writer.WriteBooleanValue(true)
            false
        | JsonValueKind.False ->
            writer.WriteBooleanValue(false)
            false
        | JsonValueKind.Null
        | JsonValueKind.Undefined -> failInvalid "JSON null and undefined are forbidden; use literal 'unknown'."
        | kind -> failInvalid $"Unsupported JSON kind: {kind}."

    let redactAndCanonicalizePayload policy (element: JsonElement) =
        validateElement element
        let mutable redacted = false

        let bytes =
            Internal.jsonBytes false (fun writer -> redacted <- writeRedacted policy writer element)

        bytes, redacted

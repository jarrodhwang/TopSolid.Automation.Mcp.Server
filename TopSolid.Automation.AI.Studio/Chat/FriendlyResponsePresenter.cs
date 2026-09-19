using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>
/// Display only. Execution, approval authority, model history and diagnostic receipts
/// always retain their original identities. Aliases come only from server receipts.
/// </summary>
public sealed class FriendlyResponsePresenter
{
    private const int MaximumAliases = 4096;
    private readonly object gate = new();
    // Document revisions are opaque PDM strings, not necessarily GUIDs. Element
    // numbers are document-local; CAM parameter names are local to their owner.
    private sealed record Identity(string Kind, string Scope, string Value);
    private readonly Dictionary<Identity, string?> names = new();
    private readonly Dictionary<string, string?> elementScopes = new(StringComparer.Ordinal);
    private bool unscopedLookupSaturated;
    private static readonly Regex GuidText = new(
        @"(?<![\da-f])(?:urn:uuid:)?[({]?(?:[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}|[\da-f]{32})[)}]?(?![\da-f])|\{\s*0x[\da-f]{8}\s*,\s*0x[\da-f]{4}\s*,\s*0x[\da-f]{4}\s*,\s*\{(?:\s*0x[\da-f]{2}\s*,){7}\s*0x[\da-f]{2}\s*\}\s*\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string IdentityWords = @"(?:(?:Requested\s*)?PDM\s*(?:Object\s*)?IDs?|(?:Pdm\s*)?(?:Major|Minor)\s*Revision\s*IDs?|(?:Source\s*|Original\s*|Latest\s*|Definition\s*|Family\s*)?Document\s*IDs?|Element\s*(?:Ex|Item)?\s*IDs?|(?:Project|Owner|Template|Preparation|Plan|Parameter|CamParameter|Entity|Entities|Operation|Part|Tool|Machine|Holder|Pocket|Universal|CurrentUniversal|Type)\s*IDs?|(?:Type\s*|EnumerationDefinition\s*)?GUIDs?|UIDs?|IDs?|Confirmation\s*Token)";
    private static readonly Regex IdentityRow = new(@"^\s*(?:[-*•]\s*)?[`""']*" + IdentityWords + @"[`""']*\s*[:=]\s*[^\r\n]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex IdentityLabel = new(@"\b" + IdentityWords + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string ReferenceAtom = @"[`""']*[\w{}()/.:&@|\-]+[`""']*";
    private static readonly Regex ReferencePair = new(@"\b" + IdentityWords + @"\b[`""'*]*\s*(?:[:=#][`""'*]*\s*|\s+(?=[`""']*(?:[0-9{]|[a-f0-9]{8}-)))(?<values>" + ReferenceAtom + @"(?:[ \t]*,[ \t]*[`""']*[0-9]+[`""']*)*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ContextualNumber = new(@"(?<label>\b(?:element(?:\s*ex)?|entit(?:y|ies)|operation|part|tool|machine|holder|pocket|parameter)s?\b|요소|엔티티|엘리먼트|오퍼레이션|공정|작업|파트|부품|공구|머신|기계|홀더|포켓|파라미터|매개변수)(?<separator>[ \t]*(?:[:=#][ \t]*|\([ \t]*|[ \t]+))(?<value>[`""']*\d+[`""']*)(?!\d|\.\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CodeNumber = new(@"`(?<value>\d+)`", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MeasurementUnit = new(@"^\s*(?:mm|cm|m|in|inch|inches|ft|rpm|r/min|mm/min|m/min|mm/rev|deg|rad|s|ms|kg|g|%|°|회전|밀리미터)(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MeasurementLabel = new(@"(?:\b(?:width|height|length|diameter|radius|depth|distance|angle|speed|feed(?:\s*rate)?|rpm|count|quantity|value|current(?:\s*value)?|requested(?:\s*value)?|offset|tolerance)|폭|높이|길이|직경|반경|깊이|거리|각도|속도|이송|회전수|개수|수량|값)[`*:\s=]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MarkdownLink = new(@"!?\[([^\]\r\n]*)\]\(([^)\r\n]*)\)", RegexOptions.Compiled);
    private static readonly Regex WordBoundary = new(@"(?<=[a-z0-9])(?=[A-Z])|[_-]+", RegexOptions.Compiled);
    private static readonly Regex IdentitySuffix = new(@"(?:Id|Ids|ID|IDs|Guid|Guids|GUID|GUIDs)$|(?:^|[_ -])(?:id|ids|guid|guids)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> HiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirmationToken", "planId", "idKind", "valueIdKind", "identifiers", "moniker",
        "schemaVersion", "jsonrpc", "protocolVersion", "operationType", "operationTypeError", "operationTypeUnavailable"
    };

    public void Clear()
    {
        lock (gate) { names.Clear(); elementScopes.Clear(); unscopedLookupSaturated = false; }
    }

    public void ObserveToolResult(string json, JObject? arguments = null, string? toolName = null)
    {
        if (!TryJson(json, out var value)) return;
        lock (gate)
        {
            Learn(value!, 0);
            if (arguments == null || (value as JObject)?["isError"] is JValue { Type: JTokenType.Boolean } error && error.Value<bool>()) return;
            // Arguments identify the server read; they never supply a display name.
            // In particular a text parameter value is NOT an element name.
            foreach (var result in ReceiptObjects(value!))
            {
                var label = Name(result);
                if (label == null && toolName is "topsolid_get_element_name" or "topsolid_get_element_friendly_name" or "topsolid_get_document_name" or "topsolid_get_pdm_object_name")
                    label = StringValue(result["value"]);
                if (!ValidName(label)) continue;
                var target = arguments["element"] as JObject ?? arguments["elementEx"] as JObject;
                if (target != null && ElementIdentity(target) is { } identity && !IsCamParameter(result)) Remember(identity, label);
                else if (toolName is "topsolid_get_document_name" or "topsolid_get_pdm_object_name")
                    foreach (var property in arguments.Properties().Where(p => Normalize(p.Name) is "documentid" or "pdmobjectid"))
                        if (GlobalIdentity(property.Value) is { } global) Remember(global, label);
            }
        }
    }

    public void Observe(JToken value)
    {
        lock (gate) Learn(value, 0);
    }

    public string Present(string text, bool developerMode = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (developerMode) return text;
        text = CamDisplay.Text(text);
        lock (gate)
        {
            var trimmed = text.Trim();
            if (IntegerText(trimmed) && IsKnownNumber(trimmed)) return FriendlyReference(trimmed);
            if (TryJson(trimmed, out var data)) return Render(Clean(Unwrap(data!)));
            // Receipts sometimes follow an explanation, or sit inside a fenced code block.
            // Parse balanced objects rather than removing whole lines (which can hide failures).
            var formatted = ReplaceObjects(text);
            formatted = string.Join("\n", formatted.Replace("\r\n", "\n").Split('\n').Select(line =>
            {
                if (!IdentityRow.IsMatch(line)) return line;
                // A reference-only row adds nothing to a friendly response. Keep any
                // explanatory suffix, including failure details, instead of discarding it.
                var colon = line.IndexOfAny([':', '=']);
                var value = line[(colon + 1)..].Trim().Trim('`', '"', '\'');
                if (Guid.TryParse(value, out _) || !value.Any(char.IsWhiteSpace)) return "";
                return line;
            }));
            var result = Scrub(formatted).Trim();
            return result.Length > 0 ? result : Scrub(text).Trim() is { Length: > 0 } reference ? reference : StudioStrings.Get("Response.UnnamedItem");
        }
    }

    /// <summary>Detached, friendly review facts. Never send this object back to MCP.</summary>
    public JObject Review(JObject proposal)
    {
        lock (gate)
        {
            var review = Clean(proposal);
            return review as JObject ?? new JObject { [StudioStrings.Get("Response.Item")] = review };
        }
    }

    private void Learn(JToken value, int depth, string? documentScope = null, string? propertyKind = null, bool allowNames = true)
    {
        if (depth > 48) return;
        if (value is JObject obj)
        {
            var name = allowNames ? Name(obj) : null;
            var scope = Scalar(obj["documentId"]) ?? documentScope;
            var parameter = ParameterIdentity(obj, propertyKind);
            var element = ElementIdentity(obj);
            var documentSummary = IsDocumentSummary(obj);
            var hasHandle = element != null || obj.Properties().Any(p => p.Value is JObject h && ElementIdentity(h) != null ||
                IsIdentity(p.Name) && Scalar(p.Value) is { } scalar && IntegerText(scalar));
            var parameterName = documentSummary ? allowNames ? StringValue(obj["parameterName"]) : null : name;
            if (parameter != null) Remember(parameter, parameterName == parameter.Value && names.GetValueOrDefault(parameter) != null ? null : parameterName);
            else if (element != null) Remember(element, name);
            foreach (var property in obj.Properties())
            {
                var key = Normalize(property.Name);
                if (property.Value is JObject handle)
                {
                    var pairedName = allowNames ? StringValue(obj[property.Name + "Name"]) : null;
                    if (ParameterIdentity(handle, property.Name) is { } parameterKey)
                        Remember(parameterKey, pairedName);
                    else if (ElementIdentity(handle) is { } handleKey)
                    {
                        var ownHandle = key is "element" or "elementex" or "entity" or "operation";
                        // A CAM ParameterId includes its owner's element; its parameter
                        // name must never rename that operation (hundreds can share it).
                        var label = ValidName(pairedName) ? pairedName : parameter == null && ownHandle && !documentSummary ? name : null;
                        Remember(handleKey, label);
                    }
                }
                if (IsIdentity(property.Name) && property.Value is JValue scalar)
                {
                    var pairedName = allowNames ? StringValue(obj[Regex.Replace(property.Name, @"(?:Ids?|Guids?)$", "Name", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]) : null;
                    // An entity name is not its containing document's name. Never map a
                    // parent's document ID from a document-local element handle.
                    var ownIdentity = key is "id" or "pdmobjectid" or "documentid";
                    if (key == "documentid" && hasHandle && !documentSummary) ownIdentity = false;
                    var label = ValidName(pairedName) ? pairedName : ownIdentity ? name : null;
                    if (key == "id" && element != null) continue;
                    if (IntegerText(scalar.ToString()) && scope != null)
                        Remember(new Identity("element", Canonical(scope), scalar.ToString()), label);
                    else if (!IntegerText(scalar.ToString()) && GlobalIdentity(scalar) is { } global) Remember(global, label);
                }
                if (property.Name.Equals("text", StringComparison.OrdinalIgnoreCase) && TryJson(property.Value.ToString(), out var embedded)) Learn(embedded!, depth + 1, scope, allowNames: allowNames);
                // A server preview repeats the requested arguments. A proposed rename
                // is not a name observed in TopSolid until execution succeeds.
                else Learn(property.Value, depth + 1, scope, parameter != null && key == "element" ? "parameterOwner" : property.Name, allowNames && key != "arguments");
            }
        }
        else if (value is JArray array) foreach (var child in array) Learn(child, depth + 1, documentScope, propertyKind, allowNames);
    }

    private void Remember(Identity key, string? name)
    {
        if (key.Kind == "element")
        {
            if (elementScopes.TryGetValue(key.Value, out var scope))
            { if (scope != key.Scope) elementScopes[key.Value] = null; }
            else if (elementScopes.Count < MaximumAliases) elementScopes[key.Value] = key.Scope;
            else unscopedLookupSaturated = true;
        }
        if (names.Count >= MaximumAliases && !names.ContainsKey(key)) names.Remove(names.Keys.First());
        // Keep unnamed observed handles too, so an unresolved handle in another
        // document cannot accidentally receive the first document's element name.
        if (ValidName(name) || !names.ContainsKey(key)) names[key] = ValidName(name) ? name : null;
    }

    private JToken Clean(JToken value, string? documentScope = null, string? propertyKind = null)
    {
        if (value is JObject obj)
        {
            var scope = Scalar(obj["documentId"]) ?? documentScope;
            if (ElementIdentity(obj) is { } key && obj.Properties().All(p => IsIdentity(p.Name) || Normalize(p.Name) is "element" or "elementex"))
                return new JValue(names.GetValueOrDefault(key) ?? StudioStrings.Get("Response.UnnamedItem"));
            if (ParameterIdentity(obj, propertyKind) is { } parameter && obj.Properties().All(p => IsIdentity(p.Name) || Normalize(p.Name) is "element" or "name"))
                return new JValue(names.GetValueOrDefault(parameter) ?? ParameterDisplayName(StringValue(obj["name"])) ?? StudioStrings.Get("Response.UnnamedItem"));
            var output = new JObject();
            var preferredName = Name(obj);
            foreach (var property in obj.Properties())
            {
                if (HiddenKeys.Contains(property.Name)) continue;
                if (property.Name.Equals("toolName", StringComparison.OrdinalIgnoreCase) && StringValue(property.Value) is { } toolName &&
                    (toolName.StartsWith("topsolid_", StringComparison.Ordinal) || toolName.StartsWith("studio_", StringComparison.Ordinal))) continue;
                // Native friendly/localized names take priority over system names.
                if (Normalize(property.Name) is "name" or "internalname" or "friendlyname" or "displayname" or "localizedname")
                {
                    if (preferredName != null && output.Property("name") == null) output["name"] = Scrub(preferredName);
                    continue;
                }
                if (IsIdentity(property.Name))
                {
                    // Preserve different referenced objects (source/owner/document) under
                    // different friendly labels; never collapse them into one "Item" row.
                    if (ResolveReference(property.Value, scope) is { } label && label != preferredName)
                        AddField(output, ReferenceLabel(property.Name), new JValue(label));
                    else if (property.Value is JArray references && references.Count > 0)
                        AddField(output, ReferenceLabel(property.Name), new JArray(references.Select(v => ResolveReference(v, scope) ?? StudioStrings.Get("Response.UnnamedItem"))));
                    else if (preferredName == null && property.Value.Type != JTokenType.Null)
                        AddField(output, ReferenceLabel(property.Name), new JValue(StudioStrings.Get("Response.UnnamedItem")));
                    continue;
                }
                if (IsObjectReference(property.Name) && Scalar(property.Value) is { } reference && IntegerText(reference))
                {
                    AddField(output, FriendlyLabel(property.Name), new JValue(ResolveReference(property.Value, scope) ?? StudioStrings.Get("Response.UnnamedItem")));
                    continue;
                }
                AddField(output, Scrub(property.Name), Clean(property.Value, scope, property.Name));
            }
            if (!output.HasValues && obj.HasValues) output[StudioStrings.Get("Response.Item")] = StudioStrings.Get("Response.UnnamedItem");
            return output;
        }
        if (value is JArray array) return new JArray(array.Select(v => Clean(v, documentScope, propertyKind)));
        if (value.Type is JTokenType.String or JTokenType.Guid) return new JValue(Scrub(value.ToString()));
        return value.DeepClone();
    }

    private string Scrub(string text)
    {
        // Do not leave technical IDs hidden inside Markdown link destinations.
        text = MarkdownLink.Replace(text, m => IsTechnicalLink(m.Groups[2].Value)
            ? m.Groups[1].Value : m.Value);
        text = ScrubIdentityTableColumns(text);
        text = ReferencePair.Replace(text, m =>
        {
            return string.Join(", ", m.Groups["values"].Value.Split(',').Select(FriendlyReference));
        });
        text = ContextualNumber.Replace(text, m => IsMeasurement(text, m) || names.Values.Contains(m.Value) ? m.Value :
            m.Groups["label"].Value + m.Groups["separator"].Value + FriendlyReference(m.Groups["value"].Value));
        text = CodeNumber.Replace(text, m => IsKnownNumber(m.Groups["value"].Value) && !IsMeasurement(text, m)
            ? FriendlyReference(m.Groups["value"].Value) : m.Value);
        // Opaque PDM revision strings can appear without an ID label. Unlike local
        // integers they cannot be dimensions; replace only actually observed ones.
        foreach (var identity in names.Keys.Where(k => k.Kind == "global" && !Guid.TryParse(k.Value, out _)).OrderByDescending(k => k.Value.Length))
            text = Regex.Replace(text, @"(?<![\w&])" + Regex.Escape(identity.Value) + @"(?![\w&])", _ => names[identity] ?? StudioStrings.Get("Response.UnnamedItem"), RegexOptions.CultureInvariant);
        text = IdentityLabel.Replace(text, m => m.Value.Equals("ID", StringComparison.OrdinalIgnoreCase) || m.Value.Equals("IDs", StringComparison.OrdinalIgnoreCase)
            ? m.Value : StudioStrings.Get("Response.Item"));
        return GuidText.Replace(text, m => Resolve(m.Value) ?? StudioStrings.Get("Response.UnnamedItem"));
    }

    private string? Resolve(string text)
    {
        text = text.Trim().Trim('`', '"', '\'');
        if (IntegerText(text))
        {
            return !unscopedLookupSaturated && elementScopes.TryGetValue(text, out var scope) && scope != null
                ? names.GetValueOrDefault(new Identity("element", scope, text)) : null;
        }
        return names.GetValueOrDefault(new Identity("global", "", Canonical(text)));
    }

    private string? ResolveReference(JToken value, string? scope)
    {
        if (value is JObject obj)
        {
            var identity = ParameterIdentity(obj) ?? ElementIdentity(obj);
            return identity == null ? null : names.GetValueOrDefault(identity);
        }
        var text = Scalar(value);
        if (text == null) return null;
        return IntegerText(text) && scope != null ? names.GetValueOrDefault(new Identity("element", Canonical(scope), text)) : Resolve(text);
    }

    private string FriendlyReference(string value)
    {
        var core = value.Trim().Trim('`', '"', '\'');
        var suffix = "";
        // A table's identity cell may also contain a failure explanation.
        var whitespace = core.IndexOfAny([' ', '\t']);
        if (whitespace > 0 && !Guid.TryParse(core, out _))
        { suffix = core[whitespace..]; core = core[..whitespace].TrimEnd('`', '"', '\''); }
        while (core.Length > 0 && (core[^1] is '.' or ';' or ':' || core.EndsWith(')') && !core.StartsWith('(') || core.EndsWith('}') && !core.StartsWith('{')))
        { suffix = core[^1] + suffix; core = core[..^1]; }
        return (Resolve(core) ?? StudioStrings.Get("Response.UnnamedItem")) + suffix;
    }

    private bool IsKnownNumber(string text) => elementScopes.ContainsKey(text);

    private static bool IsMeasurement(string text, Match match)
    {
        if (MeasurementUnit.IsMatch(text[(match.Index + match.Length)..])) return true;
        var start = match.Index > 0 ? text.LastIndexOf('\n', match.Index - 1) + 1 : 0;
        var prefix = text[start..match.Index];
        // Identity columns were already converted. Other table columns retain
        // numeric values even when their value happens to equal an element ID.
        return prefix.TrimStart().StartsWith('|') || MeasurementLabel.IsMatch(prefix);
    }

    private bool IsTechnicalLink(string target)
    {
        string decoded;
        try { decoded = Uri.UnescapeDataString(target); }
        catch (UriFormatException) { decoded = target; }
        return decoded.StartsWith("topsolid:", StringComparison.OrdinalIgnoreCase) || GuidText.IsMatch(decoded) || IdentityLabel.IsMatch(decoded) ||
            names.Keys.Any(k => k.Kind == "global" && decoded.Contains(k.Value, StringComparison.Ordinal));
    }

    private string ScrubIdentityTableColumns(string text)
    {
        HashSet<int>? identityColumns = null;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var row = 0; row < lines.Length; row++)
        {
            if (!lines[row].TrimStart().StartsWith('|')) { identityColumns = null; continue; }
            var cells = lines[row].Split('|');
            if (identityColumns == null)
            {
                identityColumns = cells.Select((cell, index) => (cell: cell.Trim().Trim('`', '*', ' '), index))
                    .Where(c => IsIdentity(c.cell) || Regex.IsMatch(c.cell, @"\b(?:IDs?|GUIDs?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    .Select(c => c.index).ToHashSet();
                foreach (var column in identityColumns) cells[column] = " " + ReferenceLabel(cells[column].Trim().Trim('`', '*', ' ')) + " ";
            }
            else
                foreach (var column in identityColumns.Where(i => i < cells.Length))
                    if (cells[column].Any(char.IsLetterOrDigit)) cells[column] = " " + string.Join(", ", cells[column].Split(',').Select(FriendlyReference)) + " ";
            lines[row] = string.Join('|', cells);
        }
        return string.Join('\n', lines);
    }

    private string ReplaceObjects(string text)
    {
        var result = new StringBuilder();
        var copied = 0; var start = -1; var depth = 0; var quoted = false; var escaped = false;
        for (var end = 0; end < text.Length; end++)
        {
            var character = text[end];
            if (start < 0)
            {
                if (character == '{') { start = end; depth = 1; }
                continue;
            }
            if (escaped) { escaped = false; continue; }
            if (quoted && character == '\\') { escaped = true; continue; }
            if (character == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (character == '{') depth++;
            if (character != '}' || --depth != 0) continue;
            var fragment = text[start..(end + 1)];
            if (TryJson(fragment, out var value))
            {
                result.Append(text, copied, start - copied).Append(Render(Clean(Unwrap(value!))));
                copied = end + 1;
            }
            start = -1;
        }
        return result.Append(text, copied, text.Length - copied).ToString();
    }

    private static JToken Unwrap(JToken value)
    {
        if (value is not JObject obj || obj["content"] is not JArray content ||
            (obj["structuredContent"] == null && obj["isError"]?.Type != JTokenType.Boolean)) return value;
        var output = new JObject();
        if (obj["isError"]?.Type == JTokenType.Boolean && obj["isError"]!.Value<bool>()) output["error"] = true;
        var structured = obj["structuredContent"];
        if (structured != null) output["result"] = structured.DeepClone();
        var extra = new JArray();
        foreach (var block in content.OfType<JObject>())
        {
            if (StringValue(block["type"]) != "text" || StringValue(block["text"]) is not { } text) continue;
            if (TryJson(text, out var embedded)) { if (!JToken.DeepEquals(embedded, structured)) extra.Add(embedded!); }
            else extra.Add(text);
        }
        if (extra.HasValues) output["details"] = extra;
        return output;
    }

    internal static string Render(JToken value, int indent = 0)
    {
        var prefix = new string(' ', indent);
        if (value is JObject obj) return string.Join("\n", obj.Properties().Select(p =>
            prefix + FriendlyLabel(p.Name) + ":" + (p.Value is JContainer ? "\n" + Render(p.Value, indent + 2) : " " + Render(p.Value))));
        if (value is JArray array) return string.Join("\n", array.Select(v => prefix + "• " + Render(v, indent + 2).TrimStart()));
        if (value.Type == JTokenType.Boolean) return StudioStrings.Get(value.Value<bool>() ? "Response.Yes" : "Response.No");
        return value.Type == JTokenType.Null ? "—" : value.ToString();
    }

    internal static string FriendlyLabel(string key)
    {
        var words = WordBoundary.Replace(key, " ");
        return StudioStrings.Text(words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..]);
    }

    private static string Normalize(string key) => string.Concat(key.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static void AddField(JObject output, string label, JToken value)
    {
        var unique = label;
        for (var suffix = output.Count + 1; output.Property(unique) != null; suffix++) unique = $"{label} ({suffix})";
        output[unique] = value;
    }
    private static string ReferenceLabel(string key)
    {
        var normalized = Normalize(key);
        return normalized is "id" or "ids" or "pdmobjectid" or "pdmobjectids" ? StudioStrings.Get("Response.Item") :
            FriendlyLabel(Regex.Replace(key, @"(?:Ids?|Guids?)$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
    private static bool IsIdentity(string key) => IdentitySuffix.IsMatch(key) || (IdentityLabel.Match(key) is { Success: true } match && match.Length == key.Length);
    private static bool IsObjectReference(string key) => Normalize(key) is "element" or "elementex" or "entity" or "operation" or "part" or "tool" or "machine" or "holder" or "pocket" or "parameter";
    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 512 && !GuidText.IsMatch(name) && !IntegerText(name);
    // elementName/operationName/parameterName describe related references, not
    // this object's own name (a CAM approval target remains its document).
    private static string? Name(JObject obj) => new[] { "friendlyName", "displayName", "localizedName", "name", "documentName", "projectName" }
        .Select(key => StringValue(obj[key])).FirstOrDefault(ValidName);
    private static string? StringValue(JToken? value) => value?.Type is JTokenType.String or JTokenType.Guid ? value.ToString() : null;
    private static string? Scalar(JToken? value) => value?.Type is JTokenType.String or JTokenType.Guid or JTokenType.Integer ? value.ToString() : null;
    private static bool IntegerText(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);
    private static string Canonical(string text)
    {
        if (text.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase)) text = text[9..];
        return Guid.TryParse(text, out var guid) ? guid.ToString("N") : text;
    }
    private static Identity? GlobalIdentity(JToken? value) => Scalar(value) is { Length: > 0 and <= 1024 } text && !IntegerText(text)
        ? new Identity("global", "", Canonical(text)) : null;
    private static bool IsDocumentSummary(JObject obj) => Scalar(obj["documentId"]) != null &&
        Scalar(obj["id"] ?? obj["itemId"]) == null && obj["element"] == null && obj["elementEx"] == null;
    private static Identity? ElementIdentity(JObject obj)
    {
        if (Scalar(obj["documentId"]) is { } document && Scalar(obj["id"] ?? obj["itemId"]) is { } id && IntegerText(id))
            return new Identity("element", Canonical(document), id);
        if (obj["element"] is JObject element) return ElementIdentity(element);
        if (obj["elementEx"] is JObject extended) return ElementIdentity(extended);
        if (GlobalIdentity(obj["preparationId"]) is { } preparation) return preparation;
        return null;
    }
    private static bool IsCamParameter(JObject obj) => obj["parameter"] is JObject || obj["parameterId"] is JObject ||
        StringValue(obj["name"]) is { } name && name.IndexOfAny(['@', '|']) >= 0 && ElementIdentity(obj) != null;
    private static Identity? ParameterIdentity(JObject obj, string? propertyKind = null)
    {
        if (obj["parameter"] is JObject parameter) return ParameterIdentity(parameter, "parameter");
        if (obj["parameterId"] is JObject parameterId) return ParameterIdentity(parameterId, "parameter");
        if (propertyKind is not ("parameter" or "parameterId") && !IsCamParameter(obj)) return null;
        if (ElementIdentity(obj) is { } owner && StringValue(obj["name"]) is { } name)
            return new Identity("parameter", owner.Scope + "\0" + owner.Value, name);
        return null;
    }
    private static string? ParameterDisplayName(string? name) => ValidName(name) ? FriendlyLabel(name!.Replace('@', ' ').Replace('|', ' ')) : null;
    private static IEnumerable<JObject> ReceiptObjects(JToken value)
    {
        if (value is not JObject obj) yield break;
        if (obj["structuredContent"] is JObject structured) yield return structured;
        if (obj["content"] is JArray content)
            foreach (var block in content.OfType<JObject>())
                if (StringValue(block["text"]) is { } text && TryJson(text, out var embedded) && embedded is JObject result) yield return result;
        if (obj["content"] == null && obj["structuredContent"] == null) yield return obj;
    }
    private static bool TryJson(string text, out JToken? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text) || text.TrimStart()[0] is not ('{' or '[')) return false;
        try
        {
            using var reader = new JsonTextReader(new System.IO.StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 48 };
            value = JToken.Load(reader);
            return !reader.Read();
        }
        catch (JsonException) { return false; }
    }
}

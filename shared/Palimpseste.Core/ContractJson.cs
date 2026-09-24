using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Palimpseste.Core
{
    public sealed class ValidationIssue
    {
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public ValidationIssue(string code, string path, string message)
        {
            Code = code;
            Path = path;
            Message = message;
        }
        public override string ToString() => Code + " " + Path + ": " + Message;
    }

    public static class ContractJson
    {
        public const int MaxDocumentBytes = 8 * 1024 * 1024;
        public const int MaxDepth = 64;
        private static readonly string[] KnownSchemas = {
            "spell-description", "spell-plan", "geometry", "compiled-spell",
            "spell-blueprint-v2", "spell-plan-v2", "compiled-spell-v2"
        };

        public static JObject ParseStrict(byte[] utf8, int maxBytes = MaxDocumentBytes)
        {
            if (utf8 == null || utf8.Length == 0 || utf8.Length > maxBytes)
                throw new JsonException("JSON size outside allowed bounds");
            // A throwing UTF-8 decoder prevents replacement characters from hiding malformed bytes.
            string value = new UTF8Encoding(false, true).GetString(utf8);
            RejectComments(value);
            using (var reader = new JsonTextReader(new StringReader(value)) {
                MaxDepth = MaxDepth, DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            })
            {
                var settings = new JsonLoadSettings {
                    CommentHandling = CommentHandling.Ignore,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Ignore
                };
                var token = JToken.ReadFrom(reader, settings);
                if (reader.Read()) throw new JsonException("Trailing JSON content");
                if (!(token is JObject result)) throw new JsonException("Expected JSON object");
                return result;
            }
        }

        private static void RejectComments(string value)
        {
            bool quoted = false, escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '/' && i + 1 < value.Length && (value[i + 1] == '/' || value[i + 1] == '*'))
                    throw new JsonException("JSON comments are not allowed");
            }
        }

        public static T DeserializeStrict<T>(byte[] utf8, string schemaName)
        {
            var token = ParseStrict(utf8);
            var issues = Validate(token, schemaName);
            if (issues.Count != 0) throw new JsonException(string.Join("; ", issues.Select(i => i.ToString())));
            return token.ToObject<T>();
        }

        public static IReadOnlyList<ValidationIssue> Validate(JToken token, string schemaName)
        {
            // Route before validation; archived schemas stay frozen and cannot accept V2 fields.
            if (schemaName == "spell-plan" && HasV2Blueprint(token)) schemaName = "spell-plan-v2";
            if (schemaName == "compiled-spell" && HasV2Blueprint(token?["plan"])) schemaName = "compiled-spell-v2";
            if (!KnownSchemas.Contains(schemaName, StringComparer.Ordinal))
                throw new ArgumentException("Unknown schema", nameof(schemaName));
            var assembly = typeof(ContractJson).GetTypeInfo().Assembly;
            var name = "Palimpseste.Core.Schemas." + schemaName + ".schema.json";
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing schema: " + name)))
            {
                var schema = JObject.Parse(reader.ReadToEnd());
                var issues = new List<ValidationIssue>();
                ValidateToken(token, schema, schema, "$", issues, 0);
                return issues;
            }
        }

        private static bool HasV2Blueprint(JToken plan) => plan?["nodes"] is JArray nodes &&
            nodes.Any(node => node?["blueprint_v2"] != null && node["blueprint_v2"].Type != JTokenType.Null);

        private static void ValidateToken(JToken token, JObject rule, JObject root, string path,
            List<ValidationIssue> issues, int depth)
        {
            if (depth > MaxDepth) { Add(issues, "depth", path, "Nesting too deep"); return; }
            var reference = (string)rule["$ref"];
            if (reference != null)
            {
                if (!reference.StartsWith("#/$defs/", StringComparison.Ordinal))
                    throw new InvalidOperationException("External schema reference is not supported: " + reference);
                var key = reference.Substring("#/$defs/".Length);
                ValidateToken(token, (JObject)root["$defs"]?[key], root, path, issues, depth + 1);
                return;
            }
            var anyOf = rule["anyOf"] as JArray;
            if (anyOf != null)
            {
                foreach (var alternative in anyOf.OfType<JObject>())
                {
                    var trial = new List<ValidationIssue>();
                    ValidateToken(token, alternative, root, path, trial, depth + 1);
                    if (trial.Count == 0) return;
                }
                Add(issues, "any_of", path, "No schema alternative matched");
                return;
            }
            var type = rule["type"];
            if (type != null && !MatchesType(token, type))
            {
                Add(issues, "type", path, "Wrong JSON type");
                return;
            }
            if (rule["const"] is JToken constant && !JToken.DeepEquals(token, constant))
                Add(issues, "const", path, "Value differs from required constant");
            var allowed = rule["enum"] as JArray;
            if (allowed != null && !allowed.Any(e => JToken.DeepEquals(e, token)))
                Add(issues, "enum", path, "Value is outside the allowed catalog");
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var properties = rule["properties"] as JObject;
                var required = rule["required"] as JArray;
                if (required != null)
                    foreach (var item in required)
                        if (obj.Property((string)item) == null)
                            Add(issues, "required", path + "." + (string)item, "Required property missing");
                foreach (var item in obj.Properties())
                {
                    var child = properties?.Property(item.Name)?.Value as JObject;
                    if (child != null) ValidateToken(item.Value, child, root, path + "." + item.Name, issues, depth + 1);
                    else if (rule["additionalProperties"]?.Type == JTokenType.Boolean && !(bool)rule["additionalProperties"])
                        Add(issues, "additional_property", path + "." + item.Name, "Unknown property");
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                var array = (JArray)token;
                CheckRange(array.Count, rule, "minItems", "maxItems", path, issues);
                if (rule["items"] is JObject itemRule)
                    for (int i = 0; i < array.Count; i++)
                        ValidateToken(array[i], itemRule, root, path + "[" + i + "]", issues, depth + 1);
            }
            else if (token.Type == JTokenType.String)
            {
                var value = (string)token;
                CheckRange(value.Length, rule, "minLength", "maxLength", path, issues);
                var pattern = (string)rule["pattern"];
                if (pattern != null && !Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
                    Add(issues, "pattern", path, "String does not match the required pattern");
                if ((string)rule["format"] == "date-time" &&
                    !DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out _))
                    Add(issues, "date_time", path, "Invalid date-time");
            }
            else if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                decimal value;
                try { value = token.Value<decimal>(); }
                catch { Add(issues, "number", path, "Unrepresentable number"); return; }
                if (rule["minimum"] != null && value < rule["minimum"].Value<decimal>())
                    Add(issues, "minimum", path, "Value below minimum");
                if (rule["maximum"] != null && value > rule["maximum"].Value<decimal>())
                    Add(issues, "maximum", path, "Value above maximum");
            }
        }

        private static bool MatchesType(JToken token, JToken rule)
        {
            if (rule is JArray alternatives) return alternatives.Any(item => MatchesType(token, item));
            switch ((string)rule)
            {
                case "object": return token.Type == JTokenType.Object;
                case "array": return token.Type == JTokenType.Array;
                case "string": return token.Type == JTokenType.String;
                case "integer": return token.Type == JTokenType.Integer;
                case "number": return token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
                case "boolean": return token.Type == JTokenType.Boolean;
                case "null": return token.Type == JTokenType.Null;
                default: throw new InvalidOperationException("Unknown schema type");
            }
        }

        private static void CheckRange(int value, JObject rule, string minimum, string maximum,
            string path, List<ValidationIssue> issues)
        {
            if (rule[minimum] != null && value < (int)rule[minimum]) Add(issues, minimum, path, "Too few elements or characters");
            if (rule[maximum] != null && value > (int)rule[maximum]) Add(issues, maximum, path, "Too many elements or characters");
        }

        private static void Add(List<ValidationIssue> issues, string code, string path, string message)
        {
            if (issues.Count < 100) issues.Add(new ValidationIssue(code, path, message));
        }
    }
}

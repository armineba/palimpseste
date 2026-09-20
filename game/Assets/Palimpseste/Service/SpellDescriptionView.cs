using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Palimpseste.Game.Service
{
    // A view of the validated interpretation returned by the visual model. This is never
    // synthesized from a fixture or from a job status message.
    public sealed class SpellDescriptionView
    {
        public string Title { get; private set; }
        public string Summary { get; private set; }
        public string[] Observations { get; private set; }
        public string[] Clauses { get; private set; }

        public static bool TryRead(byte[] utf8, out SpellDescriptionView view)
        {
            view = null;
            if (utf8 == null || utf8.Length == 0 || utf8.Length > 256_000) return false;
            try
            {
                var root = JObject.Parse(new UTF8Encoding(false, true).GetString(utf8));
                if (Text(root, "schema_version", 18, 18) != "sp.description/1.0") return false;
                var title = Text(root, "title", 1, 80);
                var summary = Text(root, "summary", 1, 1800);
                if (title == null || summary == null || root["relations"] is not JArray ||
                    root["shape_requests"] is not JArray || root["observations"] is not JArray observations ||
                    root["clauses"] is not JArray clauses || observations.Count is < 1 or > 24 ||
                    clauses.Count is < 1 or > 16) return false;

                var observationLines = new List<string>(observations.Count);
                foreach (var item in observations)
                {
                    if (item is not JObject observation) return false;
                    var visible = Text(observation, "visible_feature", 1, 320);
                    var interpretation = Text(observation, "interpretation", 1, 400);
                    var region = Text(observation, "region", 1, 16);
                    if (visible == null || interpretation == null || region == null) return false;
                    observationLines.Add(RegionName(region) + " — " + visible + "\n" + interpretation);
                }

                var clauseLines = new List<string>(clauses.Count);
                foreach (var item in clauses)
                {
                    if (item is not JObject clause) return false;
                    var text = Text(clause, "text", 1, 600);
                    var kind = Text(clause, "kind", 1, 32);
                    if (text == null || kind is not ("mechanical" or "visual_only")) return false;
                    clauseLines.Add((kind == "visual_only" ? "Apparence — " : "Règle — ") + text);
                }

                view = new SpellDescriptionView
                {
                    Title = title, Summary = summary,
                    Observations = observationLines.ToArray(), Clauses = clauseLines.ToArray()
                };
                return true;
            }
            catch (Exception e) when (e is JsonException or DecoderFallbackException or ArgumentException)
            {
                return false;
            }
        }

        private static string Text(JObject source, string name, int min, int max)
        {
            var value = source[name];
            if (value == null || value.Type != JTokenType.String) return null;
            var text = value.Value<string>();
            return text.Length >= min && text.Length <= max ? text : null;
        }

        private static string RegionName(string region)
        {
            return region switch
            {
                "core" => "Noyau", "ring" => "Couronne", "outer" => "Périphérie",
                "full" => "Parchemin", _ => region
            };
        }
    }
}

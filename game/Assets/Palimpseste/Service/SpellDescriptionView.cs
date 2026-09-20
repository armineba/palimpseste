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
        public string[] Lifecycle { get; private set; }
        public string[] Behaviors { get; private set; }

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

                var lifecycleLines = new List<string>();
                if (root["lifecycle"] != null && root["lifecycle"].Type != JTokenType.Null)
                {
                    if (root["lifecycle"] is not JArray lifecycle || lifecycle.Count is < 1 or > 16) return false;
                    foreach (var item in lifecycle)
                    {
                        if (item is not JObject subject) return false;
                        var subjectId = Text(subject,"subject_id",1,64);
                        var appearance = Text(subject,"appearance",1,800);
                        var active = Text(subject,"active",1,800);
                        var contact = Text(subject,"contact",1,800);
                        var expiration = Text(subject,"expiration",1,800);
                        if (subjectId == null || appearance == null || active == null || contact == null || expiration == null) return false;
                        lifecycleLines.Add("Apparition — " + appearance + "\n\nEn action — " + active +
                            "\n\nAu contact — " + contact + "\n\nSans contact, à la fin — " + expiration);
                    }
                }

                var behaviorLines = new List<string>();
                if (root["behaviors"] is JArray behaviors)
                    foreach (var item in behaviors)
                    {
                        if (item is not JObject behavior) return false;
                        var origin = Text(behavior,"origin",1,32);
                        var phenomenon = Text(behavior,"phenomenon",1,24);
                        var travel = Text(behavior,"travel",1,24);
                        if (origin == null || phenomenon == null || travel == null) return false;
                        var placement = origin switch {
                            "aim_ground" => "Au sol à l'endroit visé", "caster_ground" => "Au sol sous le lanceur",
                            "aim_point" => "Au point visé", "muzzle" => "Devant le lanceur", "caster" => "Depuis le lanceur",
                            "parent_ground" => "Au sol au point de contact", _ => "À l'endroit de l'événement précédent"
                        };
                        var motion = phenomenon switch {
                            "vortex" => "tourbillon en rotation continue", "spin" => "rotation continue", "orbit" => "mouvement orbital",
                            "flow" => "matière en écoulement", "flutter" => "ondulations", "turbulence" => "mouvement turbulent", _ => "forme stable"
                        };
                        behaviorLines.Add(placement + " · " + motion + (travel == "ballistic" ? " · trajectoire en cloche" : "") +
                            (Text(behavior,"attachment",1,16) == "caster" ? " · suit le lanceur" : ""));
                    }
                view = new SpellDescriptionView
                {
                    Title = title, Summary = summary,
                    Observations = observationLines.ToArray(), Clauses = clauseLines.ToArray(),
                    Lifecycle = lifecycleLines.ToArray(), Behaviors = behaviorLines.ToArray()
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

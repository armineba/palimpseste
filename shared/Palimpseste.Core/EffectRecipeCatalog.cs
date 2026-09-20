using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Palimpseste.Core
{
    // Embedded, reviewed data: never loaded from a player drawing or spell packet.
    internal sealed class EffectRecipe
    {
        public string Id;
        public string Label;
        public string TargetFilter;
        public HashSet<string> AllowedCarriers;
        public IReadOnlyList<EffectRecipeComponent> Components;

        public bool Allows(string carrier) => AllowedCarriers.Contains(carrier);

        public static string EventFor(string carrier)
        {
            switch (carrier)
            {
                case "projectile": case "beam": case "pulse": return "hit";
                case "field": return "enter";
                case "trap": return "trigger";
                default: return null;
            }
        }
    }

    internal sealed class EffectRecipeComponent
    {
        public string Kind;
        public int Amount;
        public int DurationTicks;
        public string Direction;
    }

    internal static class EffectRecipeCatalog
    {
        private static readonly Lazy<IReadOnlyDictionary<string, EffectRecipe>> Cached =
            new Lazy<IReadOnlyDictionary<string, EffectRecipe>>(Load);
        public static IReadOnlyDictionary<string, EffectRecipe> Recipes => Cached.Value;

        private static IReadOnlyDictionary<string, EffectRecipe> Load()
        {
            var assembly = typeof(EffectRecipeCatalog).GetTypeInfo().Assembly;
            using (var stream = assembly.GetManifestResourceStream("Palimpseste.Core.Data.effect-recipes.json"))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing embedded effect recipes")))
            {
                var root = JObject.Parse(reader.ReadToEnd());
                if ((string)root["schema_version"] != "sp.effect-recipes/1.0" || !(root["recipes"] is JArray rows))
                    throw new InvalidDataException("Invalid effect recipe catalog version or shape");
                var result = new Dictionary<string, EffectRecipe>(StringComparer.Ordinal);
                foreach (var row in rows.OfType<JObject>())
                {
                    var id = (string)row["id"];
                    var carriers = (row["allowed_carriers"] as JArray)?.Values<string>().ToHashSet(StringComparer.Ordinal);
                    var components = (row["components"] as JArray)?.OfType<JObject>().Select(item => new EffectRecipeComponent {
                        Kind = (string)item["kind"], Amount = (int)item["amount"],
                        DurationTicks = (int)item["duration_ticks"], Direction = (string)item["direction"]
                    }).ToArray();
                    var target = (string)row["target_filter"];
                    if (id == null || !Regex.IsMatch(id, "^[a-z][a-z0-9_]{0,63}$") ||
                        carriers == null || carriers.Count == 0 || carriers.Any(c => EffectRecipe.EventFor(c) == null) ||
                        components == null || components.Length < 1 || components.Length > 8 ||
                        !new[] { "hostile", "ally", "self", "all_actors", "environment" }.Contains(target) ||
                        !result.TryAdd(id, new EffectRecipe { Id = id, Label = (string)row["label_fr"],
                            TargetFilter = target, AllowedCarriers = carriers, Components = components }))
                        throw new InvalidDataException("Invalid or duplicate effect recipe: " + id);
                }
                if (result.Count < 100) throw new InvalidDataException("Effect recipe catalog must contain at least 100 entries");
                return result;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;

namespace Palimpseste.Core
{
    public sealed class CompilationInput
    {
        public byte[] DescriptionJson;
        public byte[] PlanJson;
        public IReadOnlyDictionary<string, byte[]> GeometryJson;
        public IReadOnlyDictionary<string, byte[]> MaskPng;
        // Values are persisted IDs exposed by /v1/artifacts/{id}; the compiler never fabricates them.
        public IReadOnlyDictionary<string, string> GeometryArtifactIds;
        public IReadOnlyDictionary<string, string> MaskArtifactIds;
        public string SpellId;
        public string ParchmentId;
        public string SignatureSeedHex;
        public string CreatedAt;
        public string MinimumClientVersion = "1.0.0";
        public SpellProvenance Provenance;
        public SpellVisualReference VisualReference;
        public string ReferenceResearchSha256;
        // Optional only after V2 canonical sampling/validation. Never defines the blueprint itself.
        public BinaryAssetEntry BlueprintAnimationSheet;
    }

    public sealed class CompilationResult
    {
        public CompiledSpell Spell { get; internal set; }
        public byte[] PayloadUtf8 { get; internal set; }
        public string PayloadSha256 { get; internal set; }
        public IReadOnlyList<ValidationIssue> Issues { get; internal set; }
        public bool Success => Spell != null && Issues.Count == 0;
    }

    public static class SpellCompiler
    {
        public const string Version = "sp.compiler/1.0";
        private static readonly Dictionary<string, string[]> Emitted = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["projectile"] = new[] { "spawn", "hit", "expire" },
            ["beam"] = new[] { "spawn", "hit", "tick", "expire" },
            ["field"] = new[] { "spawn", "enter", "tick", "expire" },
            ["pulse"] = new[] { "spawn", "hit", "expire" },
            ["barrier"] = new[] { "spawn", "block", "expire" },
            ["trap"] = new[] { "spawn", "trigger", "expire" }
        };
        private static readonly Dictionary<string, string[]> EffectEvents = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["projectile"] = new[] { "hit" }, ["beam"] = new[] { "hit" },
            ["field"] = new[] { "enter", "tick" }, ["pulse"] = new[] { "hit" },
            ["barrier"] = new[] { "block" }, ["trap"] = new[] { "trigger" }
        };
        private static readonly Dictionary<string, int> EffectMaximum = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["damage"] = 100000, ["heal"] = 100000, ["impulse"] = 200000,
            ["burn"] = 10000, ["wet"] = 0, ["slow"] = 750,
            ["bleed"] = 10000, ["poison"] = 10000, ["freeze_damage"] = 10000,
            ["regen"] = 10000, ["barrier_health"] = 100000,
            ["vulnerability"] = 500, ["weakness"] = 500, ["haste"] = 500,
            ["armor_break"] = 500, ["damage_reduction"] = 500,
            ["healing_reduction"] = 500,
            ["root"] = 0, ["stun"] = 0, ["cleanse"] = 0, ["dispel"] = 0,
            ["life_steal"] = 500, ["execute"] = 100000, ["shatter"] = 100000
        };
        private static readonly HashSet<string> PeriodicEffects = new HashSet<string>(StringComparer.Ordinal) {
            "burn", "bleed", "poison", "freeze_damage", "regen"
        };
        private static readonly HashSet<string> InstantEffects = new HashSet<string>(StringComparer.Ordinal) {
            "damage", "heal", "impulse", "cleanse", "dispel", "life_steal", "execute", "shatter"
        };

        public static CompilationResult Compile(CompilationInput input)
        {
            var issues = new List<ValidationIssue>();
            if (input == null) throw new ArgumentNullException(nameof(input));
            SpellDescription description;
            SpellPlan plan;
            try { description = ContractJson.DeserializeStrict<SpellDescription>(input.DescriptionJson, "spell-description"); }
            catch (Exception ex) { return Fail("description_json", ex.Message); }
            try { plan = ContractJson.DeserializeStrict<SpellPlan>(input.PlanJson, "spell-plan"); }
            catch (Exception ex) { return Fail("plan_json", ex.Message); }
            if (plan.description_sha256 != Sha256(input.DescriptionJson))
                Add(issues, "description_hash", "$.description_sha256", "Plan is not tied to the frozen description bytes");
            CheckDescription(description, issues);
            var geometry = LoadGeometry(input, issues, IsV2(plan));
            if (!IsV2(plan)) CheckSemanticGeometry(description, geometry, input, issues);
            CheckPlan(description, plan, geometry, issues);
            CheckVisualReference(input.DescriptionJson, plan, input.VisualReference, issues);
            CheckVisualConstruction(plan, issues);
            CheckReferenceResearch(description, plan, input.ReferenceResearchSha256, issues);
            CheckBlueprints(plan, input, issues);
            if (input.BlueprintAnimationSheet != null && (!IsV2(plan) ||
                input.BlueprintAnimationSheet.file_name != "v2-animation-sheet.png" ||
                input.BlueprintAnimationSheet.media_type != "image/png" ||
                !ValidArtifactId(input.BlueprintAnimationSheet.artifact_id) ||
                !HexDigest(input.BlueprintAnimationSheet.sha256) ||
                input.BlueprintAnimationSheet.size_bytes < 24 || input.BlueprintAnimationSheet.size_bytes > 16777216))
                Add(issues, "v2_animation_artifact", "$.binary_assets", "Canonical sheet requires a persisted, bounded, hashed PNG artifact on a V2 packet");
            if (input.GeometryJson == null || input.MaskPng == null) return new CompilationResult { Issues = issues };
            if (issues.Count != 0) return new CompilationResult { Issues = issues };
            var bounds = ComputeBounds(plan, geometry, input, issues);
            CheckArtifactIds(input, issues);
            if (issues.Count != 0) return new CompilationResult { Issues = issues };

            var minimumClient = input.MinimumClientVersion;
            if (!System.Version.TryParse(minimumClient, out var parsedClient)) parsedClient = new System.Version(0, 0, 0);
            if (IsV2(plan) && parsedClient < new System.Version(1, 8, 0))
                minimumClient = SpellBlueprintV2Limits.MinimumClient;
            else if (plan.nodes.Any(node => node.appearance.construction?.parts.Any(part =>
                    SpellVisualConstructionLimits.SourcedMaterials.Contains(part.material)) == true) &&
                parsedClient < new System.Version(1, 7, 0))
                minimumClient = "1.7.0";
            else if (input.VisualReference?.animation_sheet != null && parsedClient < new System.Version(1, 6, 0))
                minimumClient = "1.6.0";
            else if (plan.nodes.Any(node => node.behavior != null || node.physics != null || node.appearance.resource_id != null) &&
                parsedClient < new System.Version(1, 5, 0))
                minimumClient = "1.5.0";
            else if (plan.nodes.Any(node => node.appearance.lifecycle != null) && parsedClient < new System.Version(1, 4, 0))
                minimumClient = "1.4.0";
            else if (input.VisualReference != null && parsedClient < new System.Version(1, 3, 0))
                minimumClient = "1.3.0";
            else if (plan.nodes.Any(node => node.appearance.vfx != null) && parsedClient < new System.Version(1, 2, 0))
                minimumClient = "1.2.0";
            else if (plan.nodes.Any(node => node.appearance.form != null) && parsedClient < new System.Version(1, 1, 0))
                minimumClient = "1.1.0";
            var spell = new CompiledSpell {
                schema_version = "sp.compiled/1.0", spell_id = input.SpellId,
                parchment_id = input.ParchmentId,
                created_at = input.CreatedAt ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                versions = new SpellVersions { catalog = "sp.capabilities/1.0", compiler = Version,
                    geometry = "sp.geometry/1.0", rules_profile = "lab_v1", min_client = minimumClient },
                provenance = input.Provenance, signature_seed_hex = input.SignatureSeedHex,
                description_sha256 = plan.description_sha256, plan = plan,
                visual_reference = input.VisualReference,
                geometry_manifest = input.GeometryJson.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new GeometryManifestEntry { id = x.Key, kind = geometry[x.Key].kind,
                        artifact_id = input.GeometryArtifactIds[x.Key], sha256 = Sha256(x.Value), size_bytes = x.Value.Length }).ToList(),
                binary_assets = input.MaskPng.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new BinaryAssetEntry { file_name = x.Key, artifact_id = input.MaskArtifactIds[x.Key],
                        sha256 = Sha256(x.Value), size_bytes = x.Value.Length, media_type = "image/png" }).ToList(),
                resource_bounds = bounds,
                display = RulesPresenter.Describe(description, plan)
            };
            if (input.BlueprintAnimationSheet != null) spell.binary_assets.Add(input.BlueprintAnimationSheet);
            var payload = Serialize(spell);
            var schemaIssues = ContractJson.Validate(ContractJson.ParseStrict(payload), "compiled-spell");
            if (schemaIssues.Count != 0) return new CompilationResult { Issues = schemaIssues };
            return new CompilationResult { Spell = spell, PayloadUtf8 = payload,
                PayloadSha256 = Sha256(payload), Issues = Array.Empty<ValidationIssue>() };
        }

        public static byte[] Serialize(CompiledSpell spell)
        {
            var token = JObject.FromObject(spell, JsonSerializer.CreateDefault());
            if (token["visual_reference"]?.Type == JTokenType.Null)
                token.Property("visual_reference")?.Remove();
            if (token["visual_reference"] is JObject visualReference)
            {
                if (visualReference["animation_sheet"]?.Type == JTokenType.Null)
                    visualReference.Property("animation_sheet")?.Remove();
                if (visualReference["source_atlas_sha256"]?.Type == JTokenType.Null)
                    visualReference.Property("source_atlas_sha256")?.Remove();
            }
            var planToken = (JObject)token["plan"];
            if (planToken["visual_reference_sha256"]?.Type == JTokenType.Null)
                planToken.Property("visual_reference_sha256")?.Remove();
            if (planToken["reference_research_sha256"]?.Type == JTokenType.Null)
                planToken.Property("reference_research_sha256")?.Remove();
            foreach (var node in (JArray)token["plan"]["nodes"])
            {
                if (node["behavior"]?.Type == JTokenType.Null) ((JObject)node).Property("behavior")?.Remove();
                if (node["physics"]?.Type == JTokenType.Null) ((JObject)node).Property("physics")?.Remove();
                if (node["blueprint_v2"]?.Type == JTokenType.Null) ((JObject)node).Property("blueprint_v2")?.Remove();
                var options = (JObject)node["options"];
                foreach (var property in options.Properties().ToList())
                    if (property.Value.Type == JTokenType.Null) property.Remove();
                var appearance = (JObject)node["appearance"];
                if (appearance["palette"]?.Type == JTokenType.Null)
                    appearance.Property("palette")?.Remove();
                if (appearance["form"]?.Type == JTokenType.Null)
                    appearance.Property("form")?.Remove();
                if (appearance["vfx"]?.Type == JTokenType.Null)
                    appearance.Property("vfx")?.Remove();
                if (appearance["construction"]?.Type == JTokenType.Null)
                    appearance.Property("construction")?.Remove();
                if (appearance["lifecycle"]?.Type == JTokenType.Null)
                    appearance.Property("lifecycle")?.Remove();
                if (appearance["resource_id"]?.Type == JTokenType.Null)
                    appearance.Property("resource_id")?.Remove();
                if (appearance["signature_geometry_id"]?.Type == JTokenType.Null)
                    appearance.Property("signature_geometry_id")?.Remove();
            }
            return new UTF8Encoding(false).GetBytes(token.ToString(Formatting.None));
        }

        public static string Sha256(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static CompilationResult Fail(string code, string message) => new CompilationResult {
            Issues = new[] { new ValidationIssue(code, "$", message) }
        };

        private static Dictionary<string, GeometryAsset> LoadGeometry(CompilationInput input, List<ValidationIssue> issues, bool canonicalV2 = false)
        {
            var result = new Dictionary<string, GeometryAsset>(StringComparer.Ordinal);
            if (input.GeometryJson == null || input.MaskPng == null) {
                Add(issues, "geometry_missing", "$.geometry", "Geometry and mask inputs are required"); return result;
            }
            if ((!canonicalV2 && input.GeometryJson.Count < 1) || input.GeometryJson.Count > 32 || input.MaskPng.Count > 32)
                Add(issues, "geometry_count", "$.geometry", "Artifact count outside profile");
            foreach (var pair in input.GeometryJson)
            {
                try {
                    var g = ContractJson.DeserializeStrict<GeometryAsset>(pair.Value, "geometry");
                    if (pair.Key != g.geometry_id) Add(issues, "geometry_id", pair.Key, "Artifact identity differs from JSON");
                    if (g.kind == "path" && (g.points.Count < 2 ||
                        g.points.All(p => p.x == g.points[0].x && p.z == g.points[0].z)))
                        Add(issues, "path_degenerate", pair.Key, "Path requires two distinct points");
                    if (g.kind == "footprint" && g.mask_file == null)
                        Add(issues, "footprint_mask", pair.Key, "Footprint requires a mask");
                    if (g.mask_file != null && (!SafeFileName(g.mask_file) || !input.MaskPng.ContainsKey(g.mask_file)))
                        Add(issues, "mask_reference", pair.Key, "Mask missing or unsafe filename");
                    result[pair.Key] = g;
                }
                catch (Exception ex) { Add(issues, "geometry_json", pair.Key, ex.Message); }
            }
            foreach (var mask in input.MaskPng)
            {
                if (!SafeFileName(mask.Key) || mask.Value == null || mask.Value.Length < 8 ||
                    !mask.Value.Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }))
                    Add(issues, "mask_png", mask.Key, "Invalid PNG signature or unsafe filename");
            }
            return result;
        }

        private static bool SafeFileName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
            name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) < 0 && !name.Contains("..") &&
            name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        private static void CheckSemanticGeometry(SpellDescription description, Dictionary<string, GeometryAsset> geometry,
            CompilationInput input, List<ValidationIssue> issues)
        {
            if (!GeometryResolver.UsesSemanticForms(description))
            {
                if (geometry.Values.Any(asset => asset.algorithm == GeometryResolver.SemanticVersion ||
                    asset.source_subject_id != null || asset.source_description_sha256 != null))
                    Add(issues, "semantic_geometry_source", "$.geometry", "Semantic geometry requires interpreted visual forms");
                return;
            }
            if (geometry.Count == 0 || input.MaskPng == null) return;
            var pixelHashes = geometry.Values.Select(asset => asset.source_pixel_sha256).Distinct(StringComparer.Ordinal).ToArray();
            if (pixelHashes.Length != 1)
                Add(issues, "semantic_pixel_source", "$.geometry", "Semantic geometry must share the captured ink provenance");
            var expected = GeometryResolver.ResolveSemantic(description, pixelHashes[0]);
            foreach (var issue in expected.Issues) issues.Add(issue);
            if (!geometry.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Assets.Keys) ||
                !input.MaskPng.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.MaskPng.Keys))
                Add(issues, "semantic_geometry_set", "$.geometry", "Semantic spells require only the generated subject geometry and masks");
            foreach (var pair in expected.Assets)
            {
                if (!geometry.TryGetValue(pair.Key, out var actual)) continue;
                if (!JToken.DeepEquals(JToken.FromObject(actual), JToken.FromObject(pair.Value)))
                    Add(issues, "semantic_geometry_source", pair.Key,
                        "Geometry differs from its interpreted subject, normalized description, or controlled resolver output");
            }
            foreach (var pair in expected.MaskPng)
                if (input.MaskPng.TryGetValue(pair.Key, out var actual) &&
                    (actual == null || !actual.SequenceEqual(pair.Value)))
                    Add(issues, "semantic_mask", pair.Key, "Semantic footprint must be the controlled circular mask");
        }

        private static void CheckArtifactIds(CompilationInput input, List<ValidationIssue> issues)
        {
            if (input.GeometryArtifactIds == null || input.MaskArtifactIds == null) {
                Add(issues, "artifact_ids", "$.artifacts", "Persisted artifact IDs are required"); return;
            }
            foreach (var key in input.GeometryJson.Keys)
                if (!input.GeometryArtifactIds.TryGetValue(key, out var id) || !ValidArtifactId(id))
                    Add(issues, "artifact_id", key, "Missing or invalid geometry artifact ID");
            foreach (var key in input.MaskPng.Keys)
                if (!input.MaskArtifactIds.TryGetValue(key, out var id) || !ValidArtifactId(id))
                    Add(issues, "artifact_id", key, "Missing or invalid mask artifact ID");
        }

        private static bool ValidArtifactId(string id) => id != null &&
            System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_.-]{0,63}$");

        public static IReadOnlyList<ValidationIssue> ValidateDescriptionJson(byte[] descriptionJson)
        {
            try {
                var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionJson, "spell-description");
                var issues = new List<ValidationIssue>();
                CheckDescription(description, issues);
                return issues;
            }
            catch (Exception ex) { return new[] { new ValidationIssue("description_json", "$", ex.Message) }; }
        }

        // New player captures use one composition. The palette gate applies only
        // to free_canvas_v2; older saved descriptions remain compilable offline.
        public static IReadOnlyList<ValidationIssue> ValidateWholeImageDescriptionJson(byte[] descriptionJson,
            bool requirePalette, bool requireVisualForm = false, bool requireLifecycle = false, bool requireBehavior = false)
        {
            var issues = ValidateDescriptionJson(descriptionJson).ToList();
            if (issues.Count != 0) return issues;
            var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionJson, "spell-description");
            if (requireLifecycle && description.lifecycle == null)
                Add(issues, "lifecycle_required", "$.lifecycle",
                    "New spells require a complete launch, active, contact and expiration description for every subject");
            if (requireBehavior && description.behaviors == null)
                Add(issues, "behavior_required", "$.behaviors",
                    "New spells require explicit deployment, travel and phenomenon choices for every subject");
            if (description.shape_requests == null || description.shape_requests.Count != 0)
                Add(issues, "regional_shape_request", "$.shape_requests",
                    "New drawings must use the whole-image geometry bank");
            if (description.observations.Any(observation => observation.region != "full"))
                Add(issues, "regional_observation", "$.observations",
                    "New drawings must be interpreted as one image");
            if (requirePalette)
                foreach (var subject in description.clauses.Select(c => c.subject_id).Distinct(StringComparer.Ordinal))
                    if (description.clauses.Where(c => c.subject_id == subject).SelectMany(c => c.facts)
                        .Count(f => f.dimension == "palette") != 1)
                        Add(issues, "palette_required", subject,
                            "New free-canvas spells require exactly one palette fact per subject");
            if (requireVisualForm)
                foreach (var subject in description.clauses.Select(c => c.subject_id).Distinct(StringComparer.Ordinal))
                    if (description.clauses.Where(c => c.subject_id == subject).SelectMany(c => c.facts)
                        .Count(f => f.dimension == "visual_form") != 1)
                        Add(issues, "visual_form_required", subject,
                            "New interpreted spells require exactly one controlled visual form per subject");
            return issues;
        }

        public static IReadOnlyList<ValidationIssue> ValidatePlanJson(byte[] descriptionJson, byte[] planJson,
            IReadOnlyDictionary<string, byte[]> geometryJson, IReadOnlyDictionary<string, byte[]> maskPng,
            SpellVisualReference visualReference = null, string referenceResearchSha256 = null)
        {
            try {
                var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionJson, "spell-description");
                var plan = ContractJson.DeserializeStrict<SpellPlan>(planJson, "spell-plan");
                var issues = new List<ValidationIssue>();
                CheckDescription(description, issues);
                if (plan.description_sha256 != Sha256(descriptionJson))
                    Add(issues, "description_hash", "$.description_sha256", "Plan is not tied to frozen description bytes");
                var input = new CompilationInput { GeometryJson = geometryJson, MaskPng = maskPng };
                var geometry = LoadGeometry(input, issues, IsV2(plan));
                if (!IsV2(plan)) CheckSemanticGeometry(description, geometry, input, issues);
                CheckPlan(description, plan, geometry, issues);
                CheckVisualReference(descriptionJson, plan, visualReference, issues);
                CheckVisualConstruction(plan, issues);
                CheckReferenceResearch(description, plan, referenceResearchSha256, issues);
                CheckBlueprints(plan, input, issues);
                if (geometryJson != null && maskPng != null && issues.Count == 0)
                    ComputeBounds(plan, geometry, input, issues);
                return issues;
            }
            catch (Exception ex) { return new[] { new ValidationIssue("plan_json", "$", ex.Message) }; }
        }

        private static void CheckVisualReference(byte[] description, SpellPlan plan,
            SpellVisualReference reference, List<ValidationIssue> issues)
        {
            if (IsV2(plan)) {
                if (reference != null || plan.visual_reference_sha256 != null)
                    Add(issues, "v2_independent_reference", "$.visual_reference", "V2 sheets are sampled from the canonical blueprint after structural validation; an independently generated image cannot define the core");
                return;
            }
            var hasConstruction = plan.nodes.Any(node => node.appearance.construction != null);
            if (reference == null)
            {
                if (plan.visual_reference_sha256 != null || hasConstruction)
                    Add(issues, "visual_reference_missing", "$.visual_reference",
                        "Image-guided construction requires the persisted generated image metadata");
                return;
            }
            if (!HexDigest(reference.sha256) || plan.visual_reference_sha256 != reference.sha256)
                Add(issues, "visual_reference_hash", "$.visual_reference_sha256",
                    "Plan must cite the exact generated image supplied to its multimodal planner");
            if (!HexDigest(reference.description_sha256) || reference.description_sha256 != Sha256(description))
                Add(issues, "visual_reference_description", "$.visual_reference.description_sha256",
                    "Generated image must be bound to the same frozen description bytes");
            if (reference.artifact_id == null || reference.artifact_id.Length != 33 || reference.artifact_id[0] != 'a' ||
                !Guid.TryParseExact(reference.artifact_id.Substring(1), "N", out _) ||
                reference.artifact_id.Skip(1).Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                Add(issues, "visual_reference_artifact", "$.visual_reference.artifact_id",
                    "Generated image requires a persisted owner-scoped artifact identifier");
            if (reference.size_bytes < 24 || reference.size_bytes > SpellVisualConstructionLimits.MaximumReferenceBytes ||
                reference.width_px < 512 || reference.width_px > SpellVisualConstructionLimits.MaximumReferenceDimension ||
                reference.height_px < 512 || reference.height_px > SpellVisualConstructionLimits.MaximumReferenceDimension)
                Add(issues, "visual_reference_size", "$.visual_reference", "Generated image exceeds its bounded PNG profile");
            if (string.IsNullOrWhiteSpace(reference.prompt_version) || reference.prompt_version.Length > 80 ||
                !reference.prompt_version.StartsWith("sp.prompt.g/", StringComparison.Ordinal) ||
                !System.Version.TryParse(reference.prompt_version.Substring("sp.prompt.g/".Length), out _))
                Add(issues, "visual_reference_prompt", "$.visual_reference.prompt_version", "Generated image prompt version is invalid");
            var sheet = reference.animation_sheet;
            if (sheet != null)
            {
                if (sheet.layout_version != SpellAnimationSheetLimits.LayoutVersion || sheet.rows != SpellAnimationSheetLimits.Rows ||
                    sheet.columns != SpellAnimationSheetLimits.Columns || !SpellAnimationSheetLimits.EndingBases.Contains(sheet.ending_basis))
                    Add(issues, "animation_sheet_layout", "$.visual_reference.animation_sheet", "Animation sheet requires the exact three-by-seven layout and one declared ending branch");
                if (!HexDigest(reference.source_atlas_sha256))
                    Add(issues, "animation_sheet_source", "$.visual_reference.source_atlas_sha256", "A composed animation sheet must preserve the native source atlas digest");
                if (plan.nodes.Any(node => node.appearance.lifecycle == null))
                    Add(issues, "animation_sheet_lifecycle", "$.nodes", "Every subject represented by an animation sheet requires its complete visual lifecycle");
            }
            else if (reference.source_atlas_sha256 != null)
                Add(issues, "animation_sheet_metadata", "$.visual_reference.animation_sheet", "A native source atlas digest requires animation sheet metadata");
            for (var i = 0; i < plan.nodes.Count; i++)
                if (plan.nodes[i].appearance.construction == null)
                    Add(issues, "visual_construction_required", "$.nodes[" + i + "].appearance.construction",
                        "Every image-guided node requires an explicit visual construction");
        }

        private static bool HexDigest(string value) => value != null && value.Length == 64 &&
            value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        private static void CheckVisualConstruction(SpellPlan plan, List<ValidationIssue> issues)
        {
            var total = 0;
            long expanded = 0;
            for (var nodeIndex = 0; nodeIndex < plan.nodes.Count; nodeIndex++)
            {
                var node = plan.nodes[nodeIndex];
                var construction = node.appearance.construction;
                if (construction == null) continue;
                var path = "$.nodes[" + nodeIndex + "].appearance.construction";
                if (construction.parts == null || construction.parts.Count < 1 ||
                    construction.parts.Count > SpellVisualConstructionLimits.MaximumPartsPerNode)
                {
                    Add(issues, "visual_part_count", path, "Visual construction requires between one and 64 parts");
                    continue;
                }
                total += construction.parts.Count;
                expanded += (long)construction.parts.Count * node.activation.copies * node.activation.max_activations;
                for (var index = 0; index < construction.parts.Count; index++)
                {
                    var part = construction.parts[index];
                    var p = path + ".parts[" + index + "]";
                    if (part == null) { Add(issues, "visual_part", p, "Visual part is missing"); continue; }
                    if (!SpellVisualConstructionLimits.Kinds.Contains(part.kind) ||
                        !SpellVisualConstructionLimits.Materials.Contains(part.material) ||
                        !VectorInRange(part.position_cm, -1000, 1000) ||
                        !VectorInRange(part.scale_cm, 1, 1000) ||
                        !VectorInRange(part.rotation_mdeg, -360000, 360000) ||
                        !VectorInRange(part.color_rgb, 0, 255) ||
                        part.opacity_milli < 0 || part.opacity_milli > 1000 ||
                        part.emission_milli < 0 || part.emission_milli > 6000)
                        Add(issues, "visual_part_bounds", p, "Visual part exceeds its controlled vocabulary or numeric bounds");
                    var pathPart = part.kind == "ribbon" || part.kind == "arc";
                    if (part.points_cm == null || part.points_cm.Count > SpellVisualConstructionLimits.MaximumPoints ||
                        (pathPart ? part.points_cm.Count < 2 : part.points_cm.Count != 0) ||
                        part.points_cm.Any(point => !VectorInRange(point, -1000, 1000)))
                        Add(issues, "visual_part_points", p + ".points_cm",
                            "Ribbon/arc requires two to 16 bounded points; other kinds require an empty list");
                    else if (pathPart && part.points_cm.Skip(1).All(point => point.SequenceEqual(part.points_cm[0])))
                        Add(issues, "visual_part_path_degenerate", p + ".points_cm", "A visual path requires distinct points");
                    var motion = part.motion;
                    if (motion == null || !SpellVisualConstructionLimits.Motions.Contains(motion.kind) ||
                        motion.amplitude_cm < 0 || motion.amplitude_cm > 150 ||
                        motion.frequency_mhz < 0 || motion.frequency_mhz > 6000 ||
                        motion.phase_mdeg < 0 || motion.phase_mdeg > 360000)
                        Add(issues, "visual_motion", p + ".motion", "Visual motion exceeds its controlled bounds");
                }
            }
            if (total > SpellVisualConstructionLimits.MaximumPartsPerPlan ||
                expanded > SpellVisualConstructionLimits.MaximumExpandedParts)
                Add(issues, "visual_resource_budget", "$.nodes",
                    "Construction exceeds 128 authored parts or 1024 parts expanded across bounded activations");
        }

        private static bool VectorInRange(int[] values, int minimum, int maximum) =>
            values != null && values.Length == 3 && values.All(value => value >= minimum && value <= maximum);

        private static void CheckDescription(SpellDescription d, List<ValidationIssue> issues)
        {
            var observationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var observation in d.observations)
                if (!observationIds.Add(observation.id)) Add(issues, "duplicate_observation", observation.id, "Duplicate observation");
            var clauseIds = new HashSet<string>(StringComparer.Ordinal);
            var subjects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clause in d.clauses)
            {
                if (!clauseIds.Add(clause.id)) Add(issues, "duplicate_clause", clause.id, "Duplicate clause");
                subjects.Add(clause.subject_id);
                foreach (var id in clause.observation_ids)
                    if (!observationIds.Contains(id)) Add(issues, "observation_reference", clause.id, "Unknown observation " + id);
                if (clause.kind == "visual_only" && clause.facts.Any(f =>
                    f.dimension == "carrier" || f.dimension == "effect" || f.dimension == "recipe" ||
                    f.dimension == "event" || f.dimension == "target"))
                    Add(issues, "visual_mechanic", clause.id, "Visual clause cannot demand mechanics");
            }
            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (var relation in d.relations)
            {
                if (!subjects.Contains(relation.source_subject_id) || !subjects.Contains(relation.target_subject_id))
                    Add(issues, "relation_subject", "$.relations", "Unknown relation subject");
                if (!incoming.Add(relation.target_subject_id))
                    Add(issues, "multiple_parents", relation.target_subject_id, "Only one incoming relation is allowed");
                if (relation.source_subject_id == relation.target_subject_id)
                    Add(issues, "self_relation", relation.source_subject_id, "Subject cannot trigger itself");
                foreach (var id in relation.clause_ids)
                {
                    var cited = d.clauses.FirstOrDefault(c => c.id == id);
                    if (cited == null || cited.kind != "mechanical" ||
                        (cited.subject_id != relation.source_subject_id && cited.subject_id != relation.target_subject_id))
                        Add(issues, "relation_clause", id, "Relation requires a mechanical clause of its source or target");
                }
            }
            foreach (var subject in subjects)
            {
                var facts = d.clauses.Where(c => c.subject_id == subject && c.kind == "mechanical")
                    .SelectMany(c => c.facts).ToArray();
                var palettes = d.clauses.Where(c => c.subject_id == subject).SelectMany(c => c.facts)
                    .Where(f => f.dimension == "palette").Select(f => f.value).Distinct(StringComparer.Ordinal).ToArray();
                if (palettes.Length > 1)
                    Add(issues, "palette_conflict", subject, "Subject has conflicting visual palettes");
                var forms = d.clauses.Where(c => c.subject_id == subject).SelectMany(c => c.facts)
                    .Where(f => f.dimension == "visual_form").ToArray();
                if (GeometryResolver.UsesSemanticForms(d) && forms.Length != 1)
                    Add(issues, "visual_form_required", subject, "A semantic description needs one visual form per subject");
                var carriers = facts.Where(f => f.dimension == "carrier").Select(f => f.value).ToArray();
                var motions = facts.Where(f => f.dimension == "motion").Select(f => f.value).ToArray();
                if (carriers.Length != 1)
                    Add(issues, "subject_carrier", subject, "Subject requires exactly one carrier fact");
                var selectedRecipeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var clause in d.clauses.Where(c => c.subject_id == subject && c.kind == "mechanical"))
                {
                    var recipeFacts = clause.facts.Where(f => f.dimension == "recipe").ToArray();
                    if (recipeFacts.Length == 0) continue;
                    var requiredKinds = new List<string>();
                    foreach (var fact in recipeFacts)
                    {
                        if (!selectedRecipeIds.Add(fact.value))
                            Add(issues, "duplicate_recipe", clause.id, "The same recipe was selected twice for one subject");
                        if (!EffectRecipeCatalog.Recipes.TryGetValue(fact.value, out var recipe))
                        {
                            Add(issues, "recipe_unknown", clause.id, "Unknown effect recipe");
                            continue;
                        }
                        requiredKinds.AddRange(recipe.Components.Select(component => component.Kind));
                        if (carriers.Length == 1 && !recipe.Allows(carriers[0]))
                            Add(issues, "recipe_carrier", clause.id, "Recipe cannot use this carrier");
                        if (clause.facts.Count(f => f.dimension == "target" && f.value == recipe.TargetFilter) != 1)
                            Add(issues, "recipe_target", clause.id, "Recipe target must appear once in its clause");
                        if (carriers.Length == 1 && clause.facts.Count(f => f.dimension == "event" &&
                            f.value == EffectRecipe.EventFor(carriers[0])) != 1)
                            Add(issues, "recipe_event", clause.id, "Recipe event must appear once in its clause");
                    }
                    var declaredKinds = clause.facts.Where(f => f.dimension == "effect").Select(f => f.value)
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                    if (!declaredKinds.SequenceEqual(requiredKinds.OrderBy(value => value, StringComparer.Ordinal)))
                        Add(issues, "recipe_components", clause.id, "Primitive effect facts must exactly match selected recipes");
                }
                if (selectedRecipeIds.Count > 0 && facts.Count(f => f.dimension == "effect") > 8)
                    Add(issues, "recipe_effect_limit", subject, "Selected recipes exceed eight effects on one carrier");
                if (motions.Length > 1)
                    Add(issues, "motion_fact", subject, "Subject cannot declare multiple motions");
                if (carriers.Length == 1 && motions.Length == 1)
                {
                    if (motions[0] == "stationary" && !new[] { "field", "barrier", "trap" }.Contains(carriers[0]))
                        Add(issues, "motion_fact", subject, "Stationary fact cannot map to this carrier");
                    if (motions[0] == "expanding" && carriers[0] != "pulse")
                        Add(issues, "motion_fact", subject, "Expanding fact requires pulse");
                    if (new[] { "straight", "curve", "homing", "ballistic" }.Contains(motions[0]) && carriers[0] != "projectile")
                        Add(issues, "motion_fact", subject, "Travel motion requires projectile");
                }
                if (d.shape_requests != null && d.shape_requests.Count > 0 &&
                    d.shape_requests.Count(s => s.subject_id == subject) != 1)
                    Add(issues, "shape_request", subject, "Subject requires one geometry request");
            }
            foreach (var request in d.shape_requests ?? Enumerable.Empty<ShapeRequest>())
                if (!subjects.Contains(request.subject_id)) Add(issues, "shape_subject", request.subject_id, "Unknown subject");
            if (GeometryResolver.UsesSemanticForms(d) && d.shape_requests?.Count > 0)
                Add(issues, "semantic_shape_request", "$.shape_requests", "Semantic forms cannot request traced ink geometry");
            CheckLifecycleDescription(d, subjects, issues);
            CheckBehaviorDescription(d, subjects, issues);
        }

        private static void CheckReferenceResearch(SpellDescription description, SpellPlan plan,
            string expectedHash, List<ValidationIssue> issues)
        {
            var usesSourcedMaterial = plan.nodes.Any(node => node.appearance.construction?.parts?.Any(part =>
                part != null && SpellVisualConstructionLimits.SourcedMaterials.Contains(part.material)) == true);
            if (!IsV2(plan) && description.behaviors == null && plan.reference_research_sha256 == null && !usesSourcedMaterial) return;
            if (!HexDigest(expectedHash) || plan.reference_research_sha256 != expectedHash)
                Add(issues, "reference_research_hash", "$.reference_research_sha256",
                    "Plan must cite the exact frozen research context supplied by the server");
        }

        private static void CheckBehaviorDescription(SpellDescription description,
            IEnumerable<string> subjects, List<ValidationIssue> issues)
        {
            if (description.behaviors == null)
            {
                if (description.clauses.SelectMany(clause => clause.facts).Any(fact => fact.dimension == "motion" && fact.value == "ballistic"))
                    Add(issues, "behavior_required", "$.behaviors", "Ballistic travel requires an explicit behavior and physics profile");
                return;
            }
            if (description.lifecycle == null)
                Add(issues, "lifecycle_required", "$.lifecycle", "Behavior intents require the complete described lifecycle");
            var expected = new HashSet<string>(subjects, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var behavior in description.behaviors)
            {
                if (behavior == null || string.IsNullOrWhiteSpace(behavior.subject_id))
                {
                    Add(issues, "behavior_subject", "$.behaviors", "Behavior requires a known subject");
                    continue;
                }
                var path = "$.behaviors." + behavior.subject_id;
                if (!expected.Contains(behavior.subject_id)) Add(issues, "behavior_subject", path, "Unknown behavior subject");
                if (!seen.Add(behavior.subject_id)) Add(issues, "behavior_duplicate", path, "One behavior intent per subject is required");
                if (!BehaviorVocabularyValid(behavior)) Add(issues, "behavior_vocabulary", path, "Unsupported behavior vocabulary");
                var clauses = description.clauses.Where(clause => clause.subject_id == behavior.subject_id).ToArray();
                var facts = clauses.Where(clause => clause.kind == "mechanical").SelectMany(clause => clause.facts).ToArray();
                var carrier = facts.FirstOrDefault(fact => fact.dimension == "carrier")?.value;
                var motions = facts.Where(fact => fact.dimension == "motion").Select(fact => fact.value).ToArray();
                if (clauses.SelectMany(clause => clause.facts).Count(fact => fact.dimension == "visual_form") != 1 ||
                    clauses.SelectMany(clause => clause.facts).Count(fact => fact.dimension == "palette") != 1)
                    Add(issues, "behavior_visual_identity", path, "Behavior requires exactly one interpreted form and palette");
                var isChild = description.relations.Any(relation => relation.target_subject_id == behavior.subject_id);
                if (isChild != SpellPhysicsLimits.IsParentOrigin(behavior.origin))
                    Add(issues, "behavior_parent_origin", path, "Child origins must be tied to their described parent event");
                if (carrier == "projectile")
                {
                    if (behavior.travel == "stationary" || motions.Length != 1 || motions[0] != behavior.travel)
                        Add(issues, "behavior_travel", path, "Projectile travel must equal its single motion fact");
                }
                else if (behavior.travel != "stationary")
                    Add(issues, "behavior_travel", path, "Only a projectile carrier can travel; other carrier centers remain stationary");
                if (behavior.attachment == "caster" &&
                    (behavior.travel != "stationary" || !new[] { "field", "barrier", "trap" }.Contains(carrier) ||
                     behavior.origin != "caster" && behavior.origin != "caster_ground"))
                    Add(issues, "behavior_attachment", path, "Caster attachment requires a stationary carrier originating at its caster");
                if (behavior.orientation == "surface_normal" && !SpellPhysicsLimits.IsGroundOrigin(behavior.origin))
                    Add(issues, "behavior_surface", path, "Surface orientation requires a ground origin");
                if (behavior.phenomenon == "vortex" && behavior.axis != "y")
                    Add(issues, "behavior_vortex_axis", path, "A vortex rotates around its declared vertical Y axis");
                if (clauses.SelectMany(clause => clause.facts).Any(fact => fact.dimension == "visual_form" && fact.value == "vortex") &&
                    behavior.phenomenon != "vortex")
                    Add(issues, "behavior_form", path, "The controlled vortex form requires a vortex phenomenon");
                if (carrier == "trap" && !new[] { "aim_ground", "parent_ground", "caster_ground" }.Contains(behavior.origin))
                    Add(issues, "behavior_trap_origin", path,
                        "A trap requires an interpreted ground origin, never a planner's default caster position");
            }
            if (!seen.SetEquals(expected))
                Add(issues, "behavior_subjects", "$.behaviors", "Behavior intents must cover every subject exactly once");
        }

        private static bool BehaviorVocabularyValid(SpellBehaviorIntent value) =>
            SpellPhysicsLimits.Origins.Contains(value.origin) && SpellPhysicsLimits.Orientations.Contains(value.orientation) &&
            SpellPhysicsLimits.Attachments.Contains(value.attachment) && SpellPhysicsLimits.Phenomena.Contains(value.phenomenon) &&
            SpellPhysicsLimits.Axes.Contains(value.axis) && SpellPhysicsLimits.Senses.Contains(value.sense) &&
            SpellPhysicsLimits.Intensities.Contains(value.intensity) && SpellPhysicsLimits.Travels.Contains(value.travel);

        private static bool BehaviorEquals(SpellBehaviorIntent expected, SpellBehaviorIntent actual) =>
            expected.subject_id == actual.subject_id && expected.origin == actual.origin && expected.orientation == actual.orientation &&
            expected.attachment == actual.attachment && expected.phenomenon == actual.phenomenon && expected.axis == actual.axis &&
            expected.sense == actual.sense && expected.intensity == actual.intensity && expected.travel == actual.travel;

        private static void CheckBehaviorPlan(SpellDescription description, SpellNode node, List<ValidationIssue> issues)
        {
            var intent = description.behaviors?.FirstOrDefault(item => item.subject_id == node.subject_id);
            var path = node.node_id + ".behavior";
            if (intent == null)
            {
                if (node.behavior != null || node.physics != null || node.appearance.resource_id != null || node.options.motion == "ballistic")
                    Add(issues, "behavior_trace", path, "New physics and resources require an interpreted behavior for the same subject");
                return;
            }
            if (node.behavior == null || node.physics == null)
            {
                Add(issues, "behavior_required", path, "Every interpreted behavior requires its complete copied intent and physics profile");
                return;
            }
            if (!BehaviorEquals(intent, node.behavior))
                Add(issues, "behavior_trace", path, "Planner altered deployment, travel or phenomenon selected by the interpreter");
            if (node.anchor != SpellPhysicsLimits.AnchorForOrigin(intent.origin))
                Add(issues, "behavior_anchor", path, "Legacy anchor must match the explicit origin");
            if (SpellPhysicsLimits.IsParentOrigin(intent.origin) != (node.activation.parent_id != null))
                Add(issues, "behavior_parent_origin", path, "Deployment must preserve the parent event relation");
            if (node.carrier == "projectile" && node.options.motion != intent.travel)
                Add(issues, "behavior_travel", path, "Projectile options.motion differs from interpreted travel");
            if (node.appearance.resource_id == null || !SpellVfxResources.All.Contains(node.appearance.resource_id))
                Add(issues, "visual_resource_required", node.node_id + ".appearance.resource_id", "Select one shipped texture resource");
            if (node.blueprint_v2 == null && (node.appearance.construction == null || node.appearance.lifecycle == null))
                Add(issues, "behavior_visual_construction", path, "Physical behavior requires explicit image construction and lifecycle");
            var physics = node.physics;
            var numericPath = node.node_id + ".physics";
            if (physics.cast_range_cm < 0 || physics.cast_range_cm > SpellPhysicsLimits.MaximumCastRangeCm ||
                !VectorInRange(physics.offset_cm, -SpellPhysicsLimits.MaximumOffsetCm, SpellPhysicsLimits.MaximumOffsetCm) ||
                physics.angular_speed_mdeg_s < 0 || physics.angular_speed_mdeg_s > SpellPhysicsLimits.MaximumAngularSpeedMdegS ||
                physics.axial_speed_cm_s < -SpellPhysicsLimits.MaximumFlowSpeedCmS || physics.axial_speed_cm_s > SpellPhysicsLimits.MaximumFlowSpeedCmS ||
                physics.radial_speed_cm_s < -SpellPhysicsLimits.MaximumFlowSpeedCmS || physics.radial_speed_cm_s > SpellPhysicsLimits.MaximumFlowSpeedCmS ||
                physics.radius_cm < 0 || physics.radius_cm > SpellPhysicsLimits.MaximumRadiusCm ||
                physics.turbulence_cm < 0 || physics.turbulence_cm > SpellPhysicsLimits.MaximumTurbulenceCm ||
                physics.frequency_mhz < 0 || physics.frequency_mhz > SpellPhysicsLimits.MaximumFrequencyMhz ||
                physics.gravity_cm_s2 < 0 || physics.gravity_cm_s2 > SpellPhysicsLimits.MaximumGravityCmS2 ||
                physics.launch_pitch_mdeg < -SpellPhysicsLimits.MaximumLaunchPitchMdeg || physics.launch_pitch_mdeg > SpellPhysicsLimits.MaximumLaunchPitchMdeg)
                Add(issues, "physics_bounds", numericPath, "Physics parameters exceed the controlled units or numeric bounds");
            if (SpellPhysicsLimits.IsAimOrigin(intent.origin) ? physics.cast_range_cm <= 0 : physics.cast_range_cm != 0)
                Add(issues, "physics_cast_range", numericPath, "Only an aimed origin has a positive bounded cast range");
            if (SpellPhysicsLimits.IsGroundOrigin(intent.origin) && physics.offset_cm?.Length == 3 && physics.offset_cm[1] != 0)
                Add(issues, "physics_ground_offset", numericPath, "Ground placement must remain on the resolved surface");
            if (intent.travel == "ballistic")
            {
                if (node.carrier != "projectile" || physics.gravity_cm_s2 <= 0)
                    Add(issues, "physics_ballistic", numericPath, "Ballistic travel requires a projectile and positive gravity");
            }
            else if (physics.gravity_cm_s2 != 0 || physics.launch_pitch_mdeg != 0)
                Add(issues, "physics_unused_gravity", numericPath, "Gravity and launch pitch are zero outside ballistic travel");
            var rotating = intent.phenomenon == "spin" || intent.phenomenon == "vortex" || intent.phenomenon == "orbit";
            if (rotating ? physics.angular_speed_mdeg_s <= 0 : physics.angular_speed_mdeg_s != 0)
                Add(issues, "physics_rotation", numericPath, "Rotating phenomena require continuous angular speed; other phenomena use zero");
            if (intent.phenomenon == "vortex" &&
                (physics.angular_speed_mdeg_s < SpellPhysicsLimits.MinimumVortexAngularSpeed(intent.intensity) || physics.axial_speed_cm_s == 0))
                Add(issues, "physics_vortex", numericPath, "Vortex intensity requires its minimum continuous angular speed and a nonzero axial flow");
            var flowing = intent.phenomenon == "vortex" || intent.phenomenon == "flow";
            if (!flowing && (physics.axial_speed_cm_s != 0 || physics.radial_speed_cm_s != 0) ||
                intent.phenomenon == "flow" && physics.axial_speed_cm_s == 0)
                Add(issues, "physics_flow", numericPath, "Axial/radial flow belongs only to a vortex or flow phenomenon, with nonzero flow travel");
            var needsRadius = flowing || intent.phenomenon == "orbit";
            if (needsRadius ? physics.radius_cm <= 0 : physics.radius_cm != 0)
                Add(issues, "physics_radius", numericPath, "Only vortex, flow and orbit have a positive bounded decorative radius");
            var noisy = flowing || intent.phenomenon == "flutter" || intent.phenomenon == "turbulence";
            if (!noisy && (physics.turbulence_cm != 0 || physics.frequency_mhz != 0) ||
                noisy && ((physics.turbulence_cm == 0) != (physics.frequency_mhz == 0)) ||
                (intent.phenomenon == "flutter" || intent.phenomenon == "turbulence") && physics.turbulence_cm <= 0)
                Add(issues, "physics_turbulence", numericPath, "Animated turbulence requires positive amplitude and frequency; unused parameters remain zero");
        }

        private static void CheckLifecycleDescription(SpellDescription description,
            IEnumerable<string> subjects, List<ValidationIssue> issues)
        {
            if (description.lifecycle == null) return; // Existing archived bytes remain valid.
            var expected = new HashSet<string>(subjects, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < description.lifecycle.Count; index++)
            {
                var lifecycle = description.lifecycle[index];
                var path = "$.lifecycle[" + index + "]";
                if (lifecycle == null || string.IsNullOrWhiteSpace(lifecycle.subject_id))
                {
                    Add(issues, "lifecycle_subject", path, "Lifecycle requires a known subject");
                    continue;
                }
                if (!expected.Contains(lifecycle.subject_id))
                    Add(issues, "lifecycle_subject", path, "Lifecycle refers to an unknown subject");
                if (!seen.Add(lifecycle.subject_id))
                    Add(issues, "lifecycle_duplicate", path, "Each subject has exactly one lifecycle description");
                if (new[] { lifecycle.appearance, lifecycle.active, lifecycle.contact, lifecycle.expiration }
                    .Any(text => string.IsNullOrWhiteSpace(text) || text.Length > 800))
                    Add(issues, "lifecycle_text", path, "Each lifecycle phase requires nonempty bounded text");
            }
            if (!seen.SetEquals(expected))
                Add(issues, "lifecycle_subjects", "$.lifecycle", "Lifecycle descriptions must cover every subject exactly once");
        }

        private static void CheckLifecyclePlan(SpellDescription description, SpellNode node,
            List<ValidationIssue> issues)
        {
            var lifecycle = node.appearance.lifecycle;
            var path = node.node_id + ".appearance.lifecycle";
            var described = description.lifecycle?.Any(item => item.subject_id == node.subject_id) == true;
            if (lifecycle == null)
            {
                if (described) Add(issues, "lifecycle_required", path,
                    "The subject lifecycle must be translated into all four visual animation phases");
                return;
            }
            if (!described)
                Add(issues, "lifecycle_trace", path, "Visual lifecycle requires the same subject in the frozen description");
            var intro = lifecycle.intro;
            if (intro == null || !SpellVisualLifecycleLimits.IntroKinds.Contains(intro.kind) ||
                intro.duration_ms < SpellVisualLifecycleLimits.MinimumDurationMs ||
                intro.duration_ms > SpellVisualLifecycleLimits.MaximumDurationMs ||
                intro.scale_start_milli < 0 || intro.scale_start_milli > 1000 ||
                intro.opacity_start_milli < 0 || intro.opacity_start_milli > 1000 ||
                intro.emission_start_milli < 0 || intro.emission_start_milli > SpellVisualLifecycleLimits.MaximumEmissionMilli)
                Add(issues, "lifecycle_intro", path + ".intro", "Launch animation exceeds controlled bounds");
            var active = lifecycle.active;
            if (active == null || !SpellVisualLifecycleLimits.ActiveKinds.Contains(active.kind) ||
                active.period_ms < SpellVisualLifecycleLimits.MinimumPeriodMs ||
                active.period_ms > SpellVisualLifecycleLimits.MaximumPeriodMs ||
                active.amplitude_milli < 0 || active.amplitude_milli > SpellVisualLifecycleLimits.MaximumAmplitudeMilli)
                Add(issues, "lifecycle_active", path + ".active", "Active animation exceeds controlled bounds");
            CheckLifecycleEnding(lifecycle.contact, path + ".contact", issues);
            CheckLifecycleEnding(lifecycle.expiration, path + ".expiration", issues);
        }

        private static void CheckLifecycleEnding(SpellVisualEnding ending, string path, List<ValidationIssue> issues)
        {
            if (ending == null || !SpellVisualLifecycleLimits.EndingKinds.Contains(ending.kind) ||
                ending.duration_ms < SpellVisualLifecycleLimits.MinimumDurationMs ||
                ending.duration_ms > SpellVisualLifecycleLimits.MaximumDurationMs ||
                ending.spread_cm < 0 || ending.spread_cm > SpellVisualLifecycleLimits.MaximumSpreadCm ||
                ending.scale_end_milli < 0 || ending.scale_end_milli > SpellVisualLifecycleLimits.MaximumEndScaleMilli)
                Add(issues, "lifecycle_ending", path, "Ending animation exceeds controlled bounds");
        }

        private static void CheckPlan(SpellDescription d, SpellPlan plan,
            Dictionary<string, GeometryAsset> geometry, List<ValidationIssue> issues)
        {
            var bySubject = new Dictionary<string, SpellNode>(StringComparer.Ordinal);
            var byNode = new Dictionary<string, SpellNode>(StringComparer.Ordinal);
            foreach (var node in plan.nodes)
            {
                if (!byNode.TryAdd(node.node_id, node)) Add(issues, "duplicate_node", node.node_id, "Duplicate node");
                if (!bySubject.TryAdd(node.subject_id, node)) Add(issues, "duplicate_subject", node.subject_id, "One node per subject");
            }
            var subjects = d.clauses.Select(c => c.subject_id).Distinct(StringComparer.Ordinal).ToArray();
            if (!subjects.ToHashSet(StringComparer.Ordinal).SetEquals(bySubject.Keys))
                Add(issues, "subjects", "$.nodes", "Plan subjects must equal description subjects");
            foreach (var node in plan.nodes)
            {
                var p = node.node_id;
                if (node.blueprint_v2 == null) CheckLifecyclePlan(d, node, issues);
                CheckBehaviorPlan(d, node, issues);
                if (!Emitted.ContainsKey(node.carrier)) { Add(issues, "carrier", p, "Unknown carrier"); continue; }
                var allSubjectClauses = d.clauses.Where(c => c.subject_id == node.subject_id).ToArray();
                var subjectClauses = allSubjectClauses.Where(c => c.kind == "mechanical").ToArray();
                var subjectIds = allSubjectClauses.Select(c => c.id).ToHashSet(StringComparer.Ordinal);
                var nodeClauseIds = node.clause_ids.ToHashSet(StringComparer.Ordinal);
                if (node.effects.Select(e => e.id).Distinct(StringComparer.Ordinal).Count() != node.effects.Count)
                    Add(issues, "duplicate_effect", p, "Effect IDs must be distinct within a node");
                if (!subjectIds.SetEquals(nodeClauseIds) || nodeClauseIds.Count != node.clause_ids.Count)
                    Add(issues, "clause_trace", p, "Node must cite every clause of its subject exactly once");
                var facts = subjectClauses.SelectMany(c => c.facts).ToArray();
                var paletteFacts = allSubjectClauses.SelectMany(c => c.facts)
                    .Where(f => f.dimension == "palette").Select(f => f.value).Distinct(StringComparer.Ordinal).ToArray();
                if (paletteFacts.Length == 0 && node.appearance.palette != null)
                    Add(issues, "palette_trace", p, "Palette was not selected by interpreter text");
                if (paletteFacts.Length == 1 && node.appearance.palette != paletteFacts[0])
                    Add(issues, "palette_trace", p, "Palette differs from interpreter text");
                var formFacts = allSubjectClauses.SelectMany(c => c.facts)
                    .Where(f => f.dimension == "visual_form").Select(f => f.value).ToArray();
                if (formFacts.Length == 0 && node.appearance.form != null ||
                    formFacts.Length == 1 && node.appearance.form != formFacts[0])
                    Add(issues, "visual_form_trace", p, "Visual form must exactly match interpreter text");
                var vfx = node.appearance.vfx;
                if (vfx != null &&
                    (!SpellVfxProfiles.Styles.Contains(vfx.style) ||
                     !SpellVfxProfiles.Motifs.Contains(vfx.motif) ||
                     !SpellVfxProfiles.Impacts.Contains(vfx.impact) ||
                     vfx.density < SpellVfxProfiles.MinimumDensity || vfx.density > SpellVfxProfiles.MaximumDensity ||
                     vfx.aura_cm < SpellVfxProfiles.MinimumAuraCm || vfx.aura_cm > SpellVfxProfiles.MaximumAuraCm ||
                     vfx.charge_ms < SpellVfxProfiles.MinimumChargeMs || vfx.charge_ms > SpellVfxProfiles.MaximumChargeMs))
                    Add(issues, "vfx_profile", p + ".appearance.vfx", "Decorative VFX profile exceeds controlled bounds");
                if (node.carrier == "projectile" && node.appearance.form != null &&
                    node.options.radius_cm < SpellVisualForms.MinimumProjectileRadiusCm(node.appearance.form))
                    Add(issues, "semantic_projectile_size", p + ".options.radius_cm",
                        "The interpreted " + node.appearance.form + " requires radius_cm >= " +
                        SpellVisualForms.MinimumProjectileRadiusCm(node.appearance.form) + " for a legible 3D form");
                if (!facts.Any(f => f.dimension == "carrier" && f.value == node.carrier))
                    Add(issues, "carrier_fact", p, "Carrier differs from description");
                var expectedEffects = facts.Where(f => f.dimension == "effect").Select(f => f.value)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var actualEffects = node.effects.Select(e => e.kind).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                if (!expectedEffects.SequenceEqual(actualEffects))
                    Add(issues, "effect_facts", p, "Effect families added or lost");
                var allowedTargets = facts.Where(f => f.dimension == "target").Select(f => f.value)
                    .ToHashSet(StringComparer.Ordinal);
                var allowedEvents = facts.Where(f => f.dimension == "event").Select(f => f.value)
                    .ToHashSet(StringComparer.Ordinal);
                var carrierFilter = node.carrier switch {
                    "projectile" => node.options.contact_filter,
                    "beam" => node.options.chain_filter,
                    "trap" => node.options.trigger_filter,
                    _ => null
                };
                if (carrierFilter != null)
                {
                    var noTargetVisual = allowedTargets.Count == 0 && node.effects.Count == 0 &&
                        carrierFilter == "environment";
                    var allActorsStated = carrierFilter == "all_actors" &&
                        new[] { "hostile", "ally", "self" }.All(allowedTargets.Contains);
                    if (!noTargetVisual && !allowedTargets.Contains(carrierFilter) && !allActorsStated)
                        Add(issues, "carrier_target", p, "Carrier receiver filter is not justified by target facts");
                    foreach (var effect in node.effects)
                        if (carrierFilter != effect.target_filter &&
                            !(carrierFilter == "all_actors" && new[] { "hostile", "ally", "self" }.Contains(effect.target_filter)))
                            Add(issues, "carrier_target", p, "Carrier filter excludes an effect target");
                }
                var motion = facts.FirstOrDefault(f => f.dimension == "motion")?.value;
                foreach (var clause in subjectClauses)
                {
                    var linked = node.effects.Where(e => e.clause_ids.Contains(clause.id)).ToArray();
                    var clauseEffects = clause.facts.Where(f => f.dimension == "effect").Select(f => f.value)
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (!clauseEffects.SequenceEqual(linked.Select(e => e.kind).OrderBy(x => x, StringComparer.Ordinal)))
                        Add(issues, "clause_effect", clause.id, "Effect trace differs from this clause");
                    var selectedRecipes = clause.facts.Where(f => f.dimension == "recipe")
                        .Select(f => EffectRecipeCatalog.Recipes.TryGetValue(f.value, out var recipe) ? recipe : null)
                        .Where(recipe => recipe != null).ToArray();
                    if (selectedRecipes.Length > 0)
                    {
                        var expectedComponents = selectedRecipes.SelectMany(recipe => recipe.Components.Select(component =>
                            RecipeSignature(component.Kind, component.Amount, component.DurationTicks,
                                component.Direction, recipe.TargetFilter, EffectRecipe.EventFor(node.carrier))))
                            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                        var actualComponents = linked.Select(effect => RecipeSignature(effect.kind, effect.amount,
                            effect.duration_ticks, effect.direction, effect.target_filter, effect.@event))
                            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                        if (!expectedComponents.SequenceEqual(actualComponents))
                            Add(issues, "recipe_plan", clause.id, "Plan effects differ from the fixed recipe components");
                    }
                    foreach (var fact in clause.facts)
                    {
                        if (fact.dimension == "target" && (linked.Length == 0 || linked.Any(e => e.target_filter != fact.value)))
                            Add(issues, "clause_target", clause.id, "Effect target differs from this clause");
                        if (fact.dimension == "event" &&
                            (!Emitted[node.carrier].Contains(fact.value) ||
                             (linked.Length > 0 && linked.Any(e => e.@event != fact.value))))
                            Add(issues, "clause_event", clause.id, "Carrier or effect event differs from this clause");
                        if (fact.dimension == "affinity" && node.appearance.affinity != fact.value)
                            Add(issues, "clause_affinity", clause.id, "Appearance affinity differs from clause");
                        if (fact.dimension == "pattern" && node.appearance.pattern != fact.value)
                            Add(issues, "clause_pattern", clause.id, "Visible pattern differs from clause");
                    }
                }
                foreach (var clause in allSubjectClauses.Where(c => c.kind == "visual_only"))
                    foreach (var fact in clause.facts)
                    {
                        if (fact.dimension == "affinity" && node.appearance.affinity != fact.value)
                            Add(issues, "clause_affinity", clause.id, "Appearance affinity differs from visual clause");
                        if (fact.dimension == "pattern" && node.appearance.pattern != fact.value)
                            Add(issues, "clause_pattern", clause.id, "Visible pattern differs from visual clause");
                    }
                if (node.carrier == "projectile" && motion != null && node.options.motion != motion)
                    Add(issues, "motion_fact", p, "Projectile motion differs from description");
                if (motion == "stationary" && !new[] { "field", "barrier", "trap" }.Contains(node.carrier))
                    Add(issues, "motion_fact", p, "Stationary fact cannot map to this carrier");
                if (motion == "expanding" && node.carrier != "pulse")
                    Add(issues, "motion_fact", p, "Expanding fact requires pulse");
                if (new[] { "straight", "curve", "homing", "ballistic" }.Contains(motion) && node.carrier != "projectile")
                    Add(issues, "motion_fact", p, "Travel motion requires projectile");
                if (node.blueprint_v2 == null) {
                var request = d.shape_requests?.FirstOrDefault(s => s.subject_id == node.subject_id);
                if (!geometry.TryGetValue(node.geometry_id, out var main))
                    Add(issues, "geometry_reference", p, "Unknown main geometry");
                else if (request != null && (main.source_region != request.region || main.kind != request.role))
                    Add(issues, "geometry_role", p, "Geometry differs from requested region or role");
                if (node.appearance.form != null)
                {
                    if (node.appearance.signature_geometry_id != null)
                        Add(issues, "semantic_signature", p, "Controlled 3D forms do not use an ink silhouette");
                    if (main?.algorithm != GeometryResolver.SemanticVersion || main.source_subject_id != node.subject_id)
                        Add(issues, "semantic_geometry_subject", p, "Controlled form requires its own interpreted subject geometry");
                }
                else if (node.appearance.signature_geometry_id == null ||
                    !geometry.TryGetValue(node.appearance.signature_geometry_id, out var signature) || signature.kind != "silhouette")
                    Add(issues, "signature_geometry", p, "Legacy visual signature needs existing silhouette");
                if ((node.carrier == "barrier" || (node.carrier == "projectile" && node.options.motion == "curve")) && main?.kind != "path")
                    Add(issues, "path_required", p, "This carrier requires a drawn path");
                if ((node.carrier == "field" || node.carrier == "trap") && main?.kind != "footprint")
                    Add(issues, "footprint_required", p, "This carrier requires a footprint");
                }
                if (node.activation.parent_id == null)
                {
                    if (node.activation.@event != "cast" || node.activation.max_activations != 1 || node.anchor == "parent_event")
                        Add(issues, "root_activation", p, "Invalid root activation");
                }
                else
                {
                    if (!byNode.TryGetValue(node.activation.parent_id, out var parent))
                        Add(issues, "parent", p, "Unknown parent node");
                    else if (!Emitted.TryGetValue(parent.carrier, out var parentEvents) || !parentEvents.Contains(node.activation.@event))
                        Add(issues, "parent_event", p, "Parent cannot emit event");
                    if (node.anchor != "parent_event") Add(issues, "child_anchor", p, "Child must use parent_event anchor");
                }
                if (node.activation.copies == 1 && node.activation.spread_mdeg != 0)
                    Add(issues, "spread", p, "One copy requires zero spread");
                ValidateCarrierOptions(node, issues);
                foreach (var effect in node.effects)
                {
                    if (!EffectEvents[node.carrier].Contains(effect.@event))
                        Add(issues, "effect_event", p, "Carrier cannot apply effect on event");
                    if (!EffectMaximum.TryGetValue(effect.kind, out int maximum) || effect.amount > maximum)
                        Add(issues, "effect_amount", p, "Effect outside catalog maximum");
                    if (!allowedTargets.Contains(effect.target_filter))
                        Add(issues, "target_fact", p, "Target filter not present in subject facts");
                    if (allowedEvents.Count > 0 && !allowedEvents.Contains(effect.@event))
                        Add(issues, "event_fact", p, "Effect event not present in subject facts");
                    if (!effect.clause_ids.All(id => subjectIds.Contains(id)) ||
                        !effect.clause_ids.Any(id => d.clauses.Any(c => c.id == id && c.facts.Any(f => f.dimension == "effect" && f.value == effect.kind))))
                        Add(issues, "effect_trace", p, "Effect lacks a matching clause");
                    if (InstantEffects.Contains(effect.kind) && effect.duration_ticks != 0)
                        Add(issues, "effect_duration", p, "Instant effect requires zero duration");
                    if (!InstantEffects.Contains(effect.kind) && effect.duration_ticks <= 0)
                        Add(issues, "effect_duration", p, "Status requires positive duration");
                    if (PeriodicEffects.Contains(effect.kind) && effect.duration_ticks % 50 != 0)
                        Add(issues, "periodic_duration", p, "Periodic duration must be a multiple of 50 ticks");
                    if ((effect.kind == "root" || effect.kind == "stun") && effect.duration_ticks > 100)
                        Add(issues, "control_duration", p, "Hard control exceeds two seconds");
                    if (effect.kind == "life_steal" &&
                        (effect.target_filter != "hostile" || !node.effects.Any(e => e.kind == "damage" &&
                        e.@event == effect.@event && e.target_filter == "hostile")))
                        Add(issues, "life_steal_source", p, "Life steal requires same-event hostile damage");
                    if (effect.kind == "execute" && effect.target_filter != "hostile")
                        Add(issues, "execute_target", p, "Execute affects hostile receivers only");
                    if (effect.kind == "shatter" && effect.target_filter != "environment")
                        Add(issues, "shatter_target", p, "Shatter affects marked breakable environment only");
                    if (new[] { "regen", "barrier_health", "haste", "damage_reduction", "cleanse" }
                        .Contains(effect.kind) && effect.target_filter != "ally" && effect.target_filter != "self")
                        Add(issues, "support_target", p, "Support status requires ally or self");
                    if (new[] { "vulnerability", "weakness", "armor_break", "healing_reduction",
                        "root", "stun", "dispel" }.Contains(effect.kind) && effect.target_filter != "hostile")
                        Add(issues, "debuff_target", p, "Debuff or dispel requires hostile receiver");
                    if (new[] { "bleed", "poison", "freeze_damage" }.Contains(effect.kind) &&
                        effect.target_filter != "hostile")
                        Add(issues, "hostile_status_target", p, "Damage-over-time status requires hostile receiver");
                    if (new[] { "regen", "barrier_health", "vulnerability", "weakness", "haste",
                        "armor_break", "damage_reduction", "healing_reduction", "root", "stun",
                        "cleanse", "dispel" }.Contains(effect.kind) &&
                        effect.target_filter == "environment")
                        Add(issues, "support_target", p, "Support effect requires an actor receiver");
                    if ((effect.kind == "impulse") == (effect.direction == "none"))
                        Add(issues, "effect_direction", p, "Invalid direction for effect kind");
                }
            }
            foreach (var relation in d.relations)
            {
                if (!bySubject.TryGetValue(relation.source_subject_id, out var source) ||
                    !bySubject.TryGetValue(relation.target_subject_id, out var child)) continue;
                if (child.activation.parent_id != source.node_id || child.activation.@event != relation.@event ||
                    child.activation.max_activations != relation.max_activations ||
                    !relation.clause_ids.All(id => child.clause_ids.Contains(id) || source.clause_ids.Contains(id)))
                    Add(issues, "relation_trace", child.node_id, "Description relation not preserved");
            }
            foreach (var node in plan.nodes.Where(n => n.activation.parent_id != null))
                if (!d.relations.Any(r => bySubject.TryGetValue(r.target_subject_id, out var child) && child.node_id == node.node_id))
                    Add(issues, "extra_relation", node.node_id, "Plan adds relation absent from description");
            // Parent chains must terminate at a root; cycles and orphan chains fail here.
            foreach (var node in plan.nodes)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var current = node;
                while (current?.activation.parent_id != null)
                {
                    if (!visited.Add(current.node_id)) { Add(issues, "cycle", node.node_id, "Activation graph contains a cycle"); break; }
                    if (!byNode.TryGetValue(current.activation.parent_id, out current)) break;
                }
            }
        }

        private static void ValidateCarrierOptions(SpellNode node, List<ValidationIssue> issues)
        {
            var o = node.options;
            var p = node.node_id;
            if (node.carrier == "projectile" && o.motion != "homing" && o.turn_mdeg_s != 0)
                Add(issues, "turn_rate", p, "Only homing projectiles may turn");
            if (node.carrier == "beam")
            {
                if (o.lifetime_ticks > 1 && o.tick_interval < 5)
                    Add(issues, "beam_interval", p, "Sustained beam interval must be at least five ticks");
                if ((o.chain_hops == 0) != (o.chain_radius_cm == 0))
                    Add(issues, "chain_radius", p, "Chain radius and hop count disagree");
            }
            if (node.carrier == "pulse" && o.front_width_cm > o.radius_cm)
                Add(issues, "front_width", p, "Front width exceeds radius");
            if (node.carrier == "trap" && o.arm_ticks >= o.lifetime_ticks)
                Add(issues, "arm_time", p, "Trap arms after expiration");
        }

        private static ResourceBounds ComputeBounds(SpellPlan plan, Dictionary<string, GeometryAsset> geometry,
            CompilationInput input, List<ValidationIssue> issues)
        {
            long instances = 0, applications = 0, colliders = 0, geometryBytes = 0;
            var byId = plan.nodes.ToDictionary(n => n.node_id, StringComparer.Ordinal);
            var endMemo = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var node in plan.nodes)
            {
                long count = (long)node.activation.copies * node.activation.max_activations;
                instances += count;
                int lifetime = node.options.lifetime_ticks ?? 0;
                long contacts;
                switch (node.carrier)
                {
                    case "projectile": contacts = 1 + (node.options.pierces ?? 0) + (node.options.bounces ?? 0); break;
                    case "beam": contacts = (1 + (node.options.chain_hops ?? 0)) *
                        (1 + (lifetime - 1) / (node.options.tick_interval ?? 1)); break;
                    case "field": contacts = 32L * (1 + lifetime / (node.options.tick_interval ?? 1)); break;
                    case "pulse": contacts = 32; break;
                    case "barrier": contacts = node.options.block_limit ?? 0; break;
                    default: contacts = node.options.trigger_limit ?? 0; break;
                }
                foreach (var effect in node.effects)
                {
                    long statusTicks = PeriodicEffects.Contains(effect.kind) ? effect.duration_ticks / 50 : 0;
                    applications += count * contacts * (1 + statusTicks);
                }
                if (node.blueprint_v2 != null) colliders += count;
                else if (node.carrier == "barrier" && geometry.TryGetValue(node.geometry_id, out var g))
                    colliders += count * Math.Min(64, Math.Max(0, g.points.Count - 1));
            }
            foreach (var pair in input.GeometryJson) geometryBytes += pair.Value?.Length ?? 0;
            foreach (var pair in input.MaskPng) geometryBytes += pair.Value?.Length ?? 0;
            long maxEnd = 0;
            foreach (var node in plan.nodes)
                maxEnd = Math.Max(maxEnd, End(node.node_id, new HashSet<string>(StringComparer.Ordinal)));
            if (instances > 128 || applications > 8192 || maxEnd > 1500 ||
                geometryBytes > 33554432 || colliders > 512)
                Add(issues, "resource_budget", "$.resource_bounds", "Plan exceeds a pessimistic resource limit");
            return new ResourceBounds { max_instances = Clamp(instances), max_effect_applications = Clamp(applications),
                max_end_tick = Clamp(maxEnd), geometry_bytes = Clamp(geometryBytes), max_colliders = Clamp(colliders) };

            long End(string id, HashSet<string> visiting)
            {
                if (endMemo.TryGetValue(id, out var cached)) return cached;
                if (!visiting.Add(id)) return 1501;
                var n = byId[id];
                long start = n.activation.parent_id == null ? n.activation.delay_ticks :
                    (byId.TryGetValue(n.activation.parent_id, out var parent) ? End(parent.node_id, visiting) : 1501) +
                    1 + n.activation.delay_ticks;
                long status = n.effects.Count == 0 ? 0 : n.effects.Max(e => e.duration_ticks);
                long end = start + (n.options.lifetime_ticks ?? 0) + status;
                visiting.Remove(id);
                endMemo[id] = end;
                return end;
            }
        }

        private static int Clamp(long number) => number > int.MaxValue ? int.MaxValue : (int)number;
        private static bool IsV2(SpellPlan plan) => plan.nodes.Any(node => node.blueprint_v2 != null);

        private static void CheckBlueprints(SpellPlan plan, CompilationInput input, List<ValidationIssue> issues)
        {
            if (!IsV2(plan)) return;
            if (plan.nodes.Any(node => node.blueprint_v2 == null))
                Add(issues, "v2_mixed_pipeline", "$.nodes", "A V2 plan cannot contain an archived V1 node");
            if (input.GeometryJson?.Count != 0 || input.MaskPng?.Count != 0)
                Add(issues, "v2_traced_geometry", "$.geometry", "V2 has a canonical core and cannot consume traced drawing geometry or masks");
            foreach (var node in plan.nodes.Where(node => node.blueprint_v2 != null))
                foreach (var issue in SpellBlueprintV2Validator.ValidateNode(node))
                    Add(issues, issue.Code, node.node_id + issue.Path.TrimStart('$'), issue.Message);
        }
        private static string RecipeSignature(string kind, int amount, int duration, string direction,
            string target, string @event) => kind + "|" + amount.ToString(CultureInfo.InvariantCulture) + "|" +
            duration.ToString(CultureInfo.InvariantCulture) + "|" + direction + "|" + target + "|" + @event;
        private static void Add(List<ValidationIssue> issues, string code, string path, string message)
        {
            if (issues.Count < 100) issues.Add(new ValidationIssue(code, path, message));
        }
    }

    public static class RulesPresenter
    {
        public static SpellDisplay Describe(SpellDescription description, SpellPlan plan)
        {
            var lines = new List<string>();
            foreach (var node in plan.nodes.OrderBy(n => n.node_id, StringComparer.Ordinal))
            {
                var effects = node.effects.Count == 0 ? "sans effet mécanique immédiat" :
                    string.Join(", ", node.effects.Select(e => e.kind + " " + e.amount + " sur " + e.target_filter +
                        " à " + e.@event + (e.duration_ticks > 0 ? " pendant " + e.duration_ticks + " ticks" : "")));
                var origin = node.activation.parent_id == null ? "au lancement" :
                    "après " + node.activation.parent_id + "." + node.activation.@event;
                lines.Add(node.carrier + " " + node.node_id + " : " + effects + ", " + origin +
                    ", durée " + node.options.lifetime_ticks + " ticks, échelle " + node.scale_cm + " cm.");
            }
            return new SpellDisplay { title = description.title,
                factual_description = string.Join(" ", lines), mechanical_lines = lines };
        }
    }
}

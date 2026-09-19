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
            ["burn"] = 10000, ["wet"] = 0, ["slow"] = 750
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
            var geometry = LoadGeometry(input, issues);
            CheckPlan(description, plan, geometry, issues);
            if (input.GeometryJson == null || input.MaskPng == null) return new CompilationResult { Issues = issues };
            if (issues.Count != 0) return new CompilationResult { Issues = issues };
            var bounds = ComputeBounds(plan, geometry, input, issues);
            CheckArtifactIds(input, issues);
            if (issues.Count != 0) return new CompilationResult { Issues = issues };

            var spell = new CompiledSpell {
                schema_version = "sp.compiled/1.0", spell_id = input.SpellId,
                parchment_id = input.ParchmentId,
                created_at = input.CreatedAt ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                versions = new SpellVersions { catalog = "sp.capabilities/1.0", compiler = Version,
                    geometry = "sp.geometry/1.0", rules_profile = "lab_v1", min_client = input.MinimumClientVersion },
                provenance = input.Provenance, signature_seed_hex = input.SignatureSeedHex,
                description_sha256 = plan.description_sha256, plan = plan,
                geometry_manifest = input.GeometryJson.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new GeometryManifestEntry { id = x.Key, kind = geometry[x.Key].kind,
                        artifact_id = input.GeometryArtifactIds[x.Key], sha256 = Sha256(x.Value), size_bytes = x.Value.Length }).ToList(),
                binary_assets = input.MaskPng.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new BinaryAssetEntry { file_name = x.Key, artifact_id = input.MaskArtifactIds[x.Key],
                        sha256 = Sha256(x.Value), size_bytes = x.Value.Length, media_type = "image/png" }).ToList(),
                resource_bounds = bounds,
                display = RulesPresenter.Describe(description, plan)
            };
            var payload = Serialize(spell);
            var schemaIssues = ContractJson.Validate(ContractJson.ParseStrict(payload), "compiled-spell");
            if (schemaIssues.Count != 0) return new CompilationResult { Issues = schemaIssues };
            return new CompilationResult { Spell = spell, PayloadUtf8 = payload,
                PayloadSha256 = Sha256(payload), Issues = Array.Empty<ValidationIssue>() };
        }

        public static byte[] Serialize(CompiledSpell spell)
        {
            var token = JObject.FromObject(spell, JsonSerializer.CreateDefault());
            foreach (var node in (JArray)token["plan"]["nodes"])
            {
                var options = (JObject)node["options"];
                foreach (var property in options.Properties().ToList())
                    if (property.Value.Type == JTokenType.Null) property.Remove();
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

        private static Dictionary<string, GeometryAsset> LoadGeometry(CompilationInput input, List<ValidationIssue> issues)
        {
            var result = new Dictionary<string, GeometryAsset>(StringComparer.Ordinal);
            if (input.GeometryJson == null || input.MaskPng == null) {
                Add(issues, "geometry_missing", "$.geometry", "Geometry and mask inputs are required"); return result;
            }
            if (input.GeometryJson.Count < 1 || input.GeometryJson.Count > 32 || input.MaskPng.Count > 32)
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

        public static IReadOnlyList<ValidationIssue> ValidatePlanJson(byte[] descriptionJson, byte[] planJson,
            IReadOnlyDictionary<string, byte[]> geometryJson, IReadOnlyDictionary<string, byte[]> maskPng)
        {
            try {
                var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionJson, "spell-description");
                var plan = ContractJson.DeserializeStrict<SpellPlan>(planJson, "spell-plan");
                var issues = new List<ValidationIssue>();
                CheckDescription(description, issues);
                if (plan.description_sha256 != Sha256(descriptionJson))
                    Add(issues, "description_hash", "$.description_sha256", "Plan is not tied to frozen description bytes");
                var input = new CompilationInput { GeometryJson = geometryJson, MaskPng = maskPng };
                var geometry = LoadGeometry(input, issues);
                CheckPlan(description, plan, geometry, issues);
                if (geometryJson != null && maskPng != null && issues.Count == 0)
                    ComputeBounds(plan, geometry, input, issues);
                return issues;
            }
            catch (Exception ex) { return new[] { new ValidationIssue("plan_json", "$", ex.Message) }; }
        }

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
                    f.dimension == "carrier" || f.dimension == "effect" || f.dimension == "event" || f.dimension == "target"))
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
                    if (!clauseIds.Contains(id)) Add(issues, "relation_clause", id, "Unknown relation clause");
            }
            foreach (var subject in subjects)
            {
                if (d.clauses.Where(c => c.subject_id == subject && c.kind == "mechanical")
                    .SelectMany(c => c.facts).Count(f => f.dimension == "carrier") != 1)
                    Add(issues, "subject_carrier", subject, "Subject requires exactly one carrier fact");
                if (d.shape_requests.Count(s => s.subject_id == subject) != 1)
                    Add(issues, "shape_request", subject, "Subject requires one geometry request");
            }
            foreach (var request in d.shape_requests)
                if (!subjects.Contains(request.subject_id)) Add(issues, "shape_subject", request.subject_id, "Unknown subject");
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
                if (!Emitted.ContainsKey(node.carrier)) { Add(issues, "carrier", p, "Unknown carrier"); continue; }
                var subjectClauses = d.clauses.Where(c => c.subject_id == node.subject_id && c.kind == "mechanical").ToArray();
                var subjectIds = subjectClauses.Select(c => c.id).ToHashSet(StringComparer.Ordinal);
                if (!subjectIds.SetEquals(node.clause_ids)) Add(issues, "clause_trace", p, "Node clauses differ from subject mechanical clauses");
                var facts = subjectClauses.SelectMany(c => c.facts).ToArray();
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
                var motion = facts.FirstOrDefault(f => f.dimension == "motion")?.value;
                foreach (var clause in subjectClauses)
                {
                    var linked = node.effects.Where(e => e.clause_ids.Contains(clause.id)).ToArray();
                    var clauseEffects = clause.facts.Where(f => f.dimension == "effect").Select(f => f.value)
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (!clauseEffects.SequenceEqual(linked.Select(e => e.kind).OrderBy(x => x, StringComparer.Ordinal)))
                        Add(issues, "clause_effect", clause.id, "Effect trace differs from this clause");
                    foreach (var fact in clause.facts)
                    {
                        if (fact.dimension == "target" && (linked.Length == 0 || linked.Any(e => e.target_filter != fact.value)))
                            Add(issues, "clause_target", clause.id, "Effect target differs from this clause");
                        if (fact.dimension == "event" && (linked.Length == 0 || linked.Any(e => e.@event != fact.value)))
                            Add(issues, "clause_event", clause.id, "Effect event differs from this clause");
                        if (fact.dimension == "affinity" && node.appearance.affinity != fact.value)
                            Add(issues, "clause_affinity", clause.id, "Appearance affinity differs from clause");
                        if (fact.dimension == "pattern" && node.appearance.pattern != fact.value)
                            Add(issues, "clause_pattern", clause.id, "Visible pattern differs from clause");
                    }
                }
                if (node.carrier == "projectile" && motion != null && node.options.motion != motion)
                    Add(issues, "motion_fact", p, "Projectile motion differs from description");
                if (motion == "stationary" && !new[] { "field", "barrier", "trap" }.Contains(node.carrier))
                    Add(issues, "motion_fact", p, "Stationary fact cannot map to this carrier");
                if (motion == "expanding" && node.carrier != "pulse")
                    Add(issues, "motion_fact", p, "Expanding fact requires pulse");
                var request = d.shape_requests.FirstOrDefault(s => s.subject_id == node.subject_id);
                if (!geometry.TryGetValue(node.geometry_id, out var main))
                    Add(issues, "geometry_reference", p, "Unknown main geometry");
                else if (request != null && (main.source_region != request.region || main.kind != request.role))
                    Add(issues, "geometry_role", p, "Geometry differs from requested region or role");
                if (!geometry.TryGetValue(node.appearance.signature_geometry_id, out var signature) || signature.kind != "silhouette")
                    Add(issues, "signature_geometry", p, "Visual signature needs existing silhouette");
                if ((node.carrier == "barrier" || (node.carrier == "projectile" && node.options.motion == "curve")) && main?.kind != "path")
                    Add(issues, "path_required", p, "This carrier requires a drawn path");
                if ((node.carrier == "field" || node.carrier == "trap") && main?.kind != "footprint")
                    Add(issues, "footprint_required", p, "This carrier requires a footprint");
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
                    if (new[] { "damage", "heal", "impulse" }.Contains(effect.kind) && effect.duration_ticks != 0)
                        Add(issues, "effect_duration", p, "Instant effect requires zero duration");
                    if (new[] { "burn", "wet", "slow" }.Contains(effect.kind) && effect.duration_ticks <= 0)
                        Add(issues, "effect_duration", p, "Status requires positive duration");
                    if (effect.kind == "burn" && effect.duration_ticks % 50 != 0)
                        Add(issues, "burn_duration", p, "Burn duration must be a multiple of 50 ticks");
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
                    long statusTicks = effect.kind == "burn" ? effect.duration_ticks / 50 : 0;
                    applications += count * contacts * (1 + statusTicks);
                }
                if (node.carrier == "barrier" && geometry.TryGetValue(node.geometry_id, out var g))
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

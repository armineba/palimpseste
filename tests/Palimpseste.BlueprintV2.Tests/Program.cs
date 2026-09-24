using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Core;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var results = new List<string>();
void Require(bool value, string name) { if (!value) throw new Exception(name); results.Add(name); }
byte[] Json(object value) {
    var token = JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
    foreach (var obj in ((JContainer)token).DescendantsAndSelf().OfType<JObject>())
        if (obj["activation"] is JObject activation && activation.Property("parent_id") == null) activation["parent_id"] = JValue.CreateNull();
    return Encoding.UTF8.GetBytes(token.ToString(Formatting.Indented));
}
T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
var blueprint = new SpellBlueprintV2 {
    schema_version = "sp.blueprint/2.0",
    intent = new SpellIntentV2 { semantic_subject = "meteor", primary_action = "straight charge", target_behavior = "strike first hostile or environment contact",
        perceived_material = "incandescent rock", motion_character = "heavy continuous travel", silhouette_priority = "single pointed meteor mass",
        visual_keywords = new List<string> { "ember", "continuous", "single body" }, gameplay_role = "projectile", impact_intent = "compress then dissolve",
        disappearance_intent = "coherent radial fade", figurative = true },
    archetype = new List<string> { "projectile" },
    identity = new SpellIdentitySpecV2 { hard_invariants = new List<string> { "one core", "fixed topology", "orange directional silhouette" },
        soft_invariants = new List<string> { "surface brightness" }, free_parameters = new List<string> { "emission" },
        topology = "fixed", coordinate_system = "local_cm_y_up_z_forward", element_count = 1, forward_axis = new[] { 0, 0, 1 },
        palette_rgb = new List<int[]> { new[] { 255, 125, 40 } }, landmarks = new List<SpellLandmarkV2> {
            new SpellLandmarkV2 { id = "tail", branch_index = -1, t_milli = 0 },
            new SpellLandmarkV2 { id = "front", branch_index = -1, t_milli = 1000 } } },
    structural_core = new StructuralCoreV2 { kind = "radial_volume", selection_reason = "A meteor is one closed directional mass rather than separate flames",
        size_cm = new[] { 100, 100, 250 }, color_rgb = new[] { 255, 125, 40 }, longitudinal_segments = 64, radial_segments = 32,
        entity_count = 1, inner_radius_milli = 0, branches = new List<CoreBranchV2>(), control_points = new List<CoreControlPointV2> {
            new CoreControlPointV2 { position_cm = new[] { 0, 0, -150 }, radius_cm = 4, width_cm = 8 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, -100 }, radius_cm = 9, width_cm = 18 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, -50 }, radius_cm = 18, width_cm = 36 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, 0 }, radius_cm = 36, width_cm = 72 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, 40 }, radius_cm = 46, width_cm = 92 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, 70 }, radius_cm = 32, width_cm = 64 },
            new CoreControlPointV2 { position_cm = new[] { 0, 0, 95 }, radius_cm = 10, width_cm = 20 } } },
    motion = new MotionSpecV2 { duration_ms = 6000, trajectory = "straight", path_cm = new List<int[]> { new[] { 0, 0, 0 }, new[] { 0, 0, 2500 } },
        deformation = "pulse", amplitude_cm = 2, frequency_mhz = 600, angular_speed_mdeg_s = 0, acceleration_cm_s2 = 0,
        anticipation_milli = 0, follow_through_milli = 200, secondary_motion_milli = 100, impact_transition = "stop", dissipation = "dissolve" },
    phases = new SpellPhasesV2 { appearance_end_milli = 100, active_end_milli = 850, active_loop = false },
    rendering_layers = new RenderingLayersV2 { core_surface = "plasma", core_opacity_milli = 850, core_emission_milli = 1000,
        secondary_kind = "flow", secondary_intensity_milli = 300, atmosphere_kind = "sparks", atmosphere_particles = 24, resource_id = "kpp_spark_01" },
    physics = new PhysicsContractV2 { collider = "sphere", collision_owner = "root", collision_response = "stop", visual_response = "impact",
        affected_components = new List<string> { "core", "secondary", "atmosphere" }, lifetime_behavior = "until_contact_or_duration",
        rigidbody_count = 0, decorative_physics = false, independent_entities = false },
    impact = new ImpactSpecV2 { contact_pose = "align_normal", hit_flash_milli = 600, deformation = "compress", reaction_duration_ms = 300,
        secondary_emission = 24, dissipation_direction = "normal", surviving_core_duration_ms = 600, destruction_mode = "dissolve" },
    disappearance = new DisappearanceSpecV2 { mode = "dissolve", direction = "radial", residual_duration_ms = 500 },
    unity_implementation = new UnityImplementationPlanV2 { root = "Spell_ROOT", core_renderer = "canonical_mesh", deformation_system = "canonical_motion_v2",
        motion_controller = "spell_runtime", collider_strategy = "root_only", vfx_graph_systems = 0, particle_systems = 2,
        shader = "Palimpseste/CanonicalSpellV2", material_count = 3, trails = "none", audio_hook = "carrier", impact_system = "canonical_impact_v2", lifetime_controller = "spell_runtime" },
    validation_rules = new ValidationRulesV2 { required_gates = SpellBlueprintV2Limits.Gates.ToList(), blind_semantic_check = true,
        reject_visible_primitives = true, require_fixed_topology = true, maximum_vertices = 65536, maximum_particles = 96,
        maximum_draw_calls = 8, maximum_materials = 4, maximum_cpu_microseconds = 10000, maximum_gpu_microseconds = 10000, maximum_transparent_layers = 16 }
};
Require(SpellBlueprintV2Validator.Validate(blueprint).Count == 0, "valid canonical meteor blueprint");
foreach (var pair in new[] { ("creature", "branched_surface"), ("beam", "beam"), ("portal", "planar_field"),
    ("environmental", "vortex_surface"), ("multi_projectile", "controlled_swarm"), ("ribbon", "ribbon") })
    Require(SpellBlueprintV2Validator.IsCompatible(pair.Item1, pair.Item2), "archetype mapping " + pair.Item1);
Require(!SpellBlueprintV2Validator.IsCompatible("creature", "radial_volume"), "creature cannot silently become a radial blob");
var bad = Clone(blueprint); bad.structural_core.control_points[1].position_cm = (int[])bad.structural_core.control_points[0].position_cm.Clone();
Require(SpellBlueprintV2Validator.Validate(bad).Any(issue => issue.Code == "v2_degenerate_path"), "reject degenerate canonical path");
bad = Clone(blueprint); bad.physics.decorative_physics = true;
Require(SpellBlueprintV2Validator.Validate(bad).Count > 0, "reject decorative rigidbody physics");
bad = Clone(blueprint); bad.validation_rules.required_gates[0] = "B_continuity";
Require(SpellBlueprintV2Validator.Validate(bad).Count > 0, "reject missing structural gate");
bad = Clone(blueprint); bad.structural_core.longitudinal_segments = int.MaxValue;
Require(SpellBlueprintV2Safety.Validate(bad).Count > 0, "Unity allocation preflight rejects hostile resolution");
var loop = Clone(blueprint); loop.phases.active_loop = true;
loop.motion.frequency_mhz = 2000; loop.motion.angular_speed_mdeg_s = 720000;
Require(SpellBlueprintV2Safety.Validate(loop).Count == 0, "stable loop accepts nine exact rotations and oscillations without rate quantization");
bad = Clone(loop); bad.motion.angular_speed_mdeg_s = 360000;
Require(SpellBlueprintV2Safety.Validate(bad).Any(error => error.Contains("whole number of rotations")), "stable loop rejects a half-rotation seam");
bad = Clone(loop); bad.motion.frequency_mhz = 600;
Require(SpellBlueprintV2Safety.Validate(bad).Any(error => error.Contains("whole number of deformation cycles")), "stable loop rejects a fractional oscillation seam");
bad = Clone(blueprint); bad.archetype[0] = "environmental"; bad.structural_core.kind = "vortex_surface";
Require(SpellBlueprintV2Validator.Validate(bad).Any(issue => issue.Code == "v2_vortex_motion"), "stationary vortex rejected before construction");

var desc = JObject.Parse(File.ReadAllText(Path.Combine(root, "examples/01_description_illustrative.json")));
desc["title"] = "V2 isolated meteor fixture"; desc["summary"] = "Deterministic developer fixture; never a player spell or model-generated result.";
desc["observations"] = new JArray(desc["observations"][0].DeepClone()); desc["observations"][0]["region"] = "full";
desc["clauses"] = new JArray(desc["clauses"][0].DeepClone()); desc["clauses"][0]["observation_ids"] = new JArray("o1");
desc["clauses"][0]["text"] = "A straight meteor hits the first hostile target for controlled fire damage.";
foreach (var fact in desc["clauses"][0]["facts"]) if ((string)fact["dimension"] == "motion") fact["value"] = "straight";
((JArray)desc["clauses"][0]["facts"]).Add(new JObject { ["dimension"] = "visual_form", ["value"] = "meteor" });
((JArray)desc["clauses"][0]["facts"]).Add(new JObject { ["dimension"] = "palette", ["value"] = "ember" });
desc["relations"] = new JArray(); desc["shape_requests"] = new JArray();
var behavior = new SpellBehaviorIntent { subject_id = "s0", origin = "muzzle", orientation = "cast_forward", attachment = "world",
    phenomenon = "static", axis = "y", sense = "counterclockwise", intensity = "brisk", travel = "straight" };
desc["behaviors"] = JArray.FromObject(new[] { behavior });
desc["lifecycle"] = JArray.FromObject(new[] { new SpellLifecycleDescription { subject_id = "s0", appearance = "One core forms continuously.",
    active = "One meteor travels forward.", contact = "Compress on contact before dissolving.", expiration = "Fade the coherent remaining core." } });
var descriptionBytes = Encoding.UTF8.GetBytes(desc.ToString(Formatting.Indented));
Require(SpellCompiler.ValidateDescriptionJson(descriptionBytes).Count == 0, "fixture description remains mechanically traceable");
var plan = JsonConvert.DeserializeObject<SpellPlan>(File.ReadAllText(Path.Combine(root, "examples/02_plan_illustratif.json")));
plan.nodes.RemoveAt(1); var node = plan.nodes[0]; node.blueprint_v2 = blueprint; node.geometry_id = "canonical.v2"; node.scale_cm = 100;
node.appearance.signature_geometry_id = null; node.appearance.form = "meteor"; node.appearance.palette = "ember"; node.appearance.resource_id = "kpp_spark_01";
node.behavior = behavior; node.physics = new SpellPhysicsProfile { offset_cm = new[] { 0, 0, 0 } }; node.options.motion = "straight";
node.options.lifetime_ticks = 300; node.options.speed_cm_s = 1000; node.options.radius_cm = 40;
plan.description_sha256 = SpellCompiler.Sha256(descriptionBytes); plan.reference_research_sha256 = SpellCompiler.Sha256(Encoding.UTF8.GetBytes("fixture: controlled source research binding; not a real provider result"));
var input = new CompilationInput { DescriptionJson = descriptionBytes, PlanJson = Json(plan), GeometryJson = new Dictionary<string, byte[]>(), MaskPng = new Dictionary<string, byte[]>(),
    GeometryArtifactIds = new Dictionary<string, string>(), MaskArtifactIds = new Dictionary<string, string>(), SpellId = "v2-isolated-meteor-fixture", ParchmentId = "v2-developer-fixture-only",
    SignatureSeedHex = "e10a330a765bc981", CreatedAt = "2026-09-25T00:00:00Z", ReferenceResearchSha256 = plan.reference_research_sha256,
    Provenance = new SpellProvenance { mode = "fixture", prompt_a_version = "fixture.manual", prompt_b_version = "fixture.manual" } };
var compiled = SpellCompiler.Compile(input);
File.WriteAllBytes(Path.Combine(root, ".runtime/v2-contract-candidate.json"), input.PlanJson);
if (!compiled.Success) throw new Exception(string.Join("\n", compiled.Issues));
Require(compiled.Spell.versions.min_client == "1.8.0", "V2 compiler raises client requirement to 1.8");
Require(compiled.Spell.geometry_manifest.Count == 0 && compiled.Spell.visual_reference == null, "V2 core does not trace ink or depend on a generated frame");
Require(ContractJson.Validate(ContractJson.ParseStrict(compiled.PayloadUtf8), "compiled-spell").Count == 0, "V2 compiled schema routing works");
var wrongDuration = Clone(plan); wrongDuration.nodes[0].blueprint_v2.motion.duration_ms += 20;
Require(SpellCompiler.ValidatePlanJson(descriptionBytes, Json(wrongDuration), input.GeometryJson, input.MaskPng, null, input.ReferenceResearchSha256)
    .Any(issue => issue.Code == "v2_lifetime_trace"), "reject mismatch between animation and actual carrier lifetime");
var mixed = JObject.Parse(Encoding.UTF8.GetString(Json(plan))); ((JArray)mixed["nodes"]).Add(JObject.FromObject(new SpellNode { node_id = "legacy" }));
Require(ContractJson.Validate(mixed, "spell-plan").Count > 0, "reject mixed V1 and V2 plan");
var oldBytes = File.ReadAllBytes(Path.Combine(root, "examples/03_paquet_illustratif.json"));
var old = ContractJson.DeserializeStrict<CompiledSpell>(oldBytes, "compiled-spell");
Require(old.plan.nodes.All(n => n.blueprint_v2 == null), "archived V1 packets remain V1");
var oldSerialized = SpellCompiler.Serialize(old);
Require(!Encoding.UTF8.GetString(oldSerialized).Contains("blueprint_v2"), "V1 serialization omits V2 field completely");
Require(ContractJson.Validate(ContractJson.ParseStrict(oldSerialized), "compiled-spell").Count == 0, "V1 serialization still validates against its frozen schema");
var expectedProvider = JObject.Parse(File.ReadAllText(Path.Combine(root, "contracts/codex/model-b-v2.output-schema.json")));
Require(expectedProvider.DescendantsAndSelf().OfType<JObject>().Where(o => o["type"]?.Type == JTokenType.String && (string)o["type"] == "object" && o["properties"] is JObject)
    .All(o => ((JObject)o["properties"]).Properties().Select(p => p.Name).ToHashSet().SetEquals(((JArray)o["required"]).Values<string>())), "provider V2 schema declares every object property required");

var fixtureDir = Path.Combine(root, "examples/v2-validation"); Directory.CreateDirectory(fixtureDir);
File.WriteAllBytes(Path.Combine(fixtureDir, "description.json"), descriptionBytes);
File.WriteAllBytes(Path.Combine(fixtureDir, "blueprint.json"), Json(blueprint));
File.WriteAllBytes(Path.Combine(fixtureDir, "plan.json"), input.PlanJson);
File.WriteAllBytes(Path.Combine(fixtureDir, "spell.json"), compiled.PayloadUtf8);
var cache = Path.Combine(root, ".runtime/v2-fixture-cache"); Directory.CreateDirectory(cache); Directory.CreateDirectory(Path.Combine(cache, "artifacts"));
File.WriteAllBytes(Path.Combine(cache, "spell.json"), compiled.PayloadUtf8);
File.WriteAllText(Path.Combine(cache, "spell.json.sha256"), compiled.PayloadSha256, new UTF8Encoding(false));
File.WriteAllBytes(Path.Combine(cache, "description.json"), descriptionBytes);
File.WriteAllBytes(Path.Combine(cache, "blueprints.json"), Json(plan.nodes.Select(n => n.blueprint_v2).ToArray()));
// Separate numerical-loop variant; never replaces the base fixture or a player spell.
var loopPlan = Clone(plan);
loopPlan.nodes[0].blueprint_v2.phases.active_loop = true;
loopPlan.nodes[0].blueprint_v2.motion.frequency_mhz = 2000; // 9 cycles over 4.5 seconds.
// Keep the frozen description's non-rotating physics. This variant closes the pulse cycle.
var loopInput = new CompilationInput { DescriptionJson = descriptionBytes, PlanJson = Json(loopPlan), GeometryJson = input.GeometryJson,
    MaskPng = input.MaskPng, GeometryArtifactIds = input.GeometryArtifactIds, MaskArtifactIds = input.MaskArtifactIds,
    SpellId = "v2-isolated-loop-fixture", ParchmentId = input.ParchmentId, SignatureSeedHex = input.SignatureSeedHex,
    CreatedAt = input.CreatedAt, ReferenceResearchSha256 = input.ReferenceResearchSha256, Provenance = input.Provenance };
var loopCompiled = SpellCompiler.Compile(loopInput);
Require(loopCompiled.Success, "stable loop fixture compiles with exact declared cycles");
var loopCache = Path.Combine(root, ".runtime/v2-loop-fixture-cache"); Directory.CreateDirectory(loopCache); Directory.CreateDirectory(Path.Combine(loopCache, "artifacts"));
File.WriteAllBytes(Path.Combine(loopCache, "spell.json"), loopCompiled.PayloadUtf8);
File.WriteAllText(Path.Combine(loopCache, "spell.json.sha256"), loopCompiled.PayloadSha256, new UTF8Encoding(false));
File.WriteAllBytes(Path.Combine(loopCache, "description.json"), descriptionBytes);
File.WriteAllBytes(Path.Combine(loopCache, "blueprints.json"), Json(loopPlan.nodes.Select(n => n.blueprint_v2).ToArray()));
var evidence = Path.Combine(root, "evidence/public/v2"); Directory.CreateDirectory(evidence);
File.WriteAllBytes(Path.Combine(evidence, "contracts-tests.json"), Json(new { completed_at = DateTimeOffset.UtcNow, passed = results.Count,
    failed = 0, tests = results, fixture_sha256 = compiled.PayloadSha256, fixture_only = true, model_calls = 0, unity_visual_acceptance = "not_measured_by_this_test" }));
Console.WriteLine("PASS " + results.Count + " V2 contract/compiler checks; isolated fixture cache: " + cache);

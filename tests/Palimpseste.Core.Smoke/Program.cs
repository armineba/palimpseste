using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Core;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
byte[] Read(string relative) => File.ReadAllBytes(Path.Combine(root, relative));
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

var descriptionJson = Read("examples/01_description_illustrative.json");
var planJson = Read("examples/02_plan_illustratif.json");
Require(SpellCompiler.ValidateDescriptionJson(descriptionJson).Count == 0, "Fixture description rejected");
var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionJson, "spell-description");
var inkPng = Read("examples/ink_synthetique.png");
var ink = PngCodec.DecodeRgba(inkPng);
Require(PngCodec.DecodeRgba(PngCodec.EncodeRgba(ink.Rgba, ink.Width, ink.Height)).Rgba.SequenceEqual(ink.Rgba),
    "PNG round trip lost pixel data");
var corrupt = (byte[])inkPng.Clone(); corrupt[33] ^= 1;
bool rejectedCrc = false;
try { PngCodec.DecodeRgba(corrupt); } catch (InvalidDataException) { rejectedCrc = true; }
Require(rejectedCrc, "PNG CRC corruption not rejected");

var resolved = GeometryResolver.Resolve(inkPng, description);
Require(resolved.Success, "Geometry extraction failed: " + string.Join("; ", resolved.Issues));
Require(resolved.Assets.ContainsKey("ring.path.0") && resolved.Assets.ContainsKey("outer.footprint.0") &&
    resolved.Assets.ContainsKey("full.silhouette.0"), "Expected real-pixel geometries missing");
Require(resolved.Assets["ring.path.0"].points.Count >= 2, "Extracted path degenerate");
var artifactIds = resolved.GeometryJson.Keys.ToDictionary(k => k, k => "a" + Guid.NewGuid().ToString("N"));
var maskIds = resolved.MaskPng.Keys.ToDictionary(k => k, k => "a" + Guid.NewGuid().ToString("N"));
var fixture = JObject.Parse(Encoding.UTF8.GetString(Read("examples/03_paquet_illustratif.json")));
var input = new CompilationInput { DescriptionJson = descriptionJson, PlanJson = planJson,
    GeometryJson = resolved.GeometryJson, MaskPng = resolved.MaskPng,
    GeometryArtifactIds = artifactIds, MaskArtifactIds = maskIds,
    SpellId = "fixture-smoke", ParchmentId = "fixture-support", SignatureSeedHex = "e10a330a765bc981",
    Provenance = fixture["provenance"].ToObject<SpellProvenance>() };
var compiled = SpellCompiler.Compile(input);
Require(compiled.Success, "Fixture did not compile: " + string.Join("; ", compiled.Issues));
Require(compiled.Spell.resource_bounds.max_end_tick >= 476, "Residual status omitted from temporal bound");
Require(SpellCompiler.Sha256(compiled.PayloadUtf8) == compiled.PayloadSha256, "Payload hash differs");
Require(ContractJson.Validate(ContractJson.ParseStrict(compiled.PayloadUtf8), "compiled-spell").Count == 0,
    "Compiled payload fails contract");
Require(((JObject)ContractJson.ParseStrict(compiled.PayloadUtf8)["plan"]["nodes"][0]["appearance"])
    .Property("palette") == null, "Legacy compiled spell unexpectedly requires a palette");
// Codex structured output requires every declared object property, while a
// legacy description has no palette to copy. The nullable transport value is
// accepted by the plan validator and omitted from the published packet.
var plannerSchema = JObject.Parse(Encoding.UTF8.GetString(Read("contracts/codex/model-b.output-schema.json")));
foreach (var schemaObject in plannerSchema.DescendantsAndSelf().OfType<JObject>()
    .Where(obj => obj["type"]?.Type == JTokenType.String && (string)obj["type"] == "object" && obj["properties"] is JObject))
{
    var properties = ((JObject)schemaObject["properties"]).Properties().Select(property => property.Name).ToHashSet();
    var required = ((JArray)schemaObject["required"]).Select(value => (string)value).ToHashSet();
    Require(properties.SetEquals(required), "Codex output schema has optional object properties");
}
var nullablePalettePlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
foreach (var node in (JArray)nullablePalettePlan["nodes"])
    node["appearance"]["palette"] = JValue.CreateNull();
var nullablePaletteBytes = Encoding.UTF8.GetBytes(nullablePalettePlan.ToString(Newtonsoft.Json.Formatting.None));
Require(SpellCompiler.ValidatePlanJson(descriptionJson, nullablePaletteBytes, resolved.GeometryJson, resolved.MaskPng).Count == 0,
    "Legacy description with Codex-required null palette was rejected");
input.PlanJson = nullablePaletteBytes;
var nullableCompiled = SpellCompiler.Compile(input);
Require(nullableCompiled.Success && ((JObject)ContractJson.ParseStrict(nullableCompiled.PayloadUtf8)["plan"]["nodes"][0]["appearance"])
    .Property("palette") == null, "Legacy null palette leaked into the published packet");
input.PlanJson = planJson;

// In the image-driven profile the interpreter does not divide the parchment
// into semantic regions. Every subject may use the real geometry of the whole
// drawing; the compiler still checks the referenced kind and all mechanics.
var wholeDescription = JObject.Parse(Encoding.UTF8.GetString(descriptionJson));
wholeDescription["shape_requests"] = new JArray();
foreach (var observation in (JArray)wholeDescription["observations"])
    observation["region"] = "full";
var wholeDescriptionBytes = Encoding.UTF8.GetBytes(wholeDescription.ToString(Newtonsoft.Json.Formatting.None));
Require(SpellCompiler.ValidateDescriptionJson(wholeDescriptionBytes).Count == 0,
    "Whole-image description without region requests rejected");
Require(SpellCompiler.ValidateWholeImageDescriptionJson(wholeDescriptionBytes, false).Count == 0 &&
    SpellCompiler.ValidateWholeImageDescriptionJson(wholeDescriptionBytes, true)
        .Any(i => i.Code == "palette_required"),
    "New free-canvas palette gate did not preserve legacy descriptions");
var paletteWholeDescription = (JObject)wholeDescription.DeepClone();
foreach (var firstClause in ((JArray)paletteWholeDescription["clauses"])
    .GroupBy(c => (string)c["subject_id"]).Select(group => group.First()))
    ((JArray)firstClause["facts"]).Add(new JObject { ["dimension"] = "palette", ["value"] = "ember" });
var paletteWholeBytes = Encoding.UTF8.GetBytes(paletteWholeDescription.ToString(Newtonsoft.Json.Formatting.None));
Require(SpellCompiler.ValidateWholeImageDescriptionJson(paletteWholeBytes, true).Count == 0,
    "New free-canvas description with one palette per subject was rejected");
((JArray)paletteWholeDescription["clauses"][0]["facts"])
    .Add(new JObject { ["dimension"] = "palette", ["value"] = "ember" });
Require(SpellCompiler.ValidateWholeImageDescriptionJson(
    Encoding.UTF8.GetBytes(paletteWholeDescription.ToString(Newtonsoft.Json.Formatting.None)), true)
    .Any(i => i.Code == "palette_required"),
    "Duplicate palette facts escaped the new-drawing gate");
var stationaryBeam = (JObject)wholeDescription.DeepClone();
var firstSubjectFacts = (JArray)stationaryBeam["clauses"][0]["facts"];
firstSubjectFacts.First(f => (string)f["dimension"] == "carrier")["value"] = "beam";
firstSubjectFacts.First(f => (string)f["dimension"] == "motion")["value"] = "stationary";
Require(SpellCompiler.ValidateDescriptionJson(
    Encoding.UTF8.GetBytes(stationaryBeam.ToString(Newtonsoft.Json.Formatting.None)))
    .Any(i => i.Code == "motion_fact"),
    "Interpreter description accepted a stationary beam that no planner can compile");
var directedBeam = (JObject)stationaryBeam.DeepClone();
((JArray)directedBeam["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "motion").Remove();
Require(SpellCompiler.ValidateDescriptionJson(
    Encoding.UTF8.GetBytes(directedBeam.ToString(Newtonsoft.Json.Formatting.None))).Count == 0,
    "Directed beam without an incompatible motion fact was rejected");
var expandingProjectile = (JObject)wholeDescription.DeepClone();
((JArray)expandingProjectile["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "motion")["value"] = "expanding";
Require(SpellCompiler.ValidateDescriptionJson(
    Encoding.UTF8.GetBytes(expandingProjectile.ToString(Newtonsoft.Json.Formatting.None)))
    .Any(i => i.Code == "motion_fact"),
    "Interpreter description accepted expanding motion on a non-pulse carrier");
var curvedBeam = (JObject)stationaryBeam.DeepClone();
((JArray)curvedBeam["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "motion")["value"] = "curve";
Require(SpellCompiler.ValidateDescriptionJson(
    Encoding.UTF8.GetBytes(curvedBeam.ToString(Newtonsoft.Json.Formatting.None)))
    .Any(i => i.Code == "motion_fact"),
    "Interpreter description accepted projectile travel motion on a beam");
var wholeGeometry = GeometryResolver.Resolve(inkPng,
    ContractJson.DeserializeStrict<SpellDescription>(wholeDescriptionBytes, "spell-description"));
Require(wholeGeometry.Success && wholeGeometry.Assets.ContainsKey("full.path.0") &&
    wholeGeometry.Assets.ContainsKey("full.footprint.0") &&
    wholeGeometry.Assets.ContainsKey("full.silhouette.0") &&
    wholeGeometry.Assets.ContainsKey("full.distribution.0"),
    "Whole-image geometry bank missing valid pixel shapes");
Require(wholeGeometry.Assets.Values.All(asset => asset.algorithm == GeometryResolver.WholeCanvasVersion) &&
    resolved.Assets.Values.All(asset => asset.algorithm == GeometryResolver.Version),
    "Whole-canvas and legacy extraction versions were not distinguished");
var wholePlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
wholePlan["description_sha256"] = SpellCompiler.Sha256(wholeDescriptionBytes);
wholePlan["nodes"][0]["geometry_id"] = "full.path.0";
wholePlan["nodes"][1]["geometry_id"] = "full.footprint.0";
var wholePlanBytes = Encoding.UTF8.GetBytes(wholePlan.ToString(Newtonsoft.Json.Formatting.None));
var wholeInput = new CompilationInput { DescriptionJson = wholeDescriptionBytes, PlanJson = wholePlanBytes,
    GeometryJson = wholeGeometry.GeometryJson, MaskPng = wholeGeometry.MaskPng,
    GeometryArtifactIds = wholeGeometry.GeometryJson.Keys.ToDictionary(k => k, k => "a" + Guid.NewGuid().ToString("N")),
    MaskArtifactIds = wholeGeometry.MaskPng.Keys.ToDictionary(k => k, k => "a" + Guid.NewGuid().ToString("N")),
    SpellId = "whole-image-smoke", ParchmentId = "whole-image-support", SignatureSeedHex = "e10a330a765bc981",
    Provenance = fixture["provenance"].ToObject<SpellProvenance>() };
Require(SpellCompiler.Compile(wholeInput).Success, "Whole-image drawing did not compile to a controlled spell");
var wrongWholePlan = (JObject)wholePlan.DeepClone();
wrongWholePlan["nodes"][1]["geometry_id"] = "full.path.0";
Require(SpellCompiler.ValidatePlanJson(wholeDescriptionBytes,
    Encoding.UTF8.GetBytes(wrongWholePlan.ToString(Newtonsoft.Json.Formatting.None)),
    wholeGeometry.GeometryJson, wholeGeometry.MaskPng).Any(i => i.Code == "footprint_required"),
    "Image-driven plan bypassed carrier geometry validation");

var badPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
badPlan["nodes"][0]["effects"][0]["kind"] = "heal";
var badIssues = SpellCompiler.ValidatePlanJson(descriptionJson, Encoding.UTF8.GetBytes(badPlan.ToString()),
    resolved.GeometryJson, resolved.MaskPng);
Require(badIssues.Count > 0, "Changed effect was accepted");
var visualDescription = JObject.Parse(Encoding.UTF8.GetString(descriptionJson));
((JArray)visualDescription["clauses"]).Add(new JObject {
    ["id"] = "c_visual", ["subject_id"] = "s0", ["kind"] = "visual_only",
    ["text"] = "Le trait reste plein, avec des braises rouge brique.", ["observation_ids"] = new JArray("o1"),
    ["facts"] = new JArray(new JObject { ["dimension"] = "pattern", ["value"] = "solid" },
        new JObject { ["dimension"] = "palette", ["value"] = "ember" })
});
var visualPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
((JArray)visualPlan["nodes"][0]["clause_ids"]).Add("c_visual");
visualPlan["nodes"][0]["appearance"]["palette"] = "ember";
var visualDescriptionBytes = Encoding.UTF8.GetBytes(visualDescription.ToString(Newtonsoft.Json.Formatting.None));
visualPlan["description_sha256"] = SpellCompiler.Sha256(visualDescriptionBytes);
Require(SpellCompiler.ValidatePlanJson(visualDescriptionBytes,
    Encoding.UTF8.GetBytes(visualPlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Count == 0,
    "A cited visual clause was rejected");
var visualInput = new CompilationInput { DescriptionJson = visualDescriptionBytes,
    PlanJson = Encoding.UTF8.GetBytes(visualPlan.ToString(Newtonsoft.Json.Formatting.None)),
    GeometryJson = resolved.GeometryJson, MaskPng = resolved.MaskPng,
    GeometryArtifactIds = artifactIds, MaskArtifactIds = maskIds,
    SpellId = "palette-smoke", ParchmentId = "palette-support", SignatureSeedHex = "e10a330a765bc981",
    Provenance = fixture["provenance"].ToObject<SpellProvenance>() };
var visualCompiled = SpellCompiler.Compile(visualInput);
Require(visualCompiled.Success &&
    (string)ContractJson.ParseStrict(visualCompiled.PayloadUtf8)["plan"]["nodes"][0]["appearance"]["palette"] == "ember",
    "Text palette did not survive compilation");
var mismatchedPalettePlan = (JObject)visualPlan.DeepClone();
mismatchedPalettePlan["nodes"][0]["appearance"]["palette"] = "lava";
Require(SpellCompiler.ValidatePlanJson(visualDescriptionBytes,
    Encoding.UTF8.GetBytes(mismatchedPalettePlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "palette_trace"),
    "A palette differing from interpreter text was accepted");
var inventedPalettePlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
inventedPalettePlan["nodes"][0]["appearance"]["palette"] = "ember";
Require(SpellCompiler.ValidatePlanJson(descriptionJson,
    Encoding.UTF8.GetBytes(inventedPalettePlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "palette_trace"),
    "Planner invented a palette absent from interpreter text");
var unsupportedPalettePlan = (JObject)visualPlan.DeepClone();
unsupportedPalettePlan["nodes"][0]["appearance"]["palette"] = "custom_shader";
Require(SpellCompiler.ValidatePlanJson(visualDescriptionBytes,
    Encoding.UTF8.GetBytes(unsupportedPalettePlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "plan_json"),
    "An unsupported VFX palette escaped the plan schema");
var mismatchedVisualPlan = (JObject)visualPlan.DeepClone();
mismatchedVisualPlan["nodes"][0]["appearance"]["pattern"] = "dotted";
Require(SpellCompiler.ValidatePlanJson(visualDescriptionBytes,
    Encoding.UTF8.GetBytes(mismatchedVisualPlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "clause_pattern"),
    "A visual clause mismatch was accepted");
var omittedVisualPlan = (JObject)visualPlan.DeepClone();
((JArray)omittedVisualPlan["nodes"][0]["clause_ids"])
    .First(id => (string)id == "c_visual").Remove();
Require(SpellCompiler.ValidatePlanJson(visualDescriptionBytes,
    Encoding.UTF8.GetBytes(omittedVisualPlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "clause_trace"),
    "An omitted visual clause was accepted");
var visualRelationDescription = (JObject)visualDescription.DeepClone();
((JArray)visualRelationDescription["relations"])[0]["clause_ids"] = new JArray("c_visual");
Require(SpellCompiler.ValidateDescriptionJson(
    Encoding.UTF8.GetBytes(visualRelationDescription.ToString(Newtonsoft.Json.Formatting.None)))
    .Any(i => i.Code == "relation_clause"),
    "A relation justified only by a visual clause was accepted");
var broadenedCarrierPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
broadenedCarrierPlan["nodes"][0]["options"]["contact_filter"] = "all_actors";
Require(SpellCompiler.ValidatePlanJson(descriptionJson,
    Encoding.UTF8.GetBytes(broadenedCarrierPlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "carrier_target"),
    "A carrier receiver filter broader than the description was accepted");
var lifecycleDescription = JObject.Parse(Encoding.UTF8.GetString(descriptionJson));
var lifecycleFacts = (JArray)lifecycleDescription["clauses"][0]["facts"];
foreach (var fact in lifecycleFacts.Where(f => new[] { "effect", "target" }.Contains((string)f["dimension"])).ToList())
    fact.Remove();
lifecycleFacts.First(f => (string)f["dimension"] == "event")["value"] = "spawn";
var lifecyclePlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
((JArray)lifecyclePlan["nodes"][0]["effects"]).Clear();
lifecyclePlan["nodes"][0]["options"]["contact_filter"] = "environment";
var lifecycleDescriptionBytes = Encoding.UTF8.GetBytes(lifecycleDescription.ToString(Newtonsoft.Json.Formatting.None));
lifecyclePlan["description_sha256"] = SpellCompiler.Sha256(lifecycleDescriptionBytes);
Require(SpellCompiler.ValidatePlanJson(lifecycleDescriptionBytes,
    Encoding.UTF8.GetBytes(lifecyclePlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Count == 0,
    "A carrier lifecycle event without an effect was rejected");
var invalidLifecycleDescription = (JObject)lifecycleDescription.DeepClone();
((JArray)invalidLifecycleDescription["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "event")["value"] = "enter";
var invalidLifecycleBytes = Encoding.UTF8.GetBytes(invalidLifecycleDescription.ToString(Newtonsoft.Json.Formatting.None));
var invalidLifecyclePlan = (JObject)lifecyclePlan.DeepClone();
invalidLifecyclePlan["description_sha256"] = SpellCompiler.Sha256(invalidLifecycleBytes);
Require(SpellCompiler.ValidatePlanJson(invalidLifecycleBytes,
    Encoding.UTF8.GetBytes(invalidLifecyclePlan.ToString(Newtonsoft.Json.Formatting.None)),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "clause_event"),
    "An event not emitted by the carrier was accepted");

// A and B must expose exactly the runtime effect catalog. Keep the palette
// present in both structured outputs: A chooses it; B copies it or null.
var catalog = JObject.Parse(Encoding.UTF8.GetString(Read("contracts/capability-catalog.json")));
var catalogKinds = ((JArray)catalog["effects"]).Select(e => (string)e["id"]).ToHashSet();
string[] effectSchemas = { "contracts/spell-description.schema.json", "contracts/spell-plan.schema.json",
    "contracts/compiled-spell.schema.json", "contracts/model-a.response-format.json",
    "contracts/model-b.response-format.json", "contracts/codex/model-a.output-schema.json",
    "contracts/codex/model-b.output-schema.json" };
foreach (var name in effectSchemas)
{
    var schema = JObject.Parse(Encoding.UTF8.GetString(Read(name)));
    var kinds = schema.DescendantsAndSelf().OfType<JArray>()
        .Where(array => array.All(value => value.Type == JTokenType.String) &&
            array.Values<string>().Contains("damage") && array.Values<string>().Contains("burn"))
        .ToArray();
    Require(kinds.Length == 1 && catalogKinds.SetEquals(kinds[0].Values<string>()),
        "Effect enum differs from production catalog: " + name);
}
var promptASchema = JObject.Parse(Encoding.UTF8.GetString(Read("contracts/codex/model-a.output-schema.json")));
var aPalette = promptASchema.DescendantsAndSelf().OfType<JArray>()
    .Single(array => array.All(value => value.Type == JTokenType.String) &&
        array.Values<string>().Contains("ember") && array.Values<string>().Contains("lava"));
var bPalettes = plannerSchema.DescendantsAndSelf().OfType<JArray>()
    .Where(array => array.All(value => value.Type == JTokenType.String || value.Type == JTokenType.Null) &&
        array.Values<string>().Contains("ember") && array.Values<string>().Contains("lava")).ToArray();
Require(bPalettes.Length == 6 && bPalettes.All(palette => palette.Any(value => value.Type == JTokenType.Null) &&
    aPalette.Values<string>().ToHashSet().SetEquals(palette.Where(value => value.Type == JTokenType.String)
        .Values<string>())), "A/B palette enums differ or legacy null is missing");
foreach (var appearance in plannerSchema.DescendantsAndSelf().OfType<JObject>()
    .Where(obj => obj["properties"]?["signature_geometry_id"] != null))
    Require(((JArray)appearance["required"]).Values<string>().Contains("palette"),
        "Structured output B could omit its palette");

byte[] JsonBytes(JObject obj) => Encoding.UTF8.GetBytes(obj.ToString(Newtonsoft.Json.Formatting.None));
JObject NewEffectDescription(string kind, string target)
{
    var candidate = JObject.Parse(Encoding.UTF8.GetString(descriptionJson));
    var facts = (JArray)candidate["clauses"][0]["facts"];
    facts.First(f => (string)f["dimension"] == "effect")["value"] = kind;
    facts.First(f => (string)f["dimension"] == "target")["value"] = target;
    return candidate;
}
JObject NewEffectPlan(byte[] descriptionBytes, string kind, string target, int amount, int duration)
{
    var candidate = JObject.Parse(Encoding.UTF8.GetString(planJson));
    candidate["description_sha256"] = SpellCompiler.Sha256(descriptionBytes);
    var effect = candidate["nodes"][0]["effects"][0];
    effect["kind"] = kind; effect["target_filter"] = target;
    effect["amount"] = amount; effect["duration_ticks"] = duration;
    candidate["nodes"][0]["options"]["contact_filter"] = target;
    return candidate;
}
var zeroAmount = new[] { "wet", "root", "stun", "cleanse", "dispel" }.ToHashSet();
var periodic = new[] { "burn", "bleed", "poison", "freeze_damage", "regen" }.ToHashSet();
var instant = new[] { "damage", "heal", "impulse", "cleanse", "dispel", "life_steal", "execute", "shatter" }.ToHashSet();
foreach (var catalogEffect in (JArray)catalog["effects"])
{
    var kind = (string)catalogEffect["id"];
    if (kind == "life_steal") continue; // Requires a same-event damage companion; exercised below.
    var target = kind == "shatter" ? "environment" :
        new[] { "regen", "barrier_health", "haste", "damage_reduction", "cleanse" }.Contains(kind) ? "ally" : "hostile";
    var amount = zeroAmount.Contains(kind) ? 0 : Math.Min(1000, (int)catalogEffect["max_amount"]);
    var duration = instant.Contains(kind) ? 0 : periodic.Contains(kind) ? 100 : 100;
    var candidateDescription = NewEffectDescription(kind, target);
    var candidateDescriptionBytes = JsonBytes(candidateDescription);
    var candidatePlan = NewEffectPlan(candidateDescriptionBytes, kind, target, amount, duration);
    if (kind == "impulse") candidatePlan["nodes"][0]["effects"][0]["direction"] = "forward";
    var candidateIssues = SpellCompiler.ValidatePlanJson(candidateDescriptionBytes, JsonBytes(candidatePlan),
        resolved.GeometryJson, resolved.MaskPng);
    Require(candidateIssues.Count == 0,
        "Catalog effect could not compile: " + kind + ": " + string.Join("; ", candidateIssues));
}
var multiDescription = JObject.Parse(Encoding.UTF8.GetString(descriptionJson));
var firstFacts = (JArray)multiDescription["clauses"][0]["facts"];
foreach (var kind in new[] { "bleed", "life_steal", "execute" })
    firstFacts.Add(new JObject { ["dimension"] = "effect", ["value"] = kind });
multiDescription["clauses"][1]["facts"].First(f => (string)f["dimension"] == "effect")["value"] = "shatter";
multiDescription["clauses"][1]["facts"].First(f => (string)f["dimension"] == "target")["value"] = "environment";
var multiDescriptionBytes = JsonBytes(multiDescription);
var multiPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
multiPlan["description_sha256"] = SpellCompiler.Sha256(multiDescriptionBytes);
var firstEffects = (JArray)multiPlan["nodes"][0]["effects"];
foreach (var (kind, amount, duration) in new[] { ("bleed", 1500, 100), ("life_steal", 300, 0), ("execute", 5000, 0) })
{
    var effect = (JObject)firstEffects[0].DeepClone();
    effect["id"] = "e_" + kind; effect["kind"] = kind;
    effect["amount"] = amount; effect["duration_ticks"] = duration;
    firstEffects.Add(effect);
}
var shatter = multiPlan["nodes"][1]["effects"][0];
shatter["kind"] = "shatter"; shatter["target_filter"] = "environment";
shatter["amount"] = 5000; shatter["duration_ticks"] = 0;
var multiPlanBytes = JsonBytes(multiPlan);
Require(SpellCompiler.ValidatePlanJson(multiDescriptionBytes, multiPlanBytes,
    resolved.GeometryJson, resolved.MaskPng).Count == 0,
    "Damage, bleed, life steal, execute and hit-linked shatter failed validation");
var multiInput = new CompilationInput { DescriptionJson = multiDescriptionBytes, PlanJson = multiPlanBytes,
    GeometryJson = resolved.GeometryJson, MaskPng = resolved.MaskPng,
    GeometryArtifactIds = artifactIds, MaskArtifactIds = maskIds,
    SpellId = "multi-effect-smoke", ParchmentId = "multi-effect-support", SignatureSeedHex = "e10a330a765bc981",
    Provenance = fixture["provenance"].ToObject<SpellProvenance>() };
Require(SpellCompiler.Compile(multiInput).Success, "Multi-effect spell did not compile");
var unpairedLifeSteal = (JObject)multiPlan.DeepClone();
((JArray)unpairedLifeSteal["nodes"][0]["effects"])[0].Remove();
Require(SpellCompiler.ValidatePlanJson(multiDescriptionBytes, JsonBytes(unpairedLifeSteal),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "life_steal_source"),
    "Life steal without actual same-event hostile damage was accepted");
var excessiveBleed = (JObject)multiPlan.DeepClone();
excessiveBleed["nodes"][0]["effects"][1]["amount"] = 10001;
Require(SpellCompiler.ValidatePlanJson(multiDescriptionBytes, JsonBytes(excessiveBleed),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "effect_amount"),
    "Periodic damage above its catalog bound was accepted");
var endlessStunDescription = NewEffectDescription("stun", "hostile");
var endlessStunDescriptionBytes = JsonBytes(endlessStunDescription);
var endlessStunPlan = NewEffectPlan(endlessStunDescriptionBytes, "stun", "hostile", 0, 101);
Require(SpellCompiler.ValidatePlanJson(endlessStunDescriptionBytes, JsonBytes(endlessStunPlan),
    resolved.GeometryJson, resolved.MaskPng).Any(i => i.Code == "control_duration"),
    "Hard control beyond two seconds was accepted");

var recipeCatalog = JObject.Parse(Encoding.UTF8.GetString(Read("contracts/effect-recipes.json")));
var recipes = (JArray)recipeCatalog["recipes"];
Require((string)recipeCatalog["schema_version"] == "sp.effect-recipes/1.0" && recipes.Count >= 100,
    "The named recipe catalog is missing or too small");
var recipeIds = recipes.Select(recipe => (string)recipe["id"]).ToHashSet(StringComparer.Ordinal);
Require(recipeIds.Count == recipes.Count, "Duplicate named recipe IDs");
var recipePromptBytes = Read("contracts/effect-recipes-prompt.json");
var recipePrompt = JObject.Parse(Encoding.UTF8.GetString(recipePromptBytes));
var promptRecipes = ((JArray)recipePrompt["recipes"]).ToDictionary(recipe => (string)recipe["id"],
    recipe => recipe, StringComparer.Ordinal);
Require(recipePromptBytes.Length <= 30000 && promptRecipes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(recipeIds),
    "Compact interpreter recipe list is missing IDs or too large");
foreach (var recipe in recipes)
{
    var summary = promptRecipes[(string)recipe["id"]];
    Require((string)summary["target_filter"] == (string)recipe["target_filter"] &&
        ((JArray)summary["kinds"]).Values<string>().SequenceEqual(
            ((JArray)recipe["components"]).Select(component => (string)component["kind"])),
        "Compact recipe projection changes an effect or target: " + recipe["id"]);
    var hint = (string)summary["hint_fr"];
    Require(!string.IsNullOrWhiteSpace(hint) && hint.Length <= 70 &&
        ((string)recipe["description_fr"]).StartsWith(hint.TrimEnd('…'), StringComparison.Ordinal),
        "Compact recipe hint invents a mechanic: " + recipe["id"]);
}
var aRecipeRule = promptASchema.DescendantsAndSelf().OfType<JObject>().Single(obj =>
    (string)obj["properties"]?["dimension"]?["const"] == "recipe");
Require(recipeIds.SetEquals(((JArray)aRecipeRule["properties"]["value"]["enum"]).Values<string>()),
    "A structured output cannot select exactly the published recipes");
foreach (var schemaObject in promptASchema.DescendantsAndSelf().OfType<JObject>()
    .Where(obj => obj["type"]?.Type == JTokenType.String && (string)obj["type"] == "object" && obj["properties"] is JObject))
    Require(((JObject)schemaObject["properties"]).Properties().Select(property => property.Name).ToHashSet()
        .SetEquals(((JArray)schemaObject["required"]).Values<string>()),
        "A structured output has an optional object property");

JObject Fact(string dimension, string value) => new JObject { ["dimension"] = dimension, ["value"] = value };
string RecipeEvent(string carrier) => carrier == "field" ? "enter" : carrier == "trap" ? "trigger" : "hit";
JObject RecipeDescription(JObject recipe, string carrier)
{
    var facts = new JArray(Fact("carrier", carrier), Fact("recipe", (string)recipe["id"]));
    foreach (var component in (JArray)recipe["components"])
        facts.Add(Fact("effect", (string)component["kind"]));
    facts.Add(Fact("target", (string)recipe["target_filter"]));
    facts.Add(Fact("event", RecipeEvent(carrier)));
    return new JObject {
        ["schema_version"] = "sp.description/1.0", ["title"] = (string)recipe["label_fr"],
        ["summary"] = (string)recipe["description_fr"],
        ["observations"] = new JArray(new JObject { ["id"] = "o1", ["region"] = "full",
            ["visible_feature"] = "Trace illustrée.", ["interpretation"] = "Lecture de recette." }),
        ["clauses"] = new JArray(new JObject { ["id"] = "c1", ["subject_id"] = "s0",
            ["kind"] = "mechanical", ["text"] = (string)recipe["description_fr"],
            ["observation_ids"] = new JArray("o1"), ["facts"] = facts }),
        ["relations"] = new JArray(), ["shape_requests"] = new JArray()
    };
}
JObject RecipePlan(JObject recipe, string carrier, byte[] descriptionBytes)
{
    var template = JObject.Parse(Encoding.UTF8.GetString(Read("examples/fixture_" + carrier + ".json")));
    var node = (JObject)template["nodes"][0];
    template["description_sha256"] = SpellCompiler.Sha256(descriptionBytes);
    node["node_id"] = "n0"; node["subject_id"] = "s0";
    node["clause_ids"] = new JArray("c1");
    node["geometry_id"] = carrier == "projectile" || carrier == "beam" ? "full.path.0" : "full.footprint.0";
    node["appearance"]["signature_geometry_id"] = "full.silhouette.0";
    var target = (string)recipe["target_filter"];
    if (carrier == "projectile") node["options"]["contact_filter"] = target;
    if (carrier == "beam") node["options"]["chain_filter"] = target;
    if (carrier == "trap") node["options"]["trigger_filter"] = target;
    var effects = new JArray();
    var index = 0;
    foreach (var component in (JArray)recipe["components"])
        effects.Add(new JObject { ["id"] = "e" + index++, ["clause_ids"] = new JArray("c1"),
            ["event"] = RecipeEvent(carrier), ["kind"] = (string)component["kind"],
            ["target_filter"] = target, ["amount"] = (int)component["amount"],
            ["duration_ticks"] = (int)component["duration_ticks"],
            ["direction"] = (string)component["direction"] });
    node["effects"] = effects;
    return template;
}
var validatedRecipeCarriers = 0;
foreach (var recipeToken in recipes)
{
    var recipe = (JObject)recipeToken;
    var firstCarrier = true;
    foreach (var carrierToken in (JArray)recipe["allowed_carriers"])
    {
        var carrier = (string)carrierToken;
        var recipeDescription = RecipeDescription(recipe, carrier);
        var recipeDescriptionBytes = JsonBytes(recipeDescription);
        var recipePlan = RecipePlan(recipe, carrier, recipeDescriptionBytes);
        var recipePlanBytes = JsonBytes(recipePlan);
        var descriptionIssues = SpellCompiler.ValidateDescriptionJson(recipeDescriptionBytes);
        Require(descriptionIssues.Count == 0,
            "Recipe description rejected: " + recipe["id"] + "/" + carrier + ": " + string.Join("; ", descriptionIssues));
        var planIssues = SpellCompiler.ValidatePlanJson(recipeDescriptionBytes, recipePlanBytes,
            wholeGeometry.GeometryJson, wholeGeometry.MaskPng);
        Require(planIssues.Count == 0,
            "Recipe plan rejected: " + recipe["id"] + "/" + carrier + ": " + string.Join("; ", planIssues));
        validatedRecipeCarriers++;
        if (!firstCarrier) continue;
        var recipeInput = new CompilationInput { DescriptionJson = recipeDescriptionBytes,
            PlanJson = recipePlanBytes, GeometryJson = wholeGeometry.GeometryJson,
            MaskPng = wholeGeometry.MaskPng, GeometryArtifactIds = wholeInput.GeometryArtifactIds,
            MaskArtifactIds = wholeInput.MaskArtifactIds, SpellId = "recipe-smoke",
            ParchmentId = "recipe-support", SignatureSeedHex = "e10a330a765bc981",
            Provenance = fixture["provenance"].ToObject<SpellProvenance>() };
        Require(SpellCompiler.Compile(recipeInput).Success, "Named recipe failed to compile: " + recipe["id"]);
        firstCarrier = false;
    }
}
Require(validatedRecipeCarriers >= recipes.Count, "Not every recipe has a validated carrier");
var firstRecipe = (JObject)recipes[0];
var firstRecipeCarrier = (string)firstRecipe["allowed_carriers"][0];
var firstRecipeDescription = RecipeDescription(firstRecipe, firstRecipeCarrier);
var unknownRecipeDescription = (JObject)firstRecipeDescription.DeepClone();
((JArray)unknownRecipeDescription["clauses"][0]["facts"]).First(f => (string)f["dimension"] == "recipe")
    ["value"] = "r_unlisted";
Require(SpellCompiler.ValidateDescriptionJson(JsonBytes(unknownRecipeDescription)).Count > 0,
    "Unknown recipe ID was accepted");
var incompatibleRecipeDescription = RecipeDescription(firstRecipe, "barrier");
((JArray)incompatibleRecipeDescription["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "event")["value"] = "block";
Require(SpellCompiler.ValidateDescriptionJson(JsonBytes(incompatibleRecipeDescription))
    .Any(i => i.Code == "recipe_carrier"), "Recipe accepted a disallowed carrier");
var missingComponentDescription = (JObject)firstRecipeDescription.DeepClone();
((JArray)missingComponentDescription["clauses"][0]["facts"])
    .First(f => (string)f["dimension"] == "effect").Remove();
Require(SpellCompiler.ValidateDescriptionJson(JsonBytes(missingComponentDescription))
    .Any(i => i.Code == "recipe_components"), "Recipe accepted a missing primitive effect fact");
var firstRecipeDescriptionBytes = JsonBytes(firstRecipeDescription);
var changedRecipePlan = RecipePlan(firstRecipe, firstRecipeCarrier, firstRecipeDescriptionBytes);
changedRecipePlan["nodes"][0]["effects"][0]["amount"] =
    (int)changedRecipePlan["nodes"][0]["effects"][0]["amount"] + 1;
Require(SpellCompiler.ValidatePlanJson(firstRecipeDescriptionBytes, JsonBytes(changedRecipePlan),
    wholeGeometry.GeometryJson, wholeGeometry.MaskPng).Any(i => i.Code == "recipe_plan"),
    "Recipe accepted a changed fixed amount");
var wrongRecipeTarget = (JObject)firstRecipeDescription.DeepClone();
((JArray)wrongRecipeTarget["clauses"][0]["facts"]).First(f => (string)f["dimension"] == "target")
    ["value"] = (string)firstRecipe["target_filter"] == "hostile" ? "ally" : "hostile";
Require(SpellCompiler.ValidateDescriptionJson(JsonBytes(wrongRecipeTarget)).Any(i => i.Code == "recipe_target"),
    "Recipe accepted an invented receiver filter");
var secondRecipe = recipes.OfType<JObject>().First(recipe => recipe != firstRecipe &&
    (string)recipe["target_filter"] == (string)firstRecipe["target_filter"] &&
    ((JArray)recipe["components"]).Count + ((JArray)firstRecipe["components"]).Count <= 8);
var combinedRecipeDescription = RecipeDescription(firstRecipe, firstRecipeCarrier);
var combinedFacts = (JArray)combinedRecipeDescription["clauses"][0]["facts"];
combinedFacts.Add(Fact("recipe", (string)secondRecipe["id"]));
foreach (var component in (JArray)secondRecipe["components"])
    combinedFacts.Add(Fact("effect", (string)component["kind"]));
var combinedRecipeDescriptionBytes = JsonBytes(combinedRecipeDescription);
var combinedRecipePlan = RecipePlan(firstRecipe, firstRecipeCarrier, combinedRecipeDescriptionBytes);
var combinedEffects = (JArray)combinedRecipePlan["nodes"][0]["effects"];
foreach (var component in (JArray)secondRecipe["components"])
    combinedEffects.Add(new JObject { ["id"] = "e" + combinedEffects.Count,
        ["clause_ids"] = new JArray("c1"), ["event"] = RecipeEvent(firstRecipeCarrier),
        ["kind"] = (string)component["kind"], ["target_filter"] = (string)firstRecipe["target_filter"],
        ["amount"] = (int)component["amount"], ["duration_ticks"] = (int)component["duration_ticks"],
        ["direction"] = (string)component["direction"] });
Require(SpellCompiler.ValidatePlanJson(combinedRecipeDescriptionBytes, JsonBytes(combinedRecipePlan),
    wholeGeometry.GeometryJson, wholeGeometry.MaskPng).Count == 0,
    "Two compatible recipes on one subject failed composition");
var duplicateRecipeEffectIds = (JObject)combinedRecipePlan.DeepClone();
var duplicateEffects = (JArray)duplicateRecipeEffectIds["nodes"][0]["effects"];
duplicateEffects.Last()["id"] = (string)duplicateEffects.First()["id"];
Require(SpellCompiler.ValidatePlanJson(combinedRecipeDescriptionBytes, JsonBytes(duplicateRecipeEffectIds),
    wholeGeometry.GeometryJson, wholeGeometry.MaskPng).Any(i => i.Code == "duplicate_effect"),
    "Recipe composition accepted duplicate effect IDs");
Console.WriteLine("Recipe compiler smoke: " + recipes.Count + " named recipes compiled; " +
    validatedRecipeCarriers + " declared recipe/carrier pairs validated.");
var duplicateJson = Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}");
bool rejectedDuplicate = false;
try { ContractJson.ParseStrict(duplicateJson); } catch { rejectedDuplicate = true; }
Require(rejectedDuplicate, "Duplicate JSON key was accepted");
bool rejectedComment = false;
try { ContractJson.ParseStrict(Encoding.UTF8.GetBytes("{\"a\":1/* comment */}")); }
catch { rejectedComment = true; }
Require(rejectedComment, "JSON comment was accepted");

// A footprint with two nested closed contours keeps its center hole.
var donut = new byte[1024 * 1024 * 4];
void Square(int lo, int hi)
{
    for (var i = lo; i <= hi; i++)
    {
        donut[(lo * 1024 + i) * 4 + 3] = 255;
        donut[(hi * 1024 + i) * 4 + 3] = 255;
        donut[(i * 1024 + lo) * 4 + 3] = 255;
        donut[(i * 1024 + hi) * 4 + 3] = 255;
    }
}
Square(350, 674); Square(450, 574);
var donutDescription = new SpellDescription {
    shape_requests = new List<ShapeRequest> { new ShapeRequest { subject_id = "s0", region = "full", role = "footprint" } }
};
var donutGeometry = GeometryResolver.ResolveRgba(donut, 1024, 1024, donutDescription);
Require(donutGeometry.Success, "Nested footprint extraction failed");
var footprint = donutGeometry.Assets["full.footprint.0"];
var footprintImage = PngCodec.DecodeRgba(donutGeometry.MaskPng[footprint.mask_file], footprint.width_px, footprint.height_px);
byte At(int x, int y) => footprintImage.Rgba[(y * footprintImage.Width + x) * 4 + 3];
Require(At(footprintImage.Width / 2, footprintImage.Height / 2) == 0, "Nested hole filled incorrectly");
Require(At(50, footprintImage.Height / 2) == 255, "Enclosed annulus was not filled");
// Marks near the rectangular canvas corners are included in the whole-image
// profile, even though they lie outside the old circular reference guide.
var cornerInk = new byte[1024 * 1024 * 4];
cornerInk[(12 * 1024 + 12) * 4 + 3] = 255;
cornerInk[(1011 * 1024 + 1011) * 4 + 3] = 255;
var cornerGeometry = GeometryResolver.ResolveRgba(cornerInk, 1024, 1024,
    new SpellDescription { shape_requests = new List<ShapeRequest>() });
Require(cornerGeometry.Success && !cornerGeometry.Assets.Keys.Any(id => id.Contains(".path.")),
    "Isolated marks should remain valid without a fabricated path");
Require(cornerGeometry.Assets["full.silhouette.0"].width_px == 1000 &&
    cornerGeometry.Assets["full.silhouette.0"].height_px == 1000,
    "Whole-image extraction lost marks outside the old circular guide");
var legacyCornerGeometry = GeometryResolver.ResolveRgba(cornerInk, 1024, 1024,
    new SpellDescription { shape_requests = new List<ShapeRequest> {
        new ShapeRequest { subject_id = "s0", region = "full", role = "silhouette" } } });
Require(legacyCornerGeometry.Issues.Any(i => i.Code == "empty_region"),
    "Old explicit full-region extraction unexpectedly changed to rectangular canvas mode");
Console.WriteLine("Core smoke passed: PNG and whole-image pixel geometry, controlled compilation, visual clause and lifecycle event checks, semantic and JSON rejection.");

// New interpretations describe a controlled 3D object. Traced strokes must not
// leak into its path, area, or visual signature; archived descriptions keep the
// original pixel profile exercised above.
var semanticDescription = (JObject)wholeDescription.DeepClone();
foreach (var firstClause in ((JArray)semanticDescription["clauses"])
    .GroupBy(clause => (string)clause["subject_id"]).Select(group => group.First()))
    ((JArray)firstClause["facts"]).Add(new JObject { ["dimension"] = "visual_form", ["value"] = "boulder" });
var semanticDescriptionBytes = JsonBytes(semanticDescription);
var semanticTyped = ContractJson.DeserializeStrict<SpellDescription>(semanticDescriptionBytes, "spell-description");
Require(SpellCompiler.ValidateWholeImageDescriptionJson(semanticDescriptionBytes, false, true).Count == 0,
    "Semantic description rejected by new interpreter gate");
Require(SpellCompiler.ValidateWholeImageDescriptionJson(wholeDescriptionBytes, false, true)
    .Any(issue => issue.Code == "visual_form_required"), "New interpreter gate accepted missing visual forms");
var semanticGeometry = GeometryResolver.Resolve(inkPng, semanticTyped);
var otherInkSemantic = GeometryResolver.ResolveRgba(cornerInk, 1024, 1024, semanticTyped);
Require(semanticGeometry.Success && otherInkSemantic.Success &&
    semanticGeometry.Assets.Count == semanticTyped.clauses.Select(clause => clause.subject_id).Distinct().Count() &&
    semanticGeometry.Assets.Values.All(asset => asset.algorithm == GeometryResolver.SemanticVersion &&
        asset.kind != "silhouette" && asset.source_description_sha256 == GeometryResolver.SemanticDescriptionSha256(semanticTyped)),
    "Semantic resolver emitted traced geometry or lost description provenance");
foreach (var pair in semanticGeometry.Assets)
{
    var changed = otherInkSemantic.Assets[pair.Key];
    Require(pair.Value.source_pixel_sha256 != changed.source_pixel_sha256 &&
        JToken.DeepEquals(JToken.FromObject(pair.Value.points), JToken.FromObject(changed.points)),
        "Semantic path copied ink contours or lost capture provenance");
}
foreach (var pair in semanticGeometry.MaskPng)
    Require(pair.Value.SequenceEqual(otherInkSemantic.MaskPng[pair.Key]), "Semantic footprint changed with scribble contours");
var semanticPlan = (JObject)wholePlan.DeepClone();
semanticPlan["description_sha256"] = SpellCompiler.Sha256(semanticDescriptionBytes);
foreach (var node in (JArray)semanticPlan["nodes"])
{
    node["appearance"]["form"] = "boulder";
    node["appearance"]["signature_geometry_id"] = JValue.CreateNull();
    if ((string)node["carrier"] == "projectile") node["options"]["radius_cm"] = 20;
    node["geometry_id"] = semanticGeometry.Assets.Values.Single(asset => asset.source_subject_id == (string)node["subject_id"]).geometry_id;
}
var semanticPlanBytes = JsonBytes(semanticPlan);
var semanticInput = new CompilationInput {
    DescriptionJson = semanticDescriptionBytes, PlanJson = semanticPlanBytes,
    GeometryJson = semanticGeometry.GeometryJson, MaskPng = semanticGeometry.MaskPng,
    GeometryArtifactIds = semanticGeometry.GeometryJson.Keys.ToDictionary(key => key, key => "a" + Guid.NewGuid().ToString("N")),
    MaskArtifactIds = semanticGeometry.MaskPng.Keys.ToDictionary(key => key, key => "a" + Guid.NewGuid().ToString("N")),
    SpellId = "semantic-smoke", ParchmentId = "semantic-support", SignatureSeedHex = "e10a330a765bc981",
    Provenance = input.Provenance
};
var semanticCompiled = SpellCompiler.Compile(semanticInput);
Require(semanticCompiled.Success, "Semantic spell did not compile: " + string.Join("; ", semanticCompiled.Issues));
var semanticPublished = ContractJson.ParseStrict(semanticCompiled.PayloadUtf8);
Require(semanticCompiled.Spell.versions.min_client == "1.1.0" &&
    ((JObject)semanticPublished["plan"]["nodes"][0]["appearance"]).Property("signature_geometry_id") == null &&
    (string)semanticPublished["plan"]["nodes"][0]["appearance"]["form"] == "boulder",
    "Published semantic packet leaked an ink signature or supports an incompatible old player");
var mismatchedForm = (JObject)semanticPlan.DeepClone();
mismatchedForm["nodes"][0]["appearance"]["form"] = "wolf";
Require(SpellCompiler.ValidatePlanJson(semanticDescriptionBytes, JsonBytes(mismatchedForm),
    semanticGeometry.GeometryJson, semanticGeometry.MaskPng).Any(issue => issue.Code == "visual_form_trace"),
    "Luna changed Astra's interpreted visual form");
var unknownForm = (JObject)semanticPlan.DeepClone();
unknownForm["nodes"][0]["appearance"]["form"] = "load_custom_mesh";
Require(SpellCompiler.ValidatePlanJson(semanticDescriptionBytes, JsonBytes(unknownForm),
    semanticGeometry.GeometryJson, semanticGeometry.MaskPng).Any(issue => issue.Code == "plan_json"),
    "Uncontrolled visual form escaped schema validation");
var tracedSignature = (JObject)semanticPlan.DeepClone();
tracedSignature["nodes"][0]["appearance"]["signature_geometry_id"] = "full.silhouette.0";
Require(SpellCompiler.ValidatePlanJson(semanticDescriptionBytes, JsonBytes(tracedSignature),
    semanticGeometry.GeometryJson, semanticGeometry.MaskPng).Any(issue => issue.Code == "semantic_signature"),
    "Semantic form accepted a scribble signature");
var pathKey = semanticGeometry.Assets.Single(pair => pair.Value.kind == "path").Key;
foreach (var field in new[] { "source_description_sha256", "source_subject_id", "algorithm", "points" })
{
    var altered = ContractJson.ParseStrict(semanticGeometry.GeometryJson[pathKey]);
    if (field == "points") altered["points"][0]["z"] = 9876;
    else altered[field] = field == "source_description_sha256" ? new string('a', 64) : field == "algorithm" ? GeometryResolver.WholeCanvasVersion : "unrelated_subject";
    var geometries = semanticGeometry.GeometryJson.ToDictionary(pair => pair.Key, pair => pair.Value);
    geometries[pathKey] = JsonBytes(altered);
    Require(SpellCompiler.ValidatePlanJson(semanticDescriptionBytes, semanticPlanBytes,
        geometries, semanticGeometry.MaskPng).Any(issue => issue.Code == "semantic_geometry_source"),
        "Semantic geometry accepted modified " + field);
}
var alteredMasks = semanticGeometry.MaskPng.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone());
alteredMasks[alteredMasks.Keys.First()][^1] ^= 1;
Require(SpellCompiler.ValidatePlanJson(semanticDescriptionBytes, semanticPlanBytes,
    semanticGeometry.GeometryJson, alteredMasks).Any(issue => issue.Code == "semantic_mask"),
    "Semantic geometry accepted a modified collision mask");
var absentFormPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
foreach (var node in (JArray)absentFormPlan["nodes"]) node["appearance"]["form"] = JValue.CreateNull();
Require(SpellCompiler.ValidatePlanJson(descriptionJson, JsonBytes(absentFormPlan), resolved.GeometryJson, resolved.MaskPng).Count == 0,
    "Explicit null visual form broke a legacy description");
var aForms = promptASchema.DescendantsAndSelf().OfType<JObject>()
    .Single(obj => (string)obj["properties"]?["dimension"]?["const"] == "visual_form")["properties"]["value"]["enum"].Values<string>().ToHashSet();
Require(aForms.SetEquals(SpellVisualForms.All) &&
    ((JArray)catalog["visual_forms"]["forms"]).Select(form => (string)form["id"]).ToHashSet().SetEquals(aForms),
    "A/runtime/capability visual form catalog differs");
foreach (var form in (JArray)catalog["visual_forms"]["forms"])
    Require((int)form["minimum_projectile_radius_cm"] == SpellVisualForms.MinimumProjectileRadiusCm((string)form["id"]) &&
        (int)form["minimum_projectile_radius_cm"] > 0 && (int)form["minimum_projectile_radius_cm"] <= 100,
        "Catalog and compiler projectile size bounds differ for " + (string)form["id"]);
var golemDescription = (JObject)semanticDescription.DeepClone();
foreach (var fact in ((JArray)golemDescription["clauses"]).SelectMany(clause => (JArray)clause["facts"])
    .Where(fact => (string)fact["dimension"] == "visual_form")) fact["value"] = "golem";
var golemDescriptionBytes = JsonBytes(golemDescription);
var golemGeometry = GeometryResolver.Resolve(inkPng,
    ContractJson.DeserializeStrict<SpellDescription>(golemDescriptionBytes, "spell-description"));
var golemPlan = (JObject)semanticPlan.DeepClone();
golemPlan["description_sha256"] = SpellCompiler.Sha256(golemDescriptionBytes);
foreach (var node in (JArray)golemPlan["nodes"])
{
    node["appearance"]["form"] = "golem";
    if ((string)node["carrier"] == "projectile") node["options"]["radius_cm"] = 1;
}
Require(SpellCompiler.ValidatePlanJson(golemDescriptionBytes, JsonBytes(golemPlan),
    golemGeometry.GeometryJson, golemGeometry.MaskPng).Any(issue => issue.Code == "semantic_projectile_size"),
    "A one-centimeter golem projectile was accepted");
foreach (var node in (JArray)golemPlan["nodes"])
    if ((string)node["carrier"] == "projectile") node["options"]["radius_cm"] = 45;
Require(SpellCompiler.ValidatePlanJson(golemDescriptionBytes, JsonBytes(golemPlan),
    golemGeometry.GeometryJson, golemGeometry.MaskPng).Count == 0,
    "A golem projectile at its controlled 45 cm radius was rejected");
foreach (var appearance in plannerSchema.DescendantsAndSelf().OfType<JObject>()
    .Where(obj => obj["properties"]?["signature_geometry_id"] != null))
    Require(((JArray)appearance["required"]).Values<string>().Contains("form") &&
        ((JArray)appearance["properties"]["form"]["enum"]).Where(value => value.Type == JTokenType.String)
            .Values<string>().ToHashSet().SetEquals(aForms), "Codex B visual forms differ from A");
Console.WriteLine("Semantic forms passed: controlled A-to-B mapping, clean geometry independent of ink contours, provenance and mask tamper rejection, client 1.1 requirement, and legacy compatibility.");

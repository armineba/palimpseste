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

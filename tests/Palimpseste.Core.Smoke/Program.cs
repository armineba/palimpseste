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

var badPlan = JObject.Parse(Encoding.UTF8.GetString(planJson));
badPlan["nodes"][0]["effects"][0]["kind"] = "heal";
var badIssues = SpellCompiler.ValidatePlanJson(descriptionJson, Encoding.UTF8.GetBytes(badPlan.ToString()),
    resolved.GeometryJson, resolved.MaskPng);
Require(badIssues.Count > 0, "Changed effect was accepted");
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
Console.WriteLine("Core smoke passed: PNG roundtrip/corruption, pixel geometry and nested hole, compilation/bounds/hash, semantic rejection, duplicate-key/comment rejection.");

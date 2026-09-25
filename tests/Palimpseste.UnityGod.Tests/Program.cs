using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Palimpseste.Provider;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var context = UnityGodMethods.Freeze(root);
var researchJson = new JsonObject { ["unity_god"] = JsonNode.Parse(context.GetRawText()) }.ToJsonString();
var research = new SpellReferenceResearch(researchJson, Hash(Encoding.UTF8.GetBytes(researchJson)));
var plan = File.ReadAllBytes(Path.Combine(root, "examples/v2-validation/plan.json"));
var planNode = JsonNode.Parse(plan)!; planNode["reference_research_sha256"] = research.Sha256;
plan = Encoding.UTF8.GetBytes(planNode.ToJsonString());
var method = context.GetProperty("methods").EnumerateArray().Single(m => m.GetProperty("id").GetString() == "tinyplay.plasma_opposed_layers");
var design = new JsonObject {
    ["skill_version"] = context.GetProperty("skill_version").GetString(),
    ["skill_sha256"] = context.GetProperty("skill_sha256").GetString(),
    ["method_catalog_sha256"] = context.GetProperty("method_catalog_sha256").GetString(),
    ["source_review"] = new JsonArray(UnityGodMethods.SourceIds.Select(s => (JsonNode)new JsonObject {
        ["source_id"] = s, ["disposition"] = s == "tinyplay_urp_shaders_collection" ? "used" : "not_applicable",
        ["reason"] = "Source examined; choose only techniques that fit this controlled fixture."
    }).ToArray()),
    ["nodes"] = new JsonArray(new JsonObject {
        ["node_id"] = "p0", ["innovation"] = "Adapt two opposing flows to a compact incandescent continuous meteor core.",
        ["expected_visual_result"] = "A directional continuous core with animated plasma surface; no refraction claimed.",
        ["choices"] = new JsonArray(new JsonObject {
            ["method_id"] = method.GetProperty("id").GetString(), ["application"] = "adapt",
            ["adaptation"] = "Use existing V2 plasma texture layers with the frozen meteor palette and lifecycle.",
            ["bindings"] = new JsonArray(new JsonObject { ["path"] = "/blueprint_v2/rendering_layers/core_surface", ["value"] = "plasma" })
        })
    })
};
var results = new List<string>();
void Require(bool pass, string name) { if (!pass) throw new Exception(name); results.Add(name); }
byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());
JsonNode Choice(JsonNode node) => node["nodes"]![0]!["choices"]![0]!;
Require(UnityGodMethods.ValidateDesign(plan, Bytes(design), research).Count == 0, "valid sourced adaptation binds actual plan");
var receipt = UnityGodMethods.CreateReceipt(plan, Bytes(design), research);
Require(UnityGodMethods.ValidateReceipt(plan, receipt, research).Count == 0, "server receipt roundtrip binds plan and frozen methods");
var tampered = design.DeepClone(); tampered["skill_sha256"] = new string('0', 64);
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count > 0, "reject different skill version bytes");
tampered = design.DeepClone(); Choice(tampered)["bindings"]![0]!["value"] = "forcefield";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Any(e => e.StartsWith("unity_god_false_plan_binding")), "reject claimed technique absent from actual field");
tampered = design.DeepClone(); Choice(tampered)["method_id"] = "tinyplay.depth_fade";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Any(e => e.StartsWith("unity_god_method_requires_development")), "reject unimplemented upstream depth method");
tampered = design.DeepClone(); Choice(tampered)["method_id"] = "invented.shader";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count > 0, "reject invented method");
tampered = design.DeepClone(); ((JsonArray)tampered["source_review"]!).RemoveAt(4);
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count > 0, "require examination of all five sources");
tampered = design.DeepClone(); Choice(tampered)["bindings"]![0]!["path"] = "/server/secrets";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count > 0, "reject paths outside blueprint data");
tampered = design.DeepClone(); ((JsonArray)Choice(tampered)["bindings"]!).Add(new JsonObject { ["path"] = "/blueprint_v2/structural_core/size_cm/0", ["value"] = "100" });
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count == 0, "scalar array coordinates can prove an adaptation");
tampered = design.DeepClone(); Choice(tampered)["method_id"] = "xtaja_lifetime_dissolve";
Choice(tampered)["application"] = "reuse";
Choice(tampered)["bindings"] = new JsonArray(new JsonObject { ["path"] = "/blueprint_v2/disappearance/mode", ["value"] = "dissolve" });
foreach (var source in tampered["source_review"]!.AsArray()) source!["disposition"] = source["source_id"]!.GetValue<string>() == "xtaja_vfx_shader" ? "used" : "not_applicable";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Any(e => e.StartsWith("unity_god_unavailable_reuse")), "unlicensed source cannot be claimed as reused implementation");
Choice(tampered)["application"] = "adapt";
Require(UnityGodMethods.ValidateDesign(plan, Bytes(tampered), research).Count == 0, "generic concept may use independently implemented dissolution");
var changedPlan = JsonNode.Parse(plan)!; changedPlan["nodes"]![0]!["blueprint_v2"]!["rendering_layers"]!["core_surface"] = "forcefield";
Require(UnityGodMethods.ValidateReceipt(Bytes(changedPlan), receipt, research).Count > 0, "receipt cannot authorize another plan");
var changedReceipt = JsonNode.Parse(receipt)!; changedReceipt["selected_methods"]![0]!["runtime_scope"] = "invented depth contact";
Require(UnityGodMethods.ValidateReceipt(plan, Bytes(changedReceipt), research).Count > 0, "reject forged source-method snapshot");
var oldResearch = new SpellReferenceResearch("{}", new string('1', 64));
Require(!UnityGodMethods.IsEnabled(oldResearch), "historical research does not silently acquire methods");
var destination = Path.Combine(root, "evidence/public/unity-god"); Directory.CreateDirectory(destination);
File.WriteAllBytes(Path.Combine(destination, "contract-tests.json"), JsonSerializer.SerializeToUtf8Bytes(new {
    observed_utc = DateTimeOffset.UtcNow, passed = results.Count, failed = 0, checks = results,
    provider_calls = 0, unity_tests = 0, database_tests = 0, scope = "deterministic method design and receipt validation; not a model output or visual acceptance"
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS {results.Count} UNITY GOD method/receipt checks");
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

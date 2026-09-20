using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.Library;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Palimpseste.Game.PlayModeTests
{
    // Graphical fixtures only: these authored nodes are neither provider output
    // nor gameplay acceptance. They isolate the shipping renderer for review.
    public sealed class StylizedCompositionPresentationTests
    {
        [UnityTest]
        public IEnumerator CaptureLayeredHealingPulseAndFire()
        {
            var output = Environment.GetEnvironmentVariable("PALIMPSESTE_STYLIZED_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) Assert.Ignore("Graphical capture was not requested");
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            Assert.IsTrue(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf));
            Directory.CreateDirectory(output);
            var previousCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.gameObject.activeSelf).ToArray();
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousFog = RenderSettings.fog;
            var previousFogColor = RenderSettings.fogColor;
            var previousFogStart = RenderSettings.fogStartDistance;
            var previousFogEnd = RenderSettings.fogEndDistance;
            var previousCaptureDelta = Time.captureDeltaTime;
            // File encoding must not consume the effect's short visual life.
            // Evidence timestamps below are simulated Unity time, not latency.
            Time.captureDeltaTime = 1f / 60f;
            var host = new GameObject("Stylized renderer review in shipping lab");
            var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var linearReadback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            var srgbOutput = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
            var frames = new List<object>();
            var fixtureDefinitions = new List<object>();
            var maximumRenderers = 0;
            var maximumParticles = 0;
            var maximumParticleCapacity = 0;
            var maximumLights = 0;
            try
            {
                // Empty, explicitly authored stage packet: no model, player
                // cache, cast mechanics or unvalidated executable content.
                var lab = host.AddComponent<SpellLab>();
                lab.InputSuppressed = true;
                lab.Initialize(JsonConvert.SerializeObject(new CompiledSpell {
                    schema_version = "sp.compiled/1.0",
                    plan = new SpellPlan { nodes = new List<SpellNode>() },
                    geometry_manifest = new List<GeometryManifestEntry>(),
                    binary_assets = new List<BinaryAssetEntry>(),
                    resource_bounds = new ResourceBounds { max_instances = 1 }
                }), output);
                Assert.IsTrue(lab.Ready, lab.Metrics);
                var camera = host.GetComponentInChildren<Camera>();
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.IsTrue(camera.allowHDR);
                Assert.IsTrue(cameraData.renderPostProcessing);
                var profile = Resources.Load<VolumeProfile>("LabVfxVolume");
                Assert.NotNull(profile);
                Assert.IsTrue(profile.TryGet<Bloom>(out var bloom) && bloom.IsActive());
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                // Close review framing; arena, lighting, shaders and profile
                // are the shipping lab's. This pose is stated in the evidence.
                camera.transform.position = new Vector3(6.5f, 5.7f, -9.8f);
                camera.transform.LookAt(new Vector3(0, 1.3f, .7f));
                camera.fieldOfView = 40;
                yield return new WaitForSeconds(.15f);
                var baselineColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
                var specs = new[] {
                    Node("healing-field", "vortex", "field", "nature", "petal", "ripple", "moss"),
                    Node("arcane-pulse", "vortex", "pulse", "arcane", "runic", "pillar", "arcane"),
                    Node("fire-projectile", "fireball", "projectile", "fire", "vortex", "nova", "ember")
                };
                foreach (var node in specs)
                {
                    fixtureDefinitions.Add(node);
                    var visualHost = new GameObject("Authored fixture " + node.node_id);
                    visualHost.transform.SetParent(host.transform, false);
                    var effectHost = new GameObject("Owned review impacts");
                    effectHost.transform.SetParent(host.transform, false);
                    var color = node.carrier == "field" ? new Color(.13f, 1f, .3f) :
                        node.carrier == "pulse" ? new Color(.63f, .12f, 1f) : new Color(1f, .2f, .02f);
                    var projectile = node.carrier == "projectile";
                    var start = projectile ? new Vector3(-2.2f, 1.7f, -1.7f) : new Vector3(0, .08f, .7f);
                    var end = new Vector3(1.5f, 1.7f, 1.6f);
                    visualHost.transform.position = start;
                    visualHost.transform.rotation = Quaternion.LookRotation(projectile ? end - start : Vector3.forward);
                    var visual = visualHost.AddComponent<SemanticSpellVisual>();
                    visual.Initialize(node, color);
                    var composition = visualHost.GetComponent<SpellVfxComposition>();
                    var times = projectile ? new[] { .08f, .36f, .9f } :
                        node.carrier == "pulse" ? new[] { .06f, .22f, .62f } : new[] { .12f, .55f, 1.2f };
                    var born = Time.time;
                    var impacted = false;
                    for (var frame = 0; frame < times.Length; frame++)
                    {
                        do
                        {
                            var age = Time.time - born;
                            if (projectile && !impacted)
                                visualHost.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(age / .72f));
                            if (node.carrier == "pulse" && !impacted) composition.SetPulseRadius(.45f + age * 4f);
                            if (!impacted && (projectile && age >= .72f || node.carrier == "pulse" && age >= .45f))
                            {
                                impacted = true;
                                var point = projectile ? end : start;
                                SpellVfxComposition.SpawnImpact(node.appearance, point, Vector3.forward, color, 1.15f);
                                var impact = GameObject.Find("Semantic impact " + node.appearance.form);
                                Assert.NotNull(impact);
                                impact.transform.SetParent(effectHost.transform, true);
                                Object.Destroy(visualHost);
                            }
                            yield return null;
                        } while (Time.time - born < times[frame]);
                        // Read the last completed GPU frame. EndOfFrame may
                        // never resume under the batch editor test runner.
                        yield return null;
                        var liveRoots = new[] { visualHost, effectHost }.Where(item => item != null).ToArray();
                        var renderers = liveRoots.SelectMany(item => item.GetComponentsInChildren<Renderer>()).ToArray();
                        var particles = liveRoots.SelectMany(item => item.GetComponentsInChildren<ParticleSystem>()).ToArray();
                        var lights = liveRoots.SelectMany(item => item.GetComponentsInChildren<Light>()).Count();
                        Assert.IsTrue(liveRoots.All(item => item.GetComponentsInChildren<Collider>().Length == 0));
                        Assert.AreEqual(baselineColliders, Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length,
                            "Decorative layers must not change the lab's collisions");
                        foreach (var renderer in renderers)
                            Assert.IsFalse(renderer.sharedMaterial.shader.name.Contains("InternalError"));
                        maximumRenderers = Math.Max(maximumRenderers, renderers.Length);
                        maximumParticles = Math.Max(maximumParticles, particles.Sum(item => item.particleCount));
                        maximumParticleCapacity = Math.Max(maximumParticleCapacity, particles.Sum(item => item.main.maxParticles));
                        maximumLights = Math.Max(maximumLights, lights);
                        var bytes = HdrPresentationCapture.Png(target, linearReadback, srgbOutput);
                        var file = node.node_id + "-" + frame.ToString("D2") + ".png";
                        File.WriteAllBytes(Path.Combine(output, file), bytes);
                        Assert.Greater(bytes.Length, 10000);
                        frames.Add(new { fixture = node.node_id, file, sha256 = ParchmentStore.Hash(bytes),
                            elapsed_seconds = Time.time - born, impact = impacted, renderers = renderers.Length,
                            particle_systems = particles.Length, live_particles = particles.Sum(item => item.particleCount), lights });
                    }
                    if (visualHost != null) Object.Destroy(visualHost);
                    Object.Destroy(effectHost);
                    yield return null;
                    yield return null;
                }
                File.WriteAllText(Path.Combine(output, "capture.json"), JsonConvert.SerializeObject(new {
                    evidence_kind = "authored_renderer_fixtures_in_unity_lab",
                    player_output = false, model_calls = 0, art_acceptance = "not_claimed",
                    camera = "shipping lab environment and postprocess; closer review pose, 40 degree field of view, no UI",
                    capture = "ARGBHalf linear target preserves HDR before bloom; postprocessed linear result converted to sRGB PNG",
                    animation_clock = "fixed 60 Hz simulation during capture; not a performance benchmark",
                    graphics_device = SystemInfo.graphicsDeviceName,
                    maximum_renderers_per_fixture = maximumRenderers,
                    maximum_live_particles_per_fixture = maximumParticles,
                    maximum_particle_capacity_per_fixture = maximumParticleCapacity,
                    maximum_lights_per_fixture = maximumLights,
                    additional_colliders = 0, fixtures = fixtureDefinitions, frames
                }, Formatting.Indented));
                Debug.Log("PALIMPSESTE_STYLIZED_CAPTURE " + output);
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(linearReadback);
                Object.Destroy(srgbOutput);
                target.Release(); Object.Destroy(target);
                foreach (var camera in previousCameras) if (camera != null) camera.gameObject.SetActive(true);
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.fog = previousFog;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogStartDistance = previousFogStart;
                RenderSettings.fogEndDistance = previousFogEnd;
                Time.captureDeltaTime = previousCaptureDelta;
            }
            yield return null;
        }

        private static SpellNode Node(string id, string form, string carrier, string style, string motif, string impact, string palette)
            => new SpellNode {
                node_id = id, subject_id = id, carrier = carrier, scale_cm = 320,
                activation = new SpellActivation { @event = "cast", copies = 1, max_activations = 1 },
                appearance = new SpellAppearance { form = form, palette = palette, pattern = "solid",
                    vfx = new SpellVfxProfile { style = style, motif = motif, impact = impact,
                        density = 3, aura_cm = 300, charge_ms = 400 } },
                options = new SpellOptions { radius_cm = 35, width_cm = 15, lifetime_ticks = 150 },
                // Classification input for restorative visual rhythm only:
                // these preview nodes are never submitted to RuntimeEngine.
                effects = carrier == "field" ? new List<SpellEffect> {
                    new SpellEffect { id = "preview-heal", kind = "heal", @event = "tick",
                        target_filter = "ally", amount = 1000, duration_ticks = 0 }
                } : new List<SpellEffect>()
            };
    }

    internal static class HdrPresentationCapture
    {
        public static byte[] Png(RenderTexture source, Texture2D linearReadback, Texture2D srgbOutput)
        {
            // URP adopts an external target's color format for its intermediate
            // buffer, so an ARGB32 target clips emission before bloom. Keep the
            // rendered image linear/HDR until the shipping tone mapping has run.
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                linearReadback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                linearReadback.Apply();
                var pixels = linearReadback.GetPixels();
                for (var i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                srgbOutput.SetPixels(pixels);
                srgbOutput.Apply();
                return srgbOutput.EncodeToPNG();
            }
            finally { RenderTexture.active = previous; }
        }
    }
}

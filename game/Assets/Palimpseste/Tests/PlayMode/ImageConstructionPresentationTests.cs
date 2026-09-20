using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    // This test requires a real cached packet and its exact generated image.
    // It never fabricates a composition, edits the cache or calls a provider.
    public sealed class ImageConstructionPresentationTests
    {
        [UnityTest]
        public IEnumerator CaptureCachedImageConstructionInLab()
        {
            var cache = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_CACHE_DIR");
            var output = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(output))
                Assert.Ignore("Image construction capture requires explicit real cache and output directories");
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType);
            Assert.IsTrue(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf));
            var packetBytes = File.ReadAllBytes(Path.Combine(cache,"spell.json"));
            var packetHash = ParchmentStore.Hash(packetBytes);
            var expected = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_PACKET_SHA256");
            if (!string.IsNullOrWhiteSpace(expected)) Assert.AreEqual(expected.ToLowerInvariant(),packetHash);
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(System.Text.Encoding.UTF8.GetString(packetBytes));
            Assert.NotNull(packet?.visual_reference,"The packet must identify its actual generated reference image");
            Assert.NotNull(packet.plan?.nodes);
            Assert.AreEqual(packet.visual_reference.sha256,packet.plan.visual_reference_sha256);
            Assert.AreEqual(packet.description_sha256,packet.visual_reference.description_sha256);
            Assert.AreEqual(packet.description_sha256,packet.plan.description_sha256);
            var referencePath = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_REFERENCE_PATH");
            if (string.IsNullOrWhiteSpace(referencePath)) referencePath = Path.Combine(cache,"artifacts",packet.visual_reference.artifact_id);
            var referenceBytes = File.ReadAllBytes(referencePath);
            Assert.AreEqual(packet.visual_reference.sha256,ParchmentStore.Hash(referenceBytes));
            Assert.AreEqual(packet.visual_reference.size_bytes,referenceBytes.Length);
            Assert.Greater(referenceBytes.Length,24);
            CollectionAssert.AreEqual(new byte[] { 137,80,78,71,13,10,26,10 },referenceBytes.Take(8).ToArray());
            Assert.AreEqual(packet.visual_reference.width_px,PngInteger(referenceBytes,16));
            Assert.AreEqual(packet.visual_reference.height_px,PngInteger(referenceBytes,20));
            var constructionNodes = packet.plan.nodes.Where(ImageConstructedSpellVisual.Supports).ToArray();
            Assert.Greater(constructionNodes.Length,0,"Actual packet must contain image-derived construction parts");
            foreach (var node in constructionNodes) ImageConstructedSpellVisual.ValidateConstruction(node.appearance.construction);
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output,"visual-reference.png"),referenceBytes);
            var previousCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.gameObject.activeSelf).ToArray();
            var previousAmbient = RenderSettings.ambientLight; var previousAmbientMode = RenderSettings.ambientMode;
            var previousFog = RenderSettings.fog; var previousFogColor = RenderSettings.fogColor;
            var previousFogStart = RenderSettings.fogStartDistance; var previousFogEnd = RenderSettings.fogEndDistance;
            var previousCaptureRate = Time.captureFramerate;
            var host = new GameObject("Cached image construction in actual spell lab");
            var shippingTarget = new RenderTexture(1280,1000,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            var sideTarget = new RenderTexture(1280,1000,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1280,1000,TextureFormat.RGBAFloat,false,true);
            var srgb = new Texture2D(1280,1000,TextureFormat.RGB24,false,false);
            var frames = new List<object>();
            var sequence = new List<object>();
            var sequenceDirectory = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_SEQUENCE_DIR");
            var sequenceEnabled = !string.IsNullOrWhiteSpace(sequenceDirectory);
            if (sequenceEnabled) Directory.CreateDirectory(sequenceDirectory);
            var sequencePixels = sequenceEnabled ? new Color[1280 * 1000] : null;
            var requireHit = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_REQUIRE_HIT") != "0";
            var sawConstruction = false; var maximumParts = 0; var maximumCarriers = 0;
            var completed = false;
            var castTime = 0f;
            try
            {
                Time.captureFramerate = 60;
                var lab = host.AddComponent<SpellLab>(); lab.InputSuppressed = true;
                lab.Initialize(System.Text.Encoding.UTF8.GetString(packetBytes),cache);
                Assert.IsTrue(lab.Ready,lab.Metrics);
                var camera = host.GetComponentInChildren<Camera>();
                Assert.NotNull(camera);
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.IsTrue(camera.allowHDR); Assert.IsTrue(cameraData.renderPostProcessing);
                camera.targetTexture = shippingTarget; camera.rect = new Rect(0,0,1,1);
                var sideObject = new GameObject("Additional side comparison camera - image construction");
                sideObject.transform.SetParent(host.transform,false);
                var sideCamera = sideObject.AddComponent<Camera>(); sideCamera.CopyFrom(camera);
                sideCamera.targetTexture = sideTarget; sideCamera.depth = camera.depth + 1;
                sideCamera.fieldOfView = 40;
                var sideData = sideObject.AddComponent<UniversalAdditionalCameraData>();
                sideData.renderPostProcessing = cameraData.renderPostProcessing;
                sideData.antialiasing = cameraData.antialiasing; sideData.antialiasingQuality = cameraData.antialiasingQuality;
                sideData.volumeLayerMask = cameraData.volumeLayerMask; sideData.volumeTrigger = sideCamera.transform;
                sideData.renderShadows = cameraData.renderShadows;
                sideData.requiresDepthTexture = cameraData.requiresDepthTexture; sideData.requiresColorTexture = cameraData.requiresColorTexture;
                var targets = host.GetComponentsInChildren<LabReceiver>().OrderBy(receiver => receiver.StableId).ToArray();
                var initialTargets = targets.Select(TargetState).ToArray();
                var selected = targets.First(receiver => receiver.StableId == 3);
                var aim = ParseAim(Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_AIM"),selected.transform.position);
                sideCamera.transform.position = new Vector3(7,3,-7);
                sideCamera.transform.LookAt(new Vector3(0,1.2f,-3));
                if (sequenceEnabled)
                {
                    Directory.CreateDirectory(Path.Combine(sequenceDirectory,"shipping"));
                    Directory.CreateDirectory(Path.Combine(sequenceDirectory,"side"));
                }
                yield return new WaitForSeconds(.2f);
                Assert.IsTrue(lab.Cast(aim)); castTime = Time.time;
                var sampleTimes = new[] { .08f,.25f,.45f,.70f,1.05f,1.40f,2.0f };
                var nextSample = 0; var presentationFrame = 0;
                var expiryBound = packet.plan.nodes.Sum(node => (double)(node.options?.lifetime_ticks ?? 1) + (node.activation?.delay_ticks ?? 0)) * Time.fixedDeltaTime + 1.8;
                Assert.LessOrEqual(expiryBound,120,"Capture requires a bounded real spell duration");
                var end = castTime + Mathf.Max(2.4f,(float)expiryBound);
                var lastFocus = new Vector3(0,1.1f,-3); var lastDirection = Vector3.forward;
                while (Time.time < end)
                {
                    var visuals = Object.FindObjectsByType<ImageConstructedSpellVisual>(FindObjectsSortMode.None);
                    var liveParts = visuals.Sum(item => item.PartCount);
                    if (liveParts > 0)
                    {
                        sawConstruction = true; maximumParts = Math.Max(maximumParts,liveParts);
                        foreach (var visual in visuals)
                        {
                            Assert.LessOrEqual(visual.PartCount,ImageConstructedSpellVisual.MaximumParts);
                            foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>())
                            {
                                Assert.IsNull(mesh.GetComponent<Collider>(),"Image construction meshes are decorative");
                                var renderer = mesh.GetComponent<Renderer>();
                                if (renderer != null) Assert.IsFalse(renderer.sharedMaterial.shader.name.Contains("InternalError"));
                            }
                        }
                        var activeVisual = visuals.FirstOrDefault(item => !item.IsImpact) ?? visuals[0];
                        lastDirection = activeVisual.transform.forward;
                        var renderers = activeVisual.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            var bounds = renderers[0].bounds;
                            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                            lastFocus = bounds.center;
                            var distance = Mathf.Clamp(bounds.extents.magnitude * 2.4f,5.5f,15);
                            var lateral = Vector3.Cross(Vector3.up,lastDirection).normalized;
                            if (lateral.sqrMagnitude < .001f) lateral = Vector3.right;
                            sideCamera.transform.position = lastFocus + lateral * distance + lastDirection * distance * .48f + Vector3.up * distance * .20f;
                            sideCamera.transform.LookAt(lastFocus);
                        }
                    }
                    maximumCarriers = Math.Max(maximumCarriers,lab.ActiveCarriers);
                    yield return null; // completed GPU frame; valid in batch PlayMode
                    var elapsed = Time.time - castTime;
                    if (nextSample < sampleTimes.Length && elapsed >= sampleTimes[nextSample])
                    {
                        var shippingFile = "image-spell-frame-" + nextSample.ToString("D2") + ".png";
                        var sideFile = "image-spell-side-frame-" + nextSample.ToString("D2") + ".png";
                        var bytes = HdrPresentationCapture.Png(shippingTarget,readback,srgb);
                        var sideBytes = HdrPresentationCapture.Png(sideTarget,readback,srgb);
                        Assert.Greater(bytes.Length,10000); Assert.Greater(sideBytes.Length,10000);
                        File.WriteAllBytes(Path.Combine(output,shippingFile),bytes); File.WriteAllBytes(Path.Combine(output,sideFile),sideBytes);
                        frames.Add(new { elapsed_seconds = elapsed, file = shippingFile, sha256 = ParchmentStore.Hash(bytes),
                            side_file = sideFile, side_sha256 = ParchmentStore.Hash(sideBytes), shipping_camera = Pose(camera), side_camera = Pose(sideCamera),
                            image_parts = liveParts, active_carriers = lab.ActiveCarriers, hits = lab.Hits, damage_milli = lab.DamageMilli,
                            impulses = lab.Impulses, targets = targets.Select(TargetState).ToArray() });
                        nextSample++;
                    }
                    if (sequenceEnabled && presentationFrame % 2 == 0)
                    {
                        var name = "frame-" + sequence.Count.ToString("D4") + ".jpg";
                        var shippingBytes = Jpeg(shippingTarget,readback,srgb,sequencePixels);
                        var sideBytes = Jpeg(sideTarget,readback,srgb,sequencePixels);
                        File.WriteAllBytes(Path.Combine(sequenceDirectory,"shipping",name),shippingBytes);
                        File.WriteAllBytes(Path.Combine(sequenceDirectory,"side",name),sideBytes);
                        sequence.Add(new { index = sequence.Count, elapsed_seconds = elapsed,
                            file = "shipping/" + name, sha256 = ParchmentStore.Hash(shippingBytes),
                            side_file = "side/" + name, side_sha256 = ParchmentStore.Hash(sideBytes),
                            active_carriers = lab.ActiveCarriers, image_parts = liveParts, hits = lab.Hits,
                            damage_milli = lab.DamageMilli, shipping_camera = Pose(camera), side_camera = Pose(sideCamera) });
                    }
                    presentationFrame++;
                }
                var remainingVisuals = Object.FindObjectsByType<ImageConstructedSpellVisual>(FindObjectsSortMode.None).Length;
                var cacheUnchanged = packetHash == ParchmentStore.Hash(File.ReadAllBytes(Path.Combine(cache,"spell.json")));
                completed = sawConstruction && maximumCarriers > 0 && (!requireHit || lab.Hits > 0)
                    && lab.ActiveCarriers == 0 && remainingVisuals == 0 && cacheUnchanged;
                File.WriteAllText(Path.Combine(output,"capture.json"),JsonConvert.SerializeObject(new {
                    evidence_kind = "cached_packet_image_construction_unity_replay", packet_sha256 = packetHash,
                    expected_packet_sha256 = expected, spell_title = lab.SpellTitle, visual_reference = packet.visual_reference,
                    image_file = "visual-reference.png", description_sha256 = packet.description_sha256,
                    provider = packet.provenance, model_calls = 0, art_acceptance = "not_claimed",
                    camera = "unchanged shipping lab pose and postprocessing, full arena viewport without UI",
                    side_camera = "additional 40 degree side comparison camera follows actual renderer bounds; no geometry or gameplay changes",
                    capture = "ARGBHalf linear target; completed GPU frame converted to sRGB; 60 Hz simulation is not a performance benchmark",
                    graphics_device = SystemInfo.graphicsDeviceName, authored_parts = constructionNodes.Sum(node => node.appearance.construction.parts.Count),
                    maximum_visible_parts = maximumParts, maximum_active_carriers = maximumCarriers, aim = Position(aim), require_hit = requireHit,
                    hits = lab.Hits, damage_milli = lab.DamageMilli, impulses = lab.Impulses, initial_targets = initialTargets,
                    final_targets = targets.Select(TargetState).ToArray(), expiry_bound_seconds = expiryBound,
                    final_active_carriers = lab.ActiveCarriers, final_image_renderers = remainingVisuals, cache_unchanged = cacheUnchanged, completed, frames
                },Formatting.Indented));
                Assert.IsTrue(sawConstruction,"The actual cached composition must have rendered during its life");
                Assert.Greater(maximumCarriers,0);
                if (requireHit) Assert.Greater(lab.Hits,0,"The unmodified real spell must contact a lab target at the stated aim");
                Assert.AreEqual(0,lab.ActiveCarriers,"Decorative image parts must not extend carrier lifetime");
                Assert.AreEqual(0,remainingVisuals,"Image-derived pieces must release after the spell and impact expire");
                Assert.IsTrue(cacheUnchanged,"Replay cannot edit the cached spell");
                Debug.Log("PALIMPSESTE_IMAGE_CONSTRUCTION_CAPTURE " + output);
            }
            finally
            {
                if (sequenceEnabled) File.WriteAllText(Path.Combine(sequenceDirectory,"sequence.json"),JsonConvert.SerializeObject(new {
                    evidence_kind = "cached_packet_image_construction_animation", packet_sha256 = packetHash,
                    reference_sha256 = packet.visual_reference.sha256, model_calls = 0, art_acceptance = "not_claimed",
                    completed, presentation_framerate = 60, sequence_framerate = 30, frame_count = sequence.Count, frames = sequence
                },Formatting.Indented));
                Object.Destroy(host); Object.Destroy(readback); Object.Destroy(srgb);
                shippingTarget.Release(); Object.Destroy(shippingTarget); sideTarget.Release(); Object.Destroy(sideTarget);
                Time.captureFramerate = previousCaptureRate;
                foreach (var camera in previousCameras) if (camera != null) camera.gameObject.SetActive(true);
                RenderSettings.ambientLight = previousAmbient; RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.fog = previousFog; RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogStartDistance = previousFogStart; RenderSettings.fogEndDistance = previousFogEnd;
            }
            yield return null;
        }

        private static int PngInteger(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset+1] << 16 | bytes[offset+2] << 8 | bytes[offset+3];
        private static float[] Position(Vector3 value) => new[] { value.x,value.y,value.z };
        private static object TargetState(LabReceiver receiver) => new { id = receiver.StableId, team = receiver.Team,
            health_milli = receiver.HealthMilli, position = Position(receiver.transform.position) };
        private static object Pose(Camera camera) => new { position = Position(camera.transform.position), forward = Position(camera.transform.forward),
            field_of_view_degrees = camera.fieldOfView };
        private static Vector3 ParseAim(string input, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            var values = input.Split(','); Assert.AreEqual(3,values.Length);
            return new Vector3(float.Parse(values[0],CultureInfo.InvariantCulture),float.Parse(values[1],CultureInfo.InvariantCulture),
                float.Parse(values[2],CultureInfo.InvariantCulture));
        }
        private static byte[] Jpeg(RenderTexture target, Texture2D readback, Texture2D output, Color[] pixels)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target; readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0); readback.Apply(false,false);
                var source = readback.GetRawTextureData<Color>();
                for (var i = 0; i < pixels.Length; i++) pixels[i] = source[i].gamma;
                output.SetPixels(pixels); output.Apply(false,false); return output.EncodeToJPG(95);
            }
            finally { RenderTexture.active = previous; }
        }
    }
}

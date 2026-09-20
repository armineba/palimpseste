using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Palimpseste.Game.Library;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class CachedSpectrePresentationTests
    {
        private const string PacketSha256 = "0ed08fee15298d8e61fc396eddc4f2a5ed2827d0b8efd51639e2771f2162e467";

        // An explicitly requested replay of an existing player packet. This
        // neither edits the cache nor calls a model, and is not artistic approval.
        [UnityTest]
        public IEnumerator CaptureExistingSpectreInLab()
        {
            var cache = Environment.GetEnvironmentVariable("PALIMPSESTE_SPECTRE_CACHE_DIR");
            var output = Environment.GetEnvironmentVariable("PALIMPSESTE_SPECTRE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(output))
                Assert.Ignore("Existing-player capture requires explicit cache and output directories");
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "Moving visual evidence requires a real graphics device");
            var packetPath = Path.Combine(cache, "spell.json");
            var packetBytes = File.ReadAllBytes(packetPath);
            Assert.AreEqual(PacketSha256, ParchmentStore.Hash(packetBytes),
                "This replay must retain the exact existing player's compiled spell");
            Directory.CreateDirectory(output);
            var previousCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.gameObject.activeSelf).ToArray();
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousFog = RenderSettings.fog;
            var previousFogColor = RenderSettings.fogColor;
            var previousFogStart = RenderSettings.fogStartDistance;
            var previousFogEnd = RenderSettings.fogEndDistance;
            var previousCaptureFramerate = Time.captureFramerate;
            var host = new GameObject("Existing player spell replay");
            var targetTexture = new RenderTexture(1280, 1000, 24,
                RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var sideTargetTexture = new RenderTexture(1280, 1000, 24,
                RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var image = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false, false);
            var linearReadback = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBAFloat, false, true);
            var frames = new List<object>();
            SequenceCapture sequence = null;
            try
            {
                // Fixed presentation steps make the same sampling times
                // comparable across art iterations. Gameplay stays unchanged.
                Time.captureFramerate = 60;
                var lab = host.AddComponent<SpellLab>();
                lab.InputSuppressed = true;
                lab.Initialize(System.Text.Encoding.UTF8.GetString(packetBytes), cache);
                Assert.IsTrue(lab.Ready, lab.Metrics);
                Assert.AreEqual("Envol du spectre aux longs voiles", lab.SpellTitle);
                var camera = host.GetComponentInChildren<Camera>();
                Assert.NotNull(camera);
                Assert.IsTrue(camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing);
                Assert.NotNull(Resources.Load<VolumeProfile>("LabVfxVolume"));
                camera.targetTexture = targetTexture;
                // Capture the arena viewport, with the shipping camera pose,
                // optics and postprocessing; the separate parchment UI is absent.
                camera.rect = new Rect(0, 0, 1, 1);
                var sideCameraObject = new GameObject("Reference comparison camera - actual runtime side view");
                sideCameraObject.transform.SetParent(host.transform, false);
                var sideCamera = sideCameraObject.AddComponent<Camera>();
                sideCamera.CopyFrom(camera);
                sideCamera.depth = camera.depth + 1;
                sideCamera.targetTexture = sideTargetTexture;
                sideCamera.rect = new Rect(0, 0, 1, 1);
                sideCamera.fieldOfView = 40;
                var shippingData = camera.GetComponent<UniversalAdditionalCameraData>();
                var sideData = sideCameraObject.AddComponent<UniversalAdditionalCameraData>();
                sideData.renderPostProcessing = shippingData.renderPostProcessing;
                sideData.antialiasing = shippingData.antialiasing;
                sideData.antialiasingQuality = shippingData.antialiasingQuality;
                sideData.volumeLayerMask = shippingData.volumeLayerMask;
                sideData.volumeTrigger = sideCamera.transform;
                sideData.renderShadows = shippingData.renderShadows;
                sideData.requiresDepthTexture = shippingData.requiresDepthTexture;
                sideData.requiresColorTexture = shippingData.requiresColorTexture;
                // Establish the launch view before the optional sequence's
                // first completed GPU frame, while no carrier exists yet.
                sideCamera.transform.position = new Vector3(8.5f, 2.85f, -3.65f);
                sideCamera.transform.LookAt(new Vector3(0, 1.35f, -7.15f), Vector3.up);
                var receiver = host.GetComponentsInChildren<LabReceiver>()
                    .Single(candidate => candidate.StableId == 3);
                yield return new WaitForSeconds(.2f);
                var initialHealth = receiver.HealthMilli;
                var initialPosition = receiver.transform.position;
                // Compensate for the packet's curved flight, just as aiming in
                // the lab does. The packet, collider and targets remain intact.
                Assert.IsTrue(lab.Cast(new Vector3(7.2f, 1.1f, 6f)));
                var castTime = Time.time;
                var sampleTimes = new[] { .08f, .35f, .70f, 1.05f, 1.40f, 1.70f };
                var lastFlightPosition = new Vector3(0, 1.1f, -5);
                var lastFlightDirection = Vector3.forward;
                var sequenceDirectory = Environment.GetEnvironmentVariable("PALIMPSESTE_SPECTRE_SEQUENCE_DIR");
                if (!string.IsNullOrWhiteSpace(sequenceDirectory))
                {
                    sequence = new SequenceCapture(sequenceDirectory, targetTexture.width * targetTexture.height,
                        Environment.GetEnvironmentVariable("PALIMPSESTE_SPECTRE_SEQUENCE_SIDE") == "1");
                    lab.StartCoroutine(CaptureSequence(sequence, targetTexture, sideTargetTexture,
                        linearReadback, image, camera, sideCamera, lab, castTime));
                }
                for (var index = 0; index < sampleTimes.Length; index++)
                {
                    while (Time.time - castTime < sampleTimes[index])
                    {
                        FollowReferenceView(sideCamera, ref lastFlightPosition, ref lastFlightDirection);
                        yield return null;
                    }
                    FollowReferenceView(sideCamera, ref lastFlightPosition, ref lastFlightDirection);
                    // Read the last completed GPU frame. WaitForEndOfFrame does
                    // not consistently resume in batch editor play-mode runs.
                    yield return null;
                    var fileName = "spectre-frame-" + index.ToString("D2") + ".png";
                    var bytes = HdrPresentationCapture.Png(targetTexture, linearReadback, image);
                    File.WriteAllBytes(Path.Combine(output, fileName), bytes);
                    Assert.Greater(bytes.Length, 10000, "Capture must contain rendered arena data");
                    var sideFileName = "spectre-side-frame-" + index.ToString("D2") + ".png";
                    var sideBytes = HdrPresentationCapture.Png(sideTargetTexture, linearReadback, image);
                    File.WriteAllBytes(Path.Combine(output, sideFileName), sideBytes);
                    Assert.Greater(sideBytes.Length, 10000, "Side comparison must contain actual rendered arena data");
                    var visual = Object.FindFirstObjectByType<SemanticSpellVisual>();
                    if (visual != null) Assert.AreEqual("spirit", visual.Form);
                    frames.Add(new {
                        file = fileName, sha256 = ParchmentStore.Hash(bytes),
                        shipping_camera = CameraPose(camera),
                        side_comparison = new {
                            file = sideFileName, sha256 = ParchmentStore.Hash(sideBytes),
                            camera = CameraPose(sideCamera),
                            tracked_flight_position = Position(lastFlightPosition),
                            tracked_flight_direction = Position(lastFlightDirection)
                        },
                        elapsed_seconds = Time.time - castTime,
                        active_carriers = lab.ActiveCarriers, hits = lab.Hits,
                        damage_milli = lab.DamageMilli, impulses = lab.Impulses,
                        dissolving_wake_visible = visual != null && lab.ActiveCarriers == 0,
                        visual_position = visual == null ? null : Position(visual.transform.position),
                        target_position = Position(receiver.transform.position),
                        target_health_milli = receiver.HealthMilli
                    });
                }
                File.WriteAllText(Path.Combine(output, "capture.json"), JsonConvert.SerializeObject(new {
                    evidence_kind = "existing_player_packet_unity_lab_replay",
                    packet_sha256 = PacketSha256, spell_title = lab.SpellTitle,
                    art_acceptance = "not_claimed", model_calls = 0,
                    camera = "shipping lab pose and postprocessing; arena viewport without UI",
                    side_comparison_camera = "additional low side three-quarter camera follows the real carrier and its wake; same packet, arena and postprocessing; different camera pose and 40 degree field of view; no UI; holds its final pose after the carrier expires",
                    capture_framerate = Time.captureFramerate,
                    capture = "ARGBHalf linear target; postprocessed image converted to sRGB PNG",
                    graphics_device = SystemInfo.graphicsDeviceName,
                    initial_target_position = Position(initialPosition), initial_target_health_milli = initialHealth,
                    final_target_health_milli = receiver.HealthMilli,
                    hits = lab.Hits, damage_milli = lab.DamageMilli, impulses = lab.Impulses,
                    target_displacement_m = Vector3.Distance(initialPosition, receiver.transform.position), frames
                }, Formatting.Indented));
                Assert.Greater(lab.Hits, 0, "The real spell must contact the existing lab target");
                Assert.Greater(lab.DamageMilli, 0, "The unchanged packet must deal real lab damage");
                Assert.Greater(lab.Impulses, 0, "The unchanged packet must apply its impulse");
                Assert.Less(receiver.HealthMilli, initialHealth);
                Debug.Log("PALIMPSESTE_EXISTING_SPECTRE_CAPTURE " + output);
                var damageBeforeDissipation = lab.DamageMilli;
                var hitsBeforeDissipation = lab.Hits;
                yield return new WaitForSeconds(.7f);
                Assert.AreEqual(0, lab.ActiveCarriers, "Visual dissipation cannot extend the mechanical carrier");
                Assert.AreEqual(damageBeforeDissipation, lab.DamageMilli, "Dissipating cloth cannot deal another hit");
                Assert.AreEqual(hitsBeforeDissipation, lab.Hits);
                Assert.IsNull(Object.FindFirstObjectByType<SemanticSpellVisual>(), "Retired cloth releases its renderer after fading");
                if (sequence != null) sequence.completed = true;
            }
            finally
            {
                if (sequence != null)
                {
                    sequence.active = false;
                    sequence.WriteManifest();
                }
                Object.Destroy(host);
                Object.Destroy(image);
                Object.Destroy(linearReadback);
                targetTexture.Release();
                Object.Destroy(targetTexture);
                sideTargetTexture.Release();
                Object.Destroy(sideTargetTexture);
                Time.captureFramerate = previousCaptureFramerate;
                foreach (var camera in previousCameras)
                    if (camera != null) camera.gameObject.SetActive(true);
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.fog = previousFog;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogStartDistance = previousFogStart;
                RenderSettings.fogEndDistance = previousFogEnd;
            }
            yield return null;
        }

        private static float[] Position(Vector3 value) => new[] { value.x, value.y, value.z };

        private sealed class SequenceCapture
        {
            public readonly string directory;
            public readonly bool side;
            public readonly Color[] pixels;
            public readonly List<object> frames = new List<object>();
            public bool active = true, completed;

            public SequenceCapture(string directory, int pixelCount, bool side)
            {
                this.directory = directory; this.side = side; pixels = new Color[pixelCount];
                Directory.CreateDirectory(Path.Combine(directory, "shipping"));
                if (side) Directory.CreateDirectory(Path.Combine(directory, "side"));
            }

            public void WriteManifest()
            {
                File.WriteAllText(Path.Combine(directory, "sequence.json"), JsonConvert.SerializeObject(new {
                    evidence_kind = "existing_player_packet_unity_animation_sequence",
                    packet_sha256 = PacketSha256, model_calls = 0, art_acceptance = "not_claimed",
                    completed, presentation_framerate = 60, sequence_framerate = 30,
                    capture = "last completed GPU frame every second presentation frame; ARGBHalf linear target with shipping tone mapping, converted to sRGB JPEG quality 95",
                    shipping_camera = "unchanged lab pose and postprocessing, arena viewport without UI",
                    side_camera = side ? "additional low side three-quarter comparison camera, same runtime and postprocessing, 40 degree field of view" : "not captured",
                    frame_count = frames.Count, frames
                }, Formatting.Indented));
            }
        }

        private static IEnumerator CaptureSequence(SequenceCapture sequence, RenderTexture shippingTarget,
            RenderTexture sideTarget, Texture2D linearReadback, Texture2D output,
            Camera shippingCamera, Camera sideCamera, SpellLab lab, float castTime)
        {
            var firstReadbackFrame = Time.frameCount;
            while (sequence.active)
            {
                var index = sequence.frames.Count;
                var name = "frame-" + index.ToString("D4") + ".jpg";
                var bytes = ReadbackJpeg(shippingTarget, linearReadback, output, sequence.pixels);
                File.WriteAllBytes(Path.Combine(sequence.directory, "shipping", name), bytes);
                string sideHash = null;
                if (sequence.side)
                {
                    var sideBytes = ReadbackJpeg(sideTarget, linearReadback, output, sequence.pixels);
                    File.WriteAllBytes(Path.Combine(sequence.directory, "side", name), sideBytes);
                    sideHash = ParchmentStore.Hash(sideBytes);
                }
                sequence.frames.Add(new {
                    index, file = "shipping/" + name, sha256 = ParchmentStore.Hash(bytes),
                    readback_elapsed_seconds = Time.time - castTime,
                    completed_frame_elapsed_seconds = Mathf.Max(0, Time.time - castTime - Time.deltaTime),
                    presentation_frame_offset = Time.frameCount - firstReadbackFrame,
                    active_carriers = lab.ActiveCarriers, hits = lab.Hits, damage_milli = lab.DamageMilli,
                    shipping_camera = CameraPose(shippingCamera),
                    side_file = sequence.side ? "side/" + name : null,
                    side_sha256 = sideHash,
                    side_camera = sequence.side ? CameraPose(sideCamera) : null
                });
                // With captureFramerate=60, two rendered frames produce one
                // 30 fps image. Wall time spent encoding cannot skip simulation.
                yield return null;
                yield return null;
            }
        }

        private static byte[] ReadbackJpeg(RenderTexture source, Texture2D linearReadback,
            Texture2D output, Color[] pixels)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                linearReadback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                linearReadback.Apply(false, false);
                var sourcePixels = linearReadback.GetRawTextureData<Color>();
                for (var i = 0; i < pixels.Length; i++) pixels[i] = sourcePixels[i].gamma;
                output.SetPixels(pixels);
                output.Apply(false, false);
                return output.EncodeToJPG(95);
            }
            finally { RenderTexture.active = previous; }
        }

        private static object CameraPose(Camera camera) => new {
            position = Position(camera.transform.position),
            forward = Position(camera.transform.forward),
            up = Position(camera.transform.up),
            field_of_view_degrees = camera.fieldOfView
        };

        private static void FollowReferenceView(Camera camera, ref Vector3 position, ref Vector3 direction)
        {
            var visual = Object.FindFirstObjectByType<SemanticSpellVisual>();
            if (visual == null) return;
            position = visual.transform.position;
            direction = visual.transform.forward.normalized;
            var lateral = Vector3.Cross(Vector3.up, direction).normalized;
            if (lateral.sqrMagnitude < .01f) lateral = Vector3.right;
            // Frame the real head and 5-6 m trailing cloak. The camera is on
            // the front/right quarter, so both hood opening and cloth length
            // are visible. No visual transform or spell trajectory is changed.
            var focus = position - direction * 2.15f + Vector3.up * .25f;
            camera.transform.position = focus + lateral * 8.5f + direction * 3.5f + Vector3.up * 1.5f;
            camera.transform.LookAt(focus, Vector3.up);
        }
    }
}

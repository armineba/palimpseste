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
            var host = new GameObject("Existing player spell replay");
            var targetTexture = new RenderTexture(1280, 1000, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);
            var frames = new List<object>();
            try
            {
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
                for (var index = 0; index < sampleTimes.Length; index++)
                {
                    while (Time.time - castTime < sampleTimes[index]) yield return null;
                    // Read the last completed GPU frame. WaitForEndOfFrame does
                    // not consistently resume in batch editor play-mode runs.
                    yield return null;
                    var previousTarget = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = targetTexture;
                        image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                        image.Apply();
                    }
                    finally { RenderTexture.active = previousTarget; }
                    var fileName = "spectre-frame-" + index.ToString("D2") + ".png";
                    var bytes = image.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(output, fileName), bytes);
                    Assert.Greater(bytes.Length, 10000, "Capture must contain rendered arena data");
                    var visual = Object.FindFirstObjectByType<SemanticSpellVisual>();
                    if (visual != null) Assert.AreEqual("spirit", visual.Form);
                    frames.Add(new {
                        file = fileName, sha256 = ParchmentStore.Hash(bytes),
                        elapsed_seconds = Time.time - castTime,
                        active_carriers = lab.ActiveCarriers, hits = lab.Hits,
                        damage_milli = lab.DamageMilli, impulses = lab.Impulses,
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
                yield return new WaitForSeconds(.7f);
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(image);
                targetTexture.Release();
                Object.Destroy(targetTexture);
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
    }
}

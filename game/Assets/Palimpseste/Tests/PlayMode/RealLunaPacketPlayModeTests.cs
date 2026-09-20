using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class RealLunaPacketPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealLunaBeamProducesPixelsAfterLogicalExpiry()
        {
            var directory = Environment.GetEnvironmentVariable("PALIMPSESTE_REAL_PROBE_PACKET_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                Assert.Ignore("Set PALIMPSESTE_REAL_PROBE_PACKET_DIR to a private compiled probe folder to run this test.");
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "A real graphics device is required for the pixel probe.");

            var packetJson = File.ReadAllText(Path.Combine(directory, "packet.json"));
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(packetJson);
            Assert.NotNull(packet);
            Assert.AreEqual(ExpectedDescriptionSha(), packet.description_sha256,
                "This pixel probe must use the selected frozen real Luna A description.");
            Assert.AreEqual(1, packet.plan.nodes.Count);
            Assert.AreEqual("beam", packet.plan.nodes[0].carrier);
            Assert.AreEqual(1, packet.plan.nodes[0].options.lifetime_ticks);
            Assert.AreEqual(0, packet.plan.nodes[0].effects.Count);
            var host = new GameObject("Real Luna graphics probe");
            var cameraObject = new GameObject("Isolated beam pixel camera");
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            try
            {
                var lab = host.AddComponent<SpellLab>();
                lab.Initialize(packetJson, directory);
                Assert.IsTrue(lab.Ready, lab.Metrics);
                Assert.IsTrue(lab.Cast(new Vector3(0, 0, 5)));
                lab.SimulateTicks(2);
                Assert.AreEqual(0, lab.ActiveCarriers);

                var beam = Array.Find(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None),
                    line => line != null && line.gameObject.name.StartsWith("beam ", StringComparison.Ordinal));
                Assert.NotNull(beam, "The logically expired beam must retain its graphics for rendering.");
                foreach (var part in beam.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;

                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 6;
                camera.transform.position = new Vector3(10, 1.1f, 0);
                camera.transform.LookAt(new Vector3(0, 1.1f, 0));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 30;
                camera.targetTexture = target;
                Assert.IsTrue(target.Create(), "Graphics device must create the offscreen render target.");

                yield return null;
                Assert.IsTrue(beam != null, "Afterimage must remain through one frame.");
                var visiblePixels = FirePixels(camera, target, pixels);
                Assert.Greater(visiblePixels, 100, "The real Luna beam must produce red pixels after logical expiry.");
                var framePath = Environment.GetEnvironmentVariable("PALIMPSESTE_REAL_PROBE_FRAME");
                if (!string.IsNullOrWhiteSpace(framePath)) File.WriteAllBytes(framePath, pixels.EncodeToPNG());

                yield return new WaitForSecondsRealtime(.25f);
                yield return null;
                Assert.IsTrue(beam == null, "The afterimage must be cleaned after the display window.");
                Assert.AreEqual(0, FirePixels(camera, target, pixels), "No beam pixels may remain after cleanup.");
                TestContext.WriteLine("real_luna_pixels: device=" + SystemInfo.graphicsDeviceType +
                    " fire_pixels_after_expiry=" + visiblePixels + " fire_pixels_after_cleanup=0");
            }
            finally
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator CompiledRealLunaBeamCastsWithoutInventedDamage()
        {
            var directory = Environment.GetEnvironmentVariable("PALIMPSESTE_REAL_PROBE_PACKET_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                Assert.Ignore("Set PALIMPSESTE_REAL_PROBE_PACKET_DIR to a private compiled probe folder to run this test.");

            var packetPath = Path.Combine(directory, "packet.json");
            Assert.IsTrue(File.Exists(packetPath), packetPath);
            var packetJson = File.ReadAllText(packetPath);
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(packetJson);
            Assert.NotNull(packet);
            Assert.AreEqual("sp.compiled/1.0", packet.schema_version);
            Assert.AreEqual(ExpectedDescriptionSha(), packet.description_sha256,
                "This probe must use the selected frozen real Luna A description.");
            Assert.AreEqual(1, packet.plan.nodes.Count);
            Assert.AreEqual("beam", packet.plan.nodes[0].carrier);
            Assert.AreEqual(0, packet.plan.nodes[0].effects.Count,
                "The drawing and Luna B did not justify damage or another gameplay effect.");
            var pathEntry = packet.geometry_manifest.Find(entry => entry.id == packet.plan.nodes[0].geometry_id);
            Assert.NotNull(pathEntry, "The compiled beam must carry its painted path.");
            var pathAsset = JsonConvert.DeserializeObject<GeometryAsset>(
                File.ReadAllText(Path.Combine(directory, "artifacts", pathEntry.artifact_id)));
            Assert.Greater(pathAsset.points.Count, 2, "The real drawing must supply a sampled beam path.");

            var birthSourcesBefore = BirthSources();
            var host = new GameObject("Isolated real Luna packet probe");
            try
            {
                var lab = host.AddComponent<SpellLab>();
                lab.Initialize(packetJson, directory);
                Assert.IsTrue(lab.Ready, lab.Metrics);
                Assert.AreEqual(packet.display.title, lab.SpellTitle);
                Assert.IsTrue(lab.Cast(new Vector3(0, 0, 5)));
                lab.SimulateTicks(1);

                Assert.AreEqual(1, lab.ActiveCarriers, "The one tick beam must exist before expiry.");
                var beam = Array.Find(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None),
                    line => line != null && line.gameObject.name.StartsWith("beam ", StringComparison.Ordinal));
                Assert.NotNull(beam, "A beam LineRenderer must be instantiated in the Unity scene.");
                Assert.NotNull(beam.material.shader, "The beam must use a real material shader.");
                Assert.Greater(beam.positionCount, 2,
                    "The main line must use the real drawing's path, not a generic two-point ray.");
                var realLinePositions = beam.positionCount;
                Assert.Greater(Vector3.Distance(beam.GetPosition(0), beam.GetPosition(beam.positionCount - 1)), .1f,
                    "The beam must have visible world-space length.");
                Assert.Greater(beam.startColor.r, beam.startColor.b, "The inferred fire affinity must tint the beam.");
                Assert.Greater(BirthSources(), birthSourcesBefore, "Casting must create a procedural birth sound source.");
                Assert.AreEqual(0, lab.DamageMilli, "The real Luna plan has no damage effect.");

                lab.SimulateTicks(1);
                Assert.AreEqual(0, lab.ActiveCarriers, "The one tick beam must expire.");
                Assert.IsTrue(beam != null, "The visual must remain after logical expiry.");
                Assert.AreEqual(0, beam.GetComponentsInChildren<Collider>().Length,
                    "A retired beam visual must have no collider.");
                Assert.AreEqual(0, beam.GetComponentsInChildren<AudioSource>().Length,
                    "A retired beam visual must not own an active sound source.");
                yield return null;
                Assert.IsTrue(beam != null, "The visual must survive one render opportunity after logical expiry.");
                Assert.AreEqual(0, lab.ActiveCarriers, "The visual tail cannot reactivate the carrier.");
                Assert.AreEqual(0, lab.DamageMilli, "The visual tail cannot apply damage.");
                yield return new WaitForSecondsRealtime(.25f);
                yield return null;
                Assert.IsTrue(beam == null, "The visual tail must clean itself up promptly.");
                TestContext.WriteLine("real_luna_beam: packet=" + packet.description_sha256 +
                    " cast=true source_path_points=" + pathAsset.points.Count +
                    " line_positions=" + realLinePositions + " damage_milli=" + lab.DamageMilli +
                    " expired=true afterimage_cleaned=true");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
            yield return null;
        }

        private static int BirthSources()
        {
            var count = 0;
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.clip != null && source.clip.name == "palimpseste-birth") count++;
            return count;
        }

        private static string ExpectedDescriptionSha()
        {
            var value = Environment.GetEnvironmentVariable("PALIMPSESTE_REAL_PROBE_DESCRIPTION_SHA256");
            Assert.IsFalse(string.IsNullOrWhiteSpace(value),
                "Set PALIMPSESTE_REAL_PROBE_DESCRIPTION_SHA256 to the frozen A output hash.");
            Assert.AreEqual(64, value.Length, "The frozen A output hash must be SHA-256.");
            foreach (var character in value)
                Assert.IsTrue(Uri.IsHexDigit(character), "The frozen A output hash must be hexadecimal.");
            return value.ToLowerInvariant();
        }

        private static int FirePixels(Camera camera, RenderTexture target, Texture2D pixels)
        {
            var previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply(false, false);
                var count = 0;
                foreach (var pixel in pixels.GetPixels32())
                    if (pixel.r > 30 && pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 2f) count++;
                return count;
            }
            finally { RenderTexture.active = previous; }
        }
    }
}

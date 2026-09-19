using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.Library;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class SpellLabFixtureTests
    {
        [UnityTest]
        public IEnumerator SixCarriersCastFromIsolatedManualFixtures()
        {
            Assert.NotNull(Resources.Load<Shader>("LabUnlit"), "Runtime shader must be packaged as a Resources asset");
            Assert.NotNull(Resources.Load<Material>("LabOpaque"), "Opaque URP material must be packaged as a Resources asset");
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var tempRoot = Path.GetFullPath(Application.temporaryCachePath);
            var directory = Path.Combine(tempRoot, "palimpseste-playmode-" + Guid.NewGuid().ToString("N"));
            var artifactDirectory = Path.Combine(directory, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(File.ReadAllText(Path.Combine(examples, "03_paquet_illustratif.json")));
            foreach (var entry in packet.geometry_manifest)
            {
                var source = Path.Combine(examples, "geometry", entry.id + ".json");
                var bytes = File.ReadAllBytes(source);
                Assert.AreEqual(entry.sha256, ParchmentStore.Hash(bytes), entry.id);
                File.WriteAllBytes(Path.Combine(artifactDirectory, entry.artifact_id), bytes);
            }
            foreach (var entry in packet.binary_assets)
            {
                var source = Path.Combine(examples, "geometry", entry.file_name);
                var bytes = File.ReadAllBytes(source);
                Assert.AreEqual(entry.sha256, ParchmentStore.Hash(bytes), entry.file_name);
                File.WriteAllBytes(Path.Combine(artifactDirectory, entry.artifact_id), bytes);
            }
            foreach (var name in new[] { "projectile", "beam", "field", "pulse", "barrier", "trap" })
            {
                packet.plan = JsonConvert.DeserializeObject<SpellPlan>(File.ReadAllText(Path.Combine(examples, "fixture_" + name + ".json")));
                if (name == "field" || name == "pulse" || name == "barrier" || name == "trap")
                    packet.plan.nodes[0].anchor = "aim_point";
                var host = new GameObject("Fixture " + name);
                var lab = host.AddComponent<SpellLab>();
                lab.Initialize(JsonConvert.SerializeObject(packet), directory);
                Assert.IsTrue(lab.Ready, name + ": " + lab.Metrics);
                var receivers = host.GetComponentsInChildren<LabReceiver>();
                var hostile = Array.Find(receivers, receiver => receiver.StableId == 2);
                var center = Array.Find(receivers, receiver => receiver.StableId == 3);
                var ally = Array.Find(receivers, receiver => receiver.StableId == 5);
                var environment = Array.Find(receivers, receiver => receiver.StableId == 6);
                Assert.NotNull(hostile);
                Assert.NotNull(center);
                Assert.NotNull(ally);
                Assert.NotNull(environment);
                var aim = new Vector3(0, 0, 4);
                switch (name)
                {
                    case "projectile":
                        environment.transform.position = new Vector3(0, 1.1f, 2);
                        aim = new Vector3(0, 0, 8);
                        break;
                    case "field":
                        hostile.transform.position = new Vector3(1, 1, 4);
                        center.transform.position = new Vector3(0, 1, 4);
                        center.transform.localScale = Vector3.one * .1f;
                        break;
                    case "pulse":
                        ally.transform.position = new Vector3(1, 1, 4);
                        Assert.AreEqual(15000, ally.ApplyDamage(15000));
                        break;
                    case "trap":
                        hostile.transform.position = new Vector3(1, 1, 4);
                        break;
                    case "barrier":
                        // Place the fixture wall between caster and target.
                        aim = new Vector3(0, 0, 3);
                        break;
                }
                Physics.SyncTransforms();
                Assert.IsTrue(lab.Cast(aim), name);
                lab.SimulateTicks(1);
                if (name == "projectile")
                {
                    var oneShot = Array.Find(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None),
                        source => source.clip != null && source.clip.name == "palimpseste-birth");
                    Assert.NotNull(oneShot, "A real cast must create a one-shot AudioSource");
                    Assert.Greater(oneShot.clip.samples, 0, "Procedural sound clip must contain samples");
                    TestContext.WriteLine("audio_birth: samples=" + oneShot.clip.samples + " frequency=" + oneShot.clip.frequency);
                }
                lab.SimulateTicks((name == "projectile" ? 75 : name == "pulse" ? 50 : name == "trap" ? 30 : 3) - 1);
                if (name == "projectile")
                {
                    Assert.Greater(lab.Impulses, 0, "Homing projectile must impulse the environment receiver: " + lab.Metrics);
                    yield return new WaitForFixedUpdate();
                    Assert.Greater(environment.GetComponent<Rigidbody>().linearVelocity.z, .05f,
                        "Environment rigidbody must move forward after the impulse");
                }
                if (name == "beam")
                {
                    Assert.Greater(lab.Hits, 0, "Beam must hit a real hostile receiver");
                    Assert.Greater(lab.DamageMilli, 0, "Beam must damage an admissible hostile receiver");
                }
                if (name == "field")
                {
                    Assert.Greater(hostile.WetUntil, 0, "Actor inside painted footprint must become wet");
                    Assert.AreEqual(0, center.WetUntil, "Small actor in the painted hole must remain dry");
                }
                if (name == "pulse")
                {
                    Assert.AreEqual(47000, ally.HealthMilli, "Wave must heal the injured ally by 12 points");
                    Assert.AreEqual(100000, hostile.HealthMilli, "Wave must not heal or damage a hostile actor");
                }
                if (name == "trap")
                {
                    Assert.Greater(hostile.SlowUntil, 0, "Armed trap must slow a hostile entering its painted area");
                    Assert.Less(hostile.SpeedFactor, 1f);
                }
                if (name == "barrier")
                {
                    var barrier = UnityEngine.Object.FindFirstObjectByType<BarrierReceiver>();
                    Assert.NotNull(barrier, "Barrier must create a receiver");
                    Assert.AreEqual(100000, barrier.StructureMilli);
                    Physics.SyncTransforms();
                    var rayHits = Physics.RaycastAll(new Vector3(0, 1, -3), Vector3.forward, 10);
                    Assert.IsTrue(Array.Exists(rayHits, hit => hit.collider.GetComponentInParent<BarrierReceiver>() == barrier),
                        "Barrier must physically intercept the path to a target");
                    lab.SimulateTicks(301);
                    Assert.AreEqual(0, lab.ActiveCarriers, "Barrier must expire");
                    Assert.IsFalse(Array.Exists(barrier.GetComponentsInChildren<Collider>(), collider => collider.enabled),
                        "Expired barrier must release its colliders");
                }
                TestContext.WriteLine(name + ": hits=" + lab.Hits + " damage_milli=" + lab.DamageMilli +
                    " impulses=" + lab.Impulses + " active=" + lab.ActiveCarriers +
                    " wet_until=" + hostile.WetUntil + " slow_until=" + hostile.SlowUntil +
                    " ally_health_milli=" + ally.HealthMilli);
                if (name != "barrier")
                {
                    lab.ResetTargets();
                    Assert.AreEqual(0, lab.ActiveCarriers, name + ": reset must cancel carriers");
                }
                UnityEngine.Object.Destroy(host);
                yield return null;
            }
            packet = JsonConvert.DeserializeObject<CompiledSpell>(File.ReadAllText(Path.Combine(examples, "03_paquet_illustratif.json")));
            var curveHost = new GameObject("Compiled sample curved projectile");
            var curveLab = curveHost.AddComponent<SpellLab>();
            curveLab.Initialize(JsonConvert.SerializeObject(packet), directory);
            Assert.IsTrue(curveLab.Ready, curveLab.Metrics);
            Assert.IsTrue(curveLab.Cast(new Vector3(0, 0, 8)));
            curveLab.SimulateTicks(75);
            Assert.Greater(curveLab.Hits, 0, "Curved projectile must reach a hostile receiver");
            Assert.Greater(curveLab.DamageMilli, 0, "Curved projectile must apply damage");
            Assert.IsTrue(Array.Exists(curveHost.GetComponentsInChildren<LabReceiver>(), receiver => receiver.BurnUntil > 0),
                "Child field must apply a burn status after projectile contact");
            TestContext.WriteLine("compiled_curve: hits=" + curveLab.Hits + " damage_milli=" + curveLab.DamageMilli);
            UnityEngine.Object.Destroy(curveHost);
            yield return null;
            var fullTemp = Path.GetFullPath(directory);
            Assert.IsTrue(fullTemp.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Directory.Delete(fullTemp, true);
        }
    }
}

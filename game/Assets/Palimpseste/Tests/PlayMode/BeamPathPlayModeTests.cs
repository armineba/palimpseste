using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class BeamPathPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExplicitSelfBeamCanReachCasterOnce()
        {
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(
                File.ReadAllText(Path.Combine(examples, "03_paquet_illustratif.json")));
            packet.plan = JsonConvert.DeserializeObject<SpellPlan>(
                File.ReadAllText(Path.Combine(examples, "fixture_beam.json")));
            packet.plan.nodes[0].options.chain_filter = "self";
            packet.plan.nodes[0].options.chain_hops = 0;
            packet.plan.nodes[0].effects[0].target_filter = "self";
            var host = new GameObject("Self beam proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.transform.SetParent(caster.transform);
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localScale = Vector3.one * .5f;
            var self = avatar.AddComponent<LabReceiver>();
            self.Initialize(1, "ally");
            var geometry = new GeometryAsset
            {
                geometry_id = "ring.path.0", kind = "path",
                points = new List<GeometryPoint>
                {
                    new GeometryPoint { x = -10000, z = 0 },
                    new GeometryPoint { x = 10000, z = 0 }
                }
            };
            var engine = new RuntimeEngine(packet,
                new Dictionary<string, GeometryAsset> { [geometry.geometry_id] = geometry },
                new Dictionary<string, Texture2D>(), caster.transform,
                new List<LabReceiver> { self });
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(1, engine.Hits);
                Assert.AreEqual(3000, engine.DamageMilli);
                Assert.AreEqual(97000, self.HealthMilli);
            }
            finally
            {
                engine.CancelAll();
                UnityEngine.Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeamBendChangesBothMainVisualAndPhysicalContact()
        {
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(
                File.ReadAllText(Path.Combine(examples, "03_paquet_illustratif.json")));
            packet.plan = JsonConvert.DeserializeObject<SpellPlan>(
                File.ReadAllText(Path.Combine(examples, "fixture_beam.json")));
            packet.plan.nodes[0].options.chain_hops = 0;
            packet.plan.nodes[0].options.chain_radius_cm = 0;
            packet.plan.nodes[0].effects.Add(new SpellEffect
            {
                id = "e1", clause_ids = new List<string> { "c0" }, @event = "hit",
                kind = "impulse", target_filter = "hostile", amount = 5000,
                duration_ticks = 0, direction = "forward"
            });

            var host = new GameObject("Bent beam geometry proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var bentTarget = Target(host.transform, "On painted bend", -1, 3, 20);
            var straightTarget = Target(host.transform, "On generic ray", 0, 3, 21);
            var geometry = new GeometryAsset
            {
                geometry_id = "ring.path.0", kind = "path",
                points = new List<GeometryPoint>
                {
                    new GeometryPoint { x = -10000, z = 0 },
                    new GeometryPoint { x = 0, z = 10000 },
                    new GeometryPoint { x = 10000, z = 0 }
                }
            };
            var engine = new RuntimeEngine(packet,
                new Dictionary<string, GeometryAsset> { [geometry.geometry_id] = geometry },
                new Dictionary<string, Texture2D>(), caster.transform,
                new List<LabReceiver> { bentTarget, straightTarget });
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(3000, engine.DamageMilli,
                    "The receiver on the painted bend must take the beam's real damage.");
                Assert.AreEqual(97000, bentTarget.HealthMilli);
                Assert.AreEqual(100000, straightTarget.HealthMilli,
                    "A receiver on the old generic straight ray must not be touched.");
                Assert.AreEqual(1, engine.Impulses);
                yield return new WaitForFixedUpdate();
                Assert.Greater(bentTarget.GetComponent<Rigidbody>().linearVelocity.x, .2f,
                    "A forward impulse must follow the bent contact segment, not the original cast axis.");
                var line = Array.Find(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None),
                    item => item != null && item.gameObject.name.StartsWith("beam ", StringComparison.Ordinal));
                Assert.NotNull(line);
                Assert.GreaterOrEqual(line.positionCount, 3,
                    "The drawn bend must appear in the beam's main line, before its physical hit.");
                var leftmost = 0f;
                for (var i = 0; i < line.positionCount; i++)
                    leftmost = Mathf.Min(leftmost, line.GetPosition(i).x);
                Assert.Less(leftmost, -1.5f);
                TestContext.WriteLine("painted_beam: points=" + line.positionCount +
                    " leftmost=" + leftmost + " damage_milli=" + engine.DamageMilli +
                    " generic_ray_health=" + straightTarget.HealthMilli);

                engine.CancelAll();
                bentTarget.Restore();
                straightTarget.Restore();
                bentTarget.transform.position = new Vector3(-1, 1, 3);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Solid wall on painted bend";
                wall.transform.SetParent(host.transform);
                wall.transform.position = new Vector3(-1.3f, 1, 2.7f);
                wall.transform.localScale = new Vector3(.5f, 1, .5f);
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(0, engine.DamageMilli,
                    "A solid wall on the painted route must intercept the beam before its target.");
                Assert.AreEqual(100000, bentTarget.HealthMilli);
                var stoppedLine = Array.Find(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None),
                    item => item != null && item.gameObject.name == "beam 2");
                Assert.NotNull(stoppedLine);
                Assert.GreaterOrEqual(stoppedLine.positionCount, 3);
                Assert.Less(stoppedLine.GetPosition(stoppedLine.positionCount - 1).z, 2.75f,
                    "The visible end must be clipped at the same wall as the physical beam.");
                TestContext.WriteLine("painted_beam_wall: points=" + stoppedLine.positionCount +
                    " end_z=" + stoppedLine.GetPosition(stoppedLine.positionCount - 1).z +
                    " damage_milli=" + engine.DamageMilli);
            }
            finally
            {
                engine.CancelAll();
                UnityEngine.Object.Destroy(host);
            }
            yield return null;
        }

        private static LabReceiver Target(Transform parent, string name, float x, float z, int id)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = name;
            target.transform.SetParent(parent);
            target.transform.position = new Vector3(x, 1, z);
            target.transform.localScale = new Vector3(.5f, 1f, .5f);
            var body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            var receiver = target.AddComponent<LabReceiver>();
            receiver.Initialize(id, "hostile");
            return receiver;
        }
    }
}

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
    public sealed class EffectRuntimePlayModeTests
    {
        private static readonly List<GeometryPoint> StraightPath = new List<GeometryPoint>
        {
            new GeometryPoint { x = -10000, z = 0 },
            new GeometryPoint { x = 10000, z = 0 }
        };

        [UnityTest]
        public IEnumerator ComposedBeamInflictsDamageAndTwoIndependentDotsThenHealsCasterFromActualHit()
        {
            var packet = BeamPacket("hostile");
            packet.plan.nodes[0].effects = new List<SpellEffect>
            {
                Effect("damage", "hostile", 10000),
                Effect("bleed", "hostile", 1000, 100),
                Effect("poison", "hostile", 1000, 100),
                Effect("life_steal", "hostile", 500)
            };
            var host = new GameObject("Composed effect proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var self = Receiver(caster.transform, "Self", 1, "ally", new Vector3(0, 1, 0));
            self.ApplyDamage(30000);
            var hostile = Receiver(host.transform, "Hostile", 2, "hostile", new Vector3(0, 1, 3));
            var engine = Engine(packet, caster.transform, self, hostile);
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(90000, hostile.HealthMilli, "Direct damage must come from the beam hit.");
                Assert.IsTrue(hostile.HasStatus("bleed"));
                Assert.IsTrue(hostile.HasStatus("poison"));
                Assert.AreEqual(75000, self.HealthMilli, "50% life steal must use actual 10,000 damage.");
                Assert.AreEqual(5000, engine.HealMilli);
                Assert.GreaterOrEqual(engine.Statuses, 2);
                Assert.NotNull(GameObject.Find("Effet bleed"), "Applied bleed must have visible feedback.");
                Assert.IsNull(GameObject.Find("Effet bleed").GetComponentInChildren<Collider>(),
                    "Effect VFX must never create a hitbox.");
                for (var i = 0; i < 50; i++) engine.Tick();
                Assert.AreEqual(88000, hostile.HealthMilli,
                    "Bleed and poison must each tick once after 50 simulation ticks.");
                Assert.AreEqual(12000, engine.DamageMilli);
            }
            finally
            {
                engine.CancelAll();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShatterOnlyDestroysMarkedLabPropAndResetRestoresIt()
        {
            var packet = BeamPacket("environment");
            packet.plan.nodes[0].effects = new List<SpellEffect> { Effect("shatter", "environment", 100000) };
            var host = new GameObject("Controlled destruction proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var plain = Receiver(host.transform, "Unmarked environment", 4, "environment", new Vector3(0, 1, 3));
            var marked = Receiver(host.transform, "Marked lab crate", 5, "environment", new Vector3(3, 1, 3));
            marked.gameObject.AddComponent<LabBreakable>();
            var engine = Engine(packet, caster.transform, plain, marked);
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.IsTrue(plain.gameObject.activeSelf, "An unmarked object must remain intact.");
                Assert.AreEqual(0, engine.StructuresBroken);

                plain.transform.position = new Vector3(8, 1, 3);
                marked.transform.position = new Vector3(0, 1, 3);
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.IsFalse(marked.gameObject.activeSelf, "Only the authored lab crate may shatter.");
                Assert.AreEqual(1, engine.StructuresBroken);
                Assert.NotNull(GameObject.Find("Effet shatter"), "Destruction must have a visible impact cue.");
                marked.Restore();
                Assert.IsTrue(marked.gameObject.activeSelf);
                Assert.AreEqual(100000, marked.StructureMilli);
            }
            finally
            {
                engine.CancelAll();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShieldControlAndCleanseHaveActualGameplayConsequences()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var receiver = target.AddComponent<LabReceiver>();
            receiver.Initialize(30, "ally");
            try
            {
                receiver.ApplyStatus("barrier_health", 8000, 100, 1);
                receiver.ApplyStatus("root", 0, 50, 1);
                receiver.ApplyStatus("bleed", 1000, 100, 1);
                Assert.IsFalse(receiver.CanMove);
                Assert.AreEqual(0, receiver.ApplyDamage(5000));
                Assert.AreEqual(100000, receiver.HealthMilli);
                Assert.AreEqual(3000, receiver.ShieldMilli);
                receiver.ApplyStatus("cleanse", 0, 0, 2);
                Assert.IsTrue(receiver.CanMove);
                Assert.IsFalse(receiver.HasStatus("bleed"));
                Assert.AreEqual(3000, receiver.ShieldMilli, "Cleanse preserves positive shield.");
                receiver.ApplyStatus("dispel", 0, 0, 3);
                Assert.AreEqual(0, receiver.ShieldMilli);
                Assert.AreEqual(5000, receiver.ApplyDamage(5000));
            }
            finally { Object.Destroy(target); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageModifiersRegenerationFreezeHasteAndHealingReductionChangeNumbers()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var receiver = target.AddComponent<LabReceiver>();
            receiver.Initialize(31, "hostile");
            try
            {
                receiver.ApplyStatus("damage_reduction", 300, 100, 1);
                receiver.ApplyStatus("armor_break", 200, 100, 1);
                receiver.ApplyStatus("vulnerability", 200, 100, 1);
                receiver.ApplyStatus("weakness", 100, 100, 1);
                receiver.ApplyStatus("haste", 250, 100, 1);
                receiver.ApplyStatus("healing_reduction", 500, 100, 1);
                Assert.AreEqual(900, receiver.OutgoingDamagePerMille);
                Assert.AreEqual(1.25f, receiver.SpeedFactor, .001f);
                Assert.AreEqual(9720, receiver.ApplyDamage(10000, receiver.OutgoingDamagePerMille),
                    "Weakness 10%, vulnerability 20%, protection 30% reduced by armor break 20%. ");
                Assert.AreEqual(90280, receiver.HealthMilli);

                receiver.ApplyStatus("regen", 1000, 100, 1);
                receiver.ApplyStatus("freeze_damage", 2000, 100, 1);
                Assert.AreEqual(2160, receiver.TickStatus(51),
                    "Freeze must tick through active vulnerability and protection.");
                Assert.AreEqual(500, receiver.LastTickHealMilli,
                    "Healing reduction must halve the regeneration tick.");
                Assert.AreEqual(88620, receiver.HealthMilli);
                Assert.AreEqual(2000, receiver.TickStatus(101));
                Assert.AreEqual(1000, receiver.LastTickHealMilli,
                    "The healing reduction expires before the final regeneration tick.");
                Assert.IsFalse(receiver.HasStatus("regen"));
                Assert.IsFalse(receiver.HasStatus("freeze_damage"));
            }
            finally { Object.Destroy(target); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExecuteOnlyAppliesBelowThresholdAndStunBlocksCasterUntilExpiry()
        {
            var packet = BeamPacket("hostile");
            packet.plan.nodes[0].effects = new List<SpellEffect> { Effect("execute", "hostile", 10000) };
            var host = new GameObject("Execute and stun proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var self = Receiver(caster.transform, "Self", 1, "ally", new Vector3(0, 1, 0));
            var hostile = Receiver(host.transform, "Hostile", 2, "hostile", new Vector3(0, 1, 3));
            hostile.ApplyDamage(74000);
            var engine = Engine(packet, caster.transform, self, hostile);
            try
            {
                Physics.SyncTransforms();
                self.ApplyStatus("stun", 0, 10, 0);
                Assert.IsFalse(self.CanMove);
                Assert.IsFalse(self.CanCast);
                Assert.IsFalse(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                self.ApplyStatus("cleanse", 0, 0, 0);
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(26000, hostile.HealthMilli, "Execute must not fire at 26% health.");
                Assert.AreEqual(0, engine.DamageMilli);

                hostile.ApplyDamage(2000);
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(14000, hostile.HealthMilli, "Execute must hit below 25% health.");
                Assert.AreEqual(10000, engine.DamageMilli);
            }
            finally
            {
                engine.CancelAll();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifeStealUsesDamageAfterShieldAbsorption()
        {
            var packet = BeamPacket("hostile");
            packet.plan.nodes[0].effects = new List<SpellEffect>
            {
                Effect("life_steal", "hostile", 500), // Deliberately before damage in data.
                Effect("damage", "hostile", 10000)
            };
            var host = new GameObject("Post-shield life steal proof");
            var caster = new GameObject("Caster");
            caster.transform.SetParent(host.transform);
            caster.transform.position = new Vector3(0, 1, 0);
            var self = Receiver(caster.transform, "Self", 1, "ally", new Vector3(0, 1, 0));
            self.ApplyDamage(10000);
            var hostile = Receiver(host.transform, "Shielded hostile", 2, "hostile", new Vector3(0, 1, 3));
            hostile.ApplyStatus("barrier_health", 8000, 100, 0);
            var engine = Engine(packet, caster.transform, self, hostile);
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(engine.TryCast(caster.transform.position, new Vector3(0, 1, 20), Vector3.forward));
                engine.Tick();
                Assert.AreEqual(0, hostile.ShieldMilli);
                Assert.AreEqual(98000, hostile.HealthMilli);
                Assert.AreEqual(91000, self.HealthMilli,
                    "Life steal must return half of actual 2,000 HP damage, not half of raw 10,000.");
                Assert.AreEqual(2000, engine.DamageMilli);
                Assert.AreEqual(1000, engine.HealMilli);
            }
            finally
            {
                engine.CancelAll();
                Object.Destroy(host);
            }
            yield return null;
        }

        private static CompiledSpell BeamPacket(string filter)
        {
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var packet = JsonConvert.DeserializeObject<CompiledSpell>(
                File.ReadAllText(Path.Combine(examples, "03_paquet_illustratif.json")));
            packet.plan = JsonConvert.DeserializeObject<SpellPlan>(
                File.ReadAllText(Path.Combine(examples, "fixture_beam.json")));
            packet.plan.nodes[0].options.chain_filter = filter;
            packet.plan.nodes[0].options.chain_hops = 0;
            packet.plan.nodes[0].options.lifetime_ticks = 1;
            return packet;
        }

        private static SpellEffect Effect(string kind, string filter, int amount, int duration = 0) =>
            new SpellEffect
            {
                id = "effect-" + kind, clause_ids = new List<string> { "c0" }, @event = "hit",
                kind = kind, target_filter = filter, amount = amount,
                duration_ticks = duration, direction = "none"
            };

        private static RuntimeEngine Engine(CompiledSpell packet, Transform caster, params LabReceiver[] targets)
        {
            var geometry = new GeometryAsset { geometry_id = "ring.path.0", kind = "path", points = StraightPath };
            return new RuntimeEngine(packet,
                new Dictionary<string, GeometryAsset> { [geometry.geometry_id] = geometry },
                new Dictionary<string, Texture2D>(), caster, new List<LabReceiver>(targets));
        }

        private static LabReceiver Receiver(Transform parent, string name, int id, string team, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = new Vector3(.5f, 1f, .5f);
            var receiver = go.AddComponent<LabReceiver>();
            receiver.Initialize(id, team);
            return receiver;
        }
    }
}

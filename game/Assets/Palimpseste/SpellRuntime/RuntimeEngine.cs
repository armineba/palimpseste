using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    internal sealed class CarrierState
    {
        public int id, castId, born, nextTick, bouncesLeft, piercesLeft, triggersLeft, blocksLeft;
        public SpellNode node;
        public Vector3 position, direction, origin;
        public Quaternion rotation;
        public float travelled;
        public GameObject visual;
        public List<Vector3> path;
        public int pathSegment;
        public float pathAlong;
        public readonly HashSet<int> visited = new HashSet<int>();
        public readonly HashSet<int> inside = new HashSet<int>();
        public bool expired;
    }

    internal sealed class ScheduledCarrier
    {
        public int dueTick, castId;
        public SpellNode node;
        public Vector3 position, direction;
    }

    // The packet contains data only. This fixed engine is the only place that applies mechanics.
    public sealed class RuntimeEngine
    {
        private readonly CompiledSpell spell;
        private readonly GeometryRuntime geometry;
        private readonly Transform caster;
        private readonly List<LabReceiver> targets;
        private readonly List<CarrierState> active = new List<CarrierState>();
        private readonly List<ScheduledCarrier> scheduled = new List<ScheduledCarrier>();
        private readonly Dictionary<string, int> activationCounts = new Dictionary<string, int>();
        private int nextInstance, nextCast;
        private readonly AudioClip birthClip, impactClip, expireClip;
        public int TickCount { get; private set; }
        public int Hits { get; private set; }
        public long DamageMilli { get; private set; }
        public long HealMilli { get; private set; }
        public int Impulses { get; private set; }
        public int Statuses { get; private set; }
        public int ActiveCount => active.Count;

        public RuntimeEngine(CompiledSpell spell, Dictionary<string, GeometryAsset> assets, Dictionary<string, Texture2D> masks, Transform caster, List<LabReceiver> targets)
        {
            this.spell = spell; this.geometry = new GeometryRuntime(assets, masks); this.caster = caster; this.targets = targets;
            birthClip = Tone("palimpseste-birth", 440, .18f);
            impactClip = Tone("palimpseste-impact", 177, .13f);
            expireClip = Tone("palimpseste-expire", 320, .23f);
        }

        public bool TryCast(Vector3 position, Vector3 aim, Vector3 direction)
        {
            if (spell?.plan?.nodes == null || active.Count >= spell.resource_bounds.max_instances) return false;
            var roots = 0;
            var castId = ++nextCast;
            foreach (var node in spell.plan.nodes)
            {
                if (node.activation?.parent_id != null) continue;
                roots++;
                Schedule(node, castId, node.anchor == "aim_point" ? aim : position, direction, TickCount);
            }
            return roots > 0;
        }

        public void Tick()
        {
            TickCount++;
            for (var i = scheduled.Count - 1; i >= 0; i--)
            {
                var pending = scheduled[i];
                if (pending.dueTick > TickCount) continue;
                scheduled.RemoveAt(i);
                Spawn(pending);
            }
            // Stable instance order controls simultaneous reactions and child scheduling.
            active.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var state in active)
            {
                if (state.expired) continue;
                var age = TickCount - state.born;
                switch (state.node.carrier)
                {
                    case "projectile": StepProjectile(state); break;
                    case "beam": StepBeam(state, age); break;
                    case "field": StepField(state, age); break;
                    case "pulse": StepPulse(state, age); break;
                    case "barrier": StepBarrier(state, age); break;
                    case "trap": StepTrap(state, age); break;
                }
                if (age >= Option(state.node.options.lifetime_ticks, 1) || state.expired) Expire(state);
            }
            for (var i = active.Count - 1; i >= 0; i--)
                if (active[i].expired)
                {
                    if (active[i].visual != null) UnityEngine.Object.Destroy(active[i].visual);
                    active.RemoveAt(i);
                }
            foreach (var target in targets) DamageMilli += target.TickStatus(TickCount);
        }

        public void CancelAll()
        {
            foreach (var state in active) if (state.visual != null) UnityEngine.Object.Destroy(state.visual);
            active.Clear(); scheduled.Clear(); activationCounts.Clear();
            Hits = Impulses = Statuses = 0; DamageMilli = HealMilli = 0;
        }

        private static int Option(int? value, int fallback) => value ?? fallback;

        private void Schedule(SpellNode node, int castId, Vector3 position, Vector3 direction, int parentTick)
        {
            var key = castId + ":" + node.node_id;
            activationCounts.TryGetValue(key, out var count);
            if (count >= node.activation.max_activations) return;
            activationCounts[key] = count + 1;
            scheduled.Add(new ScheduledCarrier { node = node, castId = castId, position = position, direction = direction.normalized, dueTick = parentTick + 1 + node.activation.delay_ticks });
        }

        private void Spawn(ScheduledCarrier pending)
        {
            var node = pending.node;
            for (var copy = 0; copy < node.activation.copies; copy++)
            {
                if (active.Count >= spell.resource_bounds.max_instances) break;
                var angle = node.activation.copies == 1 ? 0f : -node.activation.spread_mdeg / 2000f + copy * node.activation.spread_mdeg / (1000f * (node.activation.copies - 1));
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * pending.direction;
                if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
                var state = new CarrierState
                {
                    id = ++nextInstance, castId = pending.castId, born = TickCount,
                    node = node, position = pending.position, origin = pending.position,
                    direction = direction.normalized,
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0, node.rotation_mdeg / 1000f, 0),
                    bouncesLeft = Option(node.options.bounces, 0), piercesLeft = Option(node.options.pierces, 0),
                    triggersLeft = Option(node.options.trigger_limit, 0), blocksLeft = Option(node.options.block_limit, 0),
                    nextTick = TickCount + Option(node.options.tick_interval, 1)
                };
                state.path = geometry.Path(node);
                state.visual = CarrierVisual.Create(state, geometry.Mask(node));
                active.Add(state);
                Play(state.position, birthClip, .17f);
                Emit(state, "spawn", null, state.position, Vector3.up);
                if (node.carrier == "beam") EvaluateBeam(state);
            }
        }

        private void Emit(CarrierState state, string kind, LabReceiver receiver, Vector3 position, Vector3 normal)
        {
            if (state.expired && kind != "expire") return;
            if (receiver != null) Hits++;
            if (kind == "hit" || kind == "block" || kind == "trigger") Play(position, impactClip, .22f);
            if (state.node.effects != null)
                foreach (var effect in state.node.effects)
                    if (effect.@event == kind && receiver != null && Matches(effect.target_filter, receiver)) Apply(effect, receiver, position, state.direction);
            foreach (var child in spell.plan.nodes)
                if (child.activation.parent_id == state.node.node_id && child.activation.@event == kind)
                    Schedule(child, state.castId, position, state.direction, TickCount);
        }

        private bool Matches(string filter, LabReceiver receiver)
        {
            if (receiver == null) return false;
            switch (filter)
            {
                case "hostile": return receiver.Team == "hostile";
                case "ally": return receiver.Team == "ally" && !receiver.transform.IsChildOf(caster);
                case "self": return receiver.transform.IsChildOf(caster) || receiver.transform == caster;
                case "all_actors": return receiver.Team != "environment";
                case "environment": return receiver.Team == "environment";
                default: return false;
            }
        }

        private void Apply(SpellEffect effect, LabReceiver receiver, Vector3 source, Vector3 forward)
        {
            switch (effect.kind)
            {
                case "damage": DamageMilli += receiver.ApplyDamage(effect.amount); break;
                case "heal": HealMilli += receiver.ApplyHeal(effect.amount); break;
                case "impulse":
                    var direction = effect.direction == "up" ? Vector3.up : effect.direction == "forward" ? forward : receiver.transform.position - source;
                    if (direction.sqrMagnitude < .0001f) direction = forward;
                    if (effect.direction == "inward") direction = -direction;
                    receiver.Impulse(direction, effect.amount); Impulses++; break;
                case "burn": case "wet": case "slow":
                    var reaction = receiver.ApplyStatus(effect.kind, effect.amount, effect.duration_ticks, TickCount);
                    Statuses++;
                    if (reaction != null) Play(receiver.transform.position, expireClip, .35f);
                    break;
            }
        }

        private void Expire(CarrierState state)
        {
            if (state.expired) return;
            Emit(state, "expire", null, state.position, Vector3.up);
            state.expired = true;
            Play(state.position, expireClip, .1f);
            if (state.node.carrier == "barrier" && state.visual != null)
                foreach (var collider in state.visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private LabReceiver Receiver(Collider collider) => collider == null ? null : collider.GetComponentInParent<LabReceiver>();
        private BarrierReceiver Barrier(Collider collider) => collider == null ? null : collider.GetComponentInParent<BarrierReceiver>();

        private static RaycastHit[] Sorted(RaycastHit[] hits)
        {
            Array.Sort(hits, (a, b) =>
            {
                var d = a.distance.CompareTo(b.distance);
                if (d != 0) return d;
                var aid = a.collider.GetComponentInParent<LabReceiver>()?.StableId ?? int.MaxValue;
                var bid = b.collider.GetComponentInParent<LabReceiver>()?.StableId ?? int.MaxValue;
                return aid.CompareTo(bid);
            });
            return hits;
        }

        private void StepProjectile(CarrierState state)
        {
            var opts = state.node.options;
            var speed = Option(opts.speed_cm_s, 100) / 100f;
            var length = speed * .02f;
            var old = state.position;
            if (opts.motion == "homing")
            {
                LabReceiver chosen = null;
                var best = float.MaxValue;
                foreach (var target in targets)
                {
                    if (!Matches(opts.contact_filter, target)) continue;
                    var d = Vector3.Distance(target.transform.position, old);
                    if (d < best || (Mathf.Approximately(d, best) && (chosen == null || target.StableId < chosen.StableId))) { chosen = target; best = d; }
                }
                if (chosen != null)
                {
                    var desired = (chosen.transform.position - old).normalized;
                    state.direction = Vector3.RotateTowards(state.direction, desired, Option(opts.turn_mdeg_s, 0) * Mathf.Deg2Rad * .02f / 1000f, 0).normalized;
                }
            }
            else if (opts.motion == "curve" && state.path != null && state.path.Count > 1)
            {
                var remaining = length;
                while (remaining > 0 && state.pathSegment < state.path.Count - 1)
                {
                    var a = state.path[state.pathSegment]; var b = state.path[state.pathSegment + 1];
                    var segment = Vector3.Distance(a, b);
                    if (segment < .0001f) { state.pathSegment++; state.pathAlong = 0; continue; }
                    var advance = Mathf.Min(remaining, segment - state.pathAlong);
                    state.pathAlong += advance; remaining -= advance;
                    state.direction = state.rotation * ((b - a).normalized);
                    state.position = state.origin + state.rotation * (a - state.path[0] + (b - a).normalized * state.pathAlong);
                    if (state.pathAlong >= segment - .0001f) { state.pathSegment++; state.pathAlong = 0; }
                }
                if (remaining > 0) state.position += state.direction * remaining;
            }
            if (opts.motion != "curve" || state.path == null || state.path.Count < 2) state.position += state.direction * length;
            state.travelled += Vector3.Distance(old, state.position);
            var movement = state.position - old;
            var radius = Option(opts.radius_cm, 5) / 100f;
            if (movement.sqrMagnitude > .0000001f)
            {
                var hits = Sorted(Physics.SphereCastAll(old, radius, movement.normalized, movement.magnitude, ~0, QueryTriggerInteraction.Ignore));
                foreach (var hit in hits)
                {
                    var receiver = Receiver(hit.collider);
                    var barrier = Barrier(hit.collider);
                    if (receiver != null && receiver.transform.IsChildOf(caster) && state.travelled < 1f) continue;
                    if (receiver != null && !state.visited.Add(receiver.StableId)) continue;
                    if (barrier != null)
                    {
                        if (barrier.InstanceId == state.id) continue;
                        foreach (var effect in state.node.effects)
                            if (effect.@event == "hit" && effect.kind == "damage" && effect.target_filter == "environment")
                                barrier.Damage(effect.amount);
                        barrier.BlocksLeft--;
                        var owner = active.Find(candidate => candidate.id == barrier.InstanceId);
                        if (owner != null) Emit(owner, "block", null, hit.point, hit.normal);
                        state.position = hit.point;
                        if (barrier.BlocksLeft <= 0) barrier.StructureMilli = 0;
                        Expire(state); break;
                    }
                    if (receiver != null && Matches(opts.contact_filter, receiver))
                    {
                        state.position = hit.point;
                        Emit(state, "hit", receiver, hit.point, hit.normal);
                        if (state.piercesLeft-- > 0) continue;
                        Expire(state); break;
                    }
                    // A filtered actor is still a physical obstacle. A wall may bounce.
                    if (state.bouncesLeft-- > 0 && receiver == null)
                    {
                        state.direction = Vector3.Reflect(state.direction, hit.normal).normalized;
                        state.position = hit.point + hit.normal * .02f;
                        break;
                    }
                    state.position = hit.point;
                    Expire(state); break;
                }
            }
            if (state.visual != null) { state.visual.transform.position = state.position; state.visual.transform.rotation = Quaternion.LookRotation(state.direction); }
            if (state.travelled * 100f >= Option(opts.range_cm, 1)) Expire(state);
        }

        private void StepBeam(CarrierState state, int age)
        {
            if (age == 0 && Option(state.node.options.lifetime_ticks, 1) == 1) return;
            if (TickCount < state.nextTick) return;
            state.nextTick = TickCount + Option(state.node.options.tick_interval, 5);
            EvaluateBeam(state);
            Emit(state, "tick", null, state.position, Vector3.up);
        }

        private void EvaluateBeam(CarrierState state)
        {
            var opts = state.node.options;
            var from = state.position;
            var direction = state.direction;
            var visited = new HashSet<int>();
            var segments = new List<Vector3> { from };
            for (var hop = 0; hop <= Option(opts.chain_hops, 0); hop++)
            {
                var reach = (hop == 0 ? Option(opts.range_cm, 100) : Option(opts.chain_radius_cm, 0)) / 100f;
                if (reach <= 0) break;
                RaycastHit chosenHit = default;
                LabReceiver chosen = null;
                if (hop == 0)
                {
                    var hits = Sorted(Physics.SphereCastAll(from, Option(opts.width_cm, 1) / 200f, direction, reach, ~0, QueryTriggerInteraction.Ignore));
                    foreach (var hit in hits)
                    {
                        var candidate = Receiver(hit.collider);
                        if (candidate != null && candidate.transform.IsChildOf(caster)) continue;
                        chosenHit = hit; chosen = candidate; break;
                    }
                }
                else
                {
                    var best = float.MaxValue;
                    foreach (var target in targets)
                    {
                        if (visited.Contains(target.StableId) || !Matches(opts.chain_filter, target)) continue;
                        var delta = target.transform.position - from;
                        var d = delta.magnitude;
                        if (d > reach || d >= best) continue;
                        var occluded = Physics.Raycast(from + Vector3.up * .08f, delta.normalized, out var rayHit, d - .05f) && Receiver(rayHit.collider) != target;
                        if (occluded) continue;
                        best = d; chosen = target;
                    }
                }
                if (chosen == null || !Matches(opts.chain_filter, chosen))
                {
                    segments.Add(hop == 0 && chosenHit.collider != null ? chosenHit.point : from + direction * reach);
                    break;
                }
                visited.Add(chosen.StableId);
                var position = chosen.transform.position;
                segments.Add(position);
                Emit(state, "hit", chosen, position, direction);
                direction = (position - from).normalized;
                from = position + direction * .08f;
            }
            CarrierVisual.BeamSegments(state, segments);
        }

        private void StepField(CarrierState state, int age)
        {
            var height = Option(state.node.options.height_cm, 100) / 100f;
            var radius = state.node.scale_cm / 180f;
            var candidates = new List<LabReceiver>();
            foreach (var target in targets)
            {
                if (target.HealthMilli <= 0 || Mathf.Abs(target.transform.position.y - state.position.y) > height + 1f) continue;
                if (Vector3.Distance(new Vector3(target.transform.position.x, 0, target.transform.position.z), new Vector3(state.position.x, 0, state.position.z)) > radius + 1f) continue;
                var collider = target.GetComponent<Collider>();
                var extent = collider == null ? 0 : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
                if (geometry.Footprint(state.node, state.position, state.rotation, target.transform.position, extent)) candidates.Add(target);
            }
            candidates.Sort((a, b) => a.StableId.CompareTo(b.StableId));
            foreach (var target in candidates)
            {
                if (state.visited.Count >= 32 && !state.visited.Contains(target.StableId)) continue;
                if (state.visited.Add(target.StableId)) Emit(state, "enter", target, target.transform.position, Vector3.up);
                if (TickCount >= state.nextTick) Emit(state, "tick", target, target.transform.position, Vector3.up);
            }
            if (TickCount >= state.nextTick) state.nextTick += Option(state.node.options.tick_interval, 5);
        }

        private void StepPulse(CarrierState state, int age)
        {
            var opts = state.node.options;
            var lifetime = Option(opts.lifetime_ticks, 1);
            var outer = Option(opts.radius_cm, 100) / 100f * Mathf.Clamp01((age + 1f) / lifetime);
            var previous = Option(opts.radius_cm, 100) / 100f * Mathf.Clamp01(age / (float)lifetime);
            var inner = Mathf.Max(0, previous - Option(opts.front_width_cm, 1) / 100f);
            foreach (var target in targets)
            {
                if (state.visited.Contains(target.StableId) || target.HealthMilli <= 0) continue;
                var planar = target.transform.position - state.position; planar.y = 0;
                var collider = target.GetComponent<Collider>();
                var radius = collider == null ? 0 : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
                if (planar.magnitude - radius > outer || planar.magnitude + radius < inner) continue;
                if (!geometry.Footprint(state.node, state.position, state.rotation, target.transform.position, radius)) continue;
                state.visited.Add(target.StableId);
                Emit(state, "hit", target, target.transform.position, planar.normalized);
            }
            CarrierVisual.PulseRadius(state, outer);
        }

        private void StepBarrier(CarrierState state, int age)
        {
            if (state.visual == null) return;
            var receiver = state.visual.GetComponent<BarrierReceiver>();
            if (receiver != null && (receiver.StructureMilli <= 0 || receiver.BlocksLeft <= 0)) Expire(state);
        }

        private void StepTrap(CarrierState state, int age)
        {
            var opts = state.node.options;
            if (age < Option(opts.arm_ticks, 0) || TickCount < state.nextTick) return;
            CarrierVisual.TrapArmed(state);
            foreach (var target in targets)
            {
                if (!Matches(opts.trigger_filter, target) || target.HealthMilli <= 0) continue;
                var inside = geometry.Footprint(state.node, state.position, state.rotation, target.transform.position, .35f);
                if (!inside) { state.inside.Remove(target.StableId); continue; }
                if (!state.inside.Add(target.StableId)) continue;
                Emit(state, "trigger", target, target.transform.position, Vector3.up);
                state.triggersLeft--;
                state.nextTick = TickCount + Option(opts.rearm_ticks, 1);
                if (state.triggersLeft <= 0) Expire(state);
                break;
            }
        }

        private static AudioClip Tone(string name, float frequency, float seconds)
        {
            const int rate = 22050;
            var length = Mathf.RoundToInt(rate * seconds);
            var data = new float[length];
            for (var i = 0; i < length; i++)
            {
                var t = i / (float)rate;
                var envelope = Mathf.Sin(Mathf.PI * i / length);
                data[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * .22f;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Play(Vector3 position, AudioClip clip, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}

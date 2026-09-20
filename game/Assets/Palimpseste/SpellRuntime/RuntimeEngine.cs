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
        public Vector3 position, direction, origin, velocity, aim, surfaceNormal, attachedLocalPosition, attachedLocalForward;
        public Quaternion rotation, trajectoryRotation;
        public Quaternion attachedLocalRotation;
        public float travelled;
        public GameObject visual;
        public List<Vector3> path;
        public int pathSegment;
        public float pathAlong;
        public readonly HashSet<int> visited = new HashSet<int>();
        public readonly HashSet<int> inside = new HashSet<int>();
        public bool expired, endedByContact;
    }

    internal sealed class ScheduledCarrier
    {
        public int dueTick, castId;
        public SpellNode node;
        public Vector3 position, direction, normal, aim;
    }

    // The packet contains data only. This fixed engine is the only place that applies mechanics.
    public sealed class RuntimeEngine
    {
        private readonly CompiledSpell spell;
        private readonly GeometryRuntime geometry;
        private readonly Transform caster;
        private readonly LabReceiver casterReceiver;
        private readonly List<LabReceiver> targets;
        private readonly List<CarrierState> active = new List<CarrierState>();
        private readonly List<GameObject> retiredVisuals = new List<GameObject>();
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
        public int StructuresBroken { get; private set; }
        public int ActiveCount => active.Count;

        public RuntimeEngine(CompiledSpell spell, Dictionary<string, GeometryAsset> assets, Dictionary<string, Texture2D> masks, Transform caster, List<LabReceiver> targets)
        {
            this.spell = spell; this.geometry = new GeometryRuntime(assets, masks); this.caster = caster; this.targets = targets;
            casterReceiver = caster == null ? null : caster.GetComponentInChildren<LabReceiver>();
            birthClip = Tone("palimpseste-birth", 440, .18f);
            impactClip = Tone("palimpseste-impact", 177, .13f);
            expireClip = Tone("palimpseste-expire", 320, .23f);
        }

        public bool TryCast(Vector3 position, Vector3 aim, Vector3 direction)
        {
            if (spell?.plan?.nodes == null || active.Count >= spell.resource_bounds.max_instances ||
                (casterReceiver != null && !casterReceiver.CanCast)) return false;
            var roots = 0;
            var castId = ++nextCast;
            foreach (var node in spell.plan.nodes)
            {
                if (node.activation?.parent_id != null) continue;
                if (SpellBehaviorMotion.Enabled(node))
                {
                    if (!ResolvePlacement(node,position,aim,direction,position,Vector3.up,
                        out var placed,out var forward,out var normal)) continue;
                    Schedule(node,castId,placed,forward,TickCount,normal,aim);
                    roots++;
                    continue;
                }
                roots++;
                Schedule(node, castId, node.anchor == "aim_point" ? aim : position, direction, TickCount);
            }
            return roots > 0;
        }

        public void Tick()
        {
            TickCount++;
            foreach (var target in targets) if (target != null) target.AdvanceTick(TickCount);
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
                FollowAttachment(state);
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
                    var visual = active[i].visual;
                    if (visual != null)
                    {
                        var constructed = visual.GetComponent<ImageConstructedSpellVisual>();
                        if (constructed != null && constructed.HasLifecycle)
                        {
                            // Collisions end on this tick. Only bounded artwork
                            // survives, using the actual reason for retirement.
                            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                            var finishEntrance = active[i].node.carrier == "beam" && Option(active[i].node.options.lifetime_ticks,1) == 1;
                            var delay = finishEntrance ? constructed.RemainingEntrance : 0;
                            var duration = finishEntrance ? constructed.FinishEntranceThenRetire(active[i].endedByContact)
                                : constructed.RetireForCause(active[i].endedByContact);
                            visual.GetComponent<SpellVfxComposition>()?.DissolveWake(duration - delay,delay);
                            UnityEngine.Object.Destroy(visual,duration + .03f);
                            retiredVisuals.Add(visual);
                        }
                        else if (active[i].node.carrier == "beam" && Option(active[i].node.options.lifetime_ticks, 1) == 1)
                        {
                            CarrierVisual.KeepOneTickBeamVisible(visual);
                            retiredVisuals.Add(visual);
                        }
                        else if (active[i].node.carrier != "barrier" &&
                            visual.TryGetComponent<ImageConstructedSpellVisual>(out var construction))
                        {
                            construction.Retire(.62f);
                            visual.GetComponent<SpellVfxComposition>()?.DissolveWake();
                            UnityEngine.Object.Destroy(visual, .65f);
                            retiredVisuals.Add(visual);
                        }
                        else if (visual.TryGetComponent<SemanticSpellVisual>(out var semantic) &&
                            semantic.RetireWithDissolvingWake())
                            retiredVisuals.Add(visual);
                        else UnityEngine.Object.Destroy(visual);
                    }
                    active.RemoveAt(i);
                }
            retiredVisuals.RemoveAll(visual => visual == null);
            foreach (var target in targets)
            {
                if (target == null) continue;
                DamageMilli += target.TickStatus(TickCount);
                HealMilli += target.LastTickHealMilli;
            }
        }

        public void CancelAll()
        {
            foreach (var state in active) if (state.visual != null) UnityEngine.Object.Destroy(state.visual);
            foreach (var visual in retiredVisuals) if (visual != null) UnityEngine.Object.Destroy(visual);
            retiredVisuals.Clear();
            active.Clear(); scheduled.Clear(); activationCounts.Clear();
            Hits = Impulses = Statuses = StructuresBroken = 0; DamageMilli = HealMilli = 0;
        }

        private static int Option(int? value, int fallback) => value ?? fallback;

        private void Schedule(SpellNode node, int castId, Vector3 position, Vector3 direction, int parentTick,
            Vector3? normal = null, Vector3? aim = null)
        {
            var key = castId + ":" + node.node_id;
            activationCounts.TryGetValue(key, out var count);
            if (count >= node.activation.max_activations) return;
            activationCounts[key] = count + 1;
            scheduled.Add(new ScheduledCarrier { node = node, castId = castId, position = position, direction = direction.normalized,
                normal = normal ?? Vector3.up, aim = aim ?? position, dueTick = parentTick + 1 + node.activation.delay_ticks });
        }

        private bool ResolvePlacement(SpellNode node, Vector3 source, Vector3 aim, Vector3 forward,
            Vector3 eventPosition, Vector3 eventNormal, out Vector3 position, out Vector3 direction, out Vector3 normal)
        {
            var intent = node.behavior; var parameters = node.physics;
            direction = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
            normal = Vector3.up; position = source;
            var boundedAim = source + Vector3.ClampMagnitude(aim - source,Mathf.Clamp(parameters.cast_range_cm,0,3000) / 100f);
            switch (intent.origin)
            {
                case "aim_point": case "aim_ground": position = boundedAim; break;
                case "parent_event": case "parent_ground": position = eventPosition; normal = eventNormal; break;
                case "muzzle": position = source + direction * .8f; break;
            }
            var ground = intent.origin.EndsWith("_ground",StringComparison.Ordinal) || intent.origin == "parent_ground";
            if (ground && !GroundPoint(position,out position,out normal)) return false;
            var orientation = PlacementRotation(intent.orientation,direction,normal);
            if (parameters.offset_cm != null && parameters.offset_cm.Length == 3)
                position += orientation * new Vector3(Mathf.Clamp(parameters.offset_cm[0],-500,500),
                    Mathf.Clamp(parameters.offset_cm[1],-500,500),Mathf.Clamp(parameters.offset_cm[2],-500,500)) / 100f;
            if (ground && !GroundPoint(position,out position,out normal)) return false;
            return true;
        }

        private static Quaternion PlacementRotation(string mode, Vector3 forward, Vector3 normal)
        {
            if (mode == "world_up") return Quaternion.identity;
            if (mode == "surface_normal")
            {
                var tangent = Vector3.ProjectOnPlane(forward,normal);
                if (tangent.sqrMagnitude < .0001f) tangent = Vector3.ProjectOnPlane(Vector3.forward,normal);
                if (tangent.sqrMagnitude < .0001f) tangent = Vector3.Cross(normal,Vector3.right);
                return Quaternion.LookRotation(tangent.normalized,normal.normalized);
            }
            return Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward,Vector3.up);
        }

        private bool GroundPoint(Vector3 requested, out Vector3 point, out Vector3 normal)
        {
            point = requested; normal = Vector3.up;
            var hits = Sorted(Physics.RaycastAll(requested + Vector3.up * 35,Vector3.down,75,~0,QueryTriggerInteraction.Ignore));
            foreach (var hit in hits)
            {
                if (hit.collider == null || Receiver(hit.collider) != null || Barrier(hit.collider) != null ||
                    caster != null && hit.collider.transform.IsChildOf(caster) || hit.normal.y < .25f) continue;
                point = hit.point + hit.normal * .035f; normal = hit.normal;
                return true;
            }
            return false; // no silent fallback to the caster or a fictitious floor
        }

        private void FollowAttachment(CarrierState state)
        {
            if (!SpellBehaviorMotion.Enabled(state.node) || state.node.behavior.attachment != "caster") return;
            if (caster == null) { Expire(state); return; }
            var position = caster.TransformPoint(state.attachedLocalPosition);
            if (state.node.behavior.origin.EndsWith("_ground",StringComparison.Ordinal) &&
                !GroundPoint(position,out position,out state.surfaceNormal)) { Expire(state); return; }
            var delta = position - state.position;
            state.position = position; state.origin += delta;
            state.rotation = caster.rotation * state.attachedLocalRotation;
            state.direction = caster.TransformDirection(state.attachedLocalForward).normalized;
            state.trajectoryRotation = Quaternion.LookRotation(state.direction,Vector3.up) * Quaternion.Euler(0,state.node.rotation_mdeg / 1000f,0);
            if (state.visual != null) state.visual.transform.SetPositionAndRotation(state.position,state.rotation);
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
                    node = node, position = pending.position, origin = pending.position, aim = pending.aim,
                    surfaceNormal = pending.normal,
                    direction = direction.normalized,
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0, node.rotation_mdeg / 1000f, 0),
                    bouncesLeft = Option(node.options.bounces, 0), piercesLeft = Option(node.options.pierces, 0),
                    triggersLeft = Option(node.options.trigger_limit, 0), blocksLeft = Option(node.options.block_limit, 0),
                    nextTick = TickCount + Option(node.options.tick_interval, 1)
                };
                state.trajectoryRotation = state.rotation;
                if (SpellBehaviorMotion.Enabled(node))
                {
                    state.rotation = PlacementRotation(node.behavior.orientation,state.direction,pending.normal) * Quaternion.Euler(0,node.rotation_mdeg / 1000f,0);
                    if (caster != null)
                    {
                        state.attachedLocalPosition = caster.InverseTransformPoint(state.position);
                        state.attachedLocalRotation = Quaternion.Inverse(caster.rotation) * state.rotation;
                        state.attachedLocalForward = caster.InverseTransformDirection(state.direction);
                    }
                    if (node.options.motion == "ballistic")
                    {
                        var pitch = Mathf.Clamp(node.physics.launch_pitch_mdeg,-80000,80000) / 1000f * Mathf.Deg2Rad;
                        var horizontal = Vector3.ProjectOnPlane(state.direction,Vector3.up).normalized;
                        if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.forward;
                        state.velocity = (horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * Option(node.options.speed_cm_s,100) / 100f;
                        state.direction = state.velocity.normalized;
                    }
                }
                state.path = geometry.Path(node);
                state.visual = CarrierVisual.Create(state, geometry.Mask(node), geometry.SignatureMask(node));
                active.Add(state);
                Play(state.position, birthClip, .17f);
                Emit(state, "spawn", null, state.position, Vector3.up);
                if (node.carrier == "beam") EvaluateBeam(state);
            }
        }

        private void Emit(CarrierState state, string kind, LabReceiver receiver, Vector3 position, Vector3 normal,
            Vector3? effectForward = null)
        {
            if (state.expired && kind != "expire") return;
            if (receiver != null) Hits++;
            if (kind == "hit" || kind == "block" || kind == "trigger") Play(position, impactClip, .22f);
            if (kind == "hit" && receiver != null)
            {
                var contactVisual = CarrierVisual.ProjectileHit(state,position);
                if (contactVisual != null) retiredVisuals.Add(contactVisual);
            }
            if (state.node.effects != null)
            {
                var damageFromEvent = 0;
                foreach (var effect in state.node.effects)
                    if (effect.@event == kind && receiver != null && Matches(effect.target_filter, receiver) &&
                        effect.kind != "life_steal")
                        damageFromEvent += Apply(effect, receiver, position, effectForward ?? state.direction);
                if (receiver != null && receiver.Team == "hostile" && damageFromEvent > 0 && casterReceiver != null)
                {
                    var fraction = 0;
                    foreach (var effect in state.node.effects)
                        if (effect.@event == kind && effect.kind == "life_steal" && Matches(effect.target_filter, receiver))
                            fraction += effect.amount;
                    if (fraction > 0)
                    {
                        var gained = casterReceiver.ApplyHeal((int)((long)damageFromEvent * Mathf.Min(fraction, 500) / 1000));
                        HealMilli += gained;
                        if (gained > 0) CarrierVisual.EffectCue("life_steal", casterReceiver.transform.position);
                    }
                }
            }
            foreach (var child in spell.plan.nodes)
                if (child.activation.parent_id == state.node.node_id && child.activation.@event == kind)
                {
                    if (SpellBehaviorMotion.Enabled(child))
                    {
                        var source = caster == null ? state.origin : caster.position;
                        if (ResolvePlacement(child,source,state.aim,state.direction,position,normal,out var placed,out var forward,out var surface))
                            Schedule(child,state.castId,placed,forward,TickCount,surface,state.aim);
                    }
                    else Schedule(child,state.castId,position,state.direction,TickCount);
                }
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

        private int Apply(SpellEffect effect, LabReceiver receiver, Vector3 source, Vector3 forward)
        {
            var appliedDamage = 0;
            switch (effect.kind)
            {
                case "damage":
                    appliedDamage = receiver.ApplyDamage(effect.amount, casterReceiver?.OutgoingDamagePerMille ?? 1000);
                    DamageMilli += appliedDamage; break;
                case "execute":
                    if (receiver.Team == "hostile" && receiver.HealthMilli > 0 &&
                        (long)receiver.HealthMilli * 4 <= receiver.MaxHealthMilli)
                    {
                        appliedDamage = receiver.ApplyDamage(effect.amount, casterReceiver?.OutgoingDamagePerMille ?? 1000);
                        DamageMilli += appliedDamage;
                    }
                    break;
                case "heal": HealMilli += receiver.ApplyHeal(effect.amount); break;
                case "shatter":
                    if (receiver.Team == "environment")
                    {
                        var before = receiver.StructureMilli;
                        receiver.Shatter(effect.amount);
                        if (before > 0 && receiver.StructureMilli <= 0) StructuresBroken++;
                    }
                    break;
                case "impulse":
                    var direction = effect.direction == "up" ? Vector3.up : effect.direction == "forward" ? forward : receiver.transform.position - source;
                    if (direction.sqrMagnitude < .0001f) direction = forward;
                    if (effect.direction == "inward") direction = -direction;
                    receiver.Impulse(direction, effect.amount); Impulses++; break;
                case "burn": case "wet": case "slow": case "bleed": case "poison":
                case "freeze_damage": case "regen": case "barrier_health":
                case "vulnerability": case "weakness": case "haste": case "armor_break":
                case "damage_reduction": case "healing_reduction": case "root": case "stun":
                case "cleanse": case "dispel":
                    var reaction = receiver.ApplyStatus(effect.kind, effect.amount, effect.duration_ticks, TickCount);
                    Statuses++;
                    if (reaction != null) Play(receiver.transform.position, expireClip, .35f);
                    break;
            }
            if (effect.kind != "life_steal") CarrierVisual.EffectCue(effect.kind, receiver.transform.position);
            return appliedDamage;
        }

        private void Expire(CarrierState state, bool contact = false)
        {
            if (state.expired) return;
            Emit(state, "expire", null, state.position, Vector3.up);
            state.expired = true;
            state.endedByContact = contact;
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
            var ballistic = SpellBehaviorMotion.Enabled(state.node) && opts.motion == "ballistic";
            if (ballistic)
            {
                var acceleration = Vector3.down * Mathf.Clamp(state.node.physics.gravity_cm_s2,0,4000) / 100f;
                state.position += state.velocity * .02f + acceleration * .0002f;
                state.velocity += acceleration * .02f;
                if (state.velocity.sqrMagnitude > .0001f) state.direction = state.velocity.normalized;
            }
            else if (opts.motion == "homing")
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
                    state.direction = state.trajectoryRotation * ((b - a).normalized);
                    state.position = state.origin + state.trajectoryRotation * (a - state.path[0] + (b - a).normalized * state.pathAlong);
                    if (state.pathAlong >= segment - .0001f) { state.pathSegment++; state.pathAlong = 0; }
                }
                if (remaining > 0) state.position += state.direction * remaining;
            }
            if (!ballistic && (opts.motion != "curve" || state.path == null || state.path.Count < 2)) state.position += state.direction * length;
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
                    // A cast begins inside its owner's collider. SphereCastAll also
                    // reports an initial overlap, so use the swept sphere's actual
                    // starting volume instead of a distance already advanced this tick.
                    // A later return from outside this volume still collides normally.
                    if (receiver != null && receiver.transform.IsChildOf(caster) &&
                        (hit.collider.ClosestPoint(old) - old).sqrMagnitude <= radius * radius + .000001f)
                        continue;
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
                        if (SpellBehaviorMotion.Enabled(state.node)) Emit(state,"hit",null,hit.point,hit.normal);
                        Expire(state,true); break;
                    }
                    if (receiver != null && Matches(opts.contact_filter, receiver))
                    {
                        state.position = hit.point;
                        Emit(state, "hit", receiver, hit.point, hit.normal);
                        if (state.piercesLeft-- > 0) continue;
                        Expire(state,true); break;
                    }
                    // A filtered actor is still a physical obstacle. A wall may bounce.
                    if (receiver == null && SpellBehaviorMotion.Enabled(state.node))
                        Emit(state,"hit",null,hit.point,hit.normal);
                    if (state.bouncesLeft-- > 0 && receiver == null)
                    {
                        state.direction = Vector3.Reflect(state.direction, hit.normal).normalized;
                        if (ballistic) state.velocity = Vector3.Reflect(state.velocity,hit.normal);
                        state.position = hit.point + hit.normal * .02f;
                        break;
                    }
                    state.position = hit.point;
                    Expire(state,true); break;
                }
            }
            if (state.visual != null)
            {
                state.visual.transform.position = state.position;
                state.visual.transform.rotation = SpellBehaviorMotion.Enabled(state.node)
                    ? PlacementRotation(state.node.behavior.orientation,state.direction,state.surfaceNormal)
                    : Quaternion.LookRotation(state.direction);
            }
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
            var trace = BeamTrace(state, Option(opts.range_cm, 100) / 100f);
            if (trace.Count < 2)
            {
                if (state.visual != null) state.visual.SetActive(false);
                return;
            }
            // The cast begins inside its own collider, so a swept ray cannot
            // represent an explicit self receiver. Apply that contact once
            // at the origin, then ignore the caster collider along the path.
            if (opts.chain_filter == "self" || opts.chain_filter == "all_actors")
                foreach (var target in targets)
                    if (target != null && target.transform.IsChildOf(caster) && Matches(opts.chain_filter, target) &&
                        visited.Add(target.StableId))
                        Emit(state, "hit", target, state.position, direction);
            LabReceiver chosen = null;
            var hitDirection = direction;
            for (var i = 1; i < trace.Count; i++)
            {
                var delta = trace[i] - from;
                if (delta.sqrMagnitude < .000001f) continue;
                var hits = Sorted(Physics.SphereCastAll(from, Option(opts.width_cm, 1) / 200f,
                    delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore));
                RaycastHit firstHit = default;
                foreach (var hit in hits)
                {
                    var candidate = Receiver(hit.collider);
                    if (candidate != null && candidate.transform.IsChildOf(caster)) continue;
                    firstHit = hit; chosen = candidate; break;
                }
                if (firstHit.collider != null)
                {
                    // SphereCast reports the collider surface, while the
                    // rendered line is the beam centerline. Stop its center
                    // at the same swept distance used by the physics query.
                    segments.Add(from + delta.normalized * firstHit.distance);
                    hitDirection = delta.normalized;
                    break;
                }
                segments.Add(trace[i]);
                from = trace[i];
            }
            if (chosen == null || !Matches(opts.chain_filter, chosen))
            {
                CarrierVisual.BeamSegments(state, segments);
                return;
            }
            visited.Add(chosen.StableId);
            var position = chosen.transform.position;
            Emit(state, "hit", chosen, position, hitDirection, hitDirection);
            direction = hitDirection;
            from = position + direction * .08f;
            for (var hop = 1; hop <= Option(opts.chain_hops, 0); hop++)
            {
                var reach = Option(opts.chain_radius_cm, 0) / 100f;
                if (reach <= 0) break;
                var sourceReceiver = chosen;
                chosen = null;
                var best = float.MaxValue;
                foreach (var target in targets)
                {
                    if (visited.Contains(target.StableId) || !Matches(opts.chain_filter, target)) continue;
                    var delta = target.transform.position - from;
                    var d = delta.magnitude;
                    if (d > reach || d >= best) continue;
                    var occluded = false;
                    var hits = Sorted(Physics.SphereCastAll(from, Option(opts.width_cm, 1) / 200f,
                        delta.normalized, Mathf.Max(0, d - .05f), ~0, QueryTriggerInteraction.Ignore));
                    foreach (var hit in hits)
                    {
                        var receiver = Receiver(hit.collider);
                        if (receiver == sourceReceiver) continue;
                        if (receiver != target) occluded = true;
                        break;
                    }
                    if (occluded) continue;
                    best = d; chosen = target;
                }
                if (chosen == null) break;
                visited.Add(chosen.StableId);
                position = chosen.transform.position;
                segments.Add(position);
                direction = (position - from).normalized;
                Emit(state, "hit", chosen, position, direction, direction);
                from = position + direction * .08f;
            }
            CarrierVisual.BeamSegments(state, segments);
        }

        // A beam's main trajectory and collision use the same pixel-derived path.
        // The endpoints define an axis, never an observed stroke start: casting
        // chooses the forward direction and the drawing supplies lateral bends.
        private static List<Vector3> BeamTrace(CarrierState state, float range)
        {
            var output = new List<Vector3> { state.position };
            if (range <= 0) return output;
            var path = state.path;
            // A missing or collapsed geometry is an invalid packet: do not
            // silently turn it into a generic straight beam.
            if (path == null || path.Count < 2) return output;
            var reverse = path[0].z > path[path.Count - 1].z ||
                (Mathf.Approximately(path[0].z, path[path.Count - 1].z) && path[0].x > path[path.Count - 1].x);
            var start = reverse ? path[path.Count - 1] : path[0];
            var end = reverse ? path[0] : path[path.Count - 1];
            var axis = end - start;
            var axisLengthSquared = axis.sqrMagnitude;
            if (axisLengthSquared < .000001f)
            {
                // Closed paths have coincident endpoints. Choose the most
                // distant sampled point as a stable axis; the actual points
                // still control the traced silhouette and collisions.
                var farthest = start;
                foreach (var candidate in path)
                    if ((candidate - start).sqrMagnitude > (farthest - start).sqrMagnitude)
                        farthest = candidate;
                axis = farthest - start;
                axisLengthSquared = axis.sqrMagnitude;
                if (axisLengthSquared < .000001f) return output;
            }
            var forward = axis.normalized;
            var sideways = new Vector3(-axis.z, 0, axis.x).normalized;
            var travelled = 0f;
            var forwardDistance = 0f;
            for (var sample = 1; sample < path.Count; sample++)
            {
                var previous = path[reverse ? path.Count - sample : sample - 1];
                var current = path[reverse ? path.Count - 1 - sample : sample];
                var step = current - previous;
                // Unfold any return stroke into cast-forward distance. This
                // preserves each source segment's length and sideways bend
                // without allowing the beam to run back toward the caster.
                forwardDistance += Mathf.Abs(Vector3.Dot(step, forward));
                var lateral = Vector3.Dot(current - start, sideways);
                var next = state.position + state.trajectoryRotation * new Vector3(lateral, 0, forwardDistance);
                var delta = next - output[output.Count - 1];
                var length = delta.magnitude;
                if (length < .0001f) continue;
                if (travelled + length >= range)
                {
                    output.Add(output[output.Count - 1] + delta * ((range - travelled) / length));
                    return output;
                }
                output.Add(next);
                travelled += length;
            }
            // The painted path determines the first part of the beam. Any
            // remaining range continues straight from its endpoint.
            if (travelled < range && output.Count > 1)
                output.Add(output[output.Count - 1] + state.trajectoryRotation * Vector3.forward * (range - travelled));
            return output;
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
            if (receiver != null && (receiver.StructureMilli <= 0 || receiver.BlocksLeft <= 0)) Expire(state,true);
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
                if (state.triggersLeft <= 0) Expire(state,true);
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

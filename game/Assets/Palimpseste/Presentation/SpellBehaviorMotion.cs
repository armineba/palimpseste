using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    // Shared precompiled vocabulary. None of these decorative functions applies
    // forces, changes a collider, loads arbitrary paths or executes player text.
    internal static class SpellBehaviorMotion
    {
        internal static bool Enabled(SpellNode node) => node?.behavior != null && node.physics != null;
        internal static Vector3 Axis(SpellBehaviorIntent behavior) => behavior?.axis == "x" ? Vector3.right
            : behavior?.axis == "z" ? Vector3.forward : Vector3.up;
        internal static float Sense(SpellBehaviorIntent behavior) => behavior?.sense == "counterclockwise" ? -1 : 1;
        internal static float AngularSpeed(SpellBehaviorIntent behavior, SpellPhysicsProfile profile) =>
            Mathf.Clamp(profile.angular_speed_mdeg_s,0,2880000) / 1000f * Sense(behavior);
        internal static Vector2 Flow(SpellBehaviorIntent behavior, SpellPhysicsProfile profile)
        {
            if (behavior == null || profile == null || behavior.phenomenon == "static") return Vector2.zero;
            return new Vector2(AngularSpeed(behavior,profile) / 360f,
                Mathf.Clamp(profile.axial_speed_cm_s,-3000,3000) / Mathf.Max(100f,profile.radius_cm * 2f));
        }

        internal static void Pose(SpellBehaviorIntent behavior, SpellPhysicsProfile profile, Bounds bounds,
            float age, float seed, ref Vector3 position, ref Quaternion rotation)
        {
            if (behavior == null || profile == null || behavior.phenomenon == "static") return;
            var axis = Axis(behavior);
            var radial = position - bounds.center;
            var axial = Vector3.Dot(radial,axis);
            var phase = age * Mathf.Clamp(profile.frequency_mhz,0,6000) / 1000f * Mathf.PI * 2 + seed * Mathf.PI * 2;
            var noise = Mathf.Clamp(profile.turbulence_cm,0,300) / 100f;
            var spin = Quaternion.AngleAxis(Mathf.Repeat(age * AngularSpeed(behavior,profile),360),axis);
            switch (behavior.phenomenon)
            {
                case "spin": case "vortex":
                    if (behavior.phenomenon == "vortex")
                    {
                        // Each height rotates around one shared axis, with a
                        // continuous phase lag: a column, not independent orbs.
                        spin *= Quaternion.AngleAxis(axial * 42f + Mathf.Sin(phase) * noise * 12f,axis);
                        var direction = radial - axis * axial;
                        if (direction.sqrMagnitude > .00001f)
                            radial += direction.normalized * Mathf.Sin(phase + axial * 1.7f -
                                profile.radial_speed_cm_s / 100f * age) *
                                Mathf.Min(Mathf.Max(noise * .25f,Mathf.Abs(profile.radial_speed_cm_s) / 3000f * .18f),
                                    Mathf.Clamp(profile.radius_cm,0,1000) / 500f);
                    }
                    position = bounds.center + spin * radial;
                    rotation = spin * rotation;
                    break;
                case "orbit":
                    var right = Vector3.Cross(axis,Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                    position += spin * right * (Mathf.Clamp(profile.radius_cm,0,1000) / 100f);
                    rotation = spin * rotation;
                    break;
                case "flutter":
                    position += axis * (Mathf.Sin(phase) * noise * .18f);
                    rotation *= Quaternion.AngleAxis(Mathf.Sin(phase + axial) * Mathf.Min(38,noise * 24),axis);
                    break;
                case "turbulence":
                    position += new Vector3(Mathf.Sin(phase * .79f),Mathf.Sin(phase * 1.13f + 2),Mathf.Cos(phase)) * noise * .22f;
                    rotation *= Quaternion.Euler(Mathf.Sin(phase) * noise * 12,Mathf.Cos(phase * .81f) * noise * 14,0);
                    break;
                // Flow is surface advection and particle transport; the
                // interpreted subject itself keeps its authored position.
            }
        }

        internal static Vector3 DeformLocal(SpellBehaviorIntent behavior, SpellPhysicsProfile profile,
            Vector3 point, Vector3 localScale, float age, float seed)
        {
            if (behavior == null || profile == null || behavior.phenomenon == "static" ||
                behavior.phenomenon == "spin" || behavior.phenomenon == "orbit") return point;
            var axis = Axis(behavior);
            var metric = Vector3.Scale(point,localScale);
            var height = Vector3.Dot(metric,axis);
            var amount = Mathf.Clamp(profile.turbulence_cm,0,300) / 100f;
            var phase = age * Mathf.Clamp(profile.frequency_mhz,0,6000) / 1000f * Mathf.PI * 2 + seed * 6.283185f;
            var transportedHeight = height - Mathf.Clamp(profile.axial_speed_cm_s,-3000,3000) / 100f * age;
            if (behavior.phenomenon == "vortex")
            {
                var radial = metric - axis * height;
                var twist = Quaternion.AngleAxis(height * 55 + Mathf.Sin(phase + transportedHeight * 2.1f) * Mathf.Max(3,amount * 14),axis);
                metric = axis * height + twist * radial * (1 + Mathf.Sin(phase + transportedHeight * 3) * Mathf.Min(.2f,Mathf.Max(.025f,amount * .12f)));
            }
            else metric += axis * (Mathf.Sin(phase + transportedHeight * 2 + metric.x * 3.1f + metric.z * 2.3f) *
                (behavior.phenomenon == "flow" ? Mathf.Max(.015f,amount * .10f) : amount * .10f));
            return new Vector3(metric.x / Mathf.Max(.001f,localScale.x),metric.y / Mathf.Max(.001f,localScale.y),
                metric.z / Mathf.Max(.001f,localScale.z));
        }
    }

    internal static class SpellResourceLibrary
    {
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.Ordinal) {
            "kpp_circle_01","kpp_circle_03","kpp_fire_01","kpp_flame_01","kpp_magic_01","kpp_slash_01",
            "kpp_smoke_01","kpp_spark_01","kpp_spark_05","kpp_star_01","kpp_trace_01","kpp_twirl_01",
            "ksp_black_smoke_00","ksp_explosion_00","ksp_poison_puff_00","ksp_white_puff_00"
        };
        private static readonly Dictionary<string,Texture2D> Cached = new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        internal static bool Contains(string id) => id != null && Allowed.Contains(id);
        internal static Texture2D Load(string id)
        {
            if (!Contains(id)) throw new ArgumentException("Unknown curated VFX resource");
            if (Cached.TryGetValue(id,out var texture) && texture != null) return texture;
            texture = Resources.Load<Texture2D>("SourcedVfx/" + id);
            if (texture == null) throw new InvalidOperationException("Curated VFX resource missing from Player: " + id);
            Cached[id] = texture;
            return texture;
        }
        internal static void Bind(Material material, string id, bool particle = false)
        {
            if (string.IsNullOrEmpty(id)) return;
            material.SetTexture("_ResourceTex",Load(id));
            material.SetFloat("_ResourceEnabled",1);
            material.SetFloat("_ResourceParticle",particle ? 1 : 0);
        }
    }
}

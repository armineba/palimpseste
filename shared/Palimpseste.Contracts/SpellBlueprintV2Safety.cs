using System;
using System.Collections.Generic;

namespace Palimpseste.Contracts
{
    // Allocation preflight shared by the server and Unity's untrusted offline packet reader.
    // Does not claim visual acceptance or replace server schema/semantic validation.
    public static class SpellBlueprintV2Safety
    {
        public static List<string> Validate(SpellBlueprintV2 b)
        {
            var errors = new List<string>();
            if (b == null || b.schema_version != SpellBlueprintV2Limits.SchemaVersion) {
                errors.Add("Unknown V2 blueprint"); return errors;
            }
            if (b.intent == null || b.identity == null || b.structural_core == null || b.motion == null ||
                b.phases == null || b.rendering_layers == null || b.physics == null || b.impact == null ||
                b.disappearance == null || b.unity_implementation == null || b.validation_rules == null) {
                errors.Add("Missing V2 contract section"); return errors;
            }
            var c = b.structural_core;
            if (!Contains(SpellBlueprintV2Limits.CoreKinds, c.kind)) errors.Add("Unknown structural core");
            if (b.archetype == null || b.archetype.Count < 1 || b.archetype.Count > 4) errors.Add("Archetype count outside bounds");
            else foreach (var a in b.archetype) if (!Contains(SpellBlueprintV2Limits.Archetypes, a)) errors.Add("Unknown archetype");
            Vector(c.size_cm, 1, 2000, errors); Vector(c.color_rgb, 0, 255, errors);
            Range(c.longitudinal_segments, 8, 128, errors); Range(c.radial_segments, 6, 48, errors);
            Range(c.entity_count, 1, 16, errors); Range(c.inner_radius_milli, 0, 950, errors);
            Points(c.control_points, 32, errors);
            if (c.branches == null || c.branches.Count > 12) errors.Add("Branch allocation exceeds bounds");
            else for (int i = 0; i < c.branches.Count; i++) {
                var branch = c.branches[i];
                if (branch == null) { errors.Add("Null branch"); continue; }
                Range(branch.parent_index, -1, i - 1, errors); Range(branch.parent_t_milli, 0, 1000, errors);
                Points(branch.control_points, 16, errors);
            }
            if (b.identity.topology != "fixed" || b.identity.coordinate_system != "local_cm_y_up_z_forward" ||
                b.identity.element_count != c.entity_count) errors.Add("Canonical identity changed");
            if (b.identity.landmarks == null || b.identity.landmarks.Count < 2 || b.identity.landmarks.Count > 24) errors.Add("Landmark count outside bounds");
            else foreach (var landmark in b.identity.landmarks) {
                if (landmark == null) { errors.Add("Null landmark"); continue; }
                Range(landmark.branch_index, -1, (c.branches?.Count ?? 0) - 1, errors); Range(landmark.t_milli, 0, 1000, errors);
            }
            var m = b.motion;
            Range(m.duration_ms, 100, 30000, errors); Range(m.amplitude_cm, 0, 500, errors);
            Range(m.frequency_mhz, 0, 10000, errors); Range(m.angular_speed_mdeg_s, -2880000, 2880000, errors);
            Range(m.acceleration_cm_s2, -4000, 4000, errors); Range(m.anticipation_milli, 0, 500, errors);
            Range(m.follow_through_milli, 0, 1000, errors); Range(m.secondary_motion_milli, 0, 1000, errors);
            if (m.path_cm == null || m.path_cm.Count < 2 || m.path_cm.Count > 32) errors.Add("Motion path exceeds bounds");
            else foreach (var point in m.path_cm) Vector(point, -3000, 3000, errors);
            Range(b.phases.appearance_end_milli, 1, 450, errors); Range(b.phases.active_end_milli, 451, 999, errors);
            if (b.phases.active_loop && m.duration_ms >= 100 && m.duration_ms <= 30000 &&
                b.phases.appearance_end_milli >= 1 && b.phases.appearance_end_milli <= 450 &&
                b.phases.active_end_milli >= 451 && b.phases.active_end_milli <= 999) {
                var span = b.phases.active_end_milli - b.phases.appearance_end_milli;
                if ((long)m.angular_speed_mdeg_s * m.duration_ms * span % 360000000000L != 0)
                    errors.Add("An active loop must contain a whole number of rotations at the declared rate");
                var oscillatory = m.deformation == "undulate" || m.deformation == "flutter" || m.deformation == "pulse" ||
                    m.deformation == "twist" || m.deformation == "vortex" || c.kind == "controlled_swarm";
                if (oscillatory && m.amplitude_cm > 0 && (long)m.frequency_mhz * m.duration_ms * span % 1000000000L != 0)
                    errors.Add("An active loop must contain a whole number of deformation cycles at the declared frequency");
                if (m.deformation == "expand") errors.Add("Monotonic expansion cannot declare an active loop");
            }
            var r = b.rendering_layers;
            Range(r.core_opacity_milli, 100, 1000, errors); Range(r.core_emission_milli, 0, 6000, errors);
            Range(r.secondary_intensity_milli, 0, 1000, errors); Range(r.atmosphere_particles, 0, 512, errors);
            if (!Contains(SpellVfxResources.All, r.resource_id)) errors.Add("Unshipped V2 texture");
            if (b.physics.collision_owner != "root" || b.physics.decorative_physics || b.physics.independent_entities ||
                b.physics.rigidbody_count != 0) errors.Add("Visual components cannot own gameplay physics");
            Range(b.impact.reaction_duration_ms, 20, 3000, errors); Range(b.impact.surviving_core_duration_ms, 20, 3000, errors);
            Range(b.impact.hit_flash_milli, 0, 1000, errors); Range(b.impact.secondary_emission, 0, 128, errors);
            Range(b.disappearance.residual_duration_ms, 0, 3000, errors);
            var rules = b.validation_rules;
            Range(rules.maximum_vertices, 256, 131072, errors); Range(rules.maximum_particles, 0, 512, errors);
            Range(rules.maximum_materials, 1, 8, errors); Range(rules.maximum_draw_calls, 1, 16, errors);
            Range(rules.maximum_cpu_microseconds, 50, 20000, errors); Range(rules.maximum_gpu_microseconds, 50, 20000, errors);
            Range(rules.maximum_transparent_layers, 1, 32, errors);
            if (rules.required_gates == null || rules.required_gates.Count != SpellBlueprintV2Limits.Gates.Length)
                errors.Add("Required validation gates missing");
            else foreach (var gate in SpellBlueprintV2Limits.Gates) if (!rules.required_gates.Contains(gate)) errors.Add("Required validation gate missing");
            if (!rules.blind_semantic_check || !rules.reject_visible_primitives || !rules.require_fixed_topology)
                errors.Add("V2 validation cannot be disabled");
            if (r.atmosphere_particles + b.impact.secondary_emission > rules.maximum_particles)
                errors.Add("Particles exceed their budget");
            if (c.kind != "branched_surface" && (long)(c.longitudinal_segments + 1) * (c.radial_segments + 1) * c.entity_count + 128 > rules.maximum_vertices)
                errors.Add("Canonical mesh exceeds declared vertex budget");
            return errors;
        }

        private static void Points(List<CoreControlPointV2> points, int max, List<string> errors)
        {
            if (points == null || points.Count < 2 || points.Count > max) { errors.Add("Control point count outside bounds"); return; }
            foreach (var point in points) {
                if (point == null) { errors.Add("Null control point"); continue; }
                Vector(point.position_cm, -2000, 2000, errors);
                Range(point.radius_cm, 1, 1000, errors); Range(point.width_cm, 1, 2000, errors);
            }
        }

        private static void Vector(int[] value, int min, int max, List<string> errors)
        {
            if (value == null || value.Length != 3) { errors.Add("Expected bounded three dimensional vector"); return; }
            foreach (var component in value) Range(component, min, max, errors);
        }
        private static void Range(int value, int min, int max, List<string> errors)
        { if (value < min || value > max) errors.Add("V2 numeric parameter outside bounds"); }
        private static bool Contains(string[] values, string item) => Array.IndexOf(values, item) >= 0;
    }
}

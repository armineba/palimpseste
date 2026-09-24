using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;

namespace Palimpseste.Core
{
    public static class SpellBlueprintV2Validator
    {
        private static readonly Dictionary<string, string[]> Compatible = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["creature"] = new[] { "swept_tube", "branched_surface" },
            ["projectile"] = new[] { "radial_volume", "swept_tube", "ribbon", "branched_surface" },
            ["beam"] = new[] { "beam", "ribbon" },
            ["ribbon"] = new[] { "ribbon", "swept_tube" },
            ["orbital"] = new[] { "controlled_swarm", "ribbon", "planar_field" },
            ["area"] = new[] { "planar_field", "radial_volume", "vortex_surface" },
            ["explosion"] = new[] { "radial_volume", "controlled_swarm" },
            ["summon"] = new[] { "branched_surface", "swept_tube", "radial_volume", "controlled_swarm" },
            ["shield"] = new[] { "planar_field", "radial_volume" },
            ["portal"] = new[] { "planar_field" },
            ["environmental"] = new[] { "vortex_surface", "planar_field", "radial_volume", "ribbon", "controlled_swarm" },
            ["multi_projectile"] = new[] { "controlled_swarm" },
            ["abstract"] = SpellBlueprintV2Limits.CoreKinds
        };

        public static bool IsCompatible(string archetype, string core) =>
            archetype != null && Compatible.TryGetValue(archetype, out var allowed) && allowed.Contains(core);

        public static IReadOnlyList<ValidationIssue> Validate(SpellBlueprintV2 blueprint)
        {
            var issues = new List<ValidationIssue>();
            if (blueprint == null) return new[] { new ValidationIssue("v2_blueprint", "$", "Blueprint is required") };
            issues.AddRange(ContractJson.Validate(JObject.FromObject(blueprint), "spell-blueprint-v2"));
            foreach (var error in SpellBlueprintV2Safety.Validate(blueprint)) issues.Add(new ValidationIssue("v2_preflight", "$", error));
            if (issues.Count != 0) return issues;
            var b = blueprint; var core = b.structural_core; var identity = b.identity;
            void Error(string code, string path, string text) => issues.Add(new ValidationIssue(code, path, text));
            if (!IsCompatible(b.archetype[0], core.kind))
                Error("v2_archetype_core", "$.structural_core.kind", "The primary archetype requires a compatible structural representation");
            if (b.archetype.Distinct().Count() != b.archetype.Count)
                Error("v2_archetype_duplicate", "$.archetype", "Archetypes must be distinct");
            if (identity.element_count != core.entity_count || (core.kind != "controlled_swarm" && core.entity_count != 1))
                Error("v2_identity_count", "$.identity.element_count", "Element count is fixed; multiple elements require an explicit controlled swarm");
            if (identity.forward_axis.Sum(value => Math.Abs(value)) != 1)
                Error("v2_identity_axis", "$.identity.forward_axis", "Select exactly one signed logical forward axis");
            if (identity.landmarks.Select(item => item.id).Distinct().Count() != identity.landmarks.Count)
                Error("v2_landmarks", "$.identity.landmarks", "Every tracked landmark requires a unique identity");
            foreach (var landmark in identity.landmarks)
                if (landmark.branch_index >= core.branches.Count)
                    Error("v2_landmark_branch", "$.identity.landmarks", "Landmark references an absent canonical branch");
            if (core.kind == "branched_surface" ? core.branches.Count == 0 : core.branches.Count != 0)
                Error("v2_core_branches", "$.structural_core.branches", "Only a branched surface declares branches, and it must have at least one");
            if (core.kind != "planar_field" && core.inner_radius_milli != 0)
                Error("v2_core_aperture", "$.structural_core.inner_radius_milli", "Only a planar field supports an aperture");
            if (b.archetype[0] == "portal" && core.inner_radius_milli == 0)
                Error("v2_portal_aperture", "$.structural_core.inner_radius_milli", "A portal must preserve its open center");
            CheckPath(core.control_points, "$.structural_core.control_points", Error);
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < core.branches.Count; i++) {
                var branch = core.branches[i];
                if (branch.parent_index >= i)
                    Error("v2_branch_cycle", "$.structural_core.branches[" + i + "]", "Branches attach only to the main path or an earlier canonical branch");
                if (!branchIds.Add(branch.id))
                    Error("v2_branch_id", "$.structural_core.branches", "Branch IDs must be distinct");
                CheckPath(branch.control_points, "$.structural_core.branches[" + i + "].control_points", Error);
            }
            if (b.phases.appearance_end_milli >= b.phases.active_end_milli)
                Error("v2_phases", "$.phases", "Appearance, active and disappearance must follow normalized time");
            if (b.motion.deformation == "vortex" || core.kind == "vortex_surface") {
                if (Math.Abs(b.motion.angular_speed_mdeg_s) < 180000 || b.motion.frequency_mhz <= 0)
                    Error("v2_vortex_motion", "$.motion", "A vortex requires continuous visible rotation and nonzero temporal frequency");
            }
            if (new[] { "undulate", "flutter", "pulse", "expand" }.Contains(b.motion.deformation) &&
                (b.motion.amplitude_cm == 0 || b.motion.frequency_mhz == 0))
                Error("v2_deformation_motion", "$.motion", "The selected deformation requires amplitude and frequency");
            if (b.physics.independent_entities)
                Error("v2_independent_physics", "$.physics.independent_entities", "Independent gameplay entities must be separate spell nodes; a node's visuals share one root collision owner");
            if (b.physics.rigidbody_count != 0)
                Error("v2_rigidbody_owner", "$.physics.rigidbody_count", "The controlled carrier runtime owns collision and motion; visual roots do not add a Unity Rigidbody");
            if (b.physics.affected_components.Distinct().Count() != b.physics.affected_components.Count ||
                !b.physics.affected_components.Contains("core"))
                Error("v2_impact_components", "$.physics.affected_components", "One root response must include the core exactly once");
            if (b.impact.surviving_core_duration_ms < b.impact.reaction_duration_ms)
                Error("v2_impact_identity", "$.impact", "Core survival must cover the complete contact reaction");
            var rules = b.validation_rules;
            if (!new HashSet<string>(rules.required_gates).SetEquals(SpellBlueprintV2Limits.Gates) ||
                rules.required_gates.Distinct().Count() != rules.required_gates.Count)
                Error("v2_required_gates", "$.validation_rules.required_gates", "Every structural, temporal, visual, impact, camera, motion, semantic and performance gate is mandatory");
            if (b.rendering_layers.atmosphere_kind == "none" && b.rendering_layers.atmosphere_particles != 0 ||
                b.rendering_layers.secondary_kind == "none" && b.rendering_layers.secondary_intensity_milli != 0)
                Error("v2_unused_layer", "$.rendering_layers", "Disabled layers cannot consume particles or intensity");
            if (b.rendering_layers.atmosphere_particles + b.impact.secondary_emission > rules.maximum_particles)
                Error("v2_particle_budget", "$.validation_rules.maximum_particles", "Particle budget must include both ambient and contact emissions");
            if (b.unity_implementation.material_count > rules.maximum_materials ||
                b.unity_implementation.material_count > rules.maximum_draw_calls)
                Error("v2_material_budget", "$.unity_implementation.material_count", "Declared materials exceed the bounded rendering budget");
            var ambientSystem = b.rendering_layers.atmosphere_kind != "none" && b.rendering_layers.atmosphere_particles > 0;
            var impactSystem = b.impact.secondary_emission > 0;
            var secondaryActive = b.rendering_layers.secondary_kind != "none" && b.rendering_layers.secondary_intensity_milli > 0;
            if (b.unity_implementation.particle_systems != (ambientSystem ? 1 : 0) + (impactSystem ? 1 : 0) ||
                b.unity_implementation.material_count != 1 + (secondaryActive ? 1 : 0) + (ambientSystem || impactSystem ? 1 : 0))
                Error("v2_implementation_trace", "$.unity_implementation", "Declared systems/materials must exactly match the precompiled renderer's selected layers");
            return issues;
        }

        public static IReadOnlyList<ValidationIssue> ValidateNode(SpellNode node)
        {
            var issues = Validate(node?.blueprint_v2).ToList();
            if (issues.Count != 0) return issues;
            var b = node.blueprint_v2;
            void Error(string code, string path, string text) => issues.Add(new ValidationIssue(code, path, text));
            if (node.appearance.construction != null || node.appearance.signature_geometry_id != null || node.geometry_id != "canonical.v2")
                Error("v2_legacy_construction", "$.appearance", "V2 uses its canonical structural core, never historical primitive construction or traced ink geometry");
            if (node.behavior == null || node.physics == null)
                Error("v2_physics_required", "$.behavior", "V2 requires an explicitly interpreted deployment and a controlled carrier physics profile");
            else {
                if (b.motion.trajectory != node.behavior.travel)
                    Error("v2_motion_trace", "$.motion.trajectory", "Canonical trajectory must match the actual gameplay carrier travel");
                if (b.motion.angular_speed_mdeg_s != node.physics.angular_speed_mdeg_s * (node.behavior.sense == "clockwise" ? -1 : 1))
                    Error("v2_rotation_trace", "$.motion.angular_speed_mdeg_s", "Canonical rotation must match interpreted physical rotation and sense");
            }
            if (b.motion.duration_ms != (node.options.lifetime_ticks ?? 0) * 20)
                Error("v2_lifetime_trace", "$.motion.duration_ms", "Normalized motion duration must exactly equal the carrier lifetime at 50 ticks per second");
            if (node.appearance.resource_id != b.rendering_layers.resource_id)
                Error("v2_resource_trace", "$.rendering_layers.resource_id", "Canonical layers must cite the same controlled resource as the gameplay plan");
            var expected = node.carrier == "beam" ? "capsule" : node.carrier == "barrier" ? "plane" : node.carrier == "field" || node.carrier == "trap" ? "box" : "sphere";
            if (b.physics.collider != expected)
                Error("v2_collider_trace", "$.physics.collider", "Collider contract differs from the implemented gameplay carrier strategy");
            var response = node.carrier == "projectile" ? ((node.options.bounces ?? 0) > 0 ? "bounce" : (node.options.pierces ?? 0) > 0 ? "pierce" : "stop") : node.carrier == "barrier" ? "block" : "overlap";
            if (b.physics.collision_response != response)
                Error("v2_collision_trace", "$.physics.collision_response", "Collision response must agree with the controlled gameplay options");
            var transition = response == "stop" || response == "block" ? "stop" : response == "bounce" ? "deflect" : "continue";
            if (b.motion.impact_transition != transition)
                Error("v2_impact_transition", "$.motion.impact_transition", "Visual contact must agree with actual collision response");
            return issues;
        }

        private static void CheckPath(List<CoreControlPointV2> points, string path, Action<string, string, string> error)
        {
            for (int i = 1; i < points.Count; i++) {
                if (points[i].position_cm.SequenceEqual(points[i - 1].position_cm))
                    error("v2_degenerate_path", path, "Consecutive canonical samples must not coincide");
                if (Math.Max(points[i].radius_cm, points[i - 1].radius_cm) > Math.Min(points[i].radius_cm, points[i - 1].radius_cm) * 4)
                    error("v2_abrupt_section", path, "A continuous form cannot abruptly jump by more than four times its section radius");
            }
        }
    }
}

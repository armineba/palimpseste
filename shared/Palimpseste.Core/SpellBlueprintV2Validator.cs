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
            // The field carrier owns collision independently of the continuous
            // visual support. A stationary ribbon is also implemented by Unity.
            ["area"] = new[] { "planar_field", "radial_volume", "vortex_surface", "ribbon" },
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

        // This is the same table used by validation, exposed to the constructor
        // so a rejected combination can be corrected without guessing.
        public static string BuildCompatibilityContext() =>
            "Only archetype[0] selects the allowed structural_core.kind values. " +
            "A secondary archetype does not extend that set. Choose the primary " +
            "structural family and its core together; preserve the description and gameplay.\n" +
            string.Join("\n", Compatible.Select(entry => entry.Key + " -> " + string.Join(", ", entry.Value))) +
            "\nstructural_core.inner_radius_milli must equal 0 for every kind except planar_field. " +
            "A primary portal requires planar_field with inner_radius_milli > 0.\n" +
            "IMPLEMENTED GEOMETRY (CanonicalCoreGeometryV2; these are executable limits, not names of arbitrary meshes):\n" +
            "All geometry is created once in local centimetres with fixed topology. For path-based sweeps, " +
            "control_points.position_cm defines the full XYZ Catmull-Rom centreline; adjacent radius/width profiles " +
            "are interpolated smoothly. Point spacing does not allocate proportional time or arc-length samples. " +
            "Sections use a transported frame, not authored per-section rotations. A textual selection_reason, " +
            "landmark or identity invariant does not add geometry or an animation control.\n" +
            "swept_tube: one connected tube along the complete path, circular sections with radius_cm and flat end caps. " +
            "width_cm does not flatten this tube. size_cm does not rescale its mesh; author positions/radii to the " +
            "intended size. Changing radial_segments changes tessellation, not cross-section design or material. " +
            "A tapered or bent body needs that taper/path in its numerical controls; it is not inferred from its subject.\n" +
            "beam: the same capped circular sweep as swept_tube, not a separate beam shader or an automatic branching " +
            "lightning generator. Its own geometry uses positions/radius_cm, not width_cm or size_cm as mesh scale. " +
            "When the gameplay beam supplies a beam path, the renderer maps longitudinal U along that path and keeps " +
            "the authored X/Y section offsets; do not encode a second independent trajectory in the core.\n" +
            "radial_volume: one closed swept mass along the complete XYZ path, with ellipse section radii " +
            "width_cm/2 and radius_cm in its transported frame. Only the terminal sections taper to closure. " +
            "It is not an automatic sphere, Y-axis lathe, arbitrary polyhedron or voxel field. size_cm does not " +
            "scale its mesh; offsets and profiles define its shape. This can make a continuous flattened volume, " +
            "but not independently oriented plates, disconnected pieces or hard creases specified only in prose.\n" +
            "vortex_surface: a continuous full-circumference surface of revolution, " +
            "not an open helical strip. Its radius_cm profile controls full sections; size_cm.y controls uniform " +
            "height, control-point X/Z displace the axis, and control-point Y and width_cm do not shape its sections. " +
            "size_cm.z/size_cm.x controls the Z-to-X section ratio; an existing small three-lobed spiral modulation " +
            "does not cut the surface open. Narrowing sections creates waists, not gaps between disconnected slabs " +
            "or helical turns. Angular motion rotates this existing surface; it cannot change its connectivity.\n" +
            "ribbon: a connected double-sided open band following the full XYZ Catmull-Rom control path; " +
            "width_cm controls the band width. An explicitly helical path can provide spacing between turns " +
            "when its pitch and width permit it. The ribbon has no thickness and its orientation is transported " +
            "along the path; radius_cm, size_cm and radial_segments do not shape this mesh. Separate slab tilt, " +
            "individual pieces and arbitrary cross sections are not controls. This is an authored moving band, " +
            "not world-space trail history recorded behind a moving head.\n" +
            "planar_field: one zero-thickness elliptical disk or annulus centred on the root. The smallest size_cm " +
            "component selects its normal (Y first on ties, otherwise Z before X); the other two dimensions are " +
            "the full outer diameters. inner_radius_milli is the inner-to-outer ratio. control_points, their radius_cm " +
            "and width_cm do not sculpt this mesh. It cannot become an extruded wall, non-elliptical outline, dome " +
            "or multiple separate rings by describing those forms.\n" +
            "controlled_swarm: entity_count duplicates the ENTIRE SAME capped circular sweep at evenly spaced " +
            "angular offsets. It does not split the control path into entity_count segments or build a chain of " +
            "different body parts. Each copy has the same path, radius profile and initial orientation; width_cm " +
            "does not shape it. For angle a = 2*pi*entityIndex/entity_count, its centre offset in cm is " +
            "(0.5*size_cm.x*cos(a), 0.2*size_cm.y*sin(2*a), 0.5*size_cm.z*sin(a)). " +
            "There is no per-copy geometry, scale, layout, phase or independent collision parameter. Choose this " +
            "only for an intentional group of complete repeated forms; keep identity.element_count equal to " +
            "entity_count and preserve the declared separate connected components.\n" +
            "branched_surface: one fused smooth distance-field surface, formed from circular-radius segments along " +
            "the main path and attached branch paths. Each branch joins its parent path at parent_t_milli; its " +
            "points are in the same root coordinate system, not offsets from the attachment. It is not a set of " +
            "separate tube renderers, a skeletal rig or an arbitrary imported mesh. width_cm and size_cm do not " +
            "sculpt this surface. Extraction uses a fixed 30-cell grid per axis over all path bounds and smooth " +
            "unions; radial_segments/longitudinal_segments do not refine it. Small isolated details can disappear " +
            "at that resolution, close branches can merge, and sharp planar facets are not an exposed control.\n" +
            "There is no additional mesh, skinned_mesh, custom graph or arbitrary asset core kind. All non-swarm " +
            "cores require one connected entity. size_cm remains a declared gameplay footprint for field/trap " +
            "carriers even where it does not scale geometry; keep this footprint consistent with the authored core.\n" +
            "IMPLEMENTED CONTINUOUS MOTION (CanonicalSpellVisualV2): geometry stays attached to its gameplay root; " +
            "motion.path_cm/trajectory describe carrier movement, not a second visual translation. Non-swarm " +
            "angular motion rotates around local Y. A swarm rotates its centre offsets around Y and adds per-copy " +
            "vertical sinusoidal motion at the declared amplitude/frequency; this is orbit, not per-copy local spin. " +
            "deformation=vortex also applies the whole-core Y rotation and a height-dependent twist/wobble, so on " +
            "a swarm it is an additional deformation after centre-orbit motion, not an independent spin control. " +
            "undulate offsets X, flutter offsets Y, twist oscillates around local Z, pulse scales around mesh " +
            "bounds centre, and expand grows the same existing geometry. These deformation equations do not " +
            "create joints, split/merge entities, change section orientation independently, or add anatomical parts. " +
            "The current introduction scales the existing core from 8% to full size and reveals its surface; " +
            "contact deforms the same vertices and retirement fades, dissolves, contracts or disperses that same " +
            "topology. A demanded motion or representation absent from these controls requires development; " +
            "do not claim it is supplied by a description, source-method title or shader decoration. " +
            "All existing contract, structural, motion and visual acceptance gates still apply.";

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
                Error("v2_archetype_core", "$.structural_core.kind",
                    "Primary archetype '" + b.archetype[0] + "' does not permit core '" + core.kind +
                    "'. Allowed structural_core.kind values: " +
                    (Compatible.TryGetValue(b.archetype[0], out var allowedCores) ? string.Join(", ", allowedCores) : "none") +
                    ". Only archetype[0] selects compatibility; secondary archetypes do not extend it.");
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
                Error("v2_core_aperture", "$.structural_core.inner_radius_milli",
                    "Core '" + core.kind + "' requires inner_radius_milli = 0; received " + core.inner_radius_milli +
                    ". Only planar_field supports a nonzero aperture.");
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

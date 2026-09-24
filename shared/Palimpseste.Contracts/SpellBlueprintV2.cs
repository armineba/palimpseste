using System.Collections.Generic;

namespace Palimpseste.Contracts
{
    // Pipeline V2 is a bounded data language. It contains no shader text, C#, paths or commands.
    public sealed class SpellBlueprintV2
    {
        public string schema_version;
        public SpellIntentV2 intent;
        public List<string> archetype;
        public SpellIdentitySpecV2 identity;
        public StructuralCoreV2 structural_core;
        public MotionSpecV2 motion;
        public SpellPhasesV2 phases;
        public RenderingLayersV2 rendering_layers;
        public PhysicsContractV2 physics;
        public ImpactSpecV2 impact;
        public DisappearanceSpecV2 disappearance;
        public UnityImplementationPlanV2 unity_implementation;
        public ValidationRulesV2 validation_rules;
    }

    public sealed class SpellIntentV2
    {
        public string semantic_subject;
        public string primary_action;
        public string target_behavior;
        public string perceived_material;
        public string motion_character;
        public string silhouette_priority;
        public List<string> visual_keywords;
        public string gameplay_role;
        public string impact_intent;
        public string disappearance_intent;
        public bool figurative;
    }

    public sealed class SpellIdentitySpecV2
    {
        public List<string> hard_invariants;
        public List<string> soft_invariants;
        public List<string> free_parameters;
        public string topology;
        public string coordinate_system;
        public int element_count;
        public int[] forward_axis;
        public List<int[]> palette_rgb;
        public List<SpellLandmarkV2> landmarks;
    }

    public sealed class SpellLandmarkV2
    {
        public string id;
        // -1 is the main control path; otherwise a branches index.
        public int branch_index;
        public int t_milli;
    }

    public sealed class StructuralCoreV2
    {
        public string kind;
        public string selection_reason;
        public int[] size_cm;
        public List<CoreControlPointV2> control_points;
        public List<CoreBranchV2> branches;
        public int longitudinal_segments;
        public int radial_segments;
        public int entity_count;
        public int inner_radius_milli;
        public int[] color_rgb;
    }

    public sealed class CoreControlPointV2
    {
        public int[] position_cm;
        public int radius_cm;
        public int width_cm;
    }

    public sealed class CoreBranchV2
    {
        public string id;
        public int parent_index;
        public int parent_t_milli;
        public List<CoreControlPointV2> control_points;
    }

    public sealed class MotionSpecV2
    {
        public int duration_ms;
        public string trajectory;
        public List<int[]> path_cm;
        public string deformation;
        public int amplitude_cm;
        public int frequency_mhz;
        public int angular_speed_mdeg_s;
        public int acceleration_cm_s2;
        public int anticipation_milli;
        public int follow_through_milli;
        public int secondary_motion_milli;
        public string impact_transition;
        public string dissipation;
    }

    public sealed class SpellPhasesV2
    {
        // Normalized time: appearance [0,a], active [a,b], disappearance [b,1000].
        public int appearance_end_milli;
        public int active_end_milli;
        public bool active_loop;
    }

    public sealed class RenderingLayersV2
    {
        public string core_surface;
        public int core_opacity_milli;
        public int core_emission_milli;
        public string secondary_kind;
        public int secondary_intensity_milli;
        public string atmosphere_kind;
        public int atmosphere_particles;
        public string resource_id;
    }

    public sealed class PhysicsContractV2
    {
        public string collider;
        public string collision_owner;
        public string collision_response;
        public string visual_response;
        public List<string> affected_components;
        public string lifetime_behavior;
        public int rigidbody_count;
        public bool decorative_physics;
        public bool independent_entities;
    }

    public sealed class ImpactSpecV2
    {
        public string contact_pose;
        public int hit_flash_milli;
        public string deformation;
        public int reaction_duration_ms;
        public int secondary_emission;
        public string dissipation_direction;
        public int surviving_core_duration_ms;
        public string destruction_mode;
    }

    public sealed class DisappearanceSpecV2
    {
        public string mode;
        public string direction;
        public int residual_duration_ms;
    }

    public sealed class UnityImplementationPlanV2
    {
        public string root;
        public string core_renderer;
        public string deformation_system;
        public string motion_controller;
        public string collider_strategy;
        public int vfx_graph_systems;
        public int particle_systems;
        public string shader;
        public int material_count;
        public string trails;
        public string audio_hook;
        public string impact_system;
        public string lifetime_controller;
    }

    public sealed class ValidationRulesV2
    {
        public List<string> required_gates;
        public bool blind_semantic_check;
        public bool reject_visible_primitives;
        public bool require_fixed_topology;
        public int maximum_vertices;
        public int maximum_particles;
        public int maximum_draw_calls;
        public int maximum_materials;
        public int maximum_cpu_microseconds;
        public int maximum_gpu_microseconds;
        public int maximum_transparent_layers;
    }

    public static class SpellBlueprintV2Limits
    {
        public const string SchemaVersion = "sp.blueprint/2.0";
        public const string MinimumClient = "1.8.0";
        public static readonly string[] Archetypes = { "creature", "projectile", "beam", "ribbon", "orbital", "area", "explosion", "summon", "shield", "portal", "environmental", "multi_projectile", "abstract" };
        public static readonly string[] CoreKinds = { "swept_tube", "ribbon", "beam", "radial_volume", "vortex_surface", "planar_field", "controlled_swarm", "branched_surface" };
        public static readonly string[] Gates = { "A_structure", "B_continuity", "C_rendering", "D_impact", "E_game_camera", "F_motion", "semantic_blind", "performance" };
    }
}

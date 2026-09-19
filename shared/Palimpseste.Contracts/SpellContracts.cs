using System.Collections.Generic;

namespace Palimpseste.Contracts
{
    // Field names are the versioned wire names. Keep this assembly free of Unity and JSON libraries.
    public sealed class SpellDescription
    {
        public string schema_version;
        public string title;
        public string summary;
        public List<SpellObservation> observations;
        public List<SpellClause> clauses;
        public List<SpellRelation> relations;
        public List<ShapeRequest> shape_requests;
    }

    public sealed class SpellObservation
    {
        public string id;
        public string region;
        public string visible_feature;
        public string interpretation;
    }

    public sealed class SpellClause
    {
        public string id;
        public string subject_id;
        public string kind;
        public string text;
        public List<string> observation_ids;
        public List<SpellFact> facts;
    }

    public sealed class SpellFact
    {
        public string dimension;
        public string value;
    }

    public sealed class SpellRelation
    {
        public string source_subject_id;
        public string @event;
        public string target_subject_id;
        public int max_activations;
        public List<string> clause_ids;
    }

    public sealed class ShapeRequest
    {
        public string subject_id;
        public string region;
        public string role;
    }

    public sealed class SpellPlan
    {
        public string schema_version;
        public string description_sha256;
        public string catalog_version;
        public string rules_profile;
        public List<SpellNode> nodes;
    }

    public sealed class SpellNode
    {
        public string node_id;
        public string subject_id;
        public List<string> clause_ids;
        public string carrier;
        public SpellActivation activation;
        public string anchor;
        public string geometry_id;
        public int scale_cm;
        public int rotation_mdeg;
        public SpellAppearance appearance;
        public List<SpellEffect> effects;
        public SpellOptions options;
    }

    public sealed class SpellActivation
    {
        public string parent_id;
        public string @event;
        public int delay_ticks;
        public int max_activations;
        public int copies;
        public int spread_mdeg;
    }

    public sealed class SpellAppearance
    {
        public string affinity;
        public string pattern;
        public string signature_geometry_id;
    }

    public sealed class SpellEffect
    {
        public string id;
        public List<string> clause_ids;
        public string @event;
        public string kind;
        public string target_filter;
        public int amount;
        public int duration_ticks;
        public string direction;
    }

    // Nullable fields allow the exact per-carrier option shape to be serialized with NullValueHandling.Ignore.
    public sealed class SpellOptions
    {
        public int? lifetime_ticks;
        public int? range_cm;
        public int? speed_cm_s;
        public int? radius_cm;
        public int? width_cm;
        public int? tick_interval;
        public int? chain_hops;
        public int? chain_radius_cm;
        public int? height_cm;
        public int? front_width_cm;
        public int? thickness_cm;
        public int? structure_milli;
        public int? block_limit;
        public int? arm_ticks;
        public int? trigger_limit;
        public int? rearm_ticks;
        public int? turn_mdeg_s;
        public int? bounces;
        public int? pierces;
        public string motion;
        public string contact_filter;
        public string chain_filter;
        public string trigger_filter;
    }

    public sealed class GeometryAsset
    {
        public string schema_version;
        public string geometry_id;
        public string source_region;
        public string kind;
        public string source_pixel_sha256;
        public string algorithm;
        public List<GeometryPoint> points;
        public string mask_file;
        public int width_px;
        public int height_px;
        public string notes;
    }

    public sealed class GeometryPoint
    {
        public int x;
        public int z;
    }

    public sealed class CompiledSpell
    {
        public string schema_version;
        public string spell_id;
        public string parchment_id;
        public string created_at;
        public SpellVersions versions;
        public SpellProvenance provenance;
        public string signature_seed_hex;
        public string description_sha256;
        public SpellPlan plan;
        public List<GeometryManifestEntry> geometry_manifest;
        public List<BinaryAssetEntry> binary_assets;
        public ResourceBounds resource_bounds;
        public SpellDisplay display;
    }

    public sealed class SpellVersions
    {
        public string catalog;
        public string compiler;
        public string geometry;
        public string rules_profile;
        public string min_client;
    }

    public sealed class SpellProvenance
    {
        public string mode;
        public string capture_sha256;
        public string reference_sha256;
        public string model_a;
        public string model_b;
        public string prompt_a_version;
        public string prompt_b_version;
        public string response_a_id;
        public string response_b_id;
    }

    public sealed class GeometryManifestEntry
    {
        public string id;
        public string kind;
        public string artifact_id;
        public string sha256;
        public int size_bytes;
    }

    public sealed class BinaryAssetEntry
    {
        public string file_name;
        public string artifact_id;
        public string sha256;
        public int size_bytes;
        public string media_type;
    }

    public sealed class ResourceBounds
    {
        public int max_instances;
        public int max_effect_applications;
        public int max_end_tick;
        public int geometry_bytes;
        public int max_colliders;
    }

    public sealed class SpellDisplay
    {
        public string title;
        public string factual_description;
        public List<string> mechanical_lines;
    }
}

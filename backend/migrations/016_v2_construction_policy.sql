-- D22: additive revision windows after a changed construction policy.
-- Keep every historical pass, plan, artifact and provider attempt unchanged.
-- Supersedes revision bounds from 012 and includes the 015 pass/attempt bounds;
-- a deployment must not replay 012..015 after this migration.
BEGIN;

ALTER TABLE jobs ADD COLUMN IF NOT EXISTS v2_revision_base integer NOT NULL DEFAULT 0;
ALTER TABLE jobs ADD COLUMN IF NOT EXISTS v2_builder_version text;
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_v2_revision_base_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_v2_revision_base_check CHECK (v2_revision_base BETWEEN 0 AND 28);
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_v2_builder_version_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_v2_builder_version_check CHECK (
 v2_builder_version IS NULL OR v2_builder_version ~ '^sp[.]construction-policy/[0-9]+[.][0-9]+$'
);

ALTER TABLE spell_v2_passes DROP CONSTRAINT IF EXISTS spell_v2_passes_revision_check;
ALTER TABLE spell_v2_passes ADD CONSTRAINT spell_v2_passes_revision_check CHECK (revision BETWEEN 0 AND 31);
ALTER TABLE spell_plans DROP CONSTRAINT IF EXISTS spell_plans_revision_check;
ALTER TABLE spell_plans ADD CONSTRAINT spell_plans_revision_check CHECK (revision BETWEEN 0 AND 31);

ALTER TABLE spell_v2_passes DROP CONSTRAINT IF EXISTS spell_v2_passes_pass_check;
ALTER TABLE spell_v2_passes ADD CONSTRAINT spell_v2_passes_pass_check CHECK (
 pass IN ('research','blueprint','blueprint_recovery','provider_failure','methods','core','blind','structure','motion','secondary','impact','polish','optimization','validation','sheet')
);
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_attempt_count_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_attempt_count_check CHECK (
 attempt_count >= 0 AND attempt_count <= CASE WHEN visual_pipeline_version=5 THEN 32 ELSE 10 END
);

COMMIT;

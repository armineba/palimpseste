-- Infrastructure failures are evidence, not rejected artistic candidates.
-- No existing job, attempt or artifact is deleted or rewritten by this migration.
BEGIN;
ALTER TABLE spell_v2_passes DROP CONSTRAINT IF EXISTS spell_v2_passes_pass_check;
ALTER TABLE spell_v2_passes ADD CONSTRAINT spell_v2_passes_pass_check CHECK (
 pass IN ('research','blueprint','provider_failure','methods','core','blind','structure','motion','secondary','impact','polish','optimization','validation','sheet')
);
-- A V2 candidate can require B plus three independent J passes, up to four candidates.
-- Preserve the historical V1 bound; accommodate the V2 sequence and bounded repairs.
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_attempt_count_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_attempt_count_check CHECK (
 attempt_count >= 0 AND attempt_count <= CASE WHEN visual_pipeline_version=5 THEN 32 ELSE 10 END
);
COMMIT;

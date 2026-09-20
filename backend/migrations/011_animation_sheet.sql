-- D16: future jobs use animation sheets; existing jobs and frozen images stay unchanged.
BEGIN;
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_visual_pipeline_version_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_visual_pipeline_version_check CHECK (visual_pipeline_version IN (0,1,2,3,4));
ALTER TABLE jobs ALTER COLUMN visual_pipeline_version SET DEFAULT 4;

CREATE TABLE IF NOT EXISTS visual_atlases (
 job_id uuid PRIMARY KEY REFERENCES jobs(id),
 provider_attempt_id uuid NOT NULL UNIQUE REFERENCES provider_attempts(id),
 artifact_id uuid NOT NULL UNIQUE REFERENCES artifacts(id),
 description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
 prompt_version text NOT NULL,
 input_sha256 text NOT NULL CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
 width_px integer NOT NULL CHECK (width_px BETWEEN 512 AND 2048),
 height_px integer NOT NULL CHECK (height_px BETWEEN 512 AND 2048),
 animation_sheet jsonb NOT NULL CHECK (animation_sheet IN (
   '{"layout_version":"sp.animation-sheet/1.0","rows":3,"columns":7,"ending_basis":"contact"}'::jsonb,
   '{"layout_version":"sp.animation-sheet/1.0","rows":3,"columns":7,"ending_basis":"expiration"}'::jsonb)),
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(job_id,artifact_id)
);

ALTER TABLE visual_references ADD COLUMN IF NOT EXISTS animation_sheet jsonb;
ALTER TABLE visual_references ADD COLUMN IF NOT EXISTS source_atlas_artifact_id uuid REFERENCES artifacts(id);
ALTER TABLE visual_references DROP CONSTRAINT IF EXISTS visual_references_animation_sheet_check;
ALTER TABLE visual_references ADD CONSTRAINT visual_references_animation_sheet_check CHECK (
 (animation_sheet IS NULL AND source_atlas_artifact_id IS NULL) OR
 (animation_sheet IS NOT NULL AND source_atlas_artifact_id IS NOT NULL AND animation_sheet IN (
   '{"layout_version":"sp.animation-sheet/1.0","rows":3,"columns":7,"ending_basis":"contact"}'::jsonb,
   '{"layout_version":"sp.animation-sheet/1.0","rows":3,"columns":7,"ending_basis":"expiration"}'::jsonb)));
ALTER TABLE visual_references DROP CONSTRAINT IF EXISTS visual_references_source_atlas_job_fk;
ALTER TABLE visual_references ADD CONSTRAINT visual_references_source_atlas_job_fk
 FOREIGN KEY(job_id,source_atlas_artifact_id) REFERENCES visual_atlases(job_id,artifact_id);
COMMIT;

-- D13: an immutable generated image between interpretation and planning.
BEGIN;
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_state_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_state_check CHECK (state IN
 ('queued','interpreting','generating_visual_reference','resolving_geometry','planning','validating','ready','waiting_retry','needs_operator'));
ALTER TABLE provider_attempts DROP CONSTRAINT IF EXISTS provider_attempts_stage_check;
ALTER TABLE provider_attempts ADD CONSTRAINT provider_attempts_stage_check CHECK (stage IN ('A','G','B','repair_A','repair_B'));
-- Jobs admitted before this migration retain their original frozen pipeline.
ALTER TABLE jobs ADD COLUMN IF NOT EXISTS visual_pipeline_version integer NOT NULL DEFAULT 0 CHECK (visual_pipeline_version IN (0,1));
ALTER TABLE jobs ALTER COLUMN visual_pipeline_version SET DEFAULT 1;
CREATE TABLE IF NOT EXISTS visual_references (
 job_id uuid PRIMARY KEY REFERENCES jobs(id),
 provider_attempt_id uuid NOT NULL REFERENCES provider_attempts(id),
 artifact_id uuid NOT NULL REFERENCES artifacts(id),
 description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
 prompt_version text NOT NULL,
 input_sha256 text NOT NULL CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
 width_px integer NOT NULL CHECK (width_px BETWEEN 512 AND 2048),
 height_px integer NOT NULL CHECK (height_px BETWEEN 512 AND 2048),
 created_at timestamptz NOT NULL DEFAULT now()
);
COMMIT;

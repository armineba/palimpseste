-- D14: persistent independent visual reviews of bounded, append-only plan revisions.
-- Existing jobs retain their original pipeline version and frozen documents.
BEGIN;

ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_visual_pipeline_version_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_visual_pipeline_version_check CHECK (visual_pipeline_version IN (0,1,2));
ALTER TABLE jobs ALTER COLUMN visual_pipeline_version SET DEFAULT 2;

ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_state_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_state_check CHECK (state IN
 ('queued','interpreting','generating_visual_reference','resolving_geometry','planning','refining_visuals','validating','ready','waiting_retry','needs_operator'));

ALTER TABLE provider_attempts DROP CONSTRAINT IF EXISTS provider_attempts_stage_check;
ALTER TABLE provider_attempts ADD CONSTRAINT provider_attempts_stage_check
 CHECK (stage IN ('A','G','B','J','repair_A','repair_B'));

CREATE TABLE IF NOT EXISTS visual_reviews (
 job_id uuid NOT NULL REFERENCES jobs(id),
 plan_sha256 text NOT NULL CHECK (plan_sha256 ~ '^[0-9a-f]{64}$'),
 provider_attempt_id uuid NOT NULL UNIQUE REFERENCES provider_attempts(id),
 score integer NOT NULL CHECK (score BETWEEN 0 AND 10000),
 lifecycle_faithful boolean NOT NULL,
 verdict jsonb NOT NULL CHECK (jsonb_typeof(verdict) = 'object'),
 verdict_artifact_id uuid NOT NULL REFERENCES artifacts(id),
 capture_artifact_id uuid NOT NULL REFERENCES artifacts(id),
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (job_id,plan_sha256)
);

COMMIT;

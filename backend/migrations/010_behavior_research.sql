-- D15: mandatory reference research for future spells only; immutable old jobs.
BEGIN;
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_visual_pipeline_version_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_visual_pipeline_version_check CHECK (visual_pipeline_version IN (0,1,2,3));
ALTER TABLE jobs ALTER COLUMN visual_pipeline_version SET DEFAULT 3;
CREATE TABLE IF NOT EXISTS spell_reference_research (
 job_id uuid PRIMARY KEY REFERENCES jobs(id),
 artifact_id uuid NOT NULL UNIQUE REFERENCES artifacts(id),
 description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
 image_sha256 text NOT NULL CHECK (image_sha256 ~ '^[0-9a-f]{64}$'),
 created_at timestamptz NOT NULL DEFAULT now()
);
COMMIT;

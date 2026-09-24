-- Product Pipeline V2 = admission version5. Do not UPDATE any existing job or spell.
BEGIN;
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_visual_pipeline_version_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_visual_pipeline_version_check CHECK (visual_pipeline_version IN (0,1,2,3,4,5));
ALTER TABLE jobs ALTER COLUMN visual_pipeline_version SET DEFAULT 5;
CREATE TABLE IF NOT EXISTS spell_v2_passes (
 job_id uuid NOT NULL REFERENCES jobs(id),
 revision integer NOT NULL CHECK (revision BETWEEN 0 AND 3),
 pass text NOT NULL CHECK (pass IN ('research','blueprint','core','blind','structure','motion','secondary','impact','polish','optimization','validation','sheet')),
 artifact_id uuid NOT NULL REFERENCES artifacts(id),
 input_sha256 text NOT NULL CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
 locked_core_sha256 text CHECK (locked_core_sha256 ~ '^[0-9a-f]{64}$'),
 accepted boolean NOT NULL DEFAULT false,
 provider_attempt_id uuid REFERENCES provider_attempts(id),
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(job_id,revision,pass)
);
CREATE INDEX IF NOT EXISTS spell_v2_passes_artifact ON spell_v2_passes(artifact_id);
COMMIT;

-- Human observations about A are evidence for later prompt versions. They never
-- mutate the source job, its immutable interpretation, or an owned spell.
BEGIN;

CREATE TABLE IF NOT EXISTS interpretation_feedback (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id),
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
    prompt_version text NOT NULL,
    verdict text NOT NULL CHECK (verdict IN ('correct', 'incorrect')),
    correction text NOT NULL CHECK (length(correction) BETWEEN 1 AND 2000),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(job_id, owner_id)
);
CREATE INDEX IF NOT EXISTS interpretation_feedback_owner_idx
    ON interpretation_feedback(owner_id, created_at DESC);

COMMIT;

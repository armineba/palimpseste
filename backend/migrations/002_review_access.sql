-- Incremental migration for databases that already applied 001_initial.sql.
-- It is intentionally idempotent so an operator can run it once per database.
BEGIN;

CREATE TABLE IF NOT EXISTS spell_review_grants (
    spell_id uuid NOT NULL REFERENCES spells(id) ON DELETE CASCADE,
    reviewer_id uuid NOT NULL REFERENCES lab_principals(id) ON DELETE CASCADE,
    case_id text NOT NULL CHECK (case_id ~ '^[a-z][a-z0-9_.-]{0,63}$'),
    granted_by uuid NOT NULL REFERENCES lab_principals(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (spell_id, reviewer_id, case_id)
);
CREATE INDEX IF NOT EXISTS spell_review_grants_reviewer_idx ON spell_review_grants(reviewer_id, spell_id);

COMMIT;

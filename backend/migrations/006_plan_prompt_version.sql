-- Existing plans were produced by the only previously shipped B prompt, 1.0.
-- A frozen plan must retain that version when a later worker resumes compilation.
BEGIN;

ALTER TABLE spell_plans ADD COLUMN IF NOT EXISTS prompt_version text;
UPDATE spell_plans SET prompt_version='sp.prompt.b/1.0' WHERE prompt_version IS NULL;
ALTER TABLE spell_plans ALTER COLUMN prompt_version SET NOT NULL;

COMMIT;

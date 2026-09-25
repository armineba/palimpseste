-- Add private, per-revision method receipts. Existing research and spell bytes stay frozen.
BEGIN;
ALTER TABLE spell_v2_passes DROP CONSTRAINT IF EXISTS spell_v2_passes_pass_check;
ALTER TABLE spell_v2_passes ADD CONSTRAINT spell_v2_passes_pass_check CHECK (
 pass IN ('research','blueprint','methods','core','blind','structure','motion','secondary','impact','polish','optimization','validation','sheet')
);
COMMIT;

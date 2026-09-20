-- Preserve three-region parchments while admitting new whole-canvas drawings.
BEGIN;

ALTER TABLE parchments DROP CONSTRAINT IF EXISTS parchments_layout_version_check;
ALTER TABLE parchments ADD CONSTRAINT parchments_layout_version_check
    CHECK (layout_version IN ('three_regions_v1', 'free_canvas_v2'));

CREATE UNIQUE INDEX IF NOT EXISTS artifacts_one_free_reference
    ON artifacts(kind) WHERE owner_id IS NULL AND kind = 'reference_free_canvas';

COMMIT;

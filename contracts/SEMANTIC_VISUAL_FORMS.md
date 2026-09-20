# Interpreted 3D forms

The interpretation owns the visual subject. A `visual_form` fact selects one of
the 21 bounded forms listed in `capability-catalog.json#/visual_forms`; B copies
that exact value to `SpellAppearance.form`. A rock interpretation renders a 3D
rock, regardless of the rough outline used to suggest it. The reusable form is
combined with the interpreted palette, carrier, motion and effects. Creature
forms represent visible manifestations; they do not add autonomous companion
AI or effects absent from the description.

New descriptions contain exactly one `visual_form` per subject and an empty
`shape_requests` array. The compiler rejects missing, additional or changed
forms, unknown form IDs, and an ink `signature_geometry_id` on these nodes.
Codex B's strict transport includes `form` and `signature_geometry_id` with
nullable types. Published semantic packets omit the null signature and require
client `1.1.0` or newer. Business schemas permit absent/null `form` so archived
pixel based spells remain valid and preserve their original rendering.

Semantic projectiles also obey the selected form's
`minimum_projectile_radius_cm` in the capability catalog. For example, a golem
requires at least 45 cm, a boulder 20 cm. The compiler returns
`semantic_projectile_size` for smaller values so the planner can repair the
numeric option. This remains the physical projectile radius, capped at 100 cm
by the existing schema; the bound does not apply to archived null-form spells.

## Controlled geometry and provenance

`GeometryResolver.SemanticVersion` is `sp.geometry.resolver/2.0.semantic`.
It builds one asset per subject, independent of the drawn contour:

- Projectile and beam: straight path; a projectile with interpreted `curve`
  motion receives a smooth, fixed 25 point arc.
- Barrier: a transverse straight path.
- Field, pulse and trap: a circular 128 x 128 footprint with a smooth edge.

Paths advance on X and use Z for the sideways offset, matching the runtime's
local frame. Shape scale and motion remain bounded plan values. The rendered
3D form is separate from this path or collision footprint. No traced silhouette
or distribution is generated for a semantic description.

IDs use `semantic.<first 16 hex characters of SHA256(subject_id UTF-8)>.<kind>`.
The complete subject ID remains in `source_subject_id`. This preserves bounded
artifact ID lengths even for the longest legal subject IDs.

`source_description_sha256` hashes the description contract object serialized
with Json.NET `Formatting.None` and its declared field order. It records the
normalized structured data; `SpellPlan.description_sha256` and the compiled
packet still bind independently to the exact frozen A response bytes.
`source_pixel_sha256` remains the SHA256 of the canonical RGBA ink image as
capture lineage. Ink pixels do not determine the generated path or footprint.
`algorithm` records the exact semantic resolver version.

The compiler regenerates the controlled geometry from the description and
compares the complete asset, subject, version, description digest and mask
bytes. It rejects substituted geometry, different subjects, changed paths,
extra silhouette assets and altered footprint masks. All assets share the
same captured pixel digest. Pixel authenticity is established by the existing
capture validation before geometry resolution.

Descriptions without any `visual_form` retain the previous regional or whole
canvas resolver. No migration reinterprets an archived player's description.

## Verification

`tests/Palimpseste.Core.Smoke` covers legacy compilation, null transport form,
A/B catalog parity, text-to-form correspondence, minimum client version,
controlled geometry identical across unrelated ink contours, retained capture
lineage, and rejection of modified descriptions, subjects, algorithms, points,
mask bytes and scribble signatures. These are local deterministic checks, not
evidence of a real model call or an accepted in-game visual result.

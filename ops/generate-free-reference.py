"""Generate the neutral, reproducible 1024px reference for free_canvas_v2."""

from __future__ import annotations

import hashlib
import json
import random
from pathlib import Path

from PIL import Image


root = Path(__file__).resolve().parents[1]
target = root / "reference" / "reference_free_canvas.png"
layout = root / "reference" / "layout-v2.json"
size = 1024
noise = random.Random(21551)
image = Image.new("RGB", (size, size))
pixels = image.load()
for y in range(size):
    for x in range(size):
        grain = noise.randrange(-3, 4)
        edge = min(x, y, size - 1 - x, size - 1 - y)
        shade = -9 if edge < 5 else -4 if edge < 13 else 0
        pixels[x, y] = (230 + grain + shade, 217 + grain + shade, 188 + grain + shade)

image.save(target, format="PNG", optimize=True)
digest = hashlib.sha256(target.read_bytes()).hexdigest()
layout.write_text(
    json.dumps(
        {
            "id": "free_canvas_v2",
            "width": size,
            "height": size,
            "origin": "top_left",
            "coordinate_max": 65535,
            "reference_file": target.name,
            "reference_file_sha256": digest,
            "regions": [],
            "notes": "Whole square is drawable. The narrow edge is decorative and has no spell semantics.",
        },
        ensure_ascii=False,
        indent=2,
    ) + "\n",
    encoding="utf-8",
)
print(f"{target.name} SHA-256 {digest}")

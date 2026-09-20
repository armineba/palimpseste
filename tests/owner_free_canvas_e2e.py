"""One explicit owner-lab draw -> Astra -> Luna -> compiled spell smoke.

Uses a private one-time invitation. The captured image is synthetic test art,
so this proves the transport and compilation path, not the Player UI or an
artist's approval. No Codex credentials or API keys are read by this script.
"""

import gzip
import hashlib
import io
import json
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw


def sha(data):
    return hashlib.sha256(data).hexdigest()


def png(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


def event(sequence, op, previous, x, y):
    # Same canonical hash chain as the Unity journal contract.
    data = [sequence, op, x, y, 0, 125, 39, 31, 220, 29000, 1000, previous]
    digest = sha("|".join(map(str, data)).encode())
    return {
        "sequence": sequence, "op": op, "x": x, "y": y, "brush": 0,
        "r": 125, "g": 39, "b": 31, "a": 220,
        "diameter_milli": 29000, "pressure_milli": 1000,
        "previous_sha256": previous, "sha256": digest,
    }


def multipart(parts):
    boundary = "palimpseste" + uuid.uuid4().hex
    chunks = []
    for name, content_type, value, filename in parts:
        disposition = f'Content-Disposition: form-data; name="{name}"'
        if filename:
            disposition += f'; filename="{filename}"'
        chunks.extend([
            f"--{boundary}\r\n{disposition}\r\nContent-Type: {content_type}\r\n\r\n".encode(),
            value, b"\r\n",
        ])
    chunks.append(f"--{boundary}--\r\n".encode())
    return b"".join(chunks), "multipart/form-data; boundary=" + boundary


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: owner_free_canvas_e2e.py PRIVATE_INVITATION OUTPUT_EVIDENCE")
    invitation = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
    base = invitation["service_url"].rstrip("/")
    if base != "http://127.0.0.1:18080":
        raise RuntimeError("This test is restricted to the owner lab loopback API")
    token = None

    def call(method, path, data=None, content_type=None, key=None):
        headers = {}
        if token:
            headers["Authorization"] = "Bearer " + token
        if content_type:
            headers["Content-Type"] = content_type
        if key:
            headers["Idempotency-Key"] = key
        request = urllib.request.Request(base + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return response.status, dict(response.headers), response.read()
        except urllib.error.HTTPError as error:
            return error.code, dict(error.headers), error.read()

    def json_call(method, path, value=None, key=None):
        data = json.dumps(value, separators=(",", ":")).encode() if value is not None else None
        status, headers, body = call(method, path, data, "application/json" if data else None, key)
        return status, headers, json.loads(body) if body else None

    status, _, session = json_call("POST", "/v1/session/redeem", {
        "invitation_code": invitation["invitation_code"]
    })
    if status != 200 or not session.get("token"):
        raise RuntimeError(f"Invitation redeem failed: {status} {session}")
    token = session["token"]
    status, _, caps = json_call("GET", "/v1/capabilities")
    if status != 200 or caps.get("layout_version") != "free_canvas_v2":
        raise RuntimeError(f"Capabilities invalid: {status} {caps}")
    status, _, reference_png = call("GET", "/v1/artifacts/" + caps["reference_artifact_id"])
    if status != 200:
        raise RuntimeError(f"Reference fetch failed: {status}")
    reference = Image.open(io.BytesIO(reference_png)).convert("RGBA")
    if reference.size != (1024, 1024):
        raise RuntimeError("Reference dimensions invalid")

    status, _, parchment = json_call("POST", "/v1/parchments", {
        "layout_version": "free_canvas_v2"
    }, uuid.uuid4().hex)
    if status != 201:
        raise RuntimeError(f"Allocation failed: {status} {parchment}")
    parchment_id = parchment["parchment_id"]
    ink = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    brush = ImageDraw.Draw(ink, "RGBA")
    brush.line([(145, 720), (295, 620), (465, 485), (655, 355), (850, 210)],
               fill=(125, 39, 31, 220), width=29, joint="curve")
    brush.arc((790, 150, 900, 260), start=210, end=520,
              fill=(125, 39, 31, 220), width=16)
    drawing = Image.alpha_composite(reference, ink)
    drawing_png, ink_png = png(drawing), png(ink)
    points = [
        ("down", 9000, 45500), ("move", 18300, 39000),
        ("move", 29000, 31000), ("move", 41000, 22000),
        ("move", 53000, 13500), ("up", 53000, 13500),
        ("down", 52000, 12000), ("move", 55000, 15000),
        ("up", 55000, 15000), ("close", 0, 0),
    ]
    events = []
    previous = "0" * 64
    for sequence, (op, x, y) in enumerate(points, start=1):
        entry = event(sequence, op, previous, x, y)
        events.append(entry)
        previous = entry["sha256"]
    journal = gzip.compress(("\n".join(json.dumps(e, separators=(",", ":")) for e in events) + "\n").encode())
    status, _, begun = json_call("POST", f"/v1/parchments/{parchment_id}/begin", {
        "first_sequence": 1, "first_block_sha256": events[0]["sha256"]
    }, uuid.uuid4().hex)
    if status != 200 or begun.get("state") != "writing":
        raise RuntimeError(f"Begin failed: {status} {begun}")

    capture = {
        "schema_version": "sp.capture/1.0", "capture_id": uuid.uuid4().hex,
        "parchment_id": parchment_id, "layout_version": "free_canvas_v2",
        "reference_sha256": sha(reference_png), "raster_version": "cpu-brush/2.0",
        "drawing_file_sha256": sha(drawing_png),
        "drawing_pixel_sha256": sha(drawing.tobytes()),
        "ink_file_sha256": sha(ink_png), "journal_file_sha256": sha(journal),
        "width": 1024, "height": 1024, "used_ink_micro_units": 100000000,
        "closed_reason": "user_finished", "locked_regions": [],
        "created_at": datetime.now(timezone.utc).isoformat(),
    }
    body, content_type = multipart([
        ("capture", "application/json", json.dumps(capture, separators=(",", ":")).encode(), None),
        ("drawing", "image/png", drawing_png, "drawing.png"),
        ("ink", "image/png", ink_png, "ink.png"),
        ("journal", "application/gzip", journal, "journal.gz"),
    ])
    status, _, raw_job = call("PUT", f"/v1/parchments/{parchment_id}/capture", body,
                              content_type, uuid.uuid4().hex)
    job = json.loads(raw_job)
    if status != 202:
        raise RuntimeError(f"Capture failed: {status} {job}")
    job_id = job["job_id"]
    print(json.dumps({"event": "submitted", "job_id": job_id, "parchment_id": parchment_id}), flush=True)

    # A and B may each need a bounded repair run; keep polling beyond one
    # individual provider attempt so a slow but still live job is recorded.
    deadline = time.monotonic() + 3600
    previous_state = None
    while time.monotonic() < deadline:
        time.sleep(5)
        status, _, job = json_call("GET", "/v1/jobs/" + job_id)
        if status != 200:
            raise RuntimeError(f"Job fetch failed: {status}")
        state = job["state"]
        if state != previous_state:
            print(json.dumps({"event": "state", "state": state,
                              "resume_stage": job.get("resume_stage")}), flush=True)
            previous_state = state
        if state in ("ready", "needs_operator"):
            break
    else:
        raise TimeoutError("Job did not reach a terminal state within 60 minutes")

    evidence = {
        "kind": "free_canvas_owner_e2e", "created_at": datetime.now(timezone.utc).isoformat(),
        "principal_id": session["principal_id"], "parchment_id": parchment_id,
        "job_id": job_id, "state": job["state"], "resume_stage": job.get("resume_stage"),
        "error_code": job.get("error_code"), "description_artifact_id": job.get("description_artifact_id"),
        "spell_id": job.get("spell_id"), "layout_version": "free_canvas_v2",
        "reference_sha256": sha(reference_png), "drawing_sha256": sha(drawing_png),
        "ink_sha256": sha(ink_png), "journal_sha256": sha(journal),
    }
    if job.get("description_artifact_id"):
        status, _, description = call("GET", "/v1/artifacts/" + job["description_artifact_id"])
        if status == 200:
            parsed = json.loads(description)
            evidence["description_sha256"] = sha(description)
            evidence["description_title"] = parsed.get("title")
            evidence["shape_requests"] = len(parsed.get("shape_requests", []))
            evidence["observation_regions"] = sorted(set(x.get("region") for x in parsed.get("observations", [])))
    if job.get("spell_id"):
        status, _, spell = json_call("GET", "/v1/spells/" + job["spell_id"])
        evidence["spell_http_status"] = status
        if status == 200:
            evidence["spell_payload_sha256"] = sha(json.dumps(spell, sort_keys=True).encode())
            evidence["spell_model_a"] = spell.get("provenance", {}).get("model_a")
            evidence["spell_model_b"] = spell.get("provenance", {}).get("model_b")
    Path(sys.argv[2]).write_text(json.dumps(evidence, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"event": "complete", "state": evidence["state"],
                      "description_title": evidence.get("description_title"),
                      "spell_id": evidence.get("spell_id"),
                      "error_code": evidence.get("error_code")}, ensure_ascii=False), flush=True)
    if evidence["state"] != "ready" or evidence.get("spell_http_status") != 200:
        raise RuntimeError("New pipeline did not produce a downloadable spell")


if __name__ == "__main__":
    main()

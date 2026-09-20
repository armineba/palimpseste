"""Explicit local integration smoke. Requires a migrated disposable DB and a creator token.

Run with PALIMPSESTE_SMOKE_TOKEN and PALIMPSESTE_API_URL in the environment.
Creates one committed parchment in the configured database; never calls Codex.
"""

import gzip
import atexit
import hashlib
import io
import json
import os
import subprocess
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone

from PIL import Image


BASE = os.environ["PALIMPSESTE_API_URL"].rstrip("/")
TOKEN = os.environ["PALIMPSESTE_SMOKE_TOKEN"]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def call(method, path, body=None, content_type=None, key=None, authorized=True):
    headers = {}
    if authorized:
        headers["Authorization"] = "Bearer " + TOKEN
    if content_type:
        headers["Content-Type"] = content_type
    if key:
        headers["Idempotency-Key"] = key
    request = urllib.request.Request(BASE + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status, dict(response.headers), response.read()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read()


def json_call(method, path, value=None, key=None, authorized=True):
    body = json.dumps(value, separators=(",", ":")).encode() if value is not None else None
    status, headers, data = call(method, path, body, "application/json" if body else None, key, authorized)
    return status, headers, json.loads(data) if data else None


def multipart(parts):
    boundary = "palimpseste" + uuid.uuid4().hex
    chunks = []
    for name, content_type, value, filename in parts:
        disposition = f'Content-Disposition: form-data; name="{name}"'
        if filename:
            disposition += f'; filename="{filename}"'
        chunks.extend([f"--{boundary}\r\n{disposition}\r\nContent-Type: {content_type}\r\n\r\n".encode(), value, b"\r\n"])
    chunks.append(f"--{boundary}--\r\n".encode())
    return b"".join(chunks), "multipart/form-data; boundary=" + boundary


def png(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


def journal_event(sequence, op, previous, x=32768, y=32768):
    values = [sequence, op, x, y, 0, 125, 39, 31, 220, 1000, 1000, previous]
    digest = sha("|".join(map(str, values)).encode())
    return {
        "sequence": sequence, "op": op, "x": x, "y": y, "brush": 0,
        "r": 125, "g": 39, "b": 31, "a": 220,
        "diameter_milli": 1000, "pressure_milli": 1000,
        "previous_sha256": previous, "sha256": digest,
    }


def cleanup(parchment_id, allocation_key):
    """Remove only this run's rows/files, and only from palimpseste_test."""
    env_path = os.environ["PALIMPSESTE_TEST_DB_ENV"]
    line = next(x for x in open(env_path, encoding="utf-8-sig") if x.startswith("DATABASE_URL="))
    fields = dict(part.split("=", 1) for part in line.strip()[13:].split(";") if "=" in part)
    if fields.get("Database") != "palimpseste_test":
        raise RuntimeError("Smoke cleanup requires palimpseste_test")
    psql = os.environ.get("PALIMPSESTE_PSQL", r"C:\Program Files\PostgreSQL\17\bin\psql.exe")
    env = os.environ.copy()
    env["PGPASSWORD"] = fields["Password"]
    command = [psql, "-w", "-h", fields["Host"], "-U", fields["Username"], "-d", fields["Database"], "-v", "ON_ERROR_STOP=1", "-tA"]
    artifact_query = f"SELECT a.storage_key FROM artifacts a JOIN captures c ON a.id IN (c.drawing_artifact_id,c.ink_artifact_id,c.journal_artifact_id) WHERE c.parchment_id='{parchment_id}'::uuid;"
    paths = subprocess.run(command + ["-c", artifact_query], env=env, capture_output=True, text=True, check=True).stdout.splitlines()
    sql = f"""
    BEGIN;
    DO $$ BEGIN
      IF EXISTS (SELECT 1 FROM jobs WHERE parchment_id='{parchment_id}'::uuid AND state<>'queued') THEN
        RAISE EXCEPTION 'Smoke job was claimed; cleanup refused';
      END IF;
    END $$;
    CREATE TEMP TABLE smoke_artifacts ON COMMIT DROP AS
      SELECT unnest(ARRAY[c.drawing_artifact_id,c.ink_artifact_id,c.journal_artifact_id]) AS id
      FROM captures c WHERE c.parchment_id='{parchment_id}'::uuid;
    DELETE FROM idempotency_keys WHERE owner_id=(SELECT owner_id FROM parchments WHERE id='{parchment_id}'::uuid)
      AND ((operation='allocate_parchment' AND response_body->>'parchment_id'='{parchment_id}') OR operation IN ('begin:{parchment_id}','capture:{parchment_id}'));
    DELETE FROM jobs WHERE parchment_id='{parchment_id}'::uuid;
    DELETE FROM captures WHERE parchment_id='{parchment_id}'::uuid;
    DELETE FROM artifacts WHERE id IN (SELECT id FROM smoke_artifacts);
    DELETE FROM parchments WHERE id='{parchment_id}'::uuid;
    COMMIT;
    """
    subprocess.run(command, input=sql, env=env, capture_output=True, text=True, check=True)
    artifact_root = os.environ.get("PALIMPSESTE_TEST_ARTIFACT_ROOT")
    if artifact_root:
        for key in paths:
            path = os.path.abspath(os.path.join(artifact_root, key))
            if path.startswith(os.path.abspath(artifact_root) + os.sep) and os.path.isfile(path):
                os.unlink(path)


def main():
    assert call("GET", "/health/live")[0] == 200
    assert call("GET", "/health/ready")[0] == 200
    assert json_call("GET", "/v1/capabilities", authorized=False)[0] == 401
    status, _, caps = json_call("GET", "/v1/capabilities")
    assert status == 200 and len(caps["carriers"]) == 6 and len(caps["inks"]) == 4
    assert caps["layout_version"] == "free_canvas_v2"
    status, headers, reference_png = call("GET", "/v1/artifacts/" + caps["reference_artifact_id"])
    assert status == 200 and headers["X-Content-SHA256"] == sha(reference_png)

    allocation_key = uuid.uuid4().hex
    status, _, parchment = json_call("POST", "/v1/parchments", {"layout_version": "free_canvas_v2"}, allocation_key)
    assert status == 201, parchment
    status, _, duplicate = json_call("POST", "/v1/parchments", {"layout_version": "free_canvas_v2"}, allocation_key)
    assert status == 201 and duplicate == parchment
    parchment_id = parchment["parchment_id"]
    keep = os.environ.get("PALIMPSESTE_SMOKE_KEEP") == "1"
    if not keep:
        atexit.register(cleanup, parchment_id, allocation_key)

    first = journal_event(1, "down", "0" * 64)
    begin_key = uuid.uuid4().hex
    begin = {"first_sequence": 1, "first_block_sha256": first["sha256"]}
    status, _, begun = json_call("POST", f"/v1/parchments/{parchment_id}/begin", begin, begin_key)
    assert status == 200 and begun["state"] == "writing", begun
    status, _, repeat = json_call("POST", f"/v1/parchments/{parchment_id}/begin", begin, begin_key)
    assert status == 200 and repeat == begun
    status, _, conflict = json_call("POST", f"/v1/parchments/{parchment_id}/begin", {"first_sequence": 1, "first_block_sha256": "f" * 64}, begin_key)
    assert status == 409 and conflict["code"] == "idempotency_conflict", conflict

    reference = Image.open(io.BytesIO(reference_png)).convert("RGBA")
    ink = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    ink.putpixel((20, 20), (125, 39, 31, 220))
    drawing = reference.copy()
    ref = reference.getpixel((20, 20))
    alpha = 220 / 255.0
    drawing.putpixel((20, 20), tuple(round(ink.getpixel((20, 20))[i] * alpha + ref[i] * (1 - alpha)) for i in range(3)) + (255,))
    drawing_png, ink_png = png(drawing), png(ink)
    second = journal_event(2, "up", first["sha256"])
    third = journal_event(3, "close", second["sha256"])
    journal = gzip.compress(("\n".join(json.dumps(x, separators=(",", ":")) for x in (first, second, third)) + "\n").encode())
    capture = {
        "schema_version": "sp.capture/1.0", "capture_id": uuid.uuid4().hex, "parchment_id": parchment_id,
        "layout_version": "free_canvas_v2", "reference_sha256": sha(reference_png),
        "raster_version": "cpu-brush/2.0", "drawing_file_sha256": sha(drawing_png),
        "drawing_pixel_sha256": sha(drawing.tobytes()), "ink_file_sha256": sha(ink_png),
        "journal_file_sha256": sha(journal), "width": 1024, "height": 1024,
        "used_ink_micro_units": 100, "closed_reason": "user_finished", "locked_regions": [],
        "created_at": datetime.now(timezone.utc).isoformat(),
    }
    parts = [
        ("capture", "application/json", json.dumps(capture, separators=(",", ":")).encode(), None),
        ("drawing", "image/png", drawing_png, "drawing.png"),
        ("ink", "image/png", ink_png, "ink.png"),
        ("journal", "application/gzip", journal, "journal.gz"),
    ]
    bad_ink = ink.copy()
    bad_ink.putpixel((500, 500), (125, 39, 31, 220))
    bad_ink_png = png(bad_ink)
    bad_capture = dict(capture, capture_id=uuid.uuid4().hex, ink_file_sha256=sha(bad_ink_png))
    bad_parts = [
        ("capture", "application/json", json.dumps(bad_capture, separators=(",", ":")).encode(), None),
        ("drawing", "image/png", drawing_png, "drawing.png"),
        ("ink", "image/png", bad_ink_png, "ink.png"),
        ("journal", "application/gzip", journal, "journal.gz"),
    ]
    bad_body, bad_type = multipart(bad_parts)
    bad_status, _, bad_response = call("PUT", f"/v1/parchments/{parchment_id}/capture", bad_body, bad_type, uuid.uuid4().hex)
    assert bad_status == 422 and json.loads(bad_response)["code"] == "capture_incompatible"
    body, content_type = multipart(parts)
    capture_key = uuid.uuid4().hex
    status, _, result_bytes = call("PUT", f"/v1/parchments/{parchment_id}/capture", body, content_type, capture_key)
    result = json.loads(result_bytes)
    assert status == 202 and result["state"] == "queued", (status, result)
    repeat_body, repeat_type = multipart(parts)
    status, _, repeat_bytes = call("PUT", f"/v1/parchments/{parchment_id}/capture", repeat_body, repeat_type, capture_key)
    assert status == 202 and json.loads(repeat_bytes)["job_id"] == result["job_id"]
    other_capture = dict(capture, capture_id=uuid.uuid4().hex)
    other_parts = list(parts)
    other_parts[0] = ("capture", "application/json", json.dumps(other_capture, separators=(",", ":")).encode(), None)
    other_body, other_type = multipart(other_parts)
    status, _, conflict_bytes = call("PUT", f"/v1/parchments/{parchment_id}/capture", other_body, other_type, capture_key)
    assert status == 409 and json.loads(conflict_bytes)["code"] == "idempotency_conflict"
    status, _, job = json_call("GET", "/v1/jobs/" + result["job_id"])
    assert status == 200 and job["job_id"] == result["job_id"]
    status, _, page = json_call("GET", "/v1/parchments?limit=1")
    assert status == 200 and page["items"][0]["parchment_id"] == parchment_id
    status, _, unknown = json_call("GET", "/v1/jobs/" + uuid.uuid4().hex)
    assert status == 404 and unknown["code"] == "not_found"
    if not keep:
        cleanup(parchment_id, allocation_key)
        atexit.unregister(cleanup)
    print(json.dumps({"result": "passed", "parchment_id": parchment_id, "job_id": result["job_id"], "checks": 16, "cleanup": "preserved_for_restore" if keep else "completed"}))


if __name__ == "__main__":
    main()

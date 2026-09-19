"""Synthetic API contract smoke for the persisted Luna A description.

The fixture models the boundary after A has been validated and persisted. It
does not call Codex or Luna. It proves that the owner sees no description
artifact before A, receives one stable public id after A, can fetch only that
owned artifact with its verified hash, and cannot expose the job or artifact
to another principal.

Required environment variables:
  PALIMPSESTE_API_URL
  PALIMPSESTE_TEST_DB_ENV (DATABASE_URL=... for palimpseste_test)
  PALIMPSESTE_TEST_ARTIFACT_ROOT (the API's ARTIFACT_ROOT)
"""

import atexit
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.error
import urllib.request
import uuid


BASE = os.environ["PALIMPSESTE_API_URL"].rstrip("/")
ENV_FILE = os.environ["PALIMPSESTE_TEST_DB_ENV"]
ARTIFACT_ROOT = Path(os.environ["PALIMPSESTE_TEST_ARTIFACT_ROOT"]).resolve()
line = next(row for row in open(ENV_FILE, encoding="utf-8-sig") if row.startswith("DATABASE_URL="))
fields = dict(part.split("=", 1) for part in line.strip()[13:].split(";") if "=" in part)
if fields.get("Database") != "palimpseste_test":
    raise RuntimeError("Description access smoke is restricted to palimpseste_test")

PSQL = os.environ.get("PALIMPSESTE_PSQL", r"C:\Program Files\PostgreSQL\17\bin\psql.exe")
PG_ENV = os.environ.copy()
PG_ENV["PGPASSWORD"] = fields["Password"]
PG_ENV["PGCLIENTENCODING"] = "UTF8"
PG_COMMAND = [
    PSQL, "-w", "-h", fields["Host"], "-p", fields.get("Port", "5432"),
    "-U", fields["Username"], "-d", fields["Database"],
    "-v", "ON_ERROR_STOP=1", "-tA",
]

owner_id = uuid.uuid4().hex
other_id = uuid.uuid4().hex
owner_token = "synthetic-description-owner-" + uuid.uuid4().hex
other_token = "synthetic-description-other-" + uuid.uuid4().hex
owner_token_hash = hashlib.sha256(owner_token.encode()).hexdigest()
other_token_hash = hashlib.sha256(other_token.encode()).hexdigest()
parchment_id = uuid.uuid4().hex
capture_id = uuid.uuid4().hex
job_id = uuid.uuid4().hex
capture_artifact_id = uuid.uuid4().hex
description_artifact_id = uuid.uuid4().hex
raw_artifact_id = uuid.uuid4().hex

# Reuse the repository's schema-valid description fixture. The contract does
# not add parchment_id to sp.description/1.0; the client associates this JSON
# with the parchment_id carried by the Job DTO.
description_object = json.loads(
    (Path(__file__).resolve().parents[1] / "examples" / "01_description_illustrative.json")
    .read_text(encoding="utf-8")
)
description_bytes = json.dumps(description_object, separators=(",", ":"), ensure_ascii=True).encode("ascii")
description_hash = hashlib.sha256(description_bytes).hexdigest()
description_storage_key = f"{description_hash[:2]}/{description_artifact_id}.json"
capture_storage_key = f"aa/{capture_artifact_id}.bin"
raw_storage_key = f"bb/{raw_artifact_id}.bin"
description_path = (ARTIFACT_ROOT / description_storage_key).resolve()


def query(sql: str) -> str:
    result = subprocess.run(PG_COMMAND + ["-c", sql], env=PG_ENV, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"psql failed: {result.stderr.strip()}")
    return result.stdout.strip()


def request(token: str | None, method: str, path: str, key: str | None = None):
    headers = {}
    if token is not None:
        headers["Authorization"] = "Bearer " + token
    if key is not None:
        headers["Idempotency-Key"] = key
    call = urllib.request.Request(BASE + path, headers=headers, method=method)
    try:
        with urllib.request.urlopen(call, timeout=20) as response:
            return response.status, dict(response.headers), response.read()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read()


def json_request(token: str | None, method: str, path: str, key: str | None = None):
    status, headers, body = request(token, method, path, key)
    return status, headers, json.loads(body) if body else None


def header(headers, name: str):
    wanted = name.lower()
    return next((value for key, value in headers.items() if key.lower() == wanted), None)


def cleanup():
    query(f"""
    BEGIN;
    DELETE FROM interpretations WHERE job_id='{job_id}'::uuid;
    DELETE FROM jobs WHERE id='{job_id}'::uuid;
    DELETE FROM captures WHERE id='{capture_id}'::uuid;
    DELETE FROM artifacts WHERE id IN ('{capture_artifact_id}'::uuid,
                                       '{description_artifact_id}'::uuid,
                                       '{raw_artifact_id}'::uuid);
    DELETE FROM parchments WHERE id='{parchment_id}'::uuid;
    DELETE FROM idempotency_keys WHERE owner_id IN ('{owner_id}'::uuid,'{other_id}'::uuid);
    DELETE FROM lab_tokens WHERE principal_id IN ('{owner_id}'::uuid,'{other_id}'::uuid);
    DELETE FROM lab_principals WHERE id IN ('{owner_id}'::uuid,'{other_id}'::uuid);
    COMMIT;
    """)
    if description_path.is_file() and description_path.is_relative_to(ARTIFACT_ROOT):
        description_path.unlink()
        parent = description_path.parent
        if parent.is_relative_to(ARTIFACT_ROOT) and not any(parent.iterdir()):
            parent.rmdir()


atexit.register(cleanup)

ARTIFACT_ROOT.mkdir(parents=True, exist_ok=True)
description_path.parent.mkdir(parents=True, exist_ok=True)
description_path.write_bytes(description_bytes)

query(f"""
BEGIN;
INSERT INTO lab_principals(id,role,label) VALUES
  ('{owner_id}','player','description-smoke-owner'),
  ('{other_id}','player','description-smoke-other');
INSERT INTO lab_tokens(id,principal_id,token_sha256) VALUES
  ('{uuid.uuid4().hex}','{owner_id}',decode('{owner_token_hash}','hex')),
  ('{uuid.uuid4().hex}','{other_id}',decode('{other_token_hash}','hex'));
INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex)
VALUES('{parchment_id}','{owner_id}','processing','three_regions_v1',1000000,'0123456789abcdef');
INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length)
VALUES
  ('{capture_artifact_id}','{owner_id}','description_smoke_capture','{capture_storage_key}','{'c' * 64}','application/octet-stream',1),
  ('{description_artifact_id}','{owner_id}','description','{description_storage_key}','{description_hash}','application/json',{len(description_bytes)}),
  ('{raw_artifact_id}','{owner_id}','raw_unvalidated','{raw_storage_key}','{'d' * 64}','application/octet-stream',1);
INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,
  drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id)
VALUES('{capture_id}','{parchment_id}','{owner_id}','{{}}','{'e' * 64}','{'f' * 64}',
  '{capture_artifact_id}','{capture_artifact_id}','{capture_artifact_id}','{capture_artifact_id}');
INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state,resume_stage,message)
VALUES('{job_id}','{owner_id}','{parchment_id}','{capture_id}','production','queued','interpretation','Waiting for A');
COMMIT;
""")

status, _, before = json_request(owner_token, "GET", f"/v1/jobs/{job_id}")
assert status == 200 and before["description_artifact_id"] is None, (status, before)

# The row below is the durable boundary produced by SaveDescriptionAsync after
# schema validation. The API must never infer a description id from a raw
# artifact that is not linked through interpretations.
query(f"""
INSERT INTO interpretations(id,job_id,provider_attempt_id,description,description_artifact_id,
  description_sha256,prompt_version,input_sha256)
VALUES('{uuid.uuid4().hex}','{job_id}',NULL,'{description_bytes.decode("utf-8")}'::jsonb,
  '{description_artifact_id}','{description_hash}','sp.prompt.a/1.1','{'1' * 64}');
UPDATE jobs SET state='resolving_geometry',message='A persisted' WHERE id='{job_id}'::uuid;
""")

public_id = "a" + description_artifact_id
for state in ("resolving_geometry", "planning", "validating", "waiting_retry", "needs_operator", "ready"):
    query(f"UPDATE jobs SET state='{state}' WHERE id='{job_id}'::uuid;")
    status, _, job = json_request(owner_token, "GET", f"/v1/jobs/{job_id}")
    assert status == 200 and job["description_artifact_id"] == public_id and job["state"] == state, (status, job)

# The asynchronous resume response uses the same DTO and must retain the
# already persisted description id as well.
query(f"UPDATE jobs SET state='waiting_retry',retryable=true WHERE id='{job_id}'::uuid;")
status, _, resumed = json_request(owner_token, "POST", f"/v1/jobs/{job_id}/resume", uuid.uuid4().hex)
assert status == 202 and resumed["description_artifact_id"] == public_id, (status, resumed)

status, headers, body = request(owner_token, "GET", f"/v1/artifacts/{public_id}")
assert status == 200 and body == description_bytes, (status, body[:100])
assert header(headers, "X-Content-SHA256") == description_hash, headers
assert header(headers, "Content-Type").startswith("application/json"), headers

status, _, body = request(other_token, "GET", f"/v1/jobs/{job_id}")
assert status == 404, (status, body)
status, _, body = request(other_token, "GET", f"/v1/artifacts/{public_id}")
assert status == 404, (status, body)
status, _, body = request(None, "GET", f"/v1/artifacts/{public_id}")
assert status == 401, (status, body)

# The unvalidated artifact is owned but never linked to the interpretation and
# therefore cannot appear in the Job DTO.
status, _, job = json_request(owner_token, "GET", f"/v1/jobs/{job_id}")
assert status == 200 and job["description_artifact_id"] != "a" + raw_artifact_id

cleanup()
atexit.unregister(cleanup)
print(json.dumps({
    "result": "passed",
    "checks": 18,
    "synthetic": True,
    "model_calls_executed": False,
    "description_artifact_id": public_id,
    "stable_states": ["resolving_geometry", "planning", "validating", "waiting_retry", "needs_operator", "ready"],
    "ownership": "owner_only",
    "hash_header": "X-Content-SHA256",
    "cleanup": "completed",
}))

"""Synthetic owner/hash/idempotence smoke; no Luna call and no human verdict.

Requires a running test API, PALIMPSESTE_API_URL and PALIMPSESTE_TEST_DB_ENV.
The script refuses any database other than palimpseste_test. It only creates
and removes its own synthetic rows. Migration 005 must be applied first.
"""

import atexit
import hashlib
import json
import os
import subprocess
import urllib.error
import urllib.request
import uuid


base = os.environ["PALIMPSESTE_API_URL"].rstrip("/")
env_file = os.environ["PALIMPSESTE_TEST_DB_ENV"]
line = next(row for row in open(env_file, encoding="utf-8-sig") if row.startswith("DATABASE_URL="))
fields = dict(part.split("=", 1) for part in line.strip()[13:].split(";") if "=" in part)
if fields.get("Database") != "palimpseste_test":
    raise RuntimeError("Feedback smoke is restricted to palimpseste_test")

psql = os.environ.get("PALIMPSESTE_PSQL", r"C:\Program Files\PostgreSQL\17\bin\psql.exe")
pg_env = os.environ.copy()
pg_env["PGPASSWORD"] = fields["Password"]
pg_cmd = [psql, "-w", "-h", fields["Host"], "-p", fields.get("Port", "5432"),
          "-U", fields["Username"], "-d", fields["Database"], "-v", "ON_ERROR_STOP=1", "-tA"]


def query(sql):
    result = subprocess.run(pg_cmd + ["-c", sql], env=pg_env, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError("PostgreSQL operation failed")
    return result.stdout.strip()


owner, other, parchment, capture, job, artifact, description_artifact, interpretation = [
    uuid.uuid4().hex for _ in range(8)
]
owner_token = "synthetic-feedback-owner-" + uuid.uuid4().hex
other_token = "synthetic-feedback-other-" + uuid.uuid4().hex
description_sha = "a" * 64
wrong_sha = "b" * 64
path = f"/v1/jobs/{job}/interpretation-feedback"


def request(token, key, payload):
    data = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    headers = {"Authorization": "Bearer " + token, "Idempotency-Key": key,
               "Content-Type": "application/json"}
    call = urllib.request.Request(base + path, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(call, timeout=20) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error)


def cleanup():
    query(f"""
    BEGIN;
    DELETE FROM interpretation_feedback WHERE job_id='{job}'::uuid;
    DELETE FROM idempotency_keys WHERE owner_id IN ('{owner}'::uuid,'{other}'::uuid)
      AND operation='interpretation_feedback:{job}';
    DELETE FROM interpretations WHERE job_id='{job}'::uuid;
    DELETE FROM jobs WHERE id='{job}'::uuid;
    DELETE FROM captures WHERE id='{capture}'::uuid;
    DELETE FROM artifacts WHERE id IN ('{artifact}'::uuid,'{description_artifact}'::uuid);
    DELETE FROM parchments WHERE id='{parchment}'::uuid;
    DELETE FROM lab_tokens WHERE principal_id IN ('{owner}'::uuid,'{other}'::uuid);
    DELETE FROM lab_principals WHERE id IN ('{owner}'::uuid,'{other}'::uuid);
    COMMIT;
    """)


atexit.register(cleanup)
query(f"""
BEGIN;
INSERT INTO lab_principals(id,role,label) VALUES
  ('{owner}','player','feedback-smoke-owner'),('{other}','player','feedback-smoke-other');
INSERT INTO lab_tokens(id,principal_id,token_sha256) VALUES
  ('{uuid.uuid4().hex}','{owner}',decode('{hashlib.sha256(owner_token.encode()).hexdigest()}','hex')),
  ('{uuid.uuid4().hex}','{other}',decode('{hashlib.sha256(other_token.encode()).hexdigest()}','hex'));
INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex)
VALUES('{parchment}','{owner}','processing','three_regions_v1',1000000,'0123456789abcdef');
INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length)
VALUES ('{artifact}','{owner}','synthetic_capture','aa/{artifact}.bin','{'c'*64}','application/octet-stream',1),
       ('{description_artifact}','{owner}','description','bb/{description_artifact}.json','{description_sha}','application/json',2);
INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,
  drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id)
VALUES('{capture}','{parchment}','{owner}','{{}}','{'d'*64}','{'e'*64}',
  '{artifact}','{artifact}','{artifact}','{artifact}');
INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state)
VALUES('{job}','{owner}','{parchment}','{capture}','production','interpreting');
COMMIT;
""")

payload = {"description_sha256": description_sha, "verdict": "incorrect",
           "correction": "Le trait n'est pas du feu."}
first_key = uuid.uuid4().hex
status, body = request(owner_token, first_key, payload)
assert status == 409 and body["code"] == "interpretation_pending", (status, body)

query(f"""
INSERT INTO interpretations(id,job_id,description,description_artifact_id,
  description_sha256,prompt_version,input_sha256)
VALUES('{interpretation}','{job}','{{}}','{description_artifact}',
  '{description_sha}','sp.prompt.a/1.1','{'f'*64}');
""")
status, body = request(other_token, uuid.uuid4().hex, payload)
assert status == 404 and body["code"] == "not_found", (status, body)

wrong = dict(payload, description_sha256=wrong_sha)
status, body = request(owner_token, uuid.uuid4().hex, wrong)
assert status == 409 and body["code"] == "description_changed", (status, body)

status, recorded = request(owner_token, first_key, payload)
assert status == 201 and recorded["recorded"] is True, (status, recorded)
status, replay = request(owner_token, first_key, payload)
assert status == 201 and replay == recorded, (status, replay)
status, body = request(owner_token, first_key, dict(payload, correction="Autre lecture"))
assert status == 409 and body["code"] == "idempotency_conflict", (status, body)
status, body = request(owner_token, uuid.uuid4().hex, payload)
assert status == 409 and body["code"] == "feedback_already_recorded", (status, body)

persisted = query(f"SELECT description_sha256||'|'||prompt_version||'|'||verdict||'|'||correction "
                  f"FROM interpretation_feedback WHERE job_id='{job}'::uuid")
assert persisted == description_sha + "|sp.prompt.a/1.1|incorrect|" + payload["correction"]
assert query(f"SELECT state||'|'||attempt_count FROM jobs WHERE id='{job}'::uuid") == "interpreting|0"

cleanup()
atexit.unregister(cleanup)
print(json.dumps({"result": "passed", "synthetic": True, "model_calls_executed": False,
                  "checks": 9, "owner_only": True, "job_unchanged": True, "cleanup": "completed"}))

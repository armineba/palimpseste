"""Disposable HTTP/DB smoke for the review gate; synthetic data is never a human verdict.

Requires PALIMPSESTE_API_URL, PALIMPSESTE_SMOKE_TOKEN, PALIMPSESTE_TEST_DB_ENV.
The database must be the migrated palimpseste_test database and the token a creator.
"""

import atexit
import hashlib
import json
import os
import subprocess
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone


base = os.environ["PALIMPSESTE_API_URL"].rstrip("/")
token = os.environ["PALIMPSESTE_SMOKE_TOKEN"]
env_file = os.environ["PALIMPSESTE_TEST_DB_ENV"]
line = next(row for row in open(env_file, encoding="utf-8-sig") if row.startswith("DATABASE_URL="))
fields = dict(part.split("=", 1) for part in line.strip()[13:].split(";") if "=" in part)
if fields.get("Database") != "palimpseste_test":
    raise RuntimeError("Review smoke is restricted to palimpseste_test")

psql = os.environ.get("PALIMPSESTE_PSQL", r"C:\Program Files\PostgreSQL\17\bin\psql.exe")
pg_env = os.environ.copy()
pg_env["PGPASSWORD"] = fields["Password"]
pg_cmd = [
    psql, "-w", "-h", fields["Host"], "-p", fields.get("Port", "5432"),
    "-U", fields["Username"], "-d", fields["Database"],
    "-v", "ON_ERROR_STOP=1", "-tA",
]


def query(sql):
    return subprocess.run(pg_cmd + ["-c", sql], env=pg_env, capture_output=True,
                          text=True, check=True).stdout.strip()


def request(value, key):
    data = json.dumps(value, separators=(",", ":")).encode()
    headers = {"Authorization": "Bearer " + token, "Content-Type": "application/json",
               "Idempotency-Key": key}
    call = urllib.request.Request(base + "/v1/reviews", data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(call, timeout=20) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error)


token_hash = hashlib.sha256(token.encode()).hexdigest()
owner = query("SELECT p.id || '|' || p.role FROM lab_principals p JOIN lab_tokens t "
              "ON t.principal_id=p.id WHERE t.token_sha256=decode('" + token_hash +
              "','hex') AND t.revoked_at IS NULL")
if not owner.endswith("|creator"):
    raise RuntimeError("The supplied token must belong to a creator")
owner_id = owner.split("|", 1)[0].replace("-", "")
parchment, capture, job, artifact, spell = [uuid.uuid4().hex for _ in range(5)]
key = uuid.uuid4().hex
sha = "a" * 64
cleanup_sql = f"""
BEGIN;
DELETE FROM reviews WHERE owner_id='{owner_id}'::uuid AND payload->>'spell_id'='{spell}';
DELETE FROM idempotency_keys WHERE owner_id='{owner_id}'::uuid AND operation='submit_review' AND key='{key}';
DELETE FROM spells WHERE id='{spell}'::uuid;
DELETE FROM jobs WHERE id='{job}'::uuid;
DELETE FROM captures WHERE id='{capture}'::uuid;
DELETE FROM artifacts WHERE id='{artifact}'::uuid;
DELETE FROM parchments WHERE id='{parchment}'::uuid;
COMMIT;
"""


def cleanup():
    query(cleanup_sql)


atexit.register(cleanup)
query(f"""
BEGIN;
INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex)
VALUES('{parchment}','{owner_id}','ready','three_regions_v1',1000000,'0123456789abcdef');
INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length)
VALUES('{artifact}','{owner_id}','review_smoke','review-smoke/{artifact}.json','{sha}','application/json',2);
INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,
  drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id)
VALUES('{capture}','{parchment}','{owner_id}','{{}}','{sha}','{sha}',
  '{artifact}','{artifact}','{artifact}','{artifact}');
INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state)
VALUES('{job}','{owner_id}','{parchment}','{capture}','production','ready');
INSERT INTO spells(id,owner_id,parchment_id,job_id,payload_artifact_id,payload_sha256,rules_profile,catalog_version)
VALUES('{spell}','{owner_id}','{parchment}','{job}','{artifact}','{sha}','lab_v1','sp.capabilities/1.0');
COMMIT;
""")

review = {
    "schema_version": "sp.review/1.0", "case_id": "synthetic.review-smoke",
    "spell_id": spell, "reviewer_id": owner_id, "submitted_by_human": True,
    "verdict": "changes_requested", "build_commit": "synthetic-test-only",
    "capture_sha256": sha, "description_sha256": sha, "plan_sha256": sha,
    "compiled_sha256": sha,
    "judgements": {"drawing_fidelity": "uncertain", "mechanical_coherence": "uncertain",
                   "visual_identity": "uncertain", "presentation": "uncertain"},
    "comment": "Synthetic API fixture; no human assessment or Luna call.",
    "evidence_files": ["synthetic-fixture"],
    "created_at": datetime.now(timezone.utc).isoformat(),
}
invalid = dict(review, submitted_by_human=False)
status, body = request(invalid, uuid.uuid4().hex)
assert status == 422 and body["code"] == "review_invalid", (status, body)
status, body = request(dict(review, spell_id=uuid.uuid4().hex), uuid.uuid4().hex)
assert status == 404 and body["code"] == "not_found", (status, body)
status, created = request(review, key)
assert status == 201 and "review_id" in created, (status, created)
status, replay = request(review, key)
assert status == 201 and replay == created, (status, replay)
status, conflict = request(dict(review, comment="different fixture"), key)
assert status == 409 and conflict["code"] == "idempotency_conflict", (status, conflict)
count = query("SELECT count(*) FROM reviews WHERE owner_id='" + owner_id +
              "'::uuid AND payload->>'spell_id'='" + spell + "'")
assert count == "1", count
cleanup()
atexit.unregister(cleanup)
print(json.dumps({"result": "passed", "checks": 6, "synthetic": True, "cleanup": "completed"}))

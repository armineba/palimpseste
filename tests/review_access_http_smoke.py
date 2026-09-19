"""Synthetic two-creator review delegation smoke.

Requires PALIMPSESTE_API_URL and PALIMPSESTE_TEST_DB_ENV. The script creates
two disposable creator principals and tokens directly in palimpseste_test,
then removes them and all fixture rows. It never calls Codex or Luna and does
not represent a human verdict.
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
env_file = os.environ["PALIMPSESTE_TEST_DB_ENV"]
line = next(row for row in open(env_file, encoding="utf-8-sig") if row.startswith("DATABASE_URL="))
fields = dict(part.split("=", 1) for part in line.strip()[13:].split(";") if "=" in part)
if fields.get("Database") != "palimpseste_test":
    raise RuntimeError("Review access smoke is restricted to palimpseste_test")

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


def request(token, method, path, value=None, key=None):
    body = json.dumps(value, separators=(",", ":")).encode() if value is not None else None
    headers = {"Authorization": "Bearer " + token}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if key is not None:
        headers["Idempotency-Key"] = key
    call = urllib.request.Request(base + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(call, timeout=20) as response:
            data = response.read()
            return response.status, json.loads(data) if data else None
    except urllib.error.HTTPError as error:
        data = error.read()
        return error.code, json.loads(data) if data else None


owner_id = uuid.uuid4().hex
reviewer_id = uuid.uuid4().hex
owner_token = "synthetic-owner-" + uuid.uuid4().hex
reviewer_token = "synthetic-reviewer-" + uuid.uuid4().hex
owner_token_hash = hashlib.sha256(owner_token.encode()).hexdigest()
reviewer_token_hash = hashlib.sha256(reviewer_token.encode()).hexdigest()
parchment = uuid.uuid4().hex
capture = uuid.uuid4().hex
job = uuid.uuid4().hex
artifact = uuid.uuid4().hex
spell = uuid.uuid4().hex
case_id = "two-creators-review"
grant_key = uuid.uuid4().hex
reviewer_before_key = uuid.uuid4().hex
review_key = uuid.uuid4().hex
review_replay_key = review_key
revoke_key = uuid.uuid4().hex
reviewer_after_key = uuid.uuid4().hex
owner_review_key = uuid.uuid4().hex
spoof_key = uuid.uuid4().hex
non_owner_grant_key = uuid.uuid4().hex
bad_case_key = uuid.uuid4().hex
sha = "a" * 64


def cleanup():
    sql = f"""
    BEGIN;
    DELETE FROM idempotency_keys
      WHERE owner_id IN ('{owner_id}'::uuid,'{reviewer_id}'::uuid)
        AND (operation = 'submit_review' OR operation LIKE 'spell_review_access:%'
             OR operation LIKE 'spell_review_access_revoke:%');
    DELETE FROM reviews WHERE payload->>'spell_id'='{spell}' AND owner_id IN ('{owner_id}'::uuid,'{reviewer_id}'::uuid);
    DELETE FROM spell_review_grants WHERE spell_id='{spell}'::uuid;
    DELETE FROM spells WHERE id='{spell}'::uuid;
    DELETE FROM jobs WHERE id='{job}'::uuid;
    DELETE FROM captures WHERE id='{capture}'::uuid;
    DELETE FROM artifacts WHERE id='{artifact}'::uuid;
    DELETE FROM parchments WHERE id='{parchment}'::uuid;
    DELETE FROM lab_tokens WHERE principal_id IN ('{owner_id}'::uuid,'{reviewer_id}'::uuid);
    DELETE FROM lab_principals WHERE id IN ('{owner_id}'::uuid,'{reviewer_id}'::uuid);
    COMMIT;
    """
    query(sql)


atexit.register(cleanup)

query(f"""
BEGIN;
INSERT INTO lab_principals(id,role,label) VALUES
  ('{owner_id}','creator','review-access-owner'),
  ('{reviewer_id}','creator','review-access-reviewer');
INSERT INTO lab_tokens(id,principal_id,token_sha256) VALUES
  ('{uuid.uuid4().hex}','{owner_id}',decode('{owner_token_hash}','hex')),
  ('{uuid.uuid4().hex}','{reviewer_id}',decode('{reviewer_token_hash}','hex'));
INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex)
VALUES('{parchment}','{owner_id}','ready','three_regions_v1',1000000,'0123456789abcdef');
INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length)
VALUES('{artifact}','{owner_id}','review_access_smoke','review-access/{artifact}.json','{sha}','application/json',2);
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
    "schema_version": "sp.review/1.0", "case_id": case_id, "spell_id": spell,
    "reviewer_id": reviewer_id, "submitted_by_human": True,
    "verdict": "changes_requested", "build_commit": "synthetic-test-only",
    "capture_sha256": sha, "description_sha256": sha, "plan_sha256": sha,
    "compiled_sha256": sha,
    "judgements": {"drawing_fidelity": "uncertain", "mechanical_coherence": "uncertain",
                   "visual_identity": "uncertain", "presentation": "uncertain"},
    "comment": "Synthetic delegation fixture; no human assessment or Luna call.",
    "evidence_files": ["synthetic-review-access"],
    "created_at": datetime.now(timezone.utc).isoformat(),
}

spoofed_review = dict(review, reviewer_id=owner_id)
status, body = request(reviewer_token, "POST", "/v1/reviews", spoofed_review, spoof_key)
assert status == 403 and body["code"] == "reviewer_identity_mismatch", (status, body)

status, body = request(reviewer_token, "POST", f"/v1/spells/{spell}/reviewers",
                       {"reviewer_principal_id": owner_id, "case_id": case_id}, non_owner_grant_key)
assert status == 404 and body["code"] == "not_found", (status, body)

status, body = request(owner_token, "POST", f"/v1/spells/{spell}/reviewers",
                       {"reviewer_principal_id": reviewer_id, "case_id": "Bad Case"}, bad_case_key)
assert status == 422 and body["code"] == "review_access_invalid", (status, body)
assert query(f"SELECT count(*) FROM spell_review_grants WHERE spell_id='{spell}'::uuid") == "0"

status, body = request(reviewer_token, "POST", "/v1/reviews", review, reviewer_before_key)
assert status == 404 and body["code"] == "not_found", (status, body)

grant = {"reviewer_principal_id": reviewer_id, "case_id": case_id}
status, body = request(owner_token, "POST", f"/v1/spells/{spell}/reviewers", grant, grant_key)
assert status == 201 and body["granted"] is True, (status, body)
assert query(f"SELECT count(*) FROM spell_review_grants WHERE spell_id='{spell}'::uuid") == "1"

# A grant authorizes submission for this case only. It does not open the
# owner-only spell read route to every creator.
status, body = request(reviewer_token, "GET", f"/v1/spells/{spell}")
assert status == 404 and body["code"] == "not_found", (status, body)
status, body = request(reviewer_token, "POST", "/v1/reviews", review, review_key)
assert status == 201 and "review_id" in body, (status, body)
status, replay = request(reviewer_token, "POST", "/v1/reviews", review, review_replay_key)
assert status == 201 and replay == body, (status, replay)

status, body = request(owner_token, "DELETE", f"/v1/spells/{spell}/reviewers/{reviewer_id}?case_id={case_id}", None, revoke_key)
assert status == 200 and body["revoked"] is True, (status, body)
assert query(f"SELECT count(*) FROM spell_review_grants WHERE spell_id='{spell}'::uuid") == "0"

status, body = request(reviewer_token, "POST", "/v1/reviews", review, reviewer_after_key)
assert status == 404 and body["code"] == "not_found", (status, body)

# The owner path remains valid after delegation is removed.
owner_review = dict(review, reviewer_id=owner_id)
status, body = request(owner_token, "POST", "/v1/reviews", owner_review, owner_review_key)
assert status == 201 and "review_id" in body, (status, body)

count = query(f"SELECT count(*) FROM reviews WHERE payload->>'spell_id'='{spell}'")
assert count == "2", count
cleanup()
atexit.unregister(cleanup)
print(json.dumps({"result": "passed", "checks": 13, "synthetic": True,
                  "distinct_creators": True, "grant_case": case_id,
                  "read_scope": "owner_only", "cleanup": "completed"}))

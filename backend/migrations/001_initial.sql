-- Palimpseste SP-1.1 laboratory schema. Run explicitly before API/worker startup.
BEGIN;

CREATE TABLE IF NOT EXISTS lab_principals (
    id uuid PRIMARY KEY,
    role text NOT NULL CHECK (role IN ('player', 'creator')),
    label text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS lab_tokens (
    id uuid PRIMARY KEY,
    principal_id uuid NOT NULL REFERENCES lab_principals(id),
    token_sha256 bytea NOT NULL UNIQUE CHECK (octet_length(token_sha256) = 32),
    created_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz
);

CREATE TABLE IF NOT EXISTS parchments (
    id uuid PRIMARY KEY,
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    state text NOT NULL CHECK (state IN ('blank', 'writing', 'capture_received', 'processing', 'ready', 'incident')),
    layout_version text NOT NULL CHECK (layout_version = 'three_regions_v1'),
    budget_micro_units bigint NOT NULL CHECK (budget_micro_units > 0),
    signature_seed_hex text NOT NULL CHECK (signature_seed_hex ~ '^[0-9a-f]{16}$'),
    first_sequence bigint,
    first_block_sha256 text CHECK (first_block_sha256 ~ '^[0-9a-f]{64}$'),
    first_written_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK ((first_sequence IS NULL) = (first_block_sha256 IS NULL)),
    CHECK (first_sequence IS NULL OR first_sequence > 0)
);
CREATE INDEX IF NOT EXISTS parchments_owner_page ON parchments(owner_id, created_at DESC, id DESC);

CREATE TABLE IF NOT EXISTS artifacts (
    id uuid PRIMARY KEY,
    owner_id uuid REFERENCES lab_principals(id),
    kind text NOT NULL,
    storage_key text NOT NULL UNIQUE,
    sha256 text NOT NULL CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    content_type text NOT NULL,
    byte_length bigint NOT NULL CHECK (byte_length >= 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK (storage_key !~ '(^/|\\|\.\.)')
);
CREATE INDEX IF NOT EXISTS artifacts_owner_idx ON artifacts(owner_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS artifacts_one_reference ON artifacts(kind) WHERE owner_id IS NULL AND kind = 'reference_layout';

CREATE TABLE IF NOT EXISTS captures (
    id uuid PRIMARY KEY,
    parchment_id uuid NOT NULL UNIQUE REFERENCES parchments(id),
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    manifest jsonb NOT NULL,
    manifest_sha256 text NOT NULL CHECK (manifest_sha256 ~ '^[0-9a-f]{64}$'),
    request_sha256 text NOT NULL CHECK (request_sha256 ~ '^[0-9a-f]{64}$'),
    drawing_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    ink_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    journal_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    reference_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS captures_owner_idx ON captures(owner_id, id);

CREATE TABLE IF NOT EXISTS jobs (
    id uuid PRIMARY KEY,
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    parchment_id uuid REFERENCES parchments(id),
    capture_id uuid UNIQUE REFERENCES captures(id),
    kind text NOT NULL DEFAULT 'production' CHECK (kind IN ('production', 'authoring')),
    state text NOT NULL CHECK (state IN ('queued', 'interpreting', 'resolving_geometry', 'planning', 'validating', 'ready', 'waiting_retry', 'needs_operator')),
    resume_stage text,
    fence_token bigint NOT NULL DEFAULT 0 CHECK (fence_token >= 0),
    lease_until timestamptz,
    leased_by text,
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count BETWEEN 0 AND 10),
    next_attempt_at timestamptz,
    error_code text,
    retryable boolean NOT NULL DEFAULT false,
    message text NOT NULL DEFAULT '',
    spell_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK ((kind = 'production' AND parchment_id IS NOT NULL AND capture_id IS NOT NULL)
        OR (kind = 'authoring' AND parchment_id IS NULL AND capture_id IS NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS jobs_one_production_per_parchment ON jobs(parchment_id) WHERE kind = 'production';
CREATE INDEX IF NOT EXISTS jobs_claim_idx ON jobs(state, next_attempt_at, lease_until, created_at);
CREATE INDEX IF NOT EXISTS jobs_owner_idx ON jobs(owner_id, id);

CREATE TABLE IF NOT EXISTS provider_attempts (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id),
    stage text NOT NULL CHECK (stage IN ('A', 'B', 'repair_A', 'repair_B')),
    fence_token bigint NOT NULL CHECK (fence_token >= 0),
    status text NOT NULL,
    requested_model text NOT NULL,
    requested_effort text NOT NULL,
    sent_config jsonb NOT NULL DEFAULT '{}'::jsonb,
    reported_model text,
    reported_effort text,
    codex_version text,
    session_id text,
    input_sha256 text CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
    output_sha256 text CHECK (output_sha256 ~ '^[0-9a-f]{64}$'),
    protected_output_artifact_id uuid REFERENCES artifacts(id),
    usage jsonb,
    started_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz,
    error_code text
);
CREATE INDEX IF NOT EXISTS provider_attempts_job_idx ON provider_attempts(job_id, started_at);

CREATE TABLE IF NOT EXISTS interpretations (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL UNIQUE REFERENCES jobs(id),
    provider_attempt_id uuid REFERENCES provider_attempts(id),
    description jsonb NOT NULL,
    description_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
    prompt_version text NOT NULL,
    input_sha256 text NOT NULL CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS geometry_assets (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id),
    artifact_id uuid NOT NULL REFERENCES artifacts(id),
    geometry_id text NOT NULL,
    metadata jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(job_id, geometry_id)
);

CREATE TABLE IF NOT EXISTS geometry_masks (
    job_id uuid NOT NULL REFERENCES jobs(id),
    file_name text NOT NULL CHECK (file_name ~ '^[a-z][a-z0-9_.-]*\.png$' AND position('..' in file_name) = 0),
    artifact_id uuid NOT NULL REFERENCES artifacts(id),
    PRIMARY KEY(job_id, file_name)
);

CREATE TABLE IF NOT EXISTS spell_plans (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id),
    provider_attempt_id uuid REFERENCES provider_attempts(id),
    revision integer NOT NULL CHECK (revision >= 0),
    plan jsonb NOT NULL,
    plan_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    plan_sha256 text NOT NULL CHECK (plan_sha256 ~ '^[0-9a-f]{64}$'),
    validation_errors jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(job_id, revision)
);

CREATE TABLE IF NOT EXISTS authoring_inputs (
    job_id uuid PRIMARY KEY REFERENCES jobs(id),
    source_job_id uuid NOT NULL REFERENCES jobs(id),
    description_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    description_sha256 text NOT NULL CHECK (description_sha256 ~ '^[0-9a-f]{64}$'),
    geometry_artifact_ids jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS spells (
    id uuid PRIMARY KEY,
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    parchment_id uuid NOT NULL UNIQUE REFERENCES parchments(id),
    job_id uuid NOT NULL UNIQUE REFERENCES jobs(id),
    payload_artifact_id uuid NOT NULL REFERENCES artifacts(id),
    payload_sha256 text NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
    rules_profile text NOT NULL,
    catalog_version text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE jobs ADD CONSTRAINT jobs_spell_fk FOREIGN KEY (spell_id) REFERENCES spells(id) DEFERRABLE INITIALLY DEFERRED;

CREATE TABLE IF NOT EXISTS reviews (
    id uuid PRIMARY KEY,
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    payload jsonb NOT NULL,
    payload_sha256 text NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS idempotency_keys (
    owner_id uuid NOT NULL REFERENCES lab_principals(id),
    operation text NOT NULL,
    key text NOT NULL CHECK (length(key) BETWEEN 16 AND 120),
    request_sha256 text NOT NULL CHECK (request_sha256 ~ '^[0-9a-f]{64}$'),
    response_status integer NOT NULL CHECK (response_status BETWEEN 200 AND 299),
    response_body jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(owner_id, operation, key)
);

COMMIT;

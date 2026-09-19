-- One-use, short-lived invitations bootstrap a player without putting a
-- reusable laboratory credential in the Unity build.
BEGIN;

CREATE TABLE IF NOT EXISTS lab_invitations (
    id uuid PRIMARY KEY,
    invitation_sha256 bytea NOT NULL UNIQUE CHECK (octet_length(invitation_sha256) = 32),
    label text NOT NULL CHECK (length(label) BETWEEN 1 AND 120),
    expires_at timestamptz NOT NULL,
    redeemed_at timestamptz,
    principal_id uuid REFERENCES lab_principals(id),
    token_id uuid REFERENCES lab_tokens(id),
    CHECK ((redeemed_at IS NULL AND principal_id IS NULL AND token_id IS NULL)
        OR (redeemed_at IS NOT NULL AND principal_id IS NOT NULL AND token_id IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS lab_invitations_expires_idx ON lab_invitations(expires_at)
    WHERE redeemed_at IS NULL;

COMMIT;

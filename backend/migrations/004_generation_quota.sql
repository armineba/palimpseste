-- Sliding 24-hour generation admission scans jobs committed in the window.
-- The API serializes admissions with a PostgreSQL transaction advisory lock.
CREATE INDEX IF NOT EXISTS jobs_created_at_quota_idx ON jobs(created_at, owner_id);

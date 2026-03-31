ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS founded integer;

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS is_national boolean;

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_address character varying(300);

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_capacity integer;

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_city character varying(200);

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_image_url character varying(500);

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_name character varying(200);

ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS venue_surface character varying(100);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS elapsed integer;

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS last_live_status_synced_at_utc timestamp with time zone;

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS referee character varying(200);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS round character varying(200);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS status_extra integer;

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS status_long character varying(200);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS timezone character varying(100);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS venue_city character varying(200);

ALTER TABLE fixtures
    ADD COLUMN IF NOT EXISTS venue_name character varying(200);

START TRANSACTION;

ALTER TABLE teams ADD founded integer;

ALTER TABLE teams ADD is_national boolean;

ALTER TABLE teams ADD venue_address character varying(300);

ALTER TABLE teams ADD venue_capacity integer;

ALTER TABLE teams ADD venue_city character varying(200);

ALTER TABLE teams ADD venue_image_url character varying(500);

ALTER TABLE teams ADD venue_name character varying(200);

ALTER TABLE teams ADD venue_surface character varying(100);

ALTER TABLE fixtures ADD elapsed integer;

ALTER TABLE fixtures ADD last_live_status_synced_at_utc timestamp with time zone;

ALTER TABLE fixtures ADD referee character varying(200);

ALTER TABLE fixtures ADD round character varying(200);

ALTER TABLE fixtures ADD status_extra integer;

ALTER TABLE fixtures ADD status_long character varying(200);

ALTER TABLE fixtures ADD timezone character varying(100);

ALTER TABLE fixtures ADD venue_city character varying(200);

ALTER TABLE fixtures ADD venue_name character varying(200);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260331185318_Stage6LiveStatusAndMetadata', '8.0.8');

COMMIT;


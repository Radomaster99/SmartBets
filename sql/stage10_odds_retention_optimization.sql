CREATE INDEX IF NOT EXISTS ix_live_odds_collected_at_utc
    ON live_odds (collected_at_utc);

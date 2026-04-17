# SmartBets API

Това repository съдържа текущия .NET 8 backend за SmartBets.

Основната идея на приложението е:
- да синхронизира данни от API-Football към локална база
- да държи локален read model за frontend-а
- да сервира frontend-friendly endpoint-и върху данните в базата

Подробната документация за текущото състояние на endpoint-ите е тук:

- [docs/api-endpoints.md](docs/api-endpoints.md)

Stage 1 infrastructure, добавена в текущата версия:
- `league_season_coverages` за coverage flags по `leagueApiId + season`
- `sync_errors` за последни sync/request грешки
- `GET /api/supported-leagues`
- `GET /api/league-coverages`
- `GET /api/sync-errors`
- admin CRUD за `supported_leagues` през `/api/admin/supported-leagues`

Stage 2 match-center infrastructure:
- `fixture_events`
- `fixture_lineups`
- `fixture_statistics`
- `fixture_player_statistics`
- `GET /api/fixtures/{apiFixtureId}/events`
- `GET /api/fixtures/{apiFixtureId}/statistics`
- `GET /api/fixtures/{apiFixtureId}/lineups`
- `GET /api/fixtures/{apiFixtureId}/players`
- `GET /api/fixtures/{apiFixtureId}/match-center`
- targeted sync endpoint-и за quota-aware match-center refresh

Stage 3 preview infrastructure:
- `fixture_predictions`
- `fixture_injuries`
- `GET /api/fixtures/{apiFixtureId}/preview`
- `GET /api/fixtures/{apiFixtureId}/injuries`
- `GET /api/fixtures/{apiFixtureId}/head-to-head`
- `GET /api/fixtures/{apiFixtureId}/predictions`
- targeted preview sync endpoint-и с T-24h / T-3h / T-1h refresh windows

Бързи бележки:
- `GET /ping` е публичен health check
- Swagger е включен по подразбиране само в development
- ако `ApiAuth:Token` е настроен, всички непублични endpoint-и изискват header `X-API-KEY`
- admin frontend flow вече може да ползва `POST /api/admin/auth/login`, `GET /api/admin/auth/me` и `POST /api/admin/auth/logout` с `httpOnly` cookie session
- default admin cookie name е `oddsdetector_admin`
- sync/admin/debug endpoint-ите са admin-only
- ако `CORS:AllowedOrigins` липсва извън development, browser cross-origin достъпът се блокира
- при explicit `CORS:AllowedOrigins` backend-ът позволява credentialed browser requests, което е нужно за admin cookie session flows
- приложението чете порта от env var `PORT` и по подразбиране слуша на `10000`
Stage 4 analytics:
- `team_statistics`
- `league_rounds`
- `league_top_scorers`
- `league_top_assists`
- `league_top_cards`
- `GET /api/teams/{apiTeamId}/statistics`
- `GET /api/teams/{apiTeamId}/form`
- `GET /api/leagues/{apiLeagueId}/rounds`
- `GET /api/leagues/{apiLeagueId}/current-round`
- `GET /api/leagues/{apiLeagueId}/top-scorers`
- `GET /api/leagues/{apiLeagueId}/top-assists`
- `GET /api/leagues/{apiLeagueId}/top-cards`
- `GET /api/leagues/{apiLeagueId}/dashboard`
- `POST /api/teams/statistics/sync`
- `POST /api/leagues/{apiLeagueId}/analytics/sync`
- `POST /api/leagues/analytics/sync`

Stage 5 odds analytics:
- `odds_open_close`
- `odds_movements`
- `market_consensus`
- `GET /api/fixtures/{apiFixtureId}/odds/history`
- `GET /api/fixtures/{apiFixtureId}/odds/movement`
- `GET /api/fixtures/{apiFixtureId}/odds/consensus`
- `GET /api/fixtures/{apiFixtureId}/odds/value-signals`
- `POST /api/odds/analytics/rebuild`
- automatic rebuild of derived odds analytics after `POST /api/odds/sync`

Stage 6 live status + metadata enrichment:
- lightweight live fixture status sync via `POST /api/fixtures/sync-live-status`
- live status sync also performs a small recent-kickoff catch-up so fixtures do not stay stuck on `NS` after the provider live window is missed
- richer fixture metadata stored from `/fixtures`
- richer team metadata stored from `/teams`
- new fixture freshness field for live status checks
- new sync-status field for `fixtures_live`
- migration: `Migrations/20260331185318_Stage6LiveStatusAndMetadata.cs`
- SQL fallback: `sql/stage6_live_status_and_metadata_manual.sql`

Stage 7 live odds + fixture batch sync:
- `sync-live-match-center` now batches fixture detail refreshes via API-Football `fixtures?ids=...`
- new live odds reference cache: `live_bet_types`
- new live odds snapshot table: `live_odds`
- `POST /api/odds/live-bets/sync`
- `GET /api/odds/live-bets`
- `POST /api/odds/live/sync`
- `GET /api/odds/live`
- `GET /api/fixtures/{apiFixtureId}/odds/live`
- live odds ingestion now accepts both provider response shapes seen in production:
  - legacy `bookmakers[].bets[].values`
  - direct `odds[].values`
- current production behavior indicates that direct live `odds[]` are effectively a single-source `Bet365` feed
- `odds[].id` is the live bet/market id, not the bookmaker id
- when the provider omits bookmaker information, the backend now materializes those live odds under `Bet365`
- if the local bookmaker catalog cannot resolve `Bet365` yet, the temporary fallback stays `ApiBookmakerId = 0`
- migration: `Migrations/20260331190852_Stage7LiveOddsAndFixtureBatchSync.cs`
- SQL fallback: `sql/stage7_live_odds_and_fixture_batch_sync_manual.sql`

Stage 8 legacy live automation:
- the old `LiveAutomation` worker has been removed from the runtime
- all active automatic refresh orchestration now runs through `CoreDataAutomation`
- no new database migration is required for this cleanup

Stage 9 production hardening:
- runtime API-Football quota telemetry is exposed through `GET /api/sync-status`
- outbound API-Football calls now use minimum spacing plus low/critical quota backoff
- core automation suppresses expensive refreshes first when quota is tight
- `POST /api/bookmakers/sync` now refreshes from the local odds cache instead of re-downloading the heavy odds catalog
- new retention worker trims old `sync_errors`, `live_odds`, `pre_match_odds` and derived odds analytics rows
- live odds for fixtures that are no longer live are now trimmed much earlier than the global hard cap
- compact derived odds analytics can be retained longer than the bulky raw pre-match snapshots
- new config sections:
  - `ApiFootballClient`
  - `DataRetention`
- no new database migration is required for Stage 9

Stage 10 core-data automation refactor:
- backend automation now treats SmartBets as a core-data cache for:
  - countries
  - leagues
  - teams
  - fixtures
  - pre-match odds
  - live odds
  - bookmakers
- rich match details such as events, lineups, players, injuries, previews and analytics remain available through the existing endpoints, but they are no longer part of the primary automatic refresh pipeline
- automatic ingestion is no longer gated by `supported_leagues`
- daily catalog refresh now keeps:
  - all leagues metadata
  - current league-season targets for rolling automation
  - bookmaker reference data from `/odds/bookmakers`
- new primary config section: `CoreDataAutomation`
- new primary worker:
  - `CoreDataAutomationBackgroundService`
  - `CoreDataAutomationOrchestrator`
- separate internal core jobs:
  - `CatalogRefresh`
  - `TeamsRolling`
  - `FixturesRolling`
  - `OddsPreMatch`
  - `OddsLive`
  - `Repair`
- central runtime quota manager:
  - `CoreAutomationQuotaManager`
  - per-job daily budgets
  - per-job last run / skip reason / used budget in `GET /api/sync-status`
- new automatic refresh strategy:
  - countries daily
  - leagues daily
  - current league targets daily
  - bookmaker reference daily
  - teams rolling every 24h per current league-season
  - standings rolling every 24h per current league-season, with a 6h refresh for hot leagues
  - full fixtures rolling every 12h per current league-season
  - hot fixture leagues every 2h
  - live fixture heartbeat every 30s for all leagues
  - pre-match odds every 6h / 2h / 30m / 15m depending on kickoff proximity
  - live odds every 30s for live leagues
- quota defaults are now tuned for a 75 000/day plan, with early degradation before the hard limit:
  - `LowDailyRemainingThreshold = 10000`
  - `CriticalDailyRemainingThreshold = 2500`

Stage 11 automation window + historical bootstrap split:
- ongoing background automation now tracks only the current automation window:
  - `currentYear - 1`
  - `currentYear`
  - `currentYear + 1`
- older seasons are no longer re-polled automatically
- historical data from `2023+` is imported through:
  - `POST /api/preload/historical`
- the historical bootstrap can optionally exclude the active automation window, so old seasons are filled once and then left frozen unless you request another manual refresh

Stage 12 SignalR live odds push:
- new hub route: `/hubs/live-odds`
- subscribe by fixture with `JoinFixture(apiFixtureId)`
- optional subscribe by league with `JoinLeague(leagueId)`
- event name: `LiveOddsUpdated`
- live odds sync now broadcasts only after changed snapshots are persisted
- browser clients now connect with JWT through `?access_token=...`

Stage 13 JWT auth for REST and SignalR:
- REST endpoints now accept `Authorization: Bearer {jwt}`
- SignalR hub `/hubs/live-odds` accepts JWT through `?access_token=...`
- new bridge endpoint: `POST /api/auth/token`
- `POST /api/auth/token` can mint a JWT for an already authenticated caller, for example one using the legacy `X-API-KEY`
- new debug endpoint: `GET /api/auth/me`
- legacy `X-API-KEY` remains available during the transition, but JWT is now the preferred frontend auth model
- production recommendation: set a dedicated `JwtAuth:SigningKey` with at least 32 bytes
- default JWT access-token lifetime is now 60 minutes
- if `JwtAuth:SigningKey` is shorter or missing, the backend derives a stable 256-bit HMAC key from the configured secret so `POST /api/auth/token` does not fail on HS256 key length
- SignalR no longer accepts API keys through query-string `access_token`

Stage 14 live odds list-view optimization:
- `GET /api/fixtures/query` supports `includeLiveOddsSummary=true`
- each returned `FixtureDto` can now include `LiveOddsSummary`
- new batch endpoint: `POST /api/odds/live/summary`
- list summaries are cache-only reads:
  - live summary when local live odds exist
  - pre-match fallback when live odds are missing
  - `none` when neither exists
- list reads do not trigger per-row on-demand live odds sync
- SignalR now also publishes `LiveOddsSummaryUpdated`
- hub convenience methods now include:
  - `JoinFixtures(apiFixtureIds[])`
  - `LeaveFixtures(apiFixtureIds[])`
  - `JoinLiveFeed()`
  - `LeaveLiveFeed()`
- if a live fixture returns `source = prematch` or `source = none`, that can mean the provider currently has no usable live odds for that fixture, not necessarily a frontend integration issue

Live odds diagnostics:
- `GET /api/debug/provider/live-odds?fixtureId=...`
- `GET /api/debug/provider/live-odds?leagueId=...`
- these endpoints show whether the provider returned fixtures/values at all before the backend tries to save anything locally

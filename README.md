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
- Swagger е достъпен през `/swagger`
- ако `ApiAuth:Token` е настроен, всички непублични endpoint-и изискват header `X-API-KEY`
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

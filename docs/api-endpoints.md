# SmartBets API Endpoint Documentation

Тази документация описва как работят endpoint-ите в момента според реалния код в проекта.

## 1. Обща архитектура

Приложението работи в два основни режима:

1. `sync` endpoint-и
   - дърпат данни от API-Football
   - записват/обновяват локалната база
   - обновяват `sync_states` когато е приложимо

2. `read` endpoint-и
   - не удрят директно API-Football
   - четат само от локалната база
   - връщат DTO-та, удобни за frontend

Типичният поток е:

`API-Football -> sync service -> PostgreSQL -> controller -> frontend`

## 2. Автентикация и общо поведение

### 2.1 API key

Ако в конфигурацията има стойност за `ApiAuth:Token`, всички непублични endpoint-и изискват header:

```http
X-API-KEY: your-token
```

Ако `ApiAuth:Token` липсва или е празен, middleware-ът пропуска заявките без ключ.

Security note:
- sync/admin/debug endpoint-ите вече са `admin-only`
- read endpoint-ите остават достъпни за валидно аутентикирани заявки според текущия auth setup

### 2.2 Публични endpoint-и

Следните пътища са публични:

- `GET /ping`

Swagger note:
- `/swagger` е включен по подразбиране само в development
- извън development се показва само ако `Swagger:Enabled=true`

### 2.3 Формат на грешките

Глобалните необработени грешки се връщат като `application/problem+json` с полета:

- `type`
- `title`
- `status`
- `detail`

`detail` се показва само в development.

### 2.4 Времена и дати

- `KickoffAt`, `CollectedAtUtc`, `LastSyncedAtUtc` и останалите `DateTime` полета се третират като UTC
- филтрите `date`, `from`, `to` при fixtures са `DateOnly` и очакват формат `YYYY-MM-DD`

### 2.5 Конфигурация

Основните конфигурационни секции в момента са:

- `ConnectionStrings:DefaultConnection`
- `ApiFootball:BaseUrl`
- `ApiFootball:ApiKey`
- `ApiFootballClient:*`
- `ApiAuth:Token`
- `AdminAuth:*`
- `JwtAuth:*`
- `CoreDataAutomation:*`
- `DataRetention:*`
- `TheOddsApi:*`
- `Swagger:Enabled`
- `CORS:AllowedOrigins`
- env var `PORT`

CORS note:
- ако `CORS:AllowedOrigins` не е зададен извън development, приложението fail-ва closed за cross-origin browser заявки
- когато `CORS:AllowedOrigins` е конфигуриран, backend-ът вече връща и credential-friendly CORS headers, така че httpOnly admin cookie session може да работи през browser requests към позволените origins

## 3. Системни endpoint-и

### 3.1 `GET /ping`

Предназначение:
- публичен health check

Отговор:

```text
pong
```

### 3.2 `GET /swagger`

Предназначение:
- Swagger UI за ръчен преглед и тест на API-то

Важно:
- по подразбиране е наличен само в development
- за production/manual diagnostics трябва изрично `Swagger:Enabled=true`

## 4. Countries

Base route:

`/api/countries`

### 4.1 `POST /api/countries/sync`

Предназначение:
- синхронизира всички държави от API-Football към таблицата `countries`
- обновява global sync state за `countries`

Параметри:
- няма

Отговор:
- `Message`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`

### 4.2 `GET /api/countries`

Предназначение:
- връща всички държави от локалната база

Сортиране:
- по `Name`

Отговор:
- масив от `CountryDto`

`CountryDto`:
- `Id`
- `Name`
- `Code`
- `FlagUrl`

### 4.3 `GET /api/countries/debug-count`

Предназначение:
- бърз count check върху таблицата `countries`

Отговор:
- `{ count }`

## 5. Leagues

Base route:

`/api/leagues`

### 5.1 `POST /api/leagues/sync`

Предназначение:
- синхронизира лигите от API-Football
- обновява global sync state за `leagues`

Параметри:
- няма

Отговор:
- `Message`
- `LastSyncedAtUtc`
- `Processed`

### 5.2 `GET /api/leagues`

Предназначение:
- връща лигите от локалната база

Query параметри:
- `season` - optional

Сортиране:
- `CountryName`
- `Name`
- `Season desc`

Отговор:
- масив от `LeagueDto`

`LeagueDto`:
- `Id`
- `ApiLeagueId`
- `Name`
- `Season`
- `CountryId`
- `CountryName`

## 6. Teams

Base route:

`/api/teams`

### 6.1 `POST /api/teams/sync`

Предназначение:
- синхронизира отборите за сезон
- може да синхронизира или една конкретна лига, или първите `maxLeagues` лиги за сезона
- обновява sync state `teams` за всяка обработена лига/сезон

Query параметри:
- `season` - required
- `leagueId` - optional
- `maxLeagues` - optional, default `5`

Поведение:
- ако `leagueId` е подаден: sync само за тази лига
- ако `leagueId` липсва: вземат се първите `maxLeagues` от локалната таблица `leagues`
- ако `maxLeagues <= 0`: `400 BadRequest`

Отговор при единична лига:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`

Отговор при multiple mode:
- `Message`
- `Season`
- `LeaguesProcessed`
- `LastSyncedAtUtc`
- `TotalProcessed`
- `TotalInserted`
- `TotalUpdated`

### 6.2 `GET /api/teams`

Предназначение:
- връща всички отбори от локалната база

Сортиране:
- по `Name`

Отговор:
- масив от `TeamDto`

`TeamDto`:
- `Id`
- `ApiTeamId`
- `Name`
- `Code`
- `LogoUrl`
- `CountryId`
- `CountryName`

### 6.3 `GET /api/teams/{apiTeamId}`

Предназначение:
- връща един отбор по външния `ApiTeamId`
- подходящо за team page header и widget integration

Route параметри:
- `apiTeamId` - required

Отговор:
- `TeamDto`

## 7. Fixtures

Base route:

`/api/fixtures`

### 7.1 `POST /api/fixtures/sync`

Предназначение:
- синхронизира пълния fixture set за сезон
- обновява sync state `fixtures_full`

Query параметри:
- `season` - required
- `leagueId` - optional
- `maxLeagues` - optional, default `5`

Поведение:
- ако има `leagueId`, се sync-ва само една лига
- ако няма `leagueId`, се вземат първите `maxLeagues` лиги от локалната таблица `leagues`
- ако `maxLeagues <= 0`: `400 BadRequest`

Отговор при единична лига:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`
- `SkippedMissingTeams`

Отговор при multiple mode:
- `Message`
- `Season`
- `LeaguesProcessed`
- `LastSyncedAtUtc`
- `TotalProcessed`
- `TotalInserted`
- `TotalUpdated`
- `TotalSkippedMissingTeams`

### 7.2 `POST /api/fixtures/sync-upcoming`

Предназначение:
- синхронизира само upcoming fixtures за конкретна лига/сезон
- обновява sync state `fixtures_upcoming`

Query параметри:
- `leagueId` - required
- `season` - required

Отговор:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`
- `SkippedMissingTeams`

### 7.3 `GET /api/fixtures`

Предназначение:
- unpaged read endpoint за списък от fixtures от базата

Query параметри:
- `leagueId` - optional, използва `ApiLeagueId`
- `season` - optional
- `status` - optional, exact short status
- `state` - optional, bucket enum
- `teamId` - optional, използва `ApiTeamId`
- `date` - optional
- `from` - optional
- `to` - optional
- `direction` - optional, `asc` или `desc`, default `asc`

Валидиране:
- не може едновременно `date` и `from`/`to`
- ако `from > to` -> `400 BadRequest`

Отговор:
- масив от `FixtureDto`

### 7.4 `GET /api/fixtures/query`

Предназначение:
- paged вариант на fixture list endpoint-а

Query параметри:
- всички параметри от `GET /api/fixtures`
- `page` - optional, default `1`, минимум `1`
- `pageSize` - optional, default `50`, clamp `1..100`

Отговор:
- `PagedResultDto<FixtureDto>`

`PagedResultDto`:
- `Items`
- `Page`
- `PageSize`
- `TotalItems`
- `TotalPages`
- `HasNextPage`
- `HasPreviousPage`

### 7.5 `GET /api/fixtures/{apiFixtureId}`

Предназначение:
- fixture detail endpoint
- връща fixture информация + best odds summary + sync freshness за fixture/odds

Path параметри:
- `apiFixtureId` - required

Query параметри:
- `marketName` - optional, използва се при odds summary

Отговор:
- `FixtureDetailDto`

`FixtureDetailDto`:
- `Fixture`
- `BestOdds`
- `LatestOddsCollectedAtUtc`
- `FixturesUpcomingLastSyncedAtUtc`
- `FixturesFullLastSyncedAtUtc`
- `OddsLastSyncedAtUtc`

Ако fixture липсва:
- `404 NotFound`

### 7.6 `GET /api/fixtures/{apiFixtureId}/odds`

Предназначение:
- връща odds за конкретен fixture

Path параметри:
- `apiFixtureId` - required

Query параметри:
- `marketName` - optional
- `latestOnly` - optional, default `true`

Отговор:
- масив от `OddDto`

Ако няма odds:
- `404 NotFound`

### 7.7 `GET /api/fixtures/{apiFixtureId}/best-odds`

Предназначение:
- връща best odds summary за конкретен fixture

Path параметри:
- `apiFixtureId` - required

Query параметри:
- `marketName` - optional

Отговор:
- `BestOddsDto`

Ако няма odds:
- `404 NotFound`

### 7.8 `FixtureDto` полета

`FixtureDto` в момента съдържа:

- `Id` - local DB fixture id
- `ApiFixtureId`
- `Season`
- `KickoffAt`
- `Status`
- `StateBucket`
- `LeagueId`
- `LeagueApiId`
- `LeagueName`
- `CountryName`
- `HomeTeamId`
- `HomeTeamApiId`
- `HomeTeamName`
- `HomeTeamLogoUrl`
- `AwayTeamId`
- `AwayTeamApiId`
- `AwayTeamName`
- `AwayTeamLogoUrl`
- `HomeGoals`
- `AwayGoals`

Забележка:
- `LeagueId` и `LeagueApiId` в момента са една и съща стойност
- `HomeTeamId`/`AwayTeamId` са local ids
- `HomeTeamApiId`/`AwayTeamApiId` са API ids

### 7.9 `state` bucket логика

Поддържаните `FixtureStateBucket` стойности са:

- `Upcoming`
- `Live`
- `Finished`
- `Postponed`
- `Cancelled`
- `Other`

Текущото мапване е:

- `Upcoming` -> `TBD`, `NS`
- `Live` -> `1H`, `HT`, `2H`, `ET`, `BT`, `P`, `INT`, `SUSP`, `LIVE`
- `Finished` -> `FT`, `AET`, `PEN`
- `Postponed` -> `PST`
- `Cancelled` -> `CANC`, `ABD`, `AWD`, `WO`
- `Other` -> всичко извън познатите статуси

## 8. Standings

Base route:

`/api/standings`

### 8.1 `POST /api/standings/sync`

Предназначение:
- синхронизира standings за конкретна лига/сезон
- обновява sync state `standings`

Query параметри:
- `leagueId` - required
- `season` - required

Отговор:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`
- `SkippedMissingTeams`

### 8.2 `GET /api/standings`

Предназначение:
- връща standings за конкретна лига/сезон от локалната база

Query параметри:
- `leagueId` - required
- `season` - required

Отговор:
- масив от `StandingDto`

`StandingDto`:
- `Rank`
- `TeamId`
- `ApiTeamId`
- `TeamName`
- `TeamLogoUrl`
- `Points`
- `GoalsDiff`
- `GroupName`
- `Form`
- `Status`
- `Description`
- `Played`
- `Win`
- `Draw`
- `Lose`
- `GoalsFor`
- `GoalsAgainst`

## 9. Odds

Base route:

`/api/odds`

### 9.1 `POST /api/odds/sync`

Предназначение:
- синхронизира pre-match odds snapshots за league/season
- обновява sync state `odds`
- обновява sync state `bookmakers`

Query параметри:
- `leagueId` - required
- `season` - required
- `marketName` - optional, default behavior в service е `Match Winner`

Текущо поведение:
- чете odds от API-Football paginated
- пази snapshot само ако odds са променени спрямо последния запис
- upsert-ва bookmaker записите ако е нужно

Отговор:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `MarketName`
- `FixturesMatched`
- `FixturesMissingInDatabase`
- `BookmakersProcessed`
- `BookmakersInserted`
- `BookmakersUpdated`
- `SnapshotsProcessed`
- `SnapshotsInserted`
- `SnapshotsSkippedUnchanged`
- `SnapshotsSkippedUnsupportedMarket`

### 9.2 `GET /api/odds`

Предназначение:
- връща odds за конкретен fixture

Query параметри:
- `fixtureId` - optional, local DB id
- `apiFixtureId` - optional
- `marketName` - optional
- `latestOnly` - optional, default `true`

Валидиране:
- ако липсват и `fixtureId`, и `apiFixtureId` -> `400 BadRequest`

Отговор:
- масив от `OddDto`

Ако няма odds:
- `404 NotFound`

### 9.3 `GET /api/odds/best`

Предназначение:
- връща best odds summary за конкретен fixture

Query параметри:
- `fixtureId` - optional
- `apiFixtureId` - optional
- `marketName` - optional

Валидиране:
- ако липсват и `fixtureId`, и `apiFixtureId` -> `400 BadRequest`

Отговор:
- `BestOddsDto`

Ако няма odds:
- `404 NotFound`

### 9.4 `OddDto` полета

- `FixtureId`
- `ApiFixtureId`
- `BookmakerId`
- `ApiBookmakerId`
- `Bookmaker`
- `MarketName`
- `HomeOdd`
- `DrawOdd`
- `AwayOdd`
- `CollectedAtUtc`

### 9.5 `BestOddsDto` полета

- `FixtureId`
- `ApiFixtureId`
- `MarketName`
- `CollectedAtUtc`
- `BestHomeOdd`
- `BestHomeBookmaker`
- `BestDrawOdd`
- `BestDrawBookmaker`
- `BestAwayOdd`
- `BestAwayBookmaker`

## 10. Bookmakers

Base route:

`/api/bookmakers`

### 10.1 `POST /api/bookmakers/sync`

Предназначение:
- maintenance/rebuild endpoint за bookmaker catalog-а на конкретна league/season
- гледа локалните `pre_match_odds` и `live_odds` references за scope-а
- обновява sync state `bookmakers`

Query параметри:
- `leagueId` - required
- `season` - required

Важно:
- този endpoint синхронизира bookmaker записи
- не записва odds snapshots в `pre_match_odds`
- ако има real `Bet365` bookmaker, endpoint-ът reassign-ва scoped live odds rows от synthetic fallback bookmaker (`ApiBookmakerId = 0`) към real bookmaker-а
- ако synthetic fallback bookmaker вече не е рефериран никъде след merge-а, endpoint-ът го изтрива от catalog-а

Отговор:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `PreMatchOddsReferences`
- `LiveOddsReferences`
- `Processed`
- `Inserted`
- `Updated`
- `LiveOddsRowsReassigned`
- `SyntheticRowsDeleted`

### 10.2 `GET /api/bookmakers`

Предназначение:
- връща локално записаните bookmakers

Поведение:
- ако има real `Bet365` bookmaker, endpoint-ът suppress-ва synthetic fallback row-а с `ApiBookmakerId = 0`, за да не връща дублирана bookmaker identity

Отговор:
- масив от `BookmakerDto`

`BookmakerDto`:
- `Id`
- `ApiBookmakerId`
- `Name`

### 10.3 `POST /api/bookmakers/sync-reference`

Purpose:
- refreshes the global bookmaker reference catalog directly from API-Football `/odds/bookmakers`
- updates global sync state `bookmakers_reference`
- used by the Stage 10 core-data automation worker as the daily bookmaker reference refresh

## 11. Discovery

Base route:

`/api/discovery`

### 11.1 `GET /api/discovery/league-coverage`

Предназначение:
- exploratory endpoint за проверка на league coverage по сезон

Query параметри:
- `season` - required
- `maxLeaguesToCheck` - optional, default `10`

Поведение:
- ако `maxLeaguesToCheck <= 0` -> `400 BadRequest`
- ако `maxLeaguesToCheck > 25`, стойността се clamp-ва до `25`
- текущо услугата прави remote calls към API-Football за league fixtures и изчислява summary counts

Отговор:
- масив от `LeagueCoverageDto`

`LeagueCoverageDto`:
- `ApiLeagueId`
- `LeagueName`
- `Season`
- `CountryName`
- `FixturesCount`
- `UpcomingCount`
- `FinishedCount`
- `LiveCount`

## 12. Preload

Base route:

`/api/preload`

### 12.1 `POST /api/preload/run`

Query parameters:
- `season` - optional
- `maxLeagues` - optional
- `force` - optional, default `false`
- `stopOnRateLimit` - optional, default `true`
- `minMinutesSinceLastSync` - optional, default `180`
- `includeOdds` - optional, default `false`

Stage 10 note:
- preload now works in core-data mode
- it bootstraps current league-seasons from the in-memory/current catalog state
- it syncs:
  - countries
  - leagues
  - current league targets
  - bookmaker reference catalog
  - teams
  - full fixtures
- it does not automatically sync standings or team statistics anymore
- if `includeOdds=true`, preload also syncs pre-match odds for each processed league-season

Предназначение:
- bootstrap/warm-up endpoint за предварително зареждане на системата

Текущ ред на работа:
- sync countries
- sync leagues
- sync current league-season targets
- sync bookmaker reference catalog
- за всяка избрана current league/season изпълнява:
  - teams sync
  - full fixtures sync
  - optional pre-match odds sync, само ако `includeOdds=true`

Важно:
- preload не sync-ва standings в core mode
- preload не sync-ва team statistics в core mode
- preload не sync-ва match-center, preview или league analytics datasets
- полето `SupportedLeaguesCount` в response-а е legacy име; в момента показва броя на избраните current league-season targets

Отговор:
- `PreloadSyncResult`

`PreloadSyncResult`:
- `CountriesSynced`
- `LeaguesSynced`
- `SupportedLeaguesCount`
- `StoppedEarly`
- `StopReason`
- `Leagues`

Всяка entry в `Leagues` съдържа:
- `LeagueApiId`
- `Season`
- `TeamsProcessed`
- `TeamsInserted`
- `TeamsUpdated`
- `FixturesProcessed`
- `FixturesInserted`
- `FixturesUpdated`
- `FixturesSkippedMissingTeams`
- `SkippedFeatures`
- `Status`
- `Error`

### 12.2 `POST /api/preload/historical`

Purpose:
- manual bootstrap for historical league-seasons starting from `2023+`
- meant for one-off or rare backfills
- not part of the ongoing background automation loop

Query parameters:
- `fromSeason` - optional, default `2023`
- `toSeason` - optional, default `currentYear`
- `maxLeagueSeasons` - optional
- `force` - optional, default `false`
- `stopOnRateLimit` - optional, default `true`
- `minMinutesSinceLastSync` - optional, default `1440`
- `includeOdds` - optional, default `false`
- `excludeAutomationWindow` - optional, default `true`

Behavior:
- always refreshes:
  - countries
  - leagues
  - bookmaker reference catalog
- then selects local league-seasons in the requested historical range
- by default excludes the active automation window, so the background worker and the historical import do not overlap
- syncs:
  - teams
  - full fixtures
  - optional pre-match odds
- does not automatically sync:
  - standings
  - team statistics
  - match-center data
  - preview data

Recommended usage:
- keep ongoing automation focused on current seasons
- use this endpoint only when you want to backfill or repair older seasons such as `2023` and `2024`

Response:
- `HistoricalBootstrapResult`

`HistoricalBootstrapResult`:
- `CountriesSynced`
- `LeaguesSynced`
- `BookmakersReferenceSynced`
- `LeagueSeasonsSelected`
- `StoppedEarly`
- `StopReason`
- `Leagues`

Всяка entry в `Leagues` съдържа:
- `LeagueApiId`
- `Season`
- `LeagueName`
- `CountryName`
- `TeamsProcessed`
- `TeamsInserted`
- `TeamsUpdated`
- `FixturesProcessed`
- `FixturesInserted`
- `FixturesUpdated`
- `FixturesSkippedMissingTeams`
- `OddsFixturesMatched`
- `OddsSnapshotsInserted`
- `OddsSnapshotsProcessed`
- `SkippedFeatures`
- `Status`
- `Error`

## 13. Sync Status

Base route:

`/api/sync-status`

### 13.1 `GET /api/sync-status`

Предназначение:
- централен endpoint за freshness на синхронизацията

Query параметри:
- `season` - optional
- `activeOnly` - optional, default `true`

Отговор:
- `SyncStatusDto`

`SyncStatusDto`:
- `GeneratedAtUtc`
- `ApiQuota`
- `CoreAutomation`
- `Global`
- `Leagues`

`Global` съдържа:
- `countries`
- `leagues`
- `leagues_current`
- `live_bet_types`
- `bookmakers_reference`

`Leagues` съдържа по една entry за current league/season target от automation catalog-а.

Fallback поведение:
- ако in-memory current catalog е празен, endpoint-ът пада обратно към локалните `supported_leagues`
- `IsActive` и `Priority` идват от `supported_leagues`, когато такъв запис съществува

Всяка league entry съдържа:
- `LeagueApiId`
- `Season`
- `LeagueName`
- `CountryName`
- `IsActive`
- `Priority`
- `TeamsLastSyncedAtUtc`
- `FixturesLiveLastSyncedAtUtc`
- `FixturesUpcomingLastSyncedAtUtc`
- `FixturesFullLastSyncedAtUtc`
- `EventsLastSyncedAtUtc`
- `StatisticsLastSyncedAtUtc`
- `LineupsLastSyncedAtUtc`
- `PlayerStatisticsLastSyncedAtUtc`
- `PredictionsLastSyncedAtUtc`
- `InjuriesLastSyncedAtUtc`
- `TeamStatisticsLastSyncedAtUtc`
- `RoundsLastSyncedAtUtc`
- `TopScorersLastSyncedAtUtc`
- `TopAssistsLastSyncedAtUtc`
- `TopCardsLastSyncedAtUtc`
- `StandingsLastSyncedAtUtc`
- `OddsLastSyncedAtUtc`
- `LiveOddsLastSyncedAtUtc`
- `OddsAnalyticsLastSyncedAtUtc`
- `BookmakersLastSyncedAtUtc`

## 14. Debug

Base route:

`/api/debug`

### 14.1 `GET /api/debug/config`

Предназначение:
- проверка дали ключовите конфигурации са налични

Отговор:
- `ConnectionStringExists`
- `ConnectionStringPreview`
- `ApiBaseUrl`
- `ApiKeyExists`

### 14.2 `GET /api/debug/db`

Предназначение:
- бърз health snapshot на базата

Отговор:
- `CanConnect`
- `Countries`
- `Leagues`
- `Teams`
- `Fixtures`
- `SupportedLeagues`
- `LiveOdds`

### 14.3 `GET /api/debug/provider/live-odds`

Purpose:
- diagnostics endpoint for the raw live odds provider response
- helps separate provider-availability issues from local matching or save-path bugs

Query parameters:
- `fixtureId` - optional API fixture id
- `leagueId` - optional API league id
- `betId` - optional live bet id
- `bookmakerId` - optional API bookmaker id

Validation:
- requires either `fixtureId` or `leagueId`

Response:
- `FixtureId`
- `LeagueId`
- `BetId`
- `BookmakerId`
- `DirectOddsResolvedBookmaker`
- `ProviderFixturesReceived`
- `ProviderFixtureApiIds`
- `ProviderBookmakersReceived`
- `ProviderBetsReceived`
- `ProviderValuesReceived`
- `Sample`
- `LocalMatchingFixtures`

Notes:
- the endpoint understands both live odds provider response shapes currently seen in production:
  - legacy `bookmakers[].bets[].values`
  - direct `odds[].values`
- in the current production provider behavior, direct `odds[].values` effectively represent a single live source bookmaker: `Bet365`
- `odds[].id` is the live bet/market id, not the bookmaker id
- `bookmakerId` remains useful for diagnostics, but direct `odds[]` responses are not relabeled to the requested bookmaker anymore
- if `ProviderFixturesReceived = 0` and `ProviderValuesReceived = 0`, the issue is upstream provider availability
- if provider fixtures are returned but `LocalMatchingFixtures` is empty, the issue is local fixture matching

### 14.4 `GET /api/debug/provider/live-odds/candidates`

Purpose:
- helps find a truly current live fixture for end-to-end bookmaker identity checks
- cross-checks local live fixtures against the current provider live odds feed
- suggests likely bookmaker ids from stored pre-match odds for catalog cross-checking

Query parameters:
- `maxLeagues` - optional, default `4`, clamp `1..10`
- `maxFixtures` - optional, default `10`, clamp `1..25`

Response:
- `LocalLiveFixtures`
- `ScopedLeaguesChecked`
- `ProviderFixturesReceived`
- `ResolvedLiveBookmaker`
- `Candidates`

Each candidate contains:
- `ApiFixtureId`
- `LeagueApiId`
- `Season`
- `Status`
- `KickoffAtUtc`
- `HomeTeamName`
- `AwayTeamName`
- `ProviderHasFixture`
- `ProviderBookmakersReceived`
- `ProviderBetsReceived`
- `ProviderValuesReceived`
- `StoredLiveBookmakerApiIds`
- `StoredLiveHasRealBookmakers`
- `StoredLiveHasSyntheticRows`

Recommended usage:
- first call this endpoint to locate a live fixture where `ProviderValuesReceived > 0`
- then test:
  - `GET /api/debug/provider/live-odds?fixtureId=...`
  - `GET /api/fixtures/{apiFixtureId}/odds/live`

## 15. Практически бележки за frontend

### 15.1 Кои endpoint-и са подходящи за UI списъци

Препоръчителни read endpoint-и:
- fixture list -> `GET /api/fixtures/query`
- fixture detail -> `GET /api/fixtures/{apiFixtureId}`
- fixture odds -> `GET /api/fixtures/{apiFixtureId}/odds`
- best odds -> `GET /api/fixtures/{apiFixtureId}/best-odds`
- fixture corners -> `GET /api/fixtures/{apiFixtureId}/corners`
- standings -> `GET /api/standings`
- sync freshness -> `GET /api/sync-status`

### 15.2 Къде има смесване на local id и API id

В текущия contract има и двата типа id-та:
- local DB ids
- API-Football ids

Примери:
- `FixtureDto.Id` е local
- `FixtureDto.ApiFixtureId` е API id
- `FixtureDto.HomeTeamId` е local
- `FixtureDto.HomeTeamApiId` е API id

За външна интеграция е по-безопасно frontend-ът да се води основно по API ids, когато работи с route-ове като:
- `/api/fixtures/{apiFixtureId}`
- `/api/odds?apiFixtureId=...`

### 15.3 Кои endpoint-и четат директно от API-Football

Само sync/discovery логиката удря външното API.

Read endpoint-и като:
- `/api/fixtures`
- `/api/fixtures/query`
- `/api/fixtures/{apiFixtureId}`
- `/api/fixtures/{apiFixtureId}/statistics`
- `/api/fixtures/{apiFixtureId}/corners`
- `/api/fixtures/{apiFixtureId}/match-center`
- `/api/standings`
- `/api/odds`

четат основно от локалната база.

Важно изключение:
- за live fixtures `GET /api/fixtures/{apiFixtureId}/statistics`, `GET /api/fixtures/{apiFixtureId}/corners` и `GET /api/fixtures/{apiFixtureId}/match-center` могат да задействат throttled statistics-only catch-up към API-Football
- това позволява UI-то да вижда по-свеж statistics/corners snapshot без frontend-ът да вика sync endpoint-и
- ако catch-up заявката се провали, endpoint-ът пак връща последния наличен локален snapshot

### 15.4 Corners frontend usage

Препоръчителен flow:
- normal page load -> `GET /api/fixtures/{apiFixtureId}/corners`
- ако `HasData = true`, frontend-ът рендерира `Home.Corners`, `Away.Corners` и по желание `TotalCorners`
- ако `HasData = false`, UI може да покаже `N/A`, skeleton или бутон за admin refresh според контекста
- за live fixture endpoint-ът може да направи automatic statistics catch-up най-много веднъж на минута за мача, без frontend-ът да вика `POST /sync-corners`

Кога да се ползва sync:
- `POST /api/fixtures/{apiFixtureId}/sync-corners` е targeted refresh endpoint
- подходящ е за admin/debug flow или когато изрично искаш да форсираш нов statistics fetch
- не е препоръчително normal user page load да го вика автоматично

Практически бележки:
- corner данните идват от fixture statistics, не от odds слоя
- endpoint-ът връща нормализиран contract, за да не търси frontend-ът `Corner Kicks` в generic statistics масив
- `TotalCorners` е попълнен само когато и home, и away стойността са налични
- `SyncedAtUtc` е timestamp на последния stored statistics snapshot, от който са извлечени корнерите

## 16. Текущи ограничения

- `GET /api/fixtures` е unpaged и е по-подходящ за по-малки списъци или admin/debug употреба
- `GET /api/fixtures/query` е препоръчителният list endpoint за frontend
- preload flow в момента не синхронизира odds
- bookmaker sync не е заместител на odds sync
- `appsettings.Development.json` е игнориран и не се качва в git
- Swagger вече не е публичен по подразбиране извън development
- sync/admin/debug endpoint-ите вече изискват admin-authenticated caller

## 17. Примерни заявки

### 17.1 Paged fixtures

```http
GET /api/fixtures/query?leagueId=39&season=2025&state=Finished&from=2025-08-01&to=2025-08-31&page=1&pageSize=20&direction=desc
```

### 17.2 Fixture detail

```http
GET /api/fixtures/123456?marketName=Match Winner
```

### 17.3 Fixture odds

```http
GET /api/fixtures/123456/odds?marketName=Match Winner&latestOnly=true
```

### 17.3.1 Fixture corners

```http
GET /api/fixtures/123456/corners
```

### 17.3.2 Fixture corners sync

```http
POST /api/fixtures/123456/sync-corners?force=true
```

### 17.4 Odds sync

```http
POST /api/odds/sync?leagueId=39&season=2025&marketName=Match Winner
```

### 17.5 Sync status

```http
GET /api/sync-status?activeOnly=true&season=2025
```

## 18. Stage 1 Coverage And Control Plane

### 18.1 `GET /api/supported-leagues`

Предназначение:
- връща business whitelist-а на лигите, които системата поддържа активно
- обогатява всеки запис с име на лига, държава, coverage flags и freshness от `sync_states`

Query параметри:
- `season` - optional
- `leagueId` - optional, използва API-Football league id
- `activeOnly` - optional, default `true`

Отговор:
- масив от `SupportedLeagueDto`

`SupportedLeagueDto`:
- `Id`
- `LeagueApiId`
- `Season`
- `IsActive`
- `Priority`
- `CreatedAtUtc`
- `LeagueName`
- `CountryName`
- `Coverage`
- `Sync`

`Coverage` съдържа:
- `HasFixtures`
- `HasFixtureEvents`
- `HasLineups`
- `HasFixtureStatistics`
- `HasPlayerStatistics`
- `HasStandings`
- `HasPlayers`
- `HasTopScorers`
- `HasTopAssists`
- `HasTopCards`
- `HasInjuries`
- `HasPredictions`
- `HasOdds`

`Sync` съдържа:
- `TeamsLastSyncedAtUtc`
- `FixturesUpcomingLastSyncedAtUtc`
- `FixturesFullLastSyncedAtUtc`
- `StandingsLastSyncedAtUtc`
- `OddsLastSyncedAtUtc`
- `BookmakersLastSyncedAtUtc`

### 18.2 `POST /api/admin/supported-leagues`

Предназначение:
- добавя нов запис в `supported_leagues`

Body:
- `LeagueApiId`
- `Season`
- `IsActive`
- `Priority`

Валидиране:
- `Priority` не може да е отрицателен
- съответната лига и сезон трябва вече да съществуват в локалната таблица `leagues`
- не допуска duplicate запис за същите `LeagueApiId + Season`

### 18.2.1 `POST /api/admin/supported-leagues/bulk`

Предназначение:
- bulk add/update за много записи в `supported_leagues`
- удобен е когато искаш да добавиш цял списък лиги за следене наведнъж

Body:
- `Items` - масив от обекти със:
  - `LeagueApiId`
  - `Season`
  - `IsActive`
  - `Priority`

Поведение:
- ако записът не съществува: създава се
- ако записът вече съществува: обновява `IsActive` и `Priority`
- ако лигата/сезонът липсват в локалната таблица `leagues`: този елемент се маркира като failed, без да спира целия batch

Отговор:
- `Received`
- `Created`
- `Updated`
- `Unchanged`
- `Failed`
- `Results`

### 18.3 `PATCH /api/admin/supported-leagues/{id}`

Предназначение:
- променя `IsActive` и/или `Priority` на existing supported league запис

Body:
- `IsActive` - optional
- `Priority` - optional

### 18.4 `DELETE /api/admin/supported-leagues/{id}`

Предназначение:
- премахва запис от `supported_leagues`

### 18.5 `GET /api/league-coverages`

Предназначение:
- read endpoint за локално запазените coverage flags от API-Football `/leagues`

Query параметри:
- `season` - optional
- `leagueId` - optional

Отговор:
- масив от `LeagueSeasonCoverageDto`

`LeagueSeasonCoverageDto`:
- `LeagueApiId`
- `Season`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `Coverage`

### 18.6 `GET /api/sync-errors`

Предназначение:
- връща последните записани sync/request failure-и
- полезен е за admin UI, monitoring и debugging

Query параметри:
- `entityType` - optional
- `leagueId` - optional
- `season` - optional
- `source` - optional
- `limit` - optional, default `50`, clamp `1..200`

Отговор:
- масив от `SyncErrorDto`

`SyncErrorDto`:
- `Id`
- `EntityType`
- `Operation`
- `LeagueApiId`
- `Season`
- `Source`
- `ErrorMessage`
- `OccurredAtUtc`

### 18.7 Coverage-aware sync поведение

След Stage 1 част от sync услугите вече гледат локалната coverage информация:
- `FixtureSyncService` отказва fixture sync, ако за league/season `HasFixtures == false`
- `StandingsSyncService` отказва standings sync, ако `HasStandings == false`
- `BookmakerSyncService` и `PreMatchOddsService` отказват odds sync, ако `HasOdds == false`

Поведение при липсващ coverage запис:
- системата е permissive и не блокира sync-а
- blocking има само когато coverage записът съществува и даденият feature е изрично `false`

### 18.8 Preload след Stage 1

`POST /api/preload/run` вече:
- продължава да sync-ва `countries` и `leagues`
- взема само активните записи от `supported_leagues`
- пропуска `fixtures_upcoming`, ако coverage казва, че fixtures не се поддържат
- пропуска `standings`, ако coverage казва, че standings не се поддържат
- записва грешка в `sync_errors`, ако дадена supported league итерация се провали

`PreloadSyncLeagueResult` вече съдържа и:
- `SkippedFeatures`

## 19. Database Additions

### 19.1 `league_season_coverages`

Идея:
- пази capabilities на API-Football за конкретна лига и сезон
- използва се за по-умен sync и за frontend/admin visibility

Уникалност:
- unique по `league_api_id + season`

### 19.2 `sync_errors`

Идея:
- пази последните operational грешки, вместо да разчитате само на логове

Основни полета:
- `entity_type`
- `operation`
- `league_api_id`
- `season`
- `source`
- `error_message`
- `occurred_at`

## 20. Applying The Migration

Генерираната migration е:
- `Migrations/20260331135311_Stage1CoverageAndSyncErrors.cs`

Локално или на сървъра приложението към базата става с:

```bash
dotnet ef database update
```

Ако предпочиташ ръчен apply през PostgreSQL client, има и готов script:
- `sql/stage1_coverage_and_sync_errors.sql`

Важно:
- migration-ът създава `league_season_coverages` и `sync_errors`
- в текущия migration chain EF включва и `standings`, защото таблицата е в модела, но не е била част от първоначалната migration история
- ако production базата вече има `standings`, прегледай migration-а преди `database update`, за да не се опита да я създаде втори път

## 21. Stage 2 Match Center

### 21.1 Нови таблици

- `fixture_events`
- `fixture_lineups`
- `fixture_statistics`
- `fixture_player_statistics`

Допълнително `fixtures` вече пази:
- `last_event_synced_at_utc`
- `last_statistics_synced_at_utc`
- `last_lineups_synced_at_utc`
- `last_player_statistics_synced_at_utc`
- `post_finish_match_center_sync_count`

### 21.2 Нови read endpoint-и

Base route:

`/api/fixtures`

Нови route-ове:
- `GET /api/fixtures/{apiFixtureId}/events`
- `GET /api/fixtures/{apiFixtureId}/statistics`
- `GET /api/fixtures/{apiFixtureId}/corners`
- `GET /api/fixtures/{apiFixtureId}/lineups`
- `GET /api/fixtures/{apiFixtureId}/players`
- `GET /api/fixtures/{apiFixtureId}/match-center`

Поведение:
- по подразбиране тези endpoint-и четат от локалната база
- за live fixtures `GET /api/fixtures/{apiFixtureId}/statistics`, `GET /api/fixtures/{apiFixtureId}/corners` и `GET /api/fixtures/{apiFixtureId}/match-center` могат да пуснат throttled statistics-only catch-up към API-Football
- този catch-up не sync-ва events, lineups или players; той поддържа само `fixture_statistics` свежи
- ако fixture липсва -> `404 NotFound`
- ако fixture съществува, но match-center snapshot още не е sync-нат -> връщат `200` с празни масиви

`GET /api/fixtures/{apiFixtureId}/match-center` връща:
- `Detail`
- `Events`
- `Statistics`
- `Lineups`
- `Players`

`Detail` вече съдържа и:
- `Freshness.LastEventSyncedAtUtc`
- `Freshness.LastStatisticsSyncedAtUtc`
- `Freshness.LastLineupsSyncedAtUtc`
- `Freshness.LastPlayerStatisticsSyncedAtUtc`

`GET /api/fixtures/{apiFixtureId}/corners`
- convenience read endpoint over stored fixture statistics
- returns a normalized home/away corners payload instead of the raw generic statistics list
- for live fixtures can trigger a throttled statistics-only catch-up before reading the stored snapshot
- if no stored corner rows exist yet, it still returns `200` with `HasData = false`

Response:
- `ApiFixtureId`
- `SyncedAtUtc`
- `HasData`
- `TotalCorners`
- `Home`
- `Away`

`Home` / `Away` contain:
- `TeamId`
- `TeamApiId`
- `TeamName`
- `TeamLogoUrl`
- `Corners`

### 21.3 Нови sync endpoint-и

- `POST /api/fixtures/{apiFixtureId}/sync-match-center`
- `POST /api/fixtures/{apiFixtureId}/sync-corners`
- `POST /api/fixtures/sync-live-match-center`

`POST /api/fixtures/{apiFixtureId}/sync-match-center` query параметри:
- `includePlayers` - optional, default `true`
- `force` - optional, default `false`

`POST /api/fixtures/{apiFixtureId}/sync-corners` query parameters:
- `force` - optional, default `false`

Behavior:
- performs a targeted statistics-only refresh for the fixture
- this is useful when the frontend/admin needs corners without re-syncing events, lineups and players

Response:
- `ApiFixtureId`
- `LeagueApiId`
- `Season`
- `StateBucket`
- `Forced`
- `StatisticsSynced`
- `SkippedComponents`
- `ExecutedAtUtc`
- `Freshness`
- `Corners`

`POST /api/fixtures/sync-live-match-center` query параметри:
- `leagueId` - optional
- `season` - optional
- `maxFixtures` - optional, default `10`, clamp `1..20`
- `includePlayers` - optional, default `false`
- `force` - optional, default `false`

### 21.4 Rate-aware sync стратегия

За да пази лимита от 5000 заявки/ден, Stage 2 sync логиката е selective:
- повечето read endpoint-и никога не удрят API-Football
- изключение са live `GET /api/fixtures/{apiFixtureId}/statistics`, `GET /api/fixtures/{apiFixtureId}/corners` и `GET /api/fixtures/{apiFixtureId}/match-center`, които могат да направят statistics-only catch-up
- read-driven catch-up-ът е throttled до най-много веднъж на 1 минута за fixture и има кратък in-memory dedupe прозорец за паралелни заявки
- live events се refresh-ват най-много веднъж на 15 секунди
- live statistics се refresh-ват най-много веднъж на 1 минута
- live player statistics се refresh-ват най-много веднъж на 1 минута и по подразбиране са изключени в batch live sync
- lineups се refresh-ват около kickoff и по-рядко, вместо на всеки live poll
- finished fixture-ите имат максимум 2 post-finish match-center refresh-а и после спират

Това значи:
- frontend-ът чете от DB
- scheduler/admin flow пуска targeted sync endpoint-ите
- live screen може да ползва `sync-live-match-center` без да sync-ва players по подразбиране

### 21.5 Sync status

`GET /api/sync-status` и `GET /api/supported-leagues` вече връщат и:
- `EventsLastSyncedAtUtc`
- `StatisticsLastSyncedAtUtc`
- `LineupsLastSyncedAtUtc`
- `PlayerStatisticsLastSyncedAtUtc`

### 21.6 Applying The Stage 2 Schema

Генерираната migration е:
- `Migrations/20260331143110_Stage2MatchCenter.cs`

Готов SQL fallback:
- `sql/stage2_match_center.sql`

## 22. Stage 3 Preview

### 22.1 Нови таблици

- `fixture_predictions`
- `fixture_injuries`

Допълнително `fixtures` вече пази:
- `last_prediction_synced_at_utc`
- `last_injuries_synced_at_utc`

### 22.2 Нови read endpoint-и

Base route:

`/api/fixtures`

Нови route-ове:
- `GET /api/fixtures/{apiFixtureId}/preview`
- `GET /api/fixtures/{apiFixtureId}/injuries`
- `GET /api/fixtures/{apiFixtureId}/head-to-head`
- `GET /api/fixtures/{apiFixtureId}/predictions`

Поведение:
- всички тези endpoint-и четат само от локалната база
- не правят директни remote calls към API-Football
- `head-to-head` и last 5 form се изчисляват от локално sync-натите fixtures, за да не се хаби quota за отделен H2H sync

`GET /api/fixtures/{apiFixtureId}/preview` комбинира:
- `Detail`
- `Prediction`
- `Injuries`
- `HomeRecentForm`
- `AwayRecentForm`
- `HeadToHead`

`Detail.Freshness` вече съдържа и:
- `LastPredictionSyncedAtUtc`
- `LastInjuriesSyncedAtUtc`

### 22.3 Нови preview sync endpoint-и

- `POST /api/fixtures/{apiFixtureId}/sync-preview`
- `POST /api/fixtures/sync-upcoming-previews`

`POST /api/fixtures/{apiFixtureId}/sync-preview` query параметри:
- `force` - optional, default `false`

`POST /api/fixtures/sync-upcoming-previews` query параметри:
- `leagueId` - optional
- `season` - optional
- `windowHours` - optional, default `24`, clamp `1..48`
- `maxFixtures` - optional, default `10`, clamp `1..25`
- `force` - optional, default `false`

### 22.4 Preview refresh стратегия

Stage 3 preview sync логиката следва 3 pre-match прозореца:
- `T-24h` initial snapshot
- `T-3h` refresh
- `T-1h` final refresh

Как е реализирано:
- ако fixture е извън тези прозорци, preview sync се skip-ва освен при `force=true`
- ако за текущия прозорец вече има sync, не се прави нова remote заявка
- predictions и injuries се sync-ват независимо едно от друго, така че coverage липса или празен response да не блокира целия preview

Това пази лимита от 5000 заявки/ден, защото:
- read endpoint-ите не удрят API-Football
- на fixture обикновено се правят максимум 2 remote calls на preview stage: една за predictions и една за injuries
- за един upcoming fixture нормалният лимит е максимум 3 preview refresh-а общо, освен ако не се ползва `force`

### 22.5 H2H и form

`GET /api/fixtures/{apiFixtureId}/head-to-head`:
- използва локалните finished fixtures между същите два отбора
- връща summary counts и последните срещи

Last 5 form в preview:
- смята се от локалните finished fixtures на двата отбора преди kickoff-а на preview fixture-а
- понастоящем използва локално наличните fixtures за същия сезон

Това е съзнателен компромис за quota efficiency. Ако по-късно ви трябва remote fallback, може да се добави отделно.

### 22.6 Sync status

`GET /api/sync-status` и `GET /api/supported-leagues` вече връщат и:
- `PredictionsLastSyncedAtUtc`
- `InjuriesLastSyncedAtUtc`

### 22.7 Applying The Stage 3 Schema

Генерираната migration е:
- `Migrations/20260331144838_Stage3Preview.cs`

Готов SQL fallback:
- `sql/stage3_preview.sql`
## 23. Stage 4 League And Team Analytics

### 23.1 Goal

Stage 4 adds DB-backed league and team analytics pages without live polling:
- team season statistics
- recent team form
- league rounds
- current round
- top scorers
- top assists
- top cards
- one aggregated league dashboard endpoint for the frontend

Reads stay database-only. Remote API-Football calls happen only through explicit sync endpoints.

### 23.2 New Tables

- `team_statistics`
- `league_rounds`
- `league_top_scorers`
- `league_top_assists`
- `league_top_cards`

### 23.3 Team Endpoints

Base route:

`/api/teams`

#### `POST /api/teams/statistics/sync`

Purpose:
- syncs season team statistics for one league-season
- writes to `team_statistics`
- updates sync state `team_statistics`

Query params:
- `leagueId` - required, API-Football league id
- `season` - required
- `teamId` - optional, API-Football team id
- `maxTeams` - optional, default `25`, clamp `1..40`
- `force` - optional, default `false`

Behavior:
- by default skips rows refreshed in the last 20 hours
- if `teamId` is provided, sync scope is reduced to one team
- otherwise teams are resolved locally from fixtures first, standings second
- this is intentionally quota-aware and optimized for low-frequency refresh, not live polling

Automation note:
- this endpoint is currently a manual/targeted sync flow
- in the current core-data runtime, `POST /api/preload/run` does not auto-sync `team_statistics`
- the primary automation worker also does not treat `team_statistics` as part of the default rolling core-data refresh set
- frontend pages that read `GET /api/teams/{apiTeamId}/statistics` or `GET /api/teams/{apiTeamId}/form` depend on previously stored local data from manual sync or older bootstrap runs

#### `GET /api/teams/{apiTeamId}/statistics?leagueId=&season=`

Purpose:
- returns one stored team statistics snapshot from `team_statistics`

Response:
- `TeamStatisticsDto`

Returns:
- `404` if no stored statistics exist for that team/league/season

#### `GET /api/teams/{apiTeamId}/form?leagueId=&season=&last=`

Purpose:
- returns the latest finished fixtures for the team inside the given league-season
- computed locally from `fixtures`

Query params:
- `leagueId` - required
- `season` - required
- `last` - optional, default `5`, clamp `1..20`

Response:
- `TeamRecentFormDto`

Returns:
- `404` if the team or league-season does not exist locally

### 23.4 League Endpoints

Base route:

`/api/leagues`

#### `POST /api/leagues/{apiLeagueId}/analytics/sync?season=&force=`

Purpose:
- syncs one league-season analytics package:
  - rounds
  - top scorers
  - top assists
  - top cards

Behavior:
- respects coverage flags when they exist
- skips fresh sync states from the last 20 hours unless `force=true`

#### `POST /api/leagues/analytics/sync?season=&activeOnly=&maxLeagues=&force=`

Purpose:
- batch sync over `supported_leagues`

Query params:
- `season` - optional
- `activeOnly` - optional, default `true`
- `maxLeagues` - optional, default `10`, clamp `1..25`
- `force` - optional, default `false`

Quota note:
- worst case per league is 6 API calls:
  - 2 for rounds (`all` + `current`)
  - 1 for top scorers
  - 1 for top assists
  - 2 for top cards (`yellow` + `red`)
- daily freshness checks are there specifically to stay below the 5000/day budget

#### `GET /api/leagues/{apiLeagueId}/rounds?season=`

Purpose:
- returns locally stored rounds ordered by `SortOrder`

Response:
- array of `LeagueRoundDto`

#### `GET /api/leagues/{apiLeagueId}/current-round?season=`

Purpose:
- returns the locally marked current round

Response:
- `LeagueRoundDto`

Returns:
- `404` if the league-season does not exist or if no current round is stored yet

#### `GET /api/leagues/{apiLeagueId}/top-scorers?season=`

Purpose:
- returns locally stored scorers leaderboard

Response:
- array of `LeagueTopPlayerDto`

#### `GET /api/leagues/{apiLeagueId}/top-assists?season=`

Purpose:
- returns locally stored assists leaderboard

Response:
- array of `LeagueTopPlayerDto`

#### `GET /api/leagues/{apiLeagueId}/top-cards?season=`

Purpose:
- returns merged yellow/red card leaderboard from local storage

Response:
- array of `LeagueTopPlayerDto`

#### `GET /api/leagues/{apiLeagueId}/dashboard?season=`

Purpose:
- aggregated frontend endpoint
- combines:
  - rounds
  - current round
  - top scorers
  - top assists
  - top cards

Response:
- `LeagueDashboardDto`

### 23.5 DTO Summary

`TeamStatisticsDto`:
- team identity
- league identity
- season
- `Form`
- `FixturesPlayed`
- `Wins`
- `Draws`
- `Losses`
- `GoalsFor`
- `GoalsForAverage`
- `GoalsAgainst`
- `GoalsAgainstAverage`
- `CleanSheets`
- `FailedToScore`
- `Biggest`
- `SyncedAtUtc`

`LeagueRoundDto`:
- `RoundName`
- `SortOrder`
- `IsCurrent`
- `SyncedAtUtc`

`LeagueTopPlayerDto`:
- rank
- player/team identity
- appearances/minutes/position/rating
- scorer/assist/card-specific metrics depending on the leaderboard
- `SyncedAtUtc`

`LeagueDashboardDto`:
- `LeagueApiId`
- `LeagueName`
- `Season`
- `CurrentRound`
- `Rounds`
- `TopScorers`
- `TopAssists`
- `TopCards`

### 23.6 Sync Status Integration

`GET /api/sync-status` and `GET /api/supported-leagues` now also expose:
- `TeamStatisticsLastSyncedAtUtc`
- `RoundsLastSyncedAtUtc`
- `TopScorersLastSyncedAtUtc`
- `TopAssistsLastSyncedAtUtc`
- `TopCardsLastSyncedAtUtc`

### 23.7 Database Apply

Migration:
- `Migrations/20260331151223_Stage4Analytics.cs`

SQL fallback:
- `sql/stage4_analytics.sql`

### 23.8 Example Requests

```http
POST /api/teams/statistics/sync?leagueId=39&season=2025&maxTeams=20&force=false
```

```http
GET /api/teams/33/statistics?leagueId=39&season=2025
```

```http
GET /api/teams/33/form?leagueId=39&season=2025&last=5
```

```http
POST /api/leagues/39/analytics/sync?season=2025&force=false
```

```http
GET /api/leagues/39/dashboard?season=2025
```

### 23.9 Quota Strategy

Stage 4 is designed around daily or otherwise low-frequency sync only:
- team statistics should be refreshed daily, or right after a completed match if needed
- rounds should be refreshed daily
- top scorers, assists and cards should be refreshed daily
- frontend pages should never call API-Football directly through read endpoints

This keeps the feature content-rich while avoiding unnecessary live polling.

## 24. Stage 5 Odds Analytics

### 24.1 Goal

Stage 5 turns the odds layer from raw snapshot storage into usable analytics:
- opening odd
- latest odd
- peak odd
- closing odd
- delta by bookmaker
- max spread between bookmakers
- consensus and value signal read models

### 24.2 New Tables

- `odds_open_close`
- `odds_movements`
- `market_consensus`

All three are derived from `pre_match_odds`. They do not require extra API-Football calls to populate if snapshots already exist locally.

### 24.3 New Endpoints

Base route:

`/api/fixtures/{apiFixtureId}/odds`

#### `GET /api/fixtures/{apiFixtureId}/odds/history?marketName=`

Purpose:
- returns grouped raw odds history per bookmaker
- includes opening/latest/peak/closing summary for each bookmaker

Response:
- `FixtureOddsHistoryDto`

#### `GET /api/fixtures/{apiFixtureId}/odds/movement?marketName=`

Purpose:
- returns bookmaker-by-bookmaker movement analytics
- exposes delta, change percent and swing

Response:
- array of `OddsMovementDto`

#### `GET /api/fixtures/{apiFixtureId}/odds/consensus?marketName=`

Purpose:
- returns aggregated market-level view for the fixture
- includes:
  - opening consensus
  - latest consensus
  - best current odds
  - bookmaker carrying the best price
  - max spread between bookmakers

Response:
- `OddsConsensusDto`

#### `GET /api/fixtures/{apiFixtureId}/odds/value-signals?marketName=`

Purpose:
- returns outcome-level value signals for `Home`, `Draw`, `Away`
- compares best available price to latest consensus

Response:
- `OddsValueSignalsDto`

#### `POST /api/odds/analytics/rebuild?leagueId=&season=&apiFixtureId=&marketName=`

Purpose:
- rebuilds Stage 5 derived tables from existing local snapshots only
- does not hit API-Football
- useful for backfill after database migration or after importing historic `pre_match_odds`

Validation:
- requires either `apiFixtureId` or `leagueId + season`

### 24.4 Automatic Rebuild Flow

`POST /api/odds/sync` now:
- imports raw snapshots into `pre_match_odds`
- updates bookmakers
- automatically rebuilds Stage 5 derived analytics for the touched fixtures

This means new history/movement/consensus/value-signal data stays aligned with each odds sync.

### 24.5 Derived Fields

`odds_open_close` stores per fixture + bookmaker + market:
- `OpeningHomeOdd`, `OpeningDrawOdd`, `OpeningAwayOdd`
- `LatestHomeOdd`, `LatestDrawOdd`, `LatestAwayOdd`
- `PeakHomeOdd`, `PeakDrawOdd`, `PeakAwayOdd`
- `ClosingHomeOdd`, `ClosingDrawOdd`, `ClosingAwayOdd`
- `SnapshotCount`

`odds_movements` stores per fixture + bookmaker + market:
- `HomeDelta`, `DrawDelta`, `AwayDelta`
- `HomeChangePercent`, `DrawChangePercent`, `AwayChangePercent`
- `HomeSwing`, `DrawSwing`, `AwaySwing`

`market_consensus` stores per fixture + market:
- opening consensus odds
- latest consensus odds
- best current odds
- best bookmaker ids
- `MaxHomeSpread`, `MaxDrawSpread`, `MaxAwaySpread`

### 24.6 Sync Strategy With 5000 Requests/Day

Stage 5 itself does not add new remote read pressure on API-Football, because analytics are derived locally.

Recommended scheduler strategy:
- daily fixtures: run odds sync less often
- matchday fixtures: run odds sync more often
- last 6 hours pre-match: run odds sync most often

Practical rule:
- rebuild analytics every time after a raw odds sync
- use `POST /api/odds/analytics/rebuild` for local backfill instead of rerunning API imports

### 24.7 Database Apply

Migration:
- `Migrations/20260331153206_Stage5OddsAnalytics.cs`

SQL fallback:
- `sql/stage5_odds_analytics.sql`

If your database is still only on Stage 1 and you have not applied Stage 2, Stage 3 and Stage 4 changes yet, use:
- `sql/pending_stage2_to_stage5.sql`

## 25. Stage 6: Live Status And Metadata Enrichment

Stage 6 adds two practical improvements:
- richer metadata from existing `/teams` and `/fixtures` sync responses, without increasing request count for those sync jobs
- a lightweight live-status sync endpoint that updates live scores and statuses with a single API-Football call for the targeted leagues

### 25.1 `POST /api/fixtures/sync-live-status`

Purpose:
- updates live fixture state from API-Football using `/fixtures?live=...`
- refreshes local score, status, elapsed, referee, venue and round data
- can scope itself only to active `supported_leagues`, which is the safer default when you want tighter control over provider usage

Query parameters:
- `leagueId` - optional
- `activeOnly` - optional, default `true`

Behavior:
- if `leagueId` is provided: sync runs only for this league id
- if `leagueId` is missing and `activeOnly=true`: sync uses distinct active league ids from `supported_leagues`
- if there are no active supported leagues and no `leagueId`, the endpoint returns without making a remote call
- besides true `/fixtures?live=...` matches, the sync also runs a small catch-up batch for recently kicked off local fixtures that still look `NS/TBD` or stale live locally
- this catches transitions such as `NS -> FT` even when the provider live window was missed between two heartbeat runs
- sync state `fixtures_live` is updated for every touched league/season

Response:
- `LiveFixtureStatusSyncResultDto`

`LiveFixtureStatusSyncResultDto`:
- `ScopedToActiveSupportedLeagues`
- `TargetLeagueCount`
- `LiveFixturesReceived`
- `FixturesProcessed`
- `FixturesInserted`
- `FixturesUpdated`
- `FixturesUnchanged`
- `FixturesSkippedMissingLeague`
- `FixturesSkippedMissingTeams`
- `ExecutedAtUtc`

Quota note:
- this endpoint uses one remote API call per execution, not one call per live fixture
- it is intended to be the cheap live heartbeat before heavier match-center refreshes

### 25.2 Team Metadata Enrichment

`GET /api/teams` now returns additional `TeamDto` fields:
- `Founded`
- `IsNational`
- `VenueName`
- `VenueAddress`
- `VenueCity`
- `VenueCapacity`
- `VenueSurface`
- `VenueImageUrl`

These fields are synced from the existing `/teams?league=&season=` response, so team sync request cost stays the same.

### 25.3 Fixture Metadata Enrichment

`FixtureDto` now also exposes:
- `StatusLong`
- `Elapsed`
- `StatusExtra`
- `Referee`
- `Timezone`
- `VenueName`
- `VenueCity`
- `Round`

These fields are stored during:
- `POST /api/fixtures/sync`
- `POST /api/fixtures/sync-upcoming`
- `POST /api/fixtures/sync-live-status`

This means fixture detail and fixture list endpoints become more frontend-friendly without extra read-time calls.

### 25.4 Fixture Detail Freshness

`FixtureFreshnessDto` now also contains:
- `LastLiveStatusSyncedAtUtc`

`FixtureDetailDto` now also contains:
- `FixturesLiveLastSyncedAtUtc`

This makes it easier to distinguish:
- when the live score/status was last checked
- when deeper match-center components were last refreshed

### 25.5 Sync Status Additions

`GET /api/sync-status` now also exposes:
- `FixturesLiveLastSyncedAtUtc`

This is the league-level freshness marker for the new lightweight live sync flow.

### 25.6 Database Apply

EF migration:
- `Migrations/20260331185318_Stage6LiveStatusAndMetadata.cs`

Generated EF script:
- `sql/stage6_live_status_and_metadata.sql`

Safe manual SQL fallback:
- `sql/stage6_live_status_and_metadata_manual.sql`

## 26. Stage 7: Fixture Batch Sync And Live Odds

Stage 7 adds:
- batched live match-center refreshes through API-Football `fixtures?ids=...`
- live odds snapshot storage through `/odds/live`
- local cache for live bet type IDs through `/odds/live/bets`

### 26.1 Batched `sync-live-match-center`

The public endpoint is unchanged:
- `POST /api/fixtures/sync-live-match-center`

But the implementation is now more quota-efficient:
- the service first decides which live fixtures actually need events/statistics/lineups/players refresh
- then it fetches them in chunks through `GET /fixtures?ids=...`
- this reduces the number of remote calls compared to one call per fixture component

Practical effect:
- one batch call can return events, lineups, statistics and players for several fixtures together
- this is the main quota-saving change for heavy live match-center usage

### 26.2 `POST /api/odds/live-bets/sync`

Purpose:
- syncs the live bet type reference list from API-Football `/odds/live/bets`
- stores the reference locally in `live_bet_types`

Response:
- `LiveBetTypesSyncResultDto`

### 26.3 `GET /api/odds/live-bets`

Purpose:
- returns locally stored live bet type IDs
- these IDs are used only for API-Football live odds filtering via `betId`

Important:
- live bet IDs are separate from pre-match bet IDs
- do not reuse `/odds/bets` ids for `/odds/live`
- The Odds API live 1X2 flow does not use these ids

Response:
- array of `LiveBetTypeDto`

### 26.4 `POST /api/odds/live/sync`

Purpose:
- imports current live odds snapshots from API-Football `/odds/live`
- stores snapshots in `live_odds`
- updates existing bookmakers if their names changed

Query parameters:
- `fixtureId` - optional
- `leagueId` - optional
- `betId` - optional

Validation:
- requires either `fixtureId` or `leagueId`

Behavior:
- `fixtureId` and `leagueId` use API ids
- `betId` must come from `GET /api/odds/live-bets`
- when `betId` is omitted, the backend now keeps only the live 1X2 market by default
- any live market whose name contains `1x2` is kept
- names such as `1x2 - 10 min`, `1x2 - 20 min`, `1x2 - 30 min` remain distinct and are not collapsed into one label
- the live odds provider currently appears in two shapes and the backend accepts both:
  - legacy `bookmakers[].bets[].values`
  - direct `odds[].values`
- current production behavior indicates that live `odds[]` are a single-source feed for `Bet365`, not a multi-bookmaker market
- when the provider omits bookmaker information and returns direct `odds[]`, the backend materializes the rows under `Bet365`
- if `Bet365` already exists in the local bookmaker catalog, the backend reuses its real `ApiBookmakerId`
- if the local bookmaker catalog does not have `Bet365` yet, the temporary fallback is:
  - `ApiBookmakerId = 0`
  - `Bookmaker = Bet365`
- `odds[].id` is the live bet/market id, not the bookmaker id
- snapshots are stored only when the latest local value has changed
- sync state `live_odds` is updated per touched league/season

Response:
- `LiveOddsSyncResultDto`

Useful diagnostic fields in `LiveOddsSyncResultDto`:
- `BookmakerApiId`
- `Bookmaker`
- `BookmakerIdentityType`
- `ProviderFixturesReceived`
- `ProviderBookmakersReceived`
- `ProviderReturnedEmpty`
- `UsedLeagueFallback`
- `FallbackLeagueApiId`
- `LocalFixturesResolved`
- `ProviderFixtureApiIdsSample`
- `MissingFixtureApiIdsSample`

Contract note:
- `BookmakerApiId` and `Bookmaker` describe the resolved bookmaker identity actually used for storage, only when that identity is unambiguous
- in the current live direct-odds flow this usually resolves to `Bet365`
- if a response spans multiple real bookmakers from a legacy provider shape, the single resolved bookmaker fields can stay empty

### 26.5 `GET /api/odds/live`

Purpose:
- reads stored live odds from the local database

Query parameters:
- `fixtureId` - optional local fixture id
- `apiFixtureId` - optional API fixture id
- `betId` - optional live bet id
- `latestOnly` - optional, default `true`

Validation:
- requires either `fixtureId` or `apiFixtureId`

Response:
- array of `LiveOddsMarketDto`

`LiveOddsMarketDto`:
- `FixtureId`
- `ApiFixtureId`
- `BookmakerId`
- `ApiBookmakerId`
- `Bookmaker`
- `BookmakerIdentityType`
- `ApiBetId`
- `BetName`
- `CollectedAtUtc`
- `LastSnapshotCollectedAtUtc`
- `LastSyncedAtUtc`
- `Values`

Important note:
- `ApiBetId` and `BetName` identify the live market, not the casino/bookmaker
- in the current production provider behavior, live odds effectively come from a single source bookmaker: `Bet365`
- when API-Football returns direct `odds[]` without bookmaker-level data, the backend resolves them to `Bet365`
- `BookmakerIdentityType` is:
  - `real` when the row is attached to a real bookmaker id
  - `synthetic` only as a fallback when the local catalog still cannot resolve `Bet365`
- when real bookmaker rows exist for the same fixture, the read path prefers them and suppresses synthetic fallback rows

`LiveOddsValueDto`:
- `OutcomeLabel`
- `Line`
- `Odd`
- `IsMain`
- `Stopped`
- `Blocked`
- `Finished`

Freshness note:
- `CollectedAtUtc` now represents the effective freshness of the live odds market when `latestOnly=true`
- if the worker polled the provider again but the values did not change, `CollectedAtUtc` can still move forward through the league-season `live_odds` sync state
- `LastSnapshotCollectedAtUtc` keeps the timestamp of the last stored changed snapshot
- `LastSyncedAtUtc` shows the last successful live odds refresh for the league-season behind the fixture

### 26.6 `GET /api/fixtures/{apiFixtureId}/odds/live`

Purpose:
- convenience read endpoint for live odds scoped by fixture

Query parameters:
- `betId` - optional
- `latestOnly` - optional, default `true`

Response:
- same payload as `GET /api/odds/live`

### 26.7 `SignalR /hubs/live-odds`

Purpose:
- pushes realtime live odds updates after changed snapshots are stored
- complements the REST live odds endpoints, it does not replace them

Recommended frontend flow:
- initial snapshot through `GET /api/fixtures/{apiFixtureId}/odds/live`
- realtime updates through SignalR on `/hubs/live-odds`
- subscribe with `JoinFixture(apiFixtureId)`

Hub route:
- `/hubs/live-odds`

Hub methods:
- `JoinFixture(long apiFixtureId)`
- `LeaveFixture(long apiFixtureId)`
- `JoinLeague(long leagueId)`
- `LeaveLeague(long leagueId)`

Event name:
- `LiveOddsUpdated`

Payload:
- `FixtureId`
- `ApiFixtureId`
- `LeagueApiId`
- `CollectedAtUtc`
- `Markets`

Notes:
- `Markets` uses the same `LiveOddsMarketDto` shape returned by the REST endpoint
- broadcasts happen only when the live odds sync writes changed snapshots
- browser clients should pass a JWT through `access_token` on the hub connection
- `X-API-KEY` remains only as a transitional bridge for authenticated token minting through `POST /api/auth/token`

### 26.7 Sync Status Additions

`GET /api/sync-status` now also exposes:
- global `live_bet_types`
- league-level `LiveOddsLastSyncedAtUtc`

### 26.8 Database Apply

EF migration:
- `Migrations/20260331190852_Stage7LiveOddsAndFixtureBatchSync.cs`

Generated EF script:
- `sql/stage7_live_odds_and_fixture_batch_sync.sql`

Safe manual SQL fallback:
- `sql/stage7_live_odds_and_fixture_batch_sync_manual.sql`

## 27. Stage 8: Removed Live Automation Worker

The old Stage 8 `LiveAutomation` worker has been removed from the current runtime.

Current behavior:
- there is no `LiveAutomation` config section anymore
- `LiveAutomationBackgroundService` and `LiveAutomationOrchestrator` are no longer part of the running app
- automatic refresh orchestration now runs through:
  - `CoreDataAutomationBackgroundService`
  - `CoreDataAutomationOrchestrator`

Important:
- read endpoints still stay database-only
- there is no database migration associated with removing the legacy worker

## 28. Stage 9: Production Hardening

Stage 9 improves operational stability and quota efficiency without changing the schema.

### 28.1 API Quota Telemetry

`GET /api/sync-status` now also returns runtime API-Football quota information under:
- `ApiQuota`

`ApiQuotaStatusDto`:
- `Provider`
- `Mode`
- `RequestsDailyLimit`
- `RequestsDailyRemaining`
- `RequestsMinuteLimit`
- `RequestsMinuteRemaining`
- `LastObservedAtUtc`

`Mode` values:
- `Normal`
- `Low`
- `Critical`

This data is captured from API-Football response headers and kept in memory by the app instance.

### 28.2 Outbound Request Throttling

The API client now applies:
- minimum request spacing
- additional delay in `Low` quota mode
- stronger delay in `Critical` quota mode

Config section:

`ApiFootballClient`

Fields:
- `MinRequestSpacingMs`
- `LowMinuteRemainingThreshold`
- `CriticalMinuteRemainingThreshold`
- `LowDailyRemainingThreshold`
- `CriticalDailyRemainingThreshold`
- `LowQuotaDelayMs`
- `CriticalQuotaDelayMs`

### 28.3 Core Automation Quota Guards

The primary automation worker now uses quota mode and daily budgets as extra guards:
- live odds auto-sync is skipped in `Critical`
- pre-match odds and repair work are also skipped in `Critical`
- team, standings, fixture and live-odds batch sizes are reduced when quota becomes `Low` or `Critical`

This keeps the cheapest core refreshes available the longest.

### 28.4 Data Retention Worker

A new background cleanup worker periodically trims:
- `sync_errors`
- `live_odds`
- `the_odds_live_odds`
- `pre_match_odds`
- `odds_open_close`
- `odds_movements`
- `market_consensus`

Current optimization behavior:
- non-1X2 rows in `live_odds` are automatically normalized/cleaned and are no longer kept by default
- raw `live_odds` snapshots are still capped by `LiveOddsRetentionDays`
- raw `the_odds_live_odds` snapshots follow the same live retention window as `live_odds`
- additionally, live odds for fixtures that are no longer live are trimmed much earlier through `LiveOddsFinishedFixtureRetentionHours`
- raw `pre_match_odds` snapshots are trimmed by fixture age through `PreMatchOddsRetentionDays`
- compact derived odds analytics can now live longer than the raw snapshots through `DerivedOddsAnalyticsRetentionDays`
- this keeps the useful compact read models longer while reducing table growth from raw odds history

Config section:

`DataRetention`

Fields:
- `Enabled`
- `IntervalHours`
- `ErrorRetryMinutes`
- `SyncErrorsRetentionDays`
- `LiveOddsRetentionDays`
- `LiveOddsFinishedFixtureRetentionHours`
- `PreMatchOddsRetentionDays`
- `DerivedOddsAnalyticsRetentionDays`

Current default example:
- `LiveOddsRetentionDays = 3`
- `LiveOddsFinishedFixtureRetentionHours = 18`
- `PreMatchOddsRetentionDays = 14`

Database apply:
- EF migration: `Migrations/20260408191524_Stage10OddsRetentionOptimization.cs`
- SQL fallback: `sql/stage10_odds_retention_optimization.sql`

### 28.5 Bookmakers Sync Change

`POST /api/bookmakers/sync` is now local-cache based:
- it no longer downloads the heavy API-Football odds dataset just to discover bookmaker names
- it rebuilds bookmaker scope from already stored `pre_match_odds` and `live_odds`

Response now also includes:
- `Source`
- `RemoteCallsMade`
- `PreMatchOddsReferences`
- `LiveOddsReferences`

### 28.6 Database Apply

No new migration and no new SQL script are required for Stage 9.

## 29. Stage 10: Core-Data Automation Refactor

Stage 10 changes the primary backend role.

The backend is now optimized to automatically cache only the core datasets needed by SmartBets:
- countries
- leagues
- teams
- fixtures
- pre-match odds
- live odds
- bookmakers

Rich match detail datasets are still supported by the existing endpoints, but they are no longer part of the main automatic refresh pipeline:
- events
- lineups
- player stats
- injuries
- predictions
- previews
- team analytics
- league analytics

These remain available as legacy/manual flows.

### 29.1 Primary Automation Model

The old live-only worker has been removed from the runtime.

The new primary worker is:
- `CoreDataAutomationBackgroundService`
- `CoreDataAutomationOrchestrator`

It runs as an internal `BackgroundService` and does not need an external cron job.

The new primary config section is:

`CoreDataAutomation`

### 29.2 `supported_leagues` After Stage 10

`supported_leagues` is no longer used as the ingest gate for automatic core-data sync.

Its role is now:
- pinned or priority list for UI/admin usage
- optional business curation layer
- backward-compatible metadata for older read/admin screens

Automatic ingestion now targets all current league-seasons discovered from API-Football `/leagues?current=true`.

### 29.3 New Automatic Refresh Scope

The primary automatic flow now does this:

1. Daily catalog refresh
- `countries`
- full leagues catalog
- current league-season target list
- bookmaker reference list through `/odds/bookmakers`

2. Rolling team refresh
- all current league-seasons
- one league-season is synced only when due

3. Rolling standings refresh
- all current league-seasons with standings coverage
- hot leagues are refreshed more often than the daily baseline

4. Rolling fixture refresh
- full fixture sync for all current league-seasons
- extra hot-window fixture refresh for leagues with matches around `now`

5. Live scoreboard refresh
- `fixtures?live=all`
- no `supported_leagues` restriction

6. Rolling pre-match odds refresh
- only for league-seasons with fixtures in the next 72 hours
- refresh cadence depends on kickoff proximity

7. Live odds refresh
- enabled by default in the new model
- scoped only to currently live leagues

8. Repair pass
- periodic recovery sync for hot league-seasons

Internally these now run as separate automation jobs:
- `catalog_refresh`
- `teams_rolling`
- `standings_rolling`
- `fixtures_rolling`
- `odds_pre_match`
- `odds_live`
- `repair`

### 29.4 Default Timing

Default `CoreDataAutomation` timings in `appsettings.json`:

- `ActiveIntervalSeconds = 30`
- `IdleIntervalSeconds = 120`
- `CatalogRefreshHours = 24`
- `BookmakersReferenceRefreshHours = 24`
- `LiveStatusIntervalSeconds = 45`
- `LiveStatusEndgameIntervalSeconds = 20`
- `LiveStatusEndgameElapsedMinutes = 80`
- `TeamsIntervalHours = 24`
- `StandingsIntervalHours = 24`
- `StandingsHotIntervalHours = 6`
- `FixturesBaselineIntervalHours = 12`
- `FixtureHotIntervalMinutes = 120`
- `FixtureHotLookbackHours = 10`
- `FixtureHotLookaheadHours = 24`
- `OddsLookaheadHours = 144`
- `OddsFarIntervalHours = 6`
- `OddsMidIntervalHours = 3`
- `OddsNearIntervalMinutes = 40`
- `OddsFinalIntervalMinutes = 20`
- `MaxOddsFixturesPerCycle = 24`
- `LiveOddsIntervalSeconds = 40`
- `MaxLiveOddsLeaguesPerCycle = 10`
- `RepairIntervalHours = 4`
- `AllowAllLiveOddsMarkets = false`

Live odds source note:
- the current production provider behavior is effectively single-source for `Bet365`
- the old per-bookmaker live fan-out config/state has been removed from the active automation path
- when live fixtures enter the closing phase, the automation loop temporarily tightens the live-status heartbeat and prioritizes status catch-up for fixtures that may have just fallen out of the provider live feed

### 29.5 Budget Strategy For 75 000 Requests/Day

The new defaults are designed to stay comfortably below the daily limit, not to spend the entire limit.

Operational strategy:
- keep the heavy work rolling and distributed
- avoid full-resync bursts
- prefer one global live heartbeat call over many per-league live polls
- refresh odds only for league-seasons with near fixtures
- keep quota telemetry visible, but do not preemptively stop automation because of internal daily safety guards

Quota guard defaults:
- `LowDailyRemainingThreshold = 10000`
- `CriticalDailyRemainingThreshold = 2500`

Core automation daily budget defaults:
- `AutomationDailyBudget = 75000`
- `ProviderDailySafetyBuffer = 0`
- `CatalogRefreshDailyBudget = 500`
- `TeamsRollingDailyBudget = 9000`
- `StandingsRollingDailyBudget = 6000`
- `FixturesRollingDailyBudget = 22000`
- `OddsPreMatchDailyBudget = 22000`
- `OddsLiveDailyBudget = 18000`
- `RepairDailyBudget = 3500`

Current behavior:
- quota telemetry and low/critical modes are still tracked
- request pacing can still slow down a little in low/critical mode
- the automation worker does not preemptively stop jobs just because the internal daily budget or safety buffer has been reached

### 29.6 New Catalog Reference Endpoint Flow

Bookmaker reference sync now has two separate concepts:

1. `POST /api/bookmakers/sync`
- local cache rebuild for one league-season
- uses already stored odds data

2. `POST /api/bookmakers/sync-reference`
- global bookmaker reference refresh from API-Football `/odds/bookmakers`
- updates global sync state `bookmakers_reference`
- this is what the new automatic worker uses daily

### 29.7 Sync Status Impact

`GET /api/sync-status` now prefers the in-memory current league-season catalog from the new automation layer.

That means:
- the endpoint can now show automation status for all current league-seasons
- `IsActive` and `Priority` still come from `supported_leagues` when such a row exists
- leagues outside `supported_leagues` still appear in the core automation view with default `IsActive=false` and `Priority=0`
- it now also returns `CoreAutomation`, which contains:
  - catalog last refresh timestamp
  - current league-season count
  - total automation budget used/remaining for the UTC day
  - per-job status, last start/completion/skip timestamps, desired vs actual requests and processed items

New global sync-state entries used by Stage 10:
- `leagues_current`
- `bookmakers_reference`

### 29.8 What Is Now Legacy By Default

The following flows still exist, but are no longer automatically refreshed by the primary worker:
- match-center auto-refresh
- preview auto-refresh
- team statistics auto-refresh
- league analytics auto-refresh

They are still available through their current endpoints and can still be called manually.

### 29.9 Automation Window And Historical Seasons

The best-fit production model is now split into two layers:

1. Ongoing automation
- tracks only the active automation window
- default window:
  - `currentYear - 1`
  - `currentYear`
  - `currentYear + 1`
- this window is controlled through:
  - `CoreDataAutomation.AutomationSeasonLookbackYears`
  - `CoreDataAutomation.AutomationSeasonLookaheadYears`

2. Historical bootstrap
- handles older seasons starting from `2023+`
- is triggered manually through:
  - `POST /api/preload/historical`
- is not part of the recurring background worker

This means:
- older seasons still exist in the database
- older seasons can still be imported on demand
- older seasons are not continuously re-polled by the automatic jobs

## 30. Auth

The backend now supports three admin auth paths:
- `Authorization: Bearer {jwt}`
- legacy bridge: `X-API-KEY: {token}`
- admin cookie session through `POST /api/admin/auth/login`

### 30.1 Admin Cookie Session

Purpose:
- browser-friendly admin auth for `/admin` style frontend flows
- avoids exposing the legacy API key in normal admin page usage
- uses an `httpOnly` auth cookie instead of storing the session token in frontend JavaScript

Admin session endpoints:
- `POST /api/admin/auth/login`
- `GET /api/admin/auth/me`
- `POST /api/admin/auth/logout`

`POST /api/admin/auth/login` request body:
- `Username`
- `Password`

Behavior:
- validates credentials from `AdminAuth`
- issues an encrypted/signed auth cookie through ASP.NET Core cookie auth
- signed-in users receive the `admin` role and can call existing `admin-only` endpoints without passing `X-API-KEY`

`GET /api/admin/auth/me`:
- returns the current admin session state
- returns `200` even when not signed in, with `Authenticated = false`
- useful for frontend middleware, admin layout bootstrapping and route guards

`POST /api/admin/auth/logout`:
- clears the admin auth cookie

`AdminSessionDto`:
- `Configured`
- `Authenticated`
- `AuthenticationType`
- `Username`
- `DisplayName`
- `Roles`
- `AuthSource`
- `SessionExpiresAtUtc`

Cookie notes:
- the cookie is `httpOnly`
- outside development it is `Secure`
- `SameSite` is configurable through `AdminAuth:CookieSameSite`
- default cookie name is `oddsdetector_admin`
- if the frontend talks to the backend cross-origin, browser requests must include credentials and the origin must be listed in `CORS:AllowedOrigins`
- for cross-site admin cookie usage the usual production setup is `AdminAuth:CookieSameSite=None` over HTTPS

Relevant config:
- `AdminAuth:Username`
- `AdminAuth:Password`
- `AdminAuth:Users`
- `AdminAuth:CookieName`
- `AdminAuth:SessionHours`
- `AdminAuth:CookieSameSite`
- `AdminAuth:CookieDomain`

Multi-admin note:
- for a single shared admin account you can use `AdminAuth:Username` + `AdminAuth:Password`
- for multiple named admin accounts use `AdminAuth:Users:{index}:Username`, `Password` and optional `DisplayName`

### 30.2 JWT Auth

The backend still supports JWT as the preferred token model for REST and SignalR integrations.

JWT bridge endpoints:
- `POST /api/auth/token`
- `GET /api/auth/me`

`POST /api/auth/token`:
- requires an already authenticated admin caller
- can be called with the legacy API key during the transition
- returns a signed JWT access token for frontend or integration use
- default access-token lifetime is now `60` minutes unless `JwtAuth:AccessTokenMinutes` overrides it

SignalR auth:
- hub route: `/hubs/live-odds`
- browser clients should pass the JWT through `access_token`
- example negotiate path:
  - `POST /hubs/live-odds/negotiate?negotiateVersion=1&access_token={jwt}`
- SignalR no longer accepts the legacy API key through query-string `access_token`

Relevant config:
- `JwtAuth:Issuer`
- `JwtAuth:Audience`
- `JwtAuth:SigningKey`
- `JwtAuth:AccessTokenMinutes`
- `Swagger:Enabled`

Signing key note:
- preferred production setup is a dedicated `JwtAuth:SigningKey` with at least 32 bytes
- if the configured secret is shorter, the backend derives a stable 256-bit HMAC key from it
- production should still use a dedicated JWT signing key instead of relying on the legacy API key secret

## 31. Live Odds List Optimization

`GET /api/fixtures/query` now supports:
- `includeLiveOddsSummary=true`

When enabled, each `FixtureDto` may include:
- `LiveOddsSummary`

`LiveOddsSummary` fields:
- `ApiFixtureId`
- `LeagueApiId`
- `Source`
  - `live`
  - `prematch`
  - `none`
- `CollectedAtUtc`
- `BestHomeOdd`
- `BestHomeBookmaker`
- `BestDrawOdd`
- `BestDrawBookmaker`
- `BestAwayOdd`
- `BestAwayBookmaker`

Important behavior:
- this is a cache-only read path intended for list views
- it does not trigger per-row on-demand live odds sync
- if live odds are missing, the backend falls back to stored pre-match best odds
- for live fixtures this means `source = prematch` or `source = none` does not automatically mean a frontend bug
- it can also mean no usable fresh live odds are currently available from the preferred live provider path for that fixture

Additional batch endpoint:
- `POST /api/odds/live/summary`

Request body:
- `fixtureIds`

SignalR additions:
- event: `LiveOddsSummaryUpdated`
- hub methods:
  - `JoinFixtures(apiFixtureIds[])`
  - `LeaveFixtures(apiFixtureIds[])`
  - `JoinLiveFeed()`
  - `LeaveLiveFeed()`

`LiveOddsSummaryUpdated` is emitted only when the effective summary changes.

## 32. Stage 11: The Odds API Live 1X2 Provider

Stage 11 adds a second live-odds provider alongside API-Football.

Scope of the new provider:
- API-Football remains the source of truth for leagues, teams, fixtures and live status
- The Odds API is used only for live soccer odds
- current scope is only `h2h` / 1X2
- current scope is only regions `uk,eu`

### 32.1 Config

New config section:

`TheOddsApi`

Fields:
- `Enabled`
- `BaseUrl`
- `ApiKey`
- `Regions`
- `MarketKey`
- `OddsFormat`
- `DateFormat`
- `FreshnessSeconds`
- `MatchToleranceMinutes`
- `EnableViewerDrivenRefresh`
- `EnableReadDrivenCatchUp`
- `ViewerHeartbeatTtlSeconds`
- `ViewerRefreshIntervalSeconds`
- `MaxViewerFixturesPerCycle`
- `PriorityKeepaliveCount`
- `MinLeagueSyncIntervalSeconds`
- `LeagueSportKeys`

Important:
- `LeagueSportKeys` is now optional
- if a `leagueId -> sport_key` override is configured there, it wins immediately
- if no override is configured, the backend now tries to resolve and persist the mapping automatically
- Render env vars are no longer required per league for the normal path
- the default config is now intentionally conservative for test quotas
- by default read endpoints do not trigger The Odds provider catch-up when viewer-driven refresh is enabled
- by default the background worker also respects a per-league sync cooldown through `MinLeagueSyncIntervalSeconds`
- by default `PriorityKeepaliveCount = 0`, so no unseen fixtures are refreshed just because they are live

### 32.2 New Table

New table:
- `the_odds_live_odds`

Purpose:
- stores raw live `h2h` snapshots from The Odds API
- keeps provider-native string identities such as event id and bookmaker key, without forcing them into the API-Football live odds schema

### 32.3 New Manual Sync Endpoint

`POST /api/odds/live/the-odds/sync`

Query parameters:
- `apiFixtureId` - optional
- `leagueId` - optional
- `season` - optional

Validation:
- requires either `apiFixtureId`
- or `leagueId + season`

Response:
- `TheOddsLiveOddsSyncResultDto`

Key fields:
- `SourceProvider`
- `CoverageStatus`
- `CoverageMessage`
- `SportKey`
- `SportKeySource`
- `SportKeyConfidence`
- `SportKeyVerified`
- `ProviderEnabled`
- `ProviderConfigured`
- `SkippedReason`
- `ProviderError`
- `ProviderEventsReceived`
- `FixturesMatched`
- `FixturesMissingMatch`
- `BookmakersProcessed`
- `SnapshotsProcessed`
- `SnapshotsInserted`
- `SnapshotsSkippedUnchanged`

Interpretation notes:
- `200 OK` does not necessarily mean rows were inserted
- `CoverageStatus = supported` means the competition is mapped to a supported The Odds market
- `CoverageStatus = unsupported` means the backend could not resolve this competition to a reliable The Odds sport key, so this is a provider coverage limitation rather than an operational failure
- `CoverageStatus = unresolved` means the sync attempt did not establish competition coverage one way or the other, usually because the provider was disabled, not configured or the sync was skipped too early
- if `SportKey` is present but `FixturesMatched = 0`, the sport-key mapping worked and the remaining issue is fixture matching against the provider event window or provider team naming
- if `SkippedReason = provider_sync_failed`, `ProviderError` now contains the useful provider/backend error text instead of returning a generic `500`
- if `SkippedReason = recently_synced`, the backend intentionally skipped a duplicate provider call because the same league-season was refreshed too recently

### 32.3.1 Admin Cache-Or-Refresh Endpoints

These endpoints are intended for admin panels and manual operator actions.

They are different from the normal frontend read flow:
- they first try to serve cached The Odds rows
- if cache is missing, they can trigger a remote refresh
- they also support `force=true` for a deliberate admin override

`POST /api/admin/odds/live/the-odds/refresh-fixture`

Query parameters:
- `apiFixtureId` - required
- `force` - optional, default `false`
- `latestOnly` - optional, default `true`

Behavior:
- if `force=false` and cached The Odds live odds already exist for the fixture, the endpoint returns cached rows without a new provider call
- if cache is missing, it triggers a remote refresh for the fixture's league-season and then returns the latest cached rows
- if `force=true`, it always attempts a remote refresh, even if cache already exists or the league-season was recently synced

Response:
- `ApiFixtureId`
- `KickoffAtUtc`
- `Status`
- `Elapsed`
- `HomeTeamName`
- `AwayTeamName`
- `Forced`
- `ServedFromCache`
- `RefreshedRemotely`
- `HasCachedOdds`
- `MarketsReturned`
- `CoverageStatus`
- `CoverageMessage`
- `Sync`
- `Items`

`POST /api/admin/odds/live/the-odds/refresh-league`

Query parameters:
- `leagueId` - required
- `season` - required
- `force` - optional, default `false`

Behavior:
- scopes itself only to the local live fixtures for that league-season
- if all live fixtures already have cached The Odds summaries and `force=false`, it returns cache-only data
- if some live fixtures are missing cached odds, it triggers a remote refresh for the league-season and then returns the updated cache snapshot
- if `force=true`, it always attempts a remote refresh for the league-season

Response:
- `LeagueApiId`
- `Season`
- `Forced`
- `ServedFromCache`
- `RefreshedRemotely`
- `LiveFixturesInScope`
- `FixturesWithCachedOdds`
- `FixturesMissingCachedOdds`
- `CoverageStatus`
- `CoverageMessage`
- `Sync`
- `Items`

Each item in `Items` contains:
- `ApiFixtureId`
- `KickoffAtUtc`
- `Status`
- `Elapsed`
- `HomeTeamName`
- `AwayTeamName`
- `HasCachedOdds`
- `Summary`

Frontend interpretation for admin flows:
- if `CoverageStatus = unsupported`, show a calm provider-coverage message such as `Live odds are not available for this competition`
- if `CoverageStatus = unresolved` and `SkippedReason` indicates config/provider issues, treat it as an operational/admin issue
- if `CoverageStatus = supported` but `HasCachedOdds = false`, the competition is supported but the current fixture still has no stored live odds snapshot

### 32.4 Viewer-Driven Live Odds Refresh

The Odds API live odds refresh is now viewer-driven and does not depend on the live status sync layer.

Important separation:
- API-Football still owns live fixture status, score and finished-state tracking
- The Odds API is used only for live odds refresh

Global control endpoints:

`GET /api/odds/live/viewers/config`

Purpose:
- read endpoint for the current shared backend heartbeat flag
- this is the endpoint normal frontend pages should read on load before starting viewer keepalive

Response:
- `LiveOddsHeartbeatEnabled`
- `TheOddsProviderEnabled`
- `TheOddsProviderConfigured`
- `ConfigViewerDrivenRefreshEnabled`
- `EffectiveViewerDrivenRefreshEnabled`
- `ReadDrivenCatchUpEnabled`
- `ViewerHeartbeatTtlSeconds`
- `ViewerRefreshIntervalSeconds`
- `UpdatedAtUtc`

Admin endpoints:
- `GET /api/admin/odds/live/the-odds/viewer-refresh`
- `PATCH /api/admin/odds/live/the-odds/viewer-refresh`

`PATCH /api/admin/odds/live/the-odds/viewer-refresh` body:
- `LiveOddsHeartbeatEnabled`

Admin response additionally contains:
- `ActiveFixtureIds`
- `UpdatedBy`

Global control behavior:
- the backend is the source of truth for whether viewer heartbeats are currently allowed
- if the admin disables the shared flag, normal frontend pages should stop sending keepalive
- disabling the flag also clears the current in-memory active fixture registry on the running instance
- this global flag affects only The Odds viewer heartbeat flow
- it does not change API-Football live status refresh
- it does not force read-driven catch-up on or off by itself

New heartbeat endpoint:

`POST /api/odds/live/viewers/heartbeat`

Request body:
- `FixtureIds`

Behavior:
- the frontend should send only the fixtures that are actually visible on screen
- a practical frontend implementation is `IntersectionObserver` + periodic heartbeat
- the frontend should first read `GET /api/odds/live/viewers/config` and only start heartbeat when `EffectiveViewerDrivenRefreshEnabled = true`
- fixtures stay active only for a short TTL window controlled by `ViewerHeartbeatTtlSeconds`
- the backend refresh worker then polls The Odds API only for:
  - active viewed fixtures
  - plus a small priority shortlist controlled by `PriorityKeepaliveCount`
- if there are no active viewers, the background worker does not refresh The Odds live odds at all
- even with active viewers, each league-season is throttled by `MinLeagueSyncIntervalSeconds`

Recommended frontend behavior:
- on page load, read `GET /api/odds/live/viewers/config`
- only enable the heartbeat hook when the backend says `EffectiveViewerDrivenRefreshEnabled = true`
- send heartbeats every `25..30` seconds
- send only visible live fixtures
- stop heartbeats when the tab is hidden
- the backend-side The Odds API refresh loop now runs more conservatively for testing, usually every `180` seconds by default
- more frequent heartbeats do not imply more frequent provider calls; they only keep viewer interest fresh in the backend registry
- this heartbeat affects only live odds refresh priority
- it does not replace or modify the API-Football live status layer

Testing note:
- the default test-friendly settings now reduce quota burn aggressively
- visible fixtures are still the only trigger the frontend needs to send
- the backend can intentionally return cached The Odds rows for a while before the next provider refresh is allowed

Response:
- `ReceivedFixtureIds`
- `AcceptedFixtureIds`
- `ActiveFixtureIds`
- `TouchedAtUtc`
- `ViewerHeartbeatTtlSeconds`
- `LiveOddsHeartbeatEnabled`
- `EffectiveViewerDrivenRefreshEnabled`
- `HeartbeatAccepted`

Interpretation notes:
- if `HeartbeatAccepted = false`, the frontend should stop relying on this request as a refresh trigger
- if `LiveOddsHeartbeatEnabled = false`, the shared backend admin switch is currently off
- if `LiveOddsHeartbeatEnabled = true` but `EffectiveViewerDrivenRefreshEnabled = false`, the static backend config or provider setup is preventing viewer-driven refresh at the moment

### 32.5 Read Path Impact

The following read endpoints can now prefer The Odds API live 1X2 data when available and when `betId` is not explicitly requested:
- `GET /api/odds/live`
- `GET /api/fixtures/{apiFixtureId}/odds/live`
- live odds summaries returned by:
  - `GET /api/fixtures/query?includeLiveOddsSummary=true`
  - `POST /api/odds/live/summary`

`LiveOddsMarketDto` may now include:
- `SourceProvider`
- `ExternalEventId`
- `ExternalBookmakerKey`
- `ExternalMarketKey`

Current provider values:
- `api-football`
- `the-odds-api`

Behavior:
- if stored The Odds API live 1X2 rows exist for the fixture, they are preferred
- otherwise the backend falls back to the current API-Football live odds storage

Frontend contract notes:
- for `SourceProvider = the-odds-api`, the current scope is only live `1X2` / `h2h`
- the backend normalizes that market to `BetName = Match Winner`
- for `SourceProvider = the-odds-api`, `Values` should be interpreted as the three live outcomes for `Home / Draw / Away`
- for `SourceProvider = the-odds-api`, `Bookmaker` is the provider bookmaker title and `ExternalBookmakerKey` is the stable provider bookmaker identity
- for `SourceProvider = the-odds-api`, `BookmakerId` and `ApiBookmakerId` can legitimately stay `0`; this is not a frontend bug
- if the UI needs a stable React/Vue key for one live market row, prefer:
  - `SourceProvider + ExternalBookmakerKey + ExternalMarketKey`
- if `SourceProvider = api-football`, continue using the existing local/API bookmaker ids as before
- the live status / elapsed / score UI should continue to come from API-Football-backed fixture endpoints, not from The Odds API
- with the current conservative test defaults, these read endpoints are cache-first for The Odds
- frontend reads alone should not be relied on to trigger a fresh The Odds provider request
- the intended refresh trigger is the viewer heartbeat plus the backend viewer-driven worker

Recommended frontend read flow:
- live fixture list:
  - `GET /api/fixtures/query?includeLiveOddsSummary=true`
- live fixture detail odds:
  - `GET /api/fixtures/{apiFixtureId}/odds/live`
- visible-card keepalive:
  - `POST /api/odds/live/viewers/heartbeat`

Important current limitation:
- do not expect arbitrary live bet markets from The Odds path yet
- the current backend contract for The Odds live provider is intentionally limited to `Match Winner`

### 32.6 Database Apply

EF migration:
- `Migrations/20260409191810_Stage11TheOddsApiLiveOdds.cs`

SQL fallback:
- `sql/stage11_the_odds_api_live_odds.sql`

## 33. Stage 12: The Odds League Sport-Key Auto Mapping

Stage 12 removes the operational need to maintain one Render env var per league for The Odds API.

The backend now keeps a persisted local mapping between:
- API-Football `leagueId`
- The Odds API `sport_key`

### 33.1 New Table

New table:
- `the_odds_league_mappings`

Purpose:
- stores the resolved `leagueId -> sport_key` crosswalk
- avoids repeating provider discovery work on every sync
- keeps resolution metadata so production debugging is much easier

Main fields:
- `ApiFootballLeagueId`
- `LeagueName`
- `CountryName`
- `TheOddsSportKey`
- `ResolutionSource`
- `Confidence`
- `IsVerified`
- `Notes`
- `LastResolvedAtUtc`
- `LastUsedAtUtc`

### 33.2 Resolution Flow

When `POST /api/odds/live/the-odds/sync` needs a sport key, the backend now tries in this order:
- configured override from `TheOddsApi:LeagueSportKeys`
- existing row in `the_odds_league_mappings`
- built-in known aliases for common leagues
- heuristic match against the active soccer sports catalog from The Odds API
- provider-assisted fixture matching for the best candidates

If a reliable match is found:
- the mapping is persisted in `the_odds_league_mappings`
- future syncs reuse it directly

If no reliable match is found:
- the unresolved state is also persisted
- the backend does not keep hammering the provider on every request
- sync responses can classify this as `CoverageStatus = unsupported` so the frontend/admin UI can treat it as a provider coverage limitation instead of a backend failure

Current practical behavior:
- common leagues such as the Premier League can resolve immediately through the built-in alias map
- once a league is resolved once, future syncs usually reuse `the_odds_league_mappings` directly
- the frontend does not need to know or send `sport_key`
- the frontend should continue sending only fixture ids to the viewer heartbeat endpoint

### 33.3 New Debug Endpoints

`GET /api/debug/the-odds/mappings`

Purpose:
- lists the persisted The Odds sport-key mappings

Query parameters:
- `leagueId` - optional
- `resolvedOnly` - optional, default `false`
- `limit` - optional, default `100`, clamp `1..500`

Response:
- `Count`
- `Items`

Each item contains:
- `ApiFootballLeagueId`
- `LeagueName`
- `CountryName`
- `TheOddsSportKey`
- `ResolutionSource`
- `Confidence`
- `IsVerified`
- `Notes`
- `LastResolvedAtUtc`
- `LastUsedAtUtc`
- `UpdatedAtUtc`

`GET /api/debug/the-odds/mappings/suggestions`

Purpose:
- shows the current stored mapping plus the best automatic candidates for one league-season

Query parameters:
- `leagueId` - required
- `season` - required
- `limit` - optional, default `5`, clamp `1..20`

Response:
- `LeagueId`
- `Season`
- `Existing`
- `Suggestions`

Each suggestion contains:
- `SportKey`
- `Title`
- `Description`
- `Score`

### 33.4 Debug DB Snapshot Additions

`GET /api/debug/db` now also returns:
- `TheOddsLiveOdds`
- `TheOddsLeagueMappings`
- `TheOddsRuntimeSettings`

### 33.5 Database Apply

EF migration:
- `Migrations/20260410183907_Stage12TheOddsLeagueMappings.cs`

SQL fallback:
- `sql/stage12_the_odds_league_mappings.sql`

## 34. Stage 13: The Odds Global Viewer Refresh Control

Stage 13 adds a persisted backend switch for the shared viewer heartbeat flow.

Purpose:
- lets the admin panel enable or disable The Odds viewer-driven heartbeat globally for all users
- keeps the backend as the single source of truth
- avoids relying only on local browser state inside the admin panel

### 34.1 New Table

New table:
- `the_odds_runtime_settings`

Current setting key used:
- `viewer_heartbeat_enabled`

Stored fields:
- `SettingKey`
- `BoolValue`
- `UpdatedAtUtc`
- `UpdatedBy`

### 34.2 Database Apply

EF migration:
- `Migrations/20260411115043_Stage13TheOddsViewerRefreshState.cs`

SQL fallback:
- `sql/stage13_the_odds_viewer_refresh_state.sql`

## 35. Stage 14: Global Content Storage

Stage 14 moves the admin-managed marketing and curated homepage content from browser-local storage to backend storage.

Current managed collections:
- bonus codes
- hero banners
- side ads
- popular leagues

Important contract note:
- each content endpoint stores and returns a raw JSON array
- the backend does not currently impose a stricter item schema for these collections
- if no content has been saved yet, the endpoint returns `[]`

### 35.1 Public Read Endpoints

Base route:

`/api/content`

Public read endpoints:
- `GET /api/content/bonus-codes`
- `GET /api/content/hero-banners`
- `GET /api/content/side-ads`
- `GET /api/content/popular-leagues`

Behavior:
- these endpoints are public
- they return the currently stored JSON array for the requested collection
- if the collection has not been initialized yet, they return:

```json
[]
```

### 35.2 Admin Read/Write Endpoints

Base route:

`/api/admin/content`

Admin endpoints:
- `GET /api/admin/content/bonus-codes`
- `PUT /api/admin/content/bonus-codes`
- `GET /api/admin/content/hero-banners`
- `PUT /api/admin/content/hero-banners`
- `GET /api/admin/content/side-ads`
- `PUT /api/admin/content/side-ads`
- `GET /api/admin/content/popular-leagues`
- `PUT /api/admin/content/popular-leagues`

Auth:
- all `/api/admin/content/*` endpoints are `admin-only`

PUT request body:
- must be a JSON array

Validation:
- if the request body is not a JSON array, the backend returns `400 BadRequest`

PUT behavior:
- creates the collection row on first save
- replaces the stored JSON array for that collection on every update
- tracks `UpdatedAtUtc` and `UpdatedBy` internally

### 35.3 Storage Model

New table:
- `content_documents`

Stored fields:
- `ContentKey`
- `PayloadJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `UpdatedBy`

Current `ContentKey` values:
- `bonus-codes`
- `hero-banners`
- `side-ads`
- `popular-leagues`

### 35.4 Recommended Frontend Usage

Public site:
- read from:
  - `GET /api/content/bonus-codes`
  - `GET /api/content/hero-banners`
  - `GET /api/content/side-ads`
  - `GET /api/content/popular-leagues`

Admin panel:
- load current values through:
  - `GET /api/admin/content/...`
- save full arrays through:
  - `PUT /api/admin/content/...`

Current design note:
- the backend intentionally stores the full array as provided by the admin UI
- this avoids locking the system into a premature item schema while the production admin panel is still settling

### 35.5 Database Apply

EF migration:
- `Migrations/20260417163000_Stage14ContentStorage.cs`

SQL fallback:
- `sql/stage14_content_storage.sql`

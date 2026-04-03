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

### 2.2 Публични endpoint-и

Следните пътища са публични:

- `GET /ping`
- `/swagger`

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

Използваните ключове в момента са:

- `ConnectionStrings:DefaultConnection`
- `ApiFootball:BaseUrl`
- `ApiFootball:ApiKey`
- `ApiAuth:Token`
- `CORS:AllowedOrigins`
- env var `PORT`

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
- синхронизира bookmaker catalog за league/season
- обновява sync state `bookmakers`

Query параметри:
- `leagueId` - required
- `season` - required

Важно:
- този endpoint синхронизира bookmaker записи
- не записва odds snapshots в `pre_match_odds`

Отговор:
- `Message`
- `LeagueId`
- `Season`
- `LastSyncedAtUtc`
- `Processed`
- `Inserted`
- `Updated`

### 10.2 `GET /api/bookmakers`

Предназначение:
- връща локално записаните bookmakers

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
- it bootstraps current league-seasons instead of reading only `supported_leagues`
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
- взема активните записи от `supported_leagues`
- за всяка поддържана лига/сезон изпълнява:
  - teams sync
  - upcoming fixtures sync
  - standings sync

Важно:
- в текущата имплементация preload не sync-ва `fixtures_full`
- preload не sync-ва odds

Отговор:
- `PreloadSyncResult`

`PreloadSyncResult`:
- `CountriesSynced`
- `LeaguesSynced`
- `SupportedLeaguesCount`
- `StoppedEarly`
- `StopReason`
- `Leagues`

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
- `StandingsProcessed`
- `StandingsInserted`
- `StandingsUpdated`
- `StandingsSkippedMissingTeams`
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
- `Global`
- `Leagues`

`Global` съдържа:
- `countries`
- `leagues`

`Leagues` съдържа по една entry за supported league/season:
- `LeagueApiId`
- `Season`
- `LeagueName`
- `CountryName`
- `IsActive`
- `Priority`
- `TeamsLastSyncedAtUtc`
- `FixturesUpcomingLastSyncedAtUtc`
- `FixturesFullLastSyncedAtUtc`
- `StandingsLastSyncedAtUtc`
- `OddsLastSyncedAtUtc`
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
- if `ProviderFixturesReceived = 0` and `ProviderValuesReceived = 0`, the issue is upstream provider availability
- if provider fixtures are returned but `LocalMatchingFixtures` is empty, the issue is local fixture matching

## 15. Практически бележки за frontend

### 15.1 Кои endpoint-и са подходящи за UI списъци

Препоръчителни read endpoint-и:
- fixture list -> `GET /api/fixtures/query`
- fixture detail -> `GET /api/fixtures/{apiFixtureId}`
- fixture odds -> `GET /api/fixtures/{apiFixtureId}/odds`
- best odds -> `GET /api/fixtures/{apiFixtureId}/best-odds`
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
- `/api/standings`
- `/api/odds`

четат от локалната база.

## 16. Текущи ограничения

- `GET /api/fixtures` е unpaged и е по-подходящ за по-малки списъци или admin/debug употреба
- `GET /api/fixtures/query` е препоръчителният list endpoint за frontend
- preload flow в момента не синхронизира odds
- bookmaker sync не е заместител на odds sync
- `appsettings.Development.json` е игнориран и не се качва в git
- Swagger е публичен, но останалите endpoint-и минават през API key middleware, ако токенът е активен

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
- `GET /api/fixtures/{apiFixtureId}/lineups`
- `GET /api/fixtures/{apiFixtureId}/players`
- `GET /api/fixtures/{apiFixtureId}/match-center`

Поведение:
- всички тези endpoint-и четат само от локалната база
- не правят директни remote calls към API-Football
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

### 21.3 Нови sync endpoint-и

- `POST /api/fixtures/{apiFixtureId}/sync-match-center`
- `POST /api/fixtures/sync-live-match-center`

`POST /api/fixtures/{apiFixtureId}/sync-match-center` query параметри:
- `includePlayers` - optional, default `true`
- `force` - optional, default `false`

`POST /api/fixtures/sync-live-match-center` query параметри:
- `leagueId` - optional
- `season` - optional
- `maxFixtures` - optional, default `10`, clamp `1..20`
- `includePlayers` - optional, default `false`
- `force` - optional, default `false`

### 21.4 Rate-aware sync стратегия

За да пази лимита от 5000 заявки/ден, Stage 2 sync логиката е selective:
- read endpoint-ите никога не удрят API-Football
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
- this is intentionally quota-aware for the 5000 requests/day limit

Automation note:
- this endpoint is not manual-only anymore
- `POST /api/preload/run` now also runs `TeamAnalyticsService.SyncStatisticsAsync(...)` for active supported leagues after the core teams/fixtures/standings steps
- the internal background worker also runs a quota-aware daily refresh for `team_statistics` on active supported leagues
- frontend pages that read `GET /api/teams/{apiTeamId}/statistics` or `GET /api/teams/{apiTeamId}/form` can now populate automatically once preload or the scheduler has run

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

With the 5000 requests/day limit, Stage 4 is designed around daily sync only:
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
- can scope itself only to active `supported_leagues`, which is the safer default for your 5000 requests/day limit

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
- these IDs are the ones you must use with live odds

Important:
- live bet IDs are separate from pre-match bet IDs
- do not reuse `/odds/bets` ids for `/odds/live`

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
- `bookmakerId` - optional

Validation:
- requires either `fixtureId` or `leagueId`

Behavior:
- `fixtureId` and `leagueId` use API ids
- `betId` must come from `GET /api/odds/live-bets`
- the live odds provider currently appears in two shapes and the backend accepts both:
  - legacy `bookmakers[].bets[].values`
  - direct `odds[].values`
- when the provider omits bookmaker information and returns direct `odds[]`, the backend stores the rows under a synthetic bookmaker unless the sync request is explicitly scoped by `bookmakerId`
- if the sync request includes `bookmakerId`, the backend stores that bookmaker's real id/name even when the provider response is still direct `odds[]`
- if the sync request does not include `bookmakerId`, the fallback remains a synthetic bookmaker:
  - `ApiBookmakerId = 0`
  - `Bookmaker = API-Football Live Feed`
- snapshots are stored only when the latest local value has changed
- sync state `live_odds` is updated per touched league/season

Response:
- `LiveOddsSyncResultDto`

Useful diagnostic fields in `LiveOddsSyncResultDto`:
- `ProviderFixturesReceived`
- `ProviderBookmakersReceived`
- `ProviderReturnedEmpty`
- `UsedLeagueFallback`
- `FallbackLeagueApiId`
- `LocalFixturesResolved`
- `ProviderFixtureApiIdsSample`
- `MissingFixtureApiIdsSample`

### 26.5 `GET /api/odds/live`

Purpose:
- reads stored live odds from the local database

Query parameters:
- `fixtureId` - optional local fixture id
- `apiFixtureId` - optional API fixture id
- `betId` - optional live bet id
- `bookmakerId` - optional API bookmaker id
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
- `ApiBetId`
- `BetName`
- `CollectedAtUtc`
- `LastSnapshotCollectedAtUtc`
- `LastSyncedAtUtc`
- `Values`

Important note:
- `Bookmaker` can be the synthetic source `API-Football Live Feed`
- this happens when API-Football returns live odds as direct `odds[]` without bookmaker-level data
- in that case `ApiBookmakerId` is `0`
- when real bookmaker rows exist for the same fixture, the read path prefers them and suppresses the synthetic fallback rows

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
- `bookmakerId` - optional API bookmaker id
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
- `pre_match_odds`
- `odds_open_close`
- `odds_movements`
- `market_consensus`

Config section:

`DataRetention`

Fields:
- `Enabled`
- `IntervalHours`
- `ErrorRetryMinutes`
- `SyncErrorsRetentionDays`
- `LiveOddsRetentionDays`
- `PreMatchOddsRetentionDays`

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
- `LiveStatusIntervalSeconds = 30`
- `TeamsIntervalHours = 24`
- `StandingsIntervalHours = 24`
- `StandingsHotIntervalHours = 6`
- `FixturesBaselineIntervalHours = 12`
- `FixtureHotIntervalMinutes = 120`
- `FixtureHotLookbackHours = 12`
- `FixtureHotLookaheadHours = 36`
- `OddsLookaheadHours = 72`
- `OddsFarIntervalHours = 6`
- `OddsMidIntervalHours = 2`
- `OddsNearIntervalMinutes = 30`
- `OddsFinalIntervalMinutes = 15`
- `LiveOddsIntervalSeconds = 60`
- `TrackLiveOddsPerBookmaker = true`
- `MaxLiveOddsBookmakersPerLeaguePerCycle = 4`
- `RepairIntervalHours = 4`

### 29.5 Budget Strategy For 70 000 Requests/Day

The new defaults are designed to stay comfortably below the daily limit, not to spend the entire limit.

Operational strategy:
- keep the heavy work rolling and distributed
- avoid full-resync bursts
- prefer one global live heartbeat call over many per-league live polls
- refresh odds only for league-seasons with near fixtures
- degrade non-critical work early when quota becomes tight

Quota guard defaults:
- `LowDailyRemainingThreshold = 10000`
- `CriticalDailyRemainingThreshold = 2500`

Core automation daily budget defaults:
- `AutomationDailyBudget = 65000`
- `ProviderDailySafetyBuffer = 5000`
- `CatalogRefreshDailyBudget = 500`
- `TeamsRollingDailyBudget = 9000`
- `StandingsRollingDailyBudget = 6000`
- `FixturesRollingDailyBudget = 18000`
- `OddsPreMatchDailyBudget = 22000`
- `OddsLiveDailyBudget = 12000`
- `RepairDailyBudget = 3500`

When quota gets tighter:
- team rolling sync slows down first
- baseline fixture rolling sync slows down next
- hot fixtures and pre-match odds get smaller per-cycle batches
- live odds batch size is reduced
- the live heartbeat remains the cheapest last-resort live refresh

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
- standings auto-refresh

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

## 30. JWT Auth

The backend now supports JWT as the preferred auth model for both REST and SignalR.

Accepted auth methods:
- `Authorization: Bearer {jwt}`
- legacy bridge: `X-API-KEY: {token}`

JWT bridge endpoints:
- `POST /api/auth/token`
- `GET /api/auth/me`

`POST /api/auth/token`:
- requires an already authenticated caller
- can be called with the legacy API key during the transition
- returns a signed JWT access token for frontend use

SignalR auth:
- hub route: `/hubs/live-odds`
- browser clients should pass the JWT through `access_token`
- example negotiate path:
  - `POST /hubs/live-odds/negotiate?negotiateVersion=1&access_token={jwt}`

Relevant config:
- `JwtAuth:Issuer`
- `JwtAuth:Audience`
- `JwtAuth:SigningKey`
- `JwtAuth:AccessTokenMinutes`

Signing key note:
- preferred production setup is a dedicated `JwtAuth:SigningKey` with at least 32 bytes
- if the configured secret is shorter, the backend derives a stable 256-bit HMAC key from it
- this keeps HS256 valid even when the legacy API key is shorter than 256 bits

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
- it can also mean API-Football is not returning usable live odds for that fixture in the current live window

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

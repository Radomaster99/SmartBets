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

using Microsoft.EntityFrameworkCore;
using SmartBets.Entities;

namespace SmartBets.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<FixtureEvent> FixtureEvents => Set<FixtureEvent>();
    public DbSet<FixtureLineup> FixtureLineups => Set<FixtureLineup>();
    public DbSet<FixtureStatistic> FixtureStatistics => Set<FixtureStatistic>();
    public DbSet<FixturePlayerStatistic> FixturePlayerStatistics => Set<FixturePlayerStatistic>();
    public DbSet<FixturePrediction> FixturePredictions => Set<FixturePrediction>();
    public DbSet<FixtureInjury> FixtureInjuries => Set<FixtureInjury>();
    public DbSet<TeamStatistic> TeamStatistics => Set<TeamStatistic>();
    public DbSet<LeagueRound> LeagueRounds => Set<LeagueRound>();
    public DbSet<LeagueTopScorer> LeagueTopScorers => Set<LeagueTopScorer>();
    public DbSet<LeagueTopAssist> LeagueTopAssists => Set<LeagueTopAssist>();
    public DbSet<LeagueTopCard> LeagueTopCards => Set<LeagueTopCard>();
    public DbSet<Bookmaker> Bookmakers => Set<Bookmaker>();
    public DbSet<LiveBetType> LiveBetTypes => Set<LiveBetType>();
    public DbSet<LiveOdd> LiveOdds => Set<LiveOdd>();
    public DbSet<TheOddsLiveOdd> TheOddsLiveOdds => Set<TheOddsLiveOdd>();
    public DbSet<TheOddsLeagueMapping> TheOddsLeagueMappings => Set<TheOddsLeagueMapping>();
    public DbSet<TheOddsRuntimeSetting> TheOddsRuntimeSettings => Set<TheOddsRuntimeSetting>();
    public DbSet<ContentDocument> ContentDocuments => Set<ContentDocument>();
    public DbSet<PreMatchOdd> PreMatchOdds => Set<PreMatchOdd>();
    public DbSet<OddsOpenClose> OddsOpenCloses => Set<OddsOpenClose>();
    public DbSet<OddsMovement> OddsMovements => Set<OddsMovement>();
    public DbSet<MarketConsensus> MarketConsensuses => Set<MarketConsensus>();
    public DbSet<SupportedLeague> SupportedLeagues => Set<SupportedLeague>();
    public DbSet<LeagueSeasonCoverage> LeagueSeasonCoverages => Set<LeagueSeasonCoverage>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<SyncError> SyncErrors => Set<SyncError>();
    public DbSet<Standing> Standings => Set<Standing>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("countries");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Code)
                .HasColumnName("code")
                .HasMaxLength(20);

            entity.Property(x => x.FlagUrl)
                .HasColumnName("flag_url")
                .HasMaxLength(500);

            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Code);
        });

        modelBuilder.Entity<Standing>(entity =>
        {
            entity.ToTable("standings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.Rank).HasColumnName("rank");
            entity.Property(x => x.Points).HasColumnName("points");
            entity.Property(x => x.GoalsDiff).HasColumnName("goals_diff");
            entity.Property(x => x.GroupName).HasColumnName("group_name").HasMaxLength(200);
            entity.Property(x => x.Form).HasColumnName("form").HasMaxLength(50);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(200);
            entity.Property(x => x.Played).HasColumnName("played");
            entity.Property(x => x.Win).HasColumnName("win");
            entity.Property(x => x.Draw).HasColumnName("draw");
            entity.Property(x => x.Lose).HasColumnName("lose");
            entity.Property(x => x.GoalsFor).HasColumnName("goals_for");
            entity.Property(x => x.GoalsAgainst).HasColumnName("goals_against");

            entity.HasIndex(x => new { x.LeagueId, x.Season, x.TeamId }).IsUnique();
            entity.HasIndex(x => x.LeagueId);
            entity.HasIndex(x => x.TeamId);
            entity.HasIndex(x => x.Rank);

            entity.HasOne(x => x.League)
                .WithMany(x => x.Standings)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Team)
                .WithMany(x => x.Standings)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<League>(entity =>
        {
            entity.ToTable("leagues");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiLeagueId)
                .HasColumnName("api_league_id");

            entity.Property(x => x.CountryId)
                .HasColumnName("country_id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Season)
                .HasColumnName("season");

            entity.HasIndex(x => new { x.ApiLeagueId, x.Season })
                .IsUnique();

            entity.HasIndex(x => x.CountryId);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Leagues)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiTeamId)
                .HasColumnName("api_team_id");

            entity.Property(x => x.CountryId)
                .HasColumnName("country_id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Code)
                .HasColumnName("code")
                .HasMaxLength(20);

            entity.Property(x => x.LogoUrl)
                .HasColumnName("logo_url")
                .HasMaxLength(500);

            entity.Property(x => x.Founded)
                .HasColumnName("founded");

            entity.Property(x => x.IsNational)
                .HasColumnName("is_national");

            entity.Property(x => x.VenueName)
                .HasColumnName("venue_name")
                .HasMaxLength(200);

            entity.Property(x => x.VenueAddress)
                .HasColumnName("venue_address")
                .HasMaxLength(300);

            entity.Property(x => x.VenueCity)
                .HasColumnName("venue_city")
                .HasMaxLength(200);

            entity.Property(x => x.VenueCapacity)
                .HasColumnName("venue_capacity");

            entity.Property(x => x.VenueSurface)
                .HasColumnName("venue_surface")
                .HasMaxLength(100);

            entity.Property(x => x.VenueImageUrl)
                .HasColumnName("venue_image_url")
                .HasMaxLength(500);

            entity.HasIndex(x => x.ApiTeamId)
                .IsUnique();

            entity.HasIndex(x => x.CountryId);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Teams)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.ToTable("fixtures");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiFixtureId)
                .HasColumnName("api_fixture_id");

            entity.Property(x => x.LeagueId)
                .HasColumnName("league_id");

            entity.Property(x => x.Season)
                .HasColumnName("season");

            entity.Property(x => x.KickoffAt)
                .HasColumnName("kickoff_at");

            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(100);

            entity.Property(x => x.StatusLong)
                .HasColumnName("status_long")
                .HasMaxLength(200);

            entity.Property(x => x.Elapsed)
                .HasColumnName("elapsed");

            entity.Property(x => x.StatusExtra)
                .HasColumnName("status_extra");

            entity.Property(x => x.Referee)
                .HasColumnName("referee")
                .HasMaxLength(200);

            entity.Property(x => x.Timezone)
                .HasColumnName("timezone")
                .HasMaxLength(100);

            entity.Property(x => x.VenueName)
                .HasColumnName("venue_name")
                .HasMaxLength(200);

            entity.Property(x => x.VenueCity)
                .HasColumnName("venue_city")
                .HasMaxLength(200);

            entity.Property(x => x.Round)
                .HasColumnName("round")
                .HasMaxLength(200);

            entity.Property(x => x.HomeTeamId)
                .HasColumnName("home_team_id");

            entity.Property(x => x.AwayTeamId)
                .HasColumnName("away_team_id");

            entity.Property(x => x.HomeGoals)
                .HasColumnName("home_goals");

            entity.Property(x => x.AwayGoals)
                .HasColumnName("away_goals");

            entity.Property(x => x.LastLiveStatusSyncedAtUtc)
                .HasColumnName("last_live_status_synced_at_utc");

            entity.Property(x => x.LastEventSyncedAtUtc)
                .HasColumnName("last_event_synced_at_utc");

            entity.Property(x => x.LastStatisticsSyncedAtUtc)
                .HasColumnName("last_statistics_synced_at_utc");

            entity.Property(x => x.LastLineupsSyncedAtUtc)
                .HasColumnName("last_lineups_synced_at_utc");

            entity.Property(x => x.LastPlayerStatisticsSyncedAtUtc)
                .HasColumnName("last_player_statistics_synced_at_utc");

            entity.Property(x => x.LastPredictionSyncedAtUtc)
                .HasColumnName("last_prediction_synced_at_utc");

            entity.Property(x => x.LastInjuriesSyncedAtUtc)
                .HasColumnName("last_injuries_synced_at_utc");

            entity.Property(x => x.PostFinishMatchCenterSyncCount)
                .HasColumnName("post_finish_match_center_sync_count");

            entity.HasIndex(x => x.ApiFixtureId)
                .IsUnique();

            entity.HasIndex(x => x.LeagueId);
            entity.HasIndex(x => x.HomeTeamId);
            entity.HasIndex(x => x.AwayTeamId);
            entity.HasIndex(x => x.KickoffAt);

            entity.HasOne(x => x.League)
                .WithMany(x => x.Fixtures)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HomeTeam)
                .WithMany()
                .HasForeignKey(x => x.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwayTeam)
                .WithMany()
                .HasForeignKey(x => x.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FixtureEvent>(entity =>
        {
            entity.ToTable("fixture_events");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.Elapsed).HasColumnName("elapsed");
            entity.Property(x => x.Extra).HasColumnName("extra");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.ApiTeamId).HasColumnName("api_team_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.PlayerApiId).HasColumnName("player_api_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").HasMaxLength(200);
            entity.Property(x => x.AssistApiId).HasColumnName("assist_api_id");
            entity.Property(x => x.AssistName).HasColumnName("assist_name").HasMaxLength(200);
            entity.Property(x => x.Type).HasColumnName("type").IsRequired().HasMaxLength(100);
            entity.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(200);
            entity.Property(x => x.Comments).HasColumnName("comments").HasMaxLength(500);
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => new { x.FixtureId, x.SortOrder });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FixtureLineup>(entity =>
        {
            entity.ToTable("fixture_lineups");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.ApiTeamId).HasColumnName("api_team_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.Formation).HasColumnName("formation").HasMaxLength(50);
            entity.Property(x => x.CoachApiId).HasColumnName("coach_api_id");
            entity.Property(x => x.CoachName).HasColumnName("coach_name").HasMaxLength(200);
            entity.Property(x => x.CoachPhotoUrl).HasColumnName("coach_photo_url").HasMaxLength(500);
            entity.Property(x => x.IsStarting).HasColumnName("is_starting");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.PlayerApiId).HasColumnName("player_api_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerNumber).HasColumnName("player_number");
            entity.Property(x => x.PlayerPosition).HasColumnName("player_position").HasMaxLength(20);
            entity.Property(x => x.PlayerGrid).HasColumnName("player_grid").HasMaxLength(50);
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => new { x.FixtureId, x.ApiTeamId, x.IsStarting, x.SortOrder });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.Lineups)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FixtureStatistic>(entity =>
        {
            entity.ToTable("fixture_statistics");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.ApiTeamId).HasColumnName("api_team_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.Type).HasColumnName("type").IsRequired().HasMaxLength(100);
            entity.Property(x => x.Value).HasColumnName("value").HasMaxLength(100);
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => new { x.FixtureId, x.ApiTeamId, x.SortOrder });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.Statistics)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FixturePlayerStatistic>(entity =>
        {
            entity.ToTable("fixture_player_statistics");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.ApiTeamId).HasColumnName("api_team_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.PlayerApiId).HasColumnName("player_api_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerPhotoUrl).HasColumnName("player_photo_url").HasMaxLength(500);
            entity.Property(x => x.Minutes).HasColumnName("minutes");
            entity.Property(x => x.Number).HasColumnName("number");
            entity.Property(x => x.Position).HasColumnName("position").HasMaxLength(20);
            entity.Property(x => x.Rating).HasColumnName("rating").HasColumnType("numeric(6,2)");
            entity.Property(x => x.IsCaptain).HasColumnName("is_captain");
            entity.Property(x => x.IsSubstitute).HasColumnName("is_substitute");
            entity.Property(x => x.Offsides).HasColumnName("offsides");
            entity.Property(x => x.ShotsTotal).HasColumnName("shots_total");
            entity.Property(x => x.ShotsOn).HasColumnName("shots_on");
            entity.Property(x => x.GoalsTotal).HasColumnName("goals_total");
            entity.Property(x => x.GoalsConceded).HasColumnName("goals_conceded");
            entity.Property(x => x.GoalsAssists).HasColumnName("goals_assists");
            entity.Property(x => x.GoalsSaves).HasColumnName("goals_saves");
            entity.Property(x => x.PassesTotal).HasColumnName("passes_total");
            entity.Property(x => x.PassesKey).HasColumnName("passes_key");
            entity.Property(x => x.PassesAccuracy).HasColumnName("passes_accuracy").HasMaxLength(20);
            entity.Property(x => x.TacklesTotal).HasColumnName("tackles_total");
            entity.Property(x => x.TacklesBlocks).HasColumnName("tackles_blocks");
            entity.Property(x => x.TacklesInterceptions).HasColumnName("tackles_interceptions");
            entity.Property(x => x.DuelsTotal).HasColumnName("duels_total");
            entity.Property(x => x.DuelsWon).HasColumnName("duels_won");
            entity.Property(x => x.DribblesAttempts).HasColumnName("dribbles_attempts");
            entity.Property(x => x.DribblesSuccess).HasColumnName("dribbles_success");
            entity.Property(x => x.DribblesPast).HasColumnName("dribbles_past");
            entity.Property(x => x.FoulsDrawn).HasColumnName("fouls_drawn");
            entity.Property(x => x.FoulsCommitted).HasColumnName("fouls_committed");
            entity.Property(x => x.CardsYellow).HasColumnName("cards_yellow");
            entity.Property(x => x.CardsRed).HasColumnName("cards_red");
            entity.Property(x => x.PenaltyWon).HasColumnName("penalty_won");
            entity.Property(x => x.PenaltyCommitted).HasColumnName("penalty_committed");
            entity.Property(x => x.PenaltyScored).HasColumnName("penalty_scored");
            entity.Property(x => x.PenaltyMissed).HasColumnName("penalty_missed");
            entity.Property(x => x.PenaltySaved).HasColumnName("penalty_saved");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => new { x.FixtureId, x.ApiTeamId, x.PlayerApiId });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.PlayerStatistics)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FixturePrediction>(entity =>
        {
            entity.ToTable("fixture_predictions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.WinnerTeamApiId).HasColumnName("winner_team_api_id");
            entity.Property(x => x.WinnerTeamName).HasColumnName("winner_team_name").HasMaxLength(200);
            entity.Property(x => x.WinnerComment).HasColumnName("winner_comment").HasMaxLength(500);
            entity.Property(x => x.WinOrDraw).HasColumnName("win_or_draw");
            entity.Property(x => x.UnderOver).HasColumnName("under_over").HasMaxLength(50);
            entity.Property(x => x.Advice).HasColumnName("advice").HasMaxLength(500);
            entity.Property(x => x.GoalsHome).HasColumnName("goals_home").HasMaxLength(50);
            entity.Property(x => x.GoalsAway).HasColumnName("goals_away").HasMaxLength(50);
            entity.Property(x => x.PercentHome).HasColumnName("percent_home").HasMaxLength(50);
            entity.Property(x => x.PercentDraw).HasColumnName("percent_draw").HasMaxLength(50);
            entity.Property(x => x.PercentAway).HasColumnName("percent_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonFormHome).HasColumnName("comparison_form_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonFormAway).HasColumnName("comparison_form_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonAttackHome).HasColumnName("comparison_attack_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonAttackAway).HasColumnName("comparison_attack_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonDefenceHome).HasColumnName("comparison_defence_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonDefenceAway).HasColumnName("comparison_defence_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonPoissonHome).HasColumnName("comparison_poisson_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonPoissonAway).HasColumnName("comparison_poisson_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonHeadToHeadHome).HasColumnName("comparison_head_to_head_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonHeadToHeadAway).HasColumnName("comparison_head_to_head_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonGoalsHome).HasColumnName("comparison_goals_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonGoalsAway).HasColumnName("comparison_goals_away").HasMaxLength(50);
            entity.Property(x => x.ComparisonTotalHome).HasColumnName("comparison_total_home").HasMaxLength(50);
            entity.Property(x => x.ComparisonTotalAway).HasColumnName("comparison_total_away").HasMaxLength(50);
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.Predictions)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FixtureInjury>(entity =>
        {
            entity.ToTable("fixture_injuries");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.ApiTeamId).HasColumnName("api_team_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.PlayerApiId).HasColumnName("player_api_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerPhotoUrl).HasColumnName("player_photo_url").HasMaxLength(500);
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(100);
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => new { x.FixtureId, x.ApiTeamId });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.Injuries)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamStatistic>(entity =>
        {
            entity.ToTable("team_statistics");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.Form).HasColumnName("form").IsRequired().HasMaxLength(50);
            entity.Property(x => x.FixturesPlayedTotal).HasColumnName("fixtures_played_total");
            entity.Property(x => x.FixturesPlayedHome).HasColumnName("fixtures_played_home");
            entity.Property(x => x.FixturesPlayedAway).HasColumnName("fixtures_played_away");
            entity.Property(x => x.WinsTotal).HasColumnName("wins_total");
            entity.Property(x => x.WinsHome).HasColumnName("wins_home");
            entity.Property(x => x.WinsAway).HasColumnName("wins_away");
            entity.Property(x => x.DrawsTotal).HasColumnName("draws_total");
            entity.Property(x => x.DrawsHome).HasColumnName("draws_home");
            entity.Property(x => x.DrawsAway).HasColumnName("draws_away");
            entity.Property(x => x.LossesTotal).HasColumnName("losses_total");
            entity.Property(x => x.LossesHome).HasColumnName("losses_home");
            entity.Property(x => x.LossesAway).HasColumnName("losses_away");
            entity.Property(x => x.GoalsForTotal).HasColumnName("goals_for_total");
            entity.Property(x => x.GoalsForHome).HasColumnName("goals_for_home");
            entity.Property(x => x.GoalsForAway).HasColumnName("goals_for_away");
            entity.Property(x => x.GoalsForAverageTotal).HasColumnName("goals_for_average_total").HasMaxLength(20);
            entity.Property(x => x.GoalsForAverageHome).HasColumnName("goals_for_average_home").HasMaxLength(20);
            entity.Property(x => x.GoalsForAverageAway).HasColumnName("goals_for_average_away").HasMaxLength(20);
            entity.Property(x => x.GoalsAgainstTotal).HasColumnName("goals_against_total");
            entity.Property(x => x.GoalsAgainstHome).HasColumnName("goals_against_home");
            entity.Property(x => x.GoalsAgainstAway).HasColumnName("goals_against_away");
            entity.Property(x => x.GoalsAgainstAverageTotal).HasColumnName("goals_against_average_total").HasMaxLength(20);
            entity.Property(x => x.GoalsAgainstAverageHome).HasColumnName("goals_against_average_home").HasMaxLength(20);
            entity.Property(x => x.GoalsAgainstAverageAway).HasColumnName("goals_against_average_away").HasMaxLength(20);
            entity.Property(x => x.CleanSheetsTotal).HasColumnName("clean_sheets_total");
            entity.Property(x => x.CleanSheetsHome).HasColumnName("clean_sheets_home");
            entity.Property(x => x.CleanSheetsAway).HasColumnName("clean_sheets_away");
            entity.Property(x => x.FailedToScoreTotal).HasColumnName("failed_to_score_total");
            entity.Property(x => x.FailedToScoreHome).HasColumnName("failed_to_score_home");
            entity.Property(x => x.FailedToScoreAway).HasColumnName("failed_to_score_away");
            entity.Property(x => x.BiggestStreakWins).HasColumnName("biggest_streak_wins");
            entity.Property(x => x.BiggestStreakDraws).HasColumnName("biggest_streak_draws");
            entity.Property(x => x.BiggestStreakLosses).HasColumnName("biggest_streak_losses");
            entity.Property(x => x.BiggestWinHome).HasColumnName("biggest_win_home").HasMaxLength(20);
            entity.Property(x => x.BiggestWinAway).HasColumnName("biggest_win_away").HasMaxLength(20);
            entity.Property(x => x.BiggestLossHome).HasColumnName("biggest_loss_home").HasMaxLength(20);
            entity.Property(x => x.BiggestLossAway).HasColumnName("biggest_loss_away").HasMaxLength(20);
            entity.Property(x => x.BiggestGoalsForHome).HasColumnName("biggest_goals_for_home");
            entity.Property(x => x.BiggestGoalsForAway).HasColumnName("biggest_goals_for_away");
            entity.Property(x => x.BiggestGoalsAgainstHome).HasColumnName("biggest_goals_against_home");
            entity.Property(x => x.BiggestGoalsAgainstAway).HasColumnName("biggest_goals_against_away");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => new { x.TeamId, x.LeagueId, x.Season }).IsUnique();
            entity.HasIndex(x => x.TeamId);
            entity.HasIndex(x => x.LeagueId);

            entity.HasOne(x => x.Team)
                .WithMany(x => x.Statistics)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.League)
                .WithMany(x => x.TeamStatistics)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueRound>(entity =>
        {
            entity.ToTable("league_rounds");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.RoundName).HasColumnName("round_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.IsCurrent).HasColumnName("is_current");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => new { x.LeagueId, x.Season, x.RoundName }).IsUnique();
            entity.HasIndex(x => new { x.LeagueId, x.Season, x.SortOrder });

            entity.HasOne(x => x.League)
                .WithMany(x => x.Rounds)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueTopScorer>(entity =>
        {
            entity.ToTable("league_top_scorers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.Rank).HasColumnName("rank");
            entity.Property(x => x.ApiPlayerId).HasColumnName("api_player_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerPhotoUrl).HasColumnName("player_photo_url").HasMaxLength(500);
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.TeamApiId).HasColumnName("team_api_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.Appearances).HasColumnName("appearances");
            entity.Property(x => x.Minutes).HasColumnName("minutes");
            entity.Property(x => x.Position).HasColumnName("position").HasMaxLength(50);
            entity.Property(x => x.Rating).HasColumnName("rating").HasColumnType("numeric(6,2)");
            entity.Property(x => x.Goals).HasColumnName("goals");
            entity.Property(x => x.Assists).HasColumnName("assists");
            entity.Property(x => x.ShotsTotal).HasColumnName("shots_total");
            entity.Property(x => x.ShotsOn).HasColumnName("shots_on");
            entity.Property(x => x.PenaltiesScored).HasColumnName("penalties_scored");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => new { x.LeagueId, x.Season, x.Rank }).IsUnique();
            entity.HasIndex(x => new { x.LeagueId, x.Season, x.ApiPlayerId });

            entity.HasOne(x => x.League)
                .WithMany(x => x.TopScorers)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueTopAssist>(entity =>
        {
            entity.ToTable("league_top_assists");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.Rank).HasColumnName("rank");
            entity.Property(x => x.ApiPlayerId).HasColumnName("api_player_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerPhotoUrl).HasColumnName("player_photo_url").HasMaxLength(500);
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.TeamApiId).HasColumnName("team_api_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.Appearances).HasColumnName("appearances");
            entity.Property(x => x.Minutes).HasColumnName("minutes");
            entity.Property(x => x.Position).HasColumnName("position").HasMaxLength(50);
            entity.Property(x => x.Rating).HasColumnName("rating").HasColumnType("numeric(6,2)");
            entity.Property(x => x.Goals).HasColumnName("goals");
            entity.Property(x => x.Assists).HasColumnName("assists");
            entity.Property(x => x.PassesKey).HasColumnName("passes_key");
            entity.Property(x => x.ChancesCreated).HasColumnName("chances_created");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => new { x.LeagueId, x.Season, x.Rank }).IsUnique();
            entity.HasIndex(x => new { x.LeagueId, x.Season, x.ApiPlayerId });

            entity.HasOne(x => x.League)
                .WithMany(x => x.TopAssists)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueTopCard>(entity =>
        {
            entity.ToTable("league_top_cards");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueId).HasColumnName("league_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.CombinedRank).HasColumnName("combined_rank");
            entity.Property(x => x.YellowRank).HasColumnName("yellow_rank");
            entity.Property(x => x.RedRank).HasColumnName("red_rank");
            entity.Property(x => x.ApiPlayerId).HasColumnName("api_player_id");
            entity.Property(x => x.PlayerName).HasColumnName("player_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.PlayerPhotoUrl).HasColumnName("player_photo_url").HasMaxLength(500);
            entity.Property(x => x.TeamId).HasColumnName("team_id");
            entity.Property(x => x.TeamApiId).HasColumnName("team_api_id");
            entity.Property(x => x.TeamName).HasColumnName("team_name").IsRequired().HasMaxLength(200);
            entity.Property(x => x.TeamLogoUrl).HasColumnName("team_logo_url").HasMaxLength(500);
            entity.Property(x => x.Appearances).HasColumnName("appearances");
            entity.Property(x => x.Minutes).HasColumnName("minutes");
            entity.Property(x => x.Position).HasColumnName("position").HasMaxLength(50);
            entity.Property(x => x.Rating).HasColumnName("rating").HasColumnType("numeric(6,2)");
            entity.Property(x => x.YellowCards).HasColumnName("yellow_cards");
            entity.Property(x => x.RedCards).HasColumnName("red_cards");
            entity.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc");

            entity.HasIndex(x => new { x.LeagueId, x.Season, x.CombinedRank }).IsUnique();
            entity.HasIndex(x => new { x.LeagueId, x.Season, x.ApiPlayerId });

            entity.HasOne(x => x.League)
                .WithMany(x => x.TopCards)
                .HasForeignKey(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Bookmaker>(entity =>
        {
            entity.ToTable("bookmakers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiBookmakerId)
                .HasColumnName("api_bookmaker_id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.ApiBookmakerId)
                .IsUnique();
        });

        modelBuilder.Entity<LiveBetType>(entity =>
        {
            entity.ToTable("live_bet_types");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiBetId)
                .HasColumnName("api_bet_id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.SyncedAtUtc)
                .HasColumnName("synced_at_utc");

            entity.HasIndex(x => x.ApiBetId)
                .IsUnique();
        });

        modelBuilder.Entity<LiveOdd>(entity =>
        {
            entity.ToTable("live_odds");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.FixtureId)
                .HasColumnName("fixture_id");

            entity.Property(x => x.BookmakerId)
                .HasColumnName("bookmaker_id");

            entity.Property(x => x.ApiBetId)
                .HasColumnName("api_bet_id");

            entity.Property(x => x.BetName)
                .HasColumnName("bet_name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.OutcomeLabel)
                .HasColumnName("outcome_label")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Line)
                .HasColumnName("line")
                .HasMaxLength(100);

            entity.Property(x => x.Odd)
                .HasColumnName("odd");

            entity.Property(x => x.IsMain)
                .HasColumnName("is_main");

            entity.Property(x => x.Stopped)
                .HasColumnName("stopped");

            entity.Property(x => x.Blocked)
                .HasColumnName("blocked");

            entity.Property(x => x.Finished)
                .HasColumnName("finished");

            entity.Property(x => x.CollectedAtUtc)
                .HasColumnName("collected_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => x.BookmakerId);
            entity.HasIndex(x => x.ApiBetId);
            entity.HasIndex(x => x.CollectedAtUtc);
            entity.HasIndex(x => new { x.FixtureId, x.BookmakerId, x.ApiBetId, x.OutcomeLabel, x.Line, x.CollectedAtUtc });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.LiveOdds)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Bookmaker)
                .WithMany(x => x.LiveOdds)
                .HasForeignKey(x => x.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LiveBetType)
                .WithMany(x => x.LiveOdds)
                .HasForeignKey(x => x.ApiBetId)
                .HasPrincipalKey(x => x.ApiBetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TheOddsLiveOdd>(entity =>
        {
            entity.ToTable("the_odds_live_odds");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.FixtureId)
                .HasColumnName("fixture_id");

            entity.Property(x => x.ProviderEventId)
                .HasColumnName("provider_event_id")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.SportKey)
                .HasColumnName("sport_key")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.BookmakerKey)
                .HasColumnName("bookmaker_key")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.BookmakerTitle)
                .HasColumnName("bookmaker_title")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.MarketKey)
                .HasColumnName("market_key")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.MarketName)
                .HasColumnName("market_name")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.OutcomeName)
                .HasColumnName("outcome_name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Point)
                .HasColumnName("point");

            entity.Property(x => x.Price)
                .HasColumnName("price");

            entity.Property(x => x.LastUpdateUtc)
                .HasColumnName("last_update_utc");

            entity.Property(x => x.CollectedAtUtc)
                .HasColumnName("collected_at_utc");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => x.CollectedAtUtc);
            entity.HasIndex(x => new { x.FixtureId, x.BookmakerKey, x.MarketKey, x.CollectedAtUtc });
            entity.HasIndex(x => new { x.FixtureId, x.BookmakerKey, x.MarketKey, x.OutcomeName, x.Point, x.CollectedAtUtc });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.TheOddsLiveOdds)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TheOddsLeagueMapping>(entity =>
        {
            entity.ToTable("the_odds_league_mappings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ApiFootballLeagueId)
                .HasColumnName("api_football_league_id");

            entity.Property(x => x.LeagueName)
                .HasColumnName("league_name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.CountryName)
                .HasColumnName("country_name")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TheOddsSportKey)
                .HasColumnName("the_odds_sport_key")
                .HasMaxLength(100);

            entity.Property(x => x.ResolutionSource)
                .HasColumnName("resolution_source")
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Confidence)
                .HasColumnName("confidence");

            entity.Property(x => x.IsVerified)
                .HasColumnName("is_verified");

            entity.Property(x => x.Notes)
                .HasColumnName("notes")
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAtUtc)
                .HasColumnName("created_at_utc");

            entity.Property(x => x.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");

            entity.Property(x => x.LastResolvedAtUtc)
                .HasColumnName("last_resolved_at_utc");

            entity.Property(x => x.LastUsedAtUtc)
                .HasColumnName("last_used_at_utc");

            entity.HasIndex(x => x.ApiFootballLeagueId)
                .IsUnique();

            entity.HasIndex(x => x.TheOddsSportKey);
            entity.HasIndex(x => x.ResolutionSource);
        });

        modelBuilder.Entity<TheOddsRuntimeSetting>(entity =>
        {
            entity.ToTable("the_odds_runtime_settings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.SettingKey)
                .HasColumnName("setting_key")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.BoolValue)
                .HasColumnName("bool_value");

            entity.Property(x => x.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");

            entity.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by")
                .HasMaxLength(200);

            entity.HasIndex(x => x.SettingKey)
                .IsUnique();
        });

        modelBuilder.Entity<ContentDocument>(entity =>
        {
            entity.ToTable("content_documents");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.ContentKey)
                .HasColumnName("content_key")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.PayloadJson)
                .HasColumnName("payload_json")
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .HasColumnName("created_at_utc");

            entity.Property(x => x.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");

            entity.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by")
                .HasMaxLength(200);

            entity.HasIndex(x => x.ContentKey)
                .IsUnique();
        });

        modelBuilder.Entity<PreMatchOdd>(entity =>
        {
            entity.ToTable("pre_match_odds");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.FixtureId)
                .HasColumnName("fixture_id");

            entity.Property(x => x.BookmakerId)
                .HasColumnName("bookmaker_id");

            entity.Property(x => x.MarketName)
                .HasColumnName("market_name")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.HomeOdd)
                .HasColumnName("home_odd");

            entity.Property(x => x.DrawOdd)
                .HasColumnName("draw_odd");

            entity.Property(x => x.AwayOdd)
                .HasColumnName("away_odd");

            entity.Property(x => x.CollectedAt)
                .HasColumnName("collected_at");

            entity.HasIndex(x => x.FixtureId);
            entity.HasIndex(x => x.BookmakerId);
            entity.HasIndex(x => new { x.FixtureId, x.BookmakerId, x.MarketName, x.CollectedAt });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.PreMatchOdds)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Bookmaker)
                .WithMany(x => x.PreMatchOdds)
                .HasForeignKey(x => x.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OddsOpenClose>(entity =>
        {
            entity.ToTable("odds_open_close");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.BookmakerId).HasColumnName("bookmaker_id");
            entity.Property(x => x.MarketName).HasColumnName("market_name").IsRequired().HasMaxLength(100);
            entity.Property(x => x.SnapshotCount).HasColumnName("snapshot_count");
            entity.Property(x => x.OpeningHomeOdd).HasColumnName("opening_home_odd");
            entity.Property(x => x.OpeningDrawOdd).HasColumnName("opening_draw_odd");
            entity.Property(x => x.OpeningAwayOdd).HasColumnName("opening_away_odd");
            entity.Property(x => x.OpeningCollectedAtUtc).HasColumnName("opening_collected_at_utc");
            entity.Property(x => x.LatestHomeOdd).HasColumnName("latest_home_odd");
            entity.Property(x => x.LatestDrawOdd).HasColumnName("latest_draw_odd");
            entity.Property(x => x.LatestAwayOdd).HasColumnName("latest_away_odd");
            entity.Property(x => x.LatestCollectedAtUtc).HasColumnName("latest_collected_at_utc");
            entity.Property(x => x.PeakHomeOdd).HasColumnName("peak_home_odd");
            entity.Property(x => x.PeakDrawOdd).HasColumnName("peak_draw_odd");
            entity.Property(x => x.PeakAwayOdd).HasColumnName("peak_away_odd");
            entity.Property(x => x.PeakHomeCollectedAtUtc).HasColumnName("peak_home_collected_at_utc");
            entity.Property(x => x.PeakDrawCollectedAtUtc).HasColumnName("peak_draw_collected_at_utc");
            entity.Property(x => x.PeakAwayCollectedAtUtc).HasColumnName("peak_away_collected_at_utc");
            entity.Property(x => x.ClosingHomeOdd).HasColumnName("closing_home_odd");
            entity.Property(x => x.ClosingDrawOdd).HasColumnName("closing_draw_odd");
            entity.Property(x => x.ClosingAwayOdd).HasColumnName("closing_away_odd");
            entity.Property(x => x.ClosingCollectedAtUtc).HasColumnName("closing_collected_at_utc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(x => new { x.FixtureId, x.BookmakerId, x.MarketName }).IsUnique();
            entity.HasIndex(x => new { x.FixtureId, x.MarketName });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.OddsOpenCloses)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Bookmaker)
                .WithMany(x => x.OddsOpenCloses)
                .HasForeignKey(x => x.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OddsMovement>(entity =>
        {
            entity.ToTable("odds_movements");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.BookmakerId).HasColumnName("bookmaker_id");
            entity.Property(x => x.MarketName).HasColumnName("market_name").IsRequired().HasMaxLength(100);
            entity.Property(x => x.SnapshotCount).HasColumnName("snapshot_count");
            entity.Property(x => x.FirstCollectedAtUtc).HasColumnName("first_collected_at_utc");
            entity.Property(x => x.LastCollectedAtUtc).HasColumnName("last_collected_at_utc");
            entity.Property(x => x.HomeDelta).HasColumnName("home_delta");
            entity.Property(x => x.DrawDelta).HasColumnName("draw_delta");
            entity.Property(x => x.AwayDelta).HasColumnName("away_delta");
            entity.Property(x => x.HomeChangePercent).HasColumnName("home_change_percent");
            entity.Property(x => x.DrawChangePercent).HasColumnName("draw_change_percent");
            entity.Property(x => x.AwayChangePercent).HasColumnName("away_change_percent");
            entity.Property(x => x.HomeSwing).HasColumnName("home_swing");
            entity.Property(x => x.DrawSwing).HasColumnName("draw_swing");
            entity.Property(x => x.AwaySwing).HasColumnName("away_swing");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(x => new { x.FixtureId, x.BookmakerId, x.MarketName }).IsUnique();
            entity.HasIndex(x => new { x.FixtureId, x.MarketName });

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.OddsMovements)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Bookmaker)
                .WithMany(x => x.OddsMovements)
                .HasForeignKey(x => x.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MarketConsensus>(entity =>
        {
            entity.ToTable("market_consensus");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FixtureId).HasColumnName("fixture_id");
            entity.Property(x => x.MarketName).HasColumnName("market_name").IsRequired().HasMaxLength(100);
            entity.Property(x => x.SampleSize).HasColumnName("sample_size");
            entity.Property(x => x.OpeningHomeConsensusOdd).HasColumnName("opening_home_consensus_odd");
            entity.Property(x => x.OpeningDrawConsensusOdd).HasColumnName("opening_draw_consensus_odd");
            entity.Property(x => x.OpeningAwayConsensusOdd).HasColumnName("opening_away_consensus_odd");
            entity.Property(x => x.LatestHomeConsensusOdd).HasColumnName("latest_home_consensus_odd");
            entity.Property(x => x.LatestDrawConsensusOdd).HasColumnName("latest_draw_consensus_odd");
            entity.Property(x => x.LatestAwayConsensusOdd).HasColumnName("latest_away_consensus_odd");
            entity.Property(x => x.BestHomeOdd).HasColumnName("best_home_odd");
            entity.Property(x => x.BestDrawOdd).HasColumnName("best_draw_odd");
            entity.Property(x => x.BestAwayOdd).HasColumnName("best_away_odd");
            entity.Property(x => x.BestHomeBookmakerId).HasColumnName("best_home_bookmaker_id");
            entity.Property(x => x.BestDrawBookmakerId).HasColumnName("best_draw_bookmaker_id");
            entity.Property(x => x.BestAwayBookmakerId).HasColumnName("best_away_bookmaker_id");
            entity.Property(x => x.MaxHomeSpread).HasColumnName("max_home_spread");
            entity.Property(x => x.MaxDrawSpread).HasColumnName("max_draw_spread");
            entity.Property(x => x.MaxAwaySpread).HasColumnName("max_away_spread");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(x => new { x.FixtureId, x.MarketName }).IsUnique();

            entity.HasOne(x => x.Fixture)
                .WithMany(x => x.MarketConsensuses)
                .HasForeignKey(x => x.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SupportedLeague>(entity =>
        {
            entity.ToTable("supported_leagues");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueApiId).HasColumnName("league_api_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.Priority).HasColumnName("priority");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(x => new { x.LeagueApiId, x.Season }).IsUnique();
        });

        modelBuilder.Entity<LeagueSeasonCoverage>(entity =>
        {
            entity.ToTable("league_season_coverages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LeagueApiId).HasColumnName("league_api_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.HasFixtures).HasColumnName("has_fixtures");
            entity.Property(x => x.HasFixtureEvents).HasColumnName("has_fixture_events");
            entity.Property(x => x.HasLineups).HasColumnName("has_lineups");
            entity.Property(x => x.HasFixtureStatistics).HasColumnName("has_fixture_statistics");
            entity.Property(x => x.HasPlayerStatistics).HasColumnName("has_player_statistics");
            entity.Property(x => x.HasStandings).HasColumnName("has_standings");
            entity.Property(x => x.HasPlayers).HasColumnName("has_players");
            entity.Property(x => x.HasTopScorers).HasColumnName("has_top_scorers");
            entity.Property(x => x.HasTopAssists).HasColumnName("has_top_assists");
            entity.Property(x => x.HasTopCards).HasColumnName("has_top_cards");
            entity.Property(x => x.HasInjuries).HasColumnName("has_injuries");
            entity.Property(x => x.HasPredictions).HasColumnName("has_predictions");
            entity.Property(x => x.HasOdds).HasColumnName("has_odds");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => new { x.LeagueApiId, x.Season }).IsUnique();
            entity.HasIndex(x => x.Season);
        });

        modelBuilder.Entity<SyncState>(entity =>
        {
            entity.ToTable("sync_states");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(50);
            entity.Property(x => x.LeagueApiId).HasColumnName("league_api_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");

            entity.HasIndex(x => new { x.EntityType, x.LeagueApiId, x.Season }).IsUnique();
        });

        modelBuilder.Entity<SyncError>(entity =>
        {
            entity.ToTable("sync_errors");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(50);
            entity.Property(x => x.Operation).HasColumnName("operation").IsRequired().HasMaxLength(100);
            entity.Property(x => x.LeagueApiId).HasColumnName("league_api_id");
            entity.Property(x => x.Season).HasColumnName("season");
            entity.Property(x => x.Source).HasColumnName("source").IsRequired().HasMaxLength(50);
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired().HasMaxLength(2000);
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");

            entity.HasIndex(x => x.EntityType);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.EntityType, x.LeagueApiId, x.Season });
        });
    }
}

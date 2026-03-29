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
    public DbSet<Bookmaker> Bookmakers => Set<Bookmaker>();
    public DbSet<PreMatchOdd> PreMatchOdds => Set<PreMatchOdd>();
    public DbSet<SupportedLeague> SupportedLeagues => Set<SupportedLeague>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();

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

            entity.Property(x => x.HomeTeamId)
                .HasColumnName("home_team_id");

            entity.Property(x => x.AwayTeamId)
                .HasColumnName("away_team_id");

            entity.Property(x => x.HomeGoals)
                .HasColumnName("home_goals");

            entity.Property(x => x.AwayGoals)
                .HasColumnName("away_goals");

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
    }
}
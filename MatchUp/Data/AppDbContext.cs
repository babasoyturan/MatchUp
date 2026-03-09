using MatchUp.Models.Concretes;
using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace MatchUp.Data
{
    public class AppDbContext : IdentityDbContext<Player, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<TeamInvite> TeamInvites { get; set; }

        public DbSet<OpenToGameConfig> OpenToGameConfigs { get; set; }
        public DbSet<OpenToGameArea> OpenToGameAreas { get; set; }
        public DbSet<OpenToGameTimeWindow> OpenToGameTimeWindows { get; set; }
        public DbSet<OpenToGameMemberApproval> OpenToGameMemberApprovals { get; set; }

        public DbSet<GameRequest> GameRequests { get; set; }
        public DbSet<Match> Matches { get; set; }

        public DbSet<MatchVenueProposal> MatchVenueProposals { get; set; }
        public DbSet<MatchResultProposal> MatchResultProposals { get; set; }

        public DbSet<Stadium> Stadiums { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigurePlayer(modelBuilder);
            ConfigureTeam(modelBuilder);
            ConfigureOpenToGame(modelBuilder);
            ConfigureInvites(modelBuilder);
            ConfigureGameRequests(modelBuilder);
            ConfigureMatches(modelBuilder);
            ConfigureNotifications(modelBuilder);
        }

        private static void ConfigurePlayer(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>(b =>
            {
                b.Property(x => x.PlayablePositions)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => string.IsNullOrWhiteSpace(v)
                            ? new List<PlayerPosition>()
                            : JsonSerializer.Deserialize<List<PlayerPosition>>(v, (JsonSerializerOptions?)null) ?? new List<PlayerPosition>())
                    .Metadata.SetValueComparer(new ValueComparer<List<PlayerPosition>>(
                        (c1, c2) => c1!.SequenceEqual(c2!),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));

                b.Property(x => x.ImageUrl)
                    .HasMaxLength(500)
                    .IsRequired();

                b.Property(x => x.FirstName)
                    .HasMaxLength(100)
                    .IsRequired();

                b.Property(x => x.LastName)
                    .HasMaxLength(100)
                    .IsRequired();

                b.Property(x => x.Biography)
                    .HasMaxLength(1000)
                    .IsRequired();

                b.Property(x => x.Nationality)
                    .HasMaxLength(100)
                    .IsRequired();

                b.Property(x => x.BirthDate)
                    .HasColumnType("date")
                    .IsRequired();
            });
        }

        private static void ConfigureTeam(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Team>(b =>
            {
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(1000);

                b.HasOne(x => x.OpenToGameConfig)
                    .WithOne(x => x.Team)
                    .HasForeignKey<OpenToGameConfig>(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.Members)
                    .WithOne(x => x.Team)
                    .HasForeignKey(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.Invites)
                    .WithOne(x => x.Team)
                    .HasForeignKey(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.OutgoingGameRequests)
                    .WithOne(x => x.FromTeam)
                    .HasForeignKey(x => x.FromTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.IncomingGameRequests)
                    .WithOne(x => x.ToTeam)
                    .HasForeignKey(x => x.ToTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.HomeMatches)
                    .WithOne(x => x.HomeTeam)
                    .HasForeignKey(x => x.HomeTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.AwayMatches)
                    .WithOne(x => x.AwayTeam)
                    .HasForeignKey(x => x.AwayTeamId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TeamMember>(b =>
            {
                b.HasKey(x => new { x.TeamId, x.PlayerId });

                b.HasOne(x => x.Team)
                    .WithMany(x => x.Members)
                    .HasForeignKey(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Player)
                    .WithMany(x => x.TeamMemberships)
                    .HasForeignKey(x => x.PlayerId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.TeamId, x.Role });

                b.HasIndex(x => new { x.TeamId, x.SquadNumber })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
            });
        }

        private static void ConfigureOpenToGame(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OpenToGameConfig>(b =>
            {
                b.HasIndex(x => x.TeamId).IsUnique();

                b.Property(x => x.Formats)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => string.IsNullOrWhiteSpace(v)
                            ? new List<GameFormat>()
                            : JsonSerializer.Deserialize<List<GameFormat>>(v, (JsonSerializerOptions?)null) ?? new List<GameFormat>())
                    .Metadata.SetValueComparer(new ValueComparer<List<GameFormat>>(
                        (c1, c2) => c1!.SequenceEqual(c2!),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));

                b.HasMany(x => x.TimeWindows)
                    .WithOne(x => x.Config)
                    .HasForeignKey(x => x.OpenToGameConfigId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(x => x.Areas)
                    .WithOne(x => x.Config)
                    .HasForeignKey(x => x.OpenToGameConfigId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(x => x.MemberApprovals)
                    .WithOne(x => x.Config)
                    .HasForeignKey(x => x.OpenToGameConfigId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OpenToGameArea>(b =>
            {
                b.Property(x => x.CityName).HasMaxLength(100).IsRequired();
                b.Property(x => x.RegionName).HasMaxLength(100);
            });

            modelBuilder.Entity<OpenToGameTimeWindow>(b =>
            {
                b.HasCheckConstraint("CK_OpenToGameTimeWindow_Minutes", "[StartMinute] >= 0 AND [StartMinute] <= 1439 AND [EndMinute] >= 0 AND [EndMinute] <= 1439 AND [StartMinute] < [EndMinute]");
            });

            modelBuilder.Entity<OpenToGameMemberApproval>(b =>
            {
                b.HasKey(x => new { x.OpenToGameConfigId, x.PlayerId });

                b.HasOne(x => x.Config)
                    .WithMany(x => x.MemberApprovals)
                    .HasForeignKey(x => x.OpenToGameConfigId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Player)
                    .WithMany(x => x.OpenToGameApprovals)
                    .HasForeignKey(x => x.PlayerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureInvites(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TeamInvite>(b =>
            {
                b.HasOne(x => x.Team)
                    .WithMany(x => x.Invites)
                    .HasForeignKey(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.InvitedPlayer)
                    .WithMany(x => x.IncomingInvites)
                    .HasForeignKey(x => x.InvitedPlayerId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.TeamId, x.InvitedPlayerId });

                b.HasIndex(x => new { x.TeamId, x.ProposedSquadNumber })
                    .HasFilter($"[Status] = {(int)InviteStatus.Pending}");
            });
        }

        private static void ConfigureGameRequests(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GameRequest>(b =>
            {
                b.Property(x => x.Message).HasMaxLength(500);

                b.HasOne(x => x.FromTeam)
                    .WithMany(x => x.OutgoingGameRequests)
                    .HasForeignKey(x => x.FromTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.ToTeam)
                    .WithMany(x => x.IncomingGameRequests)
                    .HasForeignKey(x => x.ToTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.ToTeamId, x.Status });
                b.HasIndex(x => new { x.FromTeamId, x.Status });
            });
        }

        private static void ConfigureMatches(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Match>(b =>
            {
                b.HasOne(x => x.HomeTeam)
                    .WithMany(x => x.HomeMatches)
                    .HasForeignKey(x => x.HomeTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.AwayTeam)
                    .WithMany(x => x.AwayMatches)
                    .HasForeignKey(x => x.AwayTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.CreatedFromRequest)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedFromRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => x.CreatedFromRequestId)
                    .IsUnique()
                    .HasFilter("[CreatedFromRequestId] IS NOT NULL");

                b.HasOne(x => x.ConfirmedStadium)
                    .WithMany(x => x.Matches)
                    .HasForeignKey(x => x.ConfirmedStadiumId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.VenueProposals)
                    .WithOne(x => x.Match)
                    .HasForeignKey(x => x.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(x => x.ResultProposals)
                    .WithOne(x => x.Match)
                    .HasForeignKey(x => x.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => x.StartAtUtc);
                b.HasIndex(x => x.Status);
            });

            modelBuilder.Entity<MatchVenueProposal>(b =>
            {
                b.HasOne(x => x.Match)
                    .WithMany(x => x.VenueProposals)
                    .HasForeignKey(x => x.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.ProposedByTeam)
                    .WithMany()
                    .HasForeignKey(x => x.ProposedByTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.ProposedStadium)
                    .WithMany(x => x.VenueProposals)
                    .HasForeignKey(x => x.ProposedStadiumId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.Property(x => x.ProposedCustomVenueName).HasMaxLength(150);
                b.Property(x => x.ProposedCustomFormattedAddress).HasMaxLength(300);
            });

            modelBuilder.Entity<MatchResultProposal>(b =>
            {
                b.HasOne(x => x.Match)
                    .WithMany(x => x.ResultProposals)
                    .HasForeignKey(x => x.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.ProposedByTeam)
                    .WithMany()
                    .HasForeignKey(x => x.ProposedByTeamId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Stadium>(b =>
            {
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.FormattedAddress).HasMaxLength(300).IsRequired();
                b.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
                b.Property(x => x.CityName).HasMaxLength(100).IsRequired();
                b.Property(x => x.RegionName).HasMaxLength(100);

                b.HasIndex(x => new { x.CityName, x.RegionName });
            });
        }

        private static void ConfigureNotifications(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>(b =>
            {
                b.Property(x => x.Title).HasMaxLength(200).IsRequired();
                b.Property(x => x.Message).HasMaxLength(1000);

                b.HasOne(x => x.Player)
                    .WithMany(x => x.Notifications)
                    .HasForeignKey(x => x.PlayerId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.PlayerId, x.IsRead });
                b.HasIndex(x => new { x.PlayerId, x.CreatedAtUtc });
            });
        }
    }
}

using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchUp.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        private const string DefaultTeamLogo = "/img/default-team-logo.png";

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;

            var recentResultEntities = await _context.Matches
                .AsNoTracking()
                .Where(x =>
                    (x.ResultStatus == ResultStatus.Confirmed || x.Status == MatchStatus.Completed) &&
                    x.HomeTeamScore.HasValue &&
                    x.AwayTeamScore.HasValue)
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .OrderByDescending(x => x.StartAtUtc)
                .Take(6)
                .ToListAsync();

            var upcomingMatchEntities = await _context.Matches
                .AsNoTracking()
                .Where(x => x.Status == MatchStatus.Scheduled && x.StartAtUtc > now)
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .OrderBy(x => x.StartAtUtc)
                .Take(6)
                .ToListAsync();

            var openTeams = await _context.Teams
                .AsNoTracking()
                .Where(x => x.IsOpenToGame)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new HomeTeamCardVm
                {
                    TeamId = x.Id,
                    Name = x.Name,
                    LogoUrl = x.LogoUrl,
                    Description = x.Description,
                    ActiveMemberCount = x.Members.Count(m => m.IsActive),
                    OwnerName = x.Members
                        .Where(m => m.IsActive && m.Role == TeamRole.Owner)
                        .Select(m => m.Player != null
                            ? m.Player.FirstName + " " + m.Player.LastName
                            : "Unknown Owner")
                        .FirstOrDefault() ?? "Unknown Owner"
                })
                .Take(6)
                .ToListAsync();

            var featuredPlayers = await _context.Users
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new HomePlayerCardVm
                {
                    PlayerId = x.Id,
                    FullName = x.FirstName + " " + x.LastName,
                    ImageUrl = x.ImageUrl,
                    Nationality = x.Nationality
                })
                .Take(6)
                .ToListAsync();

            var featuredStadiums = await _context.Stadiums
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new HomeStadiumCardVm
                {
                    StadiumId = x.Id,
                    Name = x.Name,
                    ImageUrl = x.ImageUrl,
                    CityName = x.CityName,
                    FormattedAddress = x.FormattedAddress
                })
                .Take(3)
                .ToListAsync();

            var recentResults = recentResultEntities.Select(MapMatchCard).ToList();
            var upcomingMatches = upcomingMatchEntities.Select(MapMatchCard).ToList();

            var vm = new HomeIndexVm
            {
                TeamCount = await _context.Teams.AsNoTracking().CountAsync(),
                PlayerCount = await _context.Users.AsNoTracking().CountAsync(),
                ScheduledMatchCount = await _context.Matches.AsNoTracking()
                    .CountAsync(x => x.Status == MatchStatus.Scheduled && x.StartAtUtc > now),
                CompletedMatchCount = await _context.Matches.AsNoTracking()
                    .CountAsync(x => x.ResultStatus == ResultStatus.Confirmed || x.Status == MatchStatus.Completed),
                OpenToGameTeamCount = await _context.Teams.AsNoTracking().CountAsync(x => x.IsOpenToGame),

                HighlightMatch = upcomingMatches.FirstOrDefault() ?? recentResults.FirstOrDefault(),

                RecentResults = recentResults,
                UpcomingMatches = upcomingMatches,
                OpenToGameTeams = openTeams,
                FeaturedPlayers = featuredPlayers,
                FeaturedStadiums = featuredStadiums
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }

        private static HomeMatchCardVm MapMatchCard(Match match)
        {
            return new HomeMatchCardVm
            {
                MatchId = match.Id,
                HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team",
                HomeTeamLogoUrl = string.IsNullOrWhiteSpace(match.HomeTeam?.LogoUrl)
                    ? DefaultTeamLogo
                    : match.HomeTeam!.LogoUrl,

                AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team",
                AwayTeamLogoUrl = string.IsNullOrWhiteSpace(match.AwayTeam?.LogoUrl)
                    ? DefaultTeamLogo
                    : match.AwayTeam!.LogoUrl,

                StartAtUtc = match.StartAtUtc,
                Format = match.Format.ToString(),
                VenueText = BuildVenueText(match),
                HomeScore = match.HomeTeamScore,
                AwayScore = match.AwayTeamScore
            };
        }

        private static string BuildVenueText(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium && match.ConfirmedStadium is not null)
                return $"{match.ConfirmedStadium.Name} - {match.ConfirmedStadium.FormattedAddress}";

            if (match.ConfirmedVenueKind == VenueKind.Custom)
            {
                var venueName = string.IsNullOrWhiteSpace(match.ConfirmedCustomVenueName)
                    ? "Custom venue"
                    : match.ConfirmedCustomVenueName;

                var address = string.IsNullOrWhiteSpace(match.ConfirmedCustomFormattedAddress)
                    ? "Address not set"
                    : match.ConfirmedCustomFormattedAddress;

                return $"{venueName} - {address}";
            }

            return "Venue to be confirmed";
        }
    }
}
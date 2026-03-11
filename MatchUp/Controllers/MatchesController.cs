using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MatchUp.Controllers
{
    [Authorize]
    public class MatchesController : Controller
    {
        private readonly AppDbContext _context;
        private const string DefaultVenueImageUrl = "/img/locations/1.jpg";

        public MatchesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var match = await _context.Matches
                .AsNoTracking()
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .Include(x => x.VenueProposals)
                    .ThenInclude(x => x.ProposedByTeam)
                .Include(x => x.VenueProposals)
                    .ThenInclude(x => x.ProposedStadium)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (match is null)
                return NotFound();

            var currentPlayerId = GetCurrentPlayerId();

            var ownedTeamIds = new HashSet<Guid>();

            if (currentPlayerId.HasValue)
            {
                var ids = await _context.TeamMembers
                    .AsNoTracking()
                    .Where(x =>
                        x.PlayerId == currentPlayerId.Value &&
                        x.Role == TeamRole.Owner &&
                        x.IsActive)
                    .Select(x => x.TeamId)
                    .ToListAsync();

                ownedTeamIds = ids.ToHashSet();
            }

            var isHomeOwnerView = ownedTeamIds.Contains(match.HomeTeamId);
            var isAwayOwnerView = ownedTeamIds.Contains(match.AwayTeamId);

            var hasPendingVenueProposal = match.VenueProposals.Any(x => x.Status == ProposalStatus.Pending);

            var canProposeVenue =
                (isHomeOwnerView || isAwayOwnerView) &&
                match.Status == MatchStatus.Scheduled &&
                match.StartAtUtc > DateTime.UtcNow &&
                !hasPendingVenueProposal;

            var vm = new MatchDetailsVm
            {
                Id = match.Id,

                HomeTeamId = match.HomeTeamId,
                HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team",
                HomeTeamLogoUrl = match.HomeTeam?.LogoUrl,

                AwayTeamId = match.AwayTeamId,
                AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team",
                AwayTeamLogoUrl = match.AwayTeam?.LogoUrl,

                StartAtUtc = match.StartAtUtc,
                DurationMinutes = match.DurationMinutes,

                Format = match.Format.ToString(),
                Status = match.Status.ToString(),
                VenueStatus = GetVenueStatusText(match.VenueStatus),
                ResultStatus = match.ResultStatus.ToString(),

                CurrentVenueText = BuildConfirmedVenueText(match),
                VenueHeroImageUrl = BuildConfirmedVenueImageUrl(match),

                IsHomeOwnerView = isHomeOwnerView,
                IsAwayOwnerView = isAwayOwnerView,
                CanProposeVenue = canProposeVenue,
                HasPendingVenueProposal = hasPendingVenueProposal,

                VenueProposals = match.VenueProposals
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => new MatchVenueProposalListItemVm
                    {
                        Id = x.Id,
                        ProposedByTeamId = x.ProposedByTeamId,
                        ProposedByTeamName = x.ProposedByTeam?.Name ?? "Unknown Team",
                        VenueKindText = x.VenueKind.ToString(),
                        VenueText = BuildProposalVenueText(x),
                        StatusText = x.Status.ToString(),
                        CreatedAtUtc = x.CreatedAtUtc,
                        RespondedAtUtc = x.RespondedAtUtc
                    })
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ProposeVenue(Guid matchId)
        {
            var match = await _context.Matches
                .AsNoTracking()
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .Include(x => x.VenueProposals)
                .FirstOrDefaultAsync(x => x.Id == matchId);

            if (match is null)
                return NotFound();

            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var proposedByTeamId = await GetOwnerTeamIdForMatchAsync(currentPlayerId.Value, match);

            if (!proposedByTeamId.HasValue)
                return Forbid();

            if (match.Status != MatchStatus.Scheduled)
            {
                TempData["Error"] = "Venue can only be proposed for scheduled matches.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            if (match.StartAtUtc <= DateTime.UtcNow)
            {
                TempData["Error"] = "Venue can no longer be changed because the match time has passed.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            if (match.VenueProposals.Any(x => x.Status == ProposalStatus.Pending))
            {
                TempData["Error"] = "There is already a pending venue proposal for this match.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            var vm = new ProposeVenueVm
            {
                MatchId = match.Id,
                HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team",
                AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team",
                StartAtUtc = match.StartAtUtc,
                CurrentVenueText = BuildConfirmedVenueText(match),
                VenueHeroImageUrl = BuildConfirmedVenueImageUrl(match),
                VenueKind = match.ConfirmedVenueKind == VenueKind.Stadium ? VenueKind.Stadium : VenueKind.Custom
            };

            if (match.ConfirmedVenueKind == VenueKind.Stadium && match.ConfirmedStadiumId.HasValue)
            {
                vm.ProposedStadiumId = match.ConfirmedStadiumId;
                vm.VenueKind = VenueKind.Stadium;
            }
            else if (HasMeaningfulCustomVenue(match))
            {
                vm.VenueKind = VenueKind.Custom;
                vm.ProposedCustomVenueName = match.ConfirmedCustomVenueName;
                vm.ProposedCustomFormattedAddress = match.ConfirmedCustomFormattedAddress;
                vm.ProposedCustomLatitude = match.ConfirmedCustomLatitude;
                vm.ProposedCustomLongitude = match.ConfirmedCustomLongitude;
            }
            else
            {
                vm.VenueKind = VenueKind.Stadium;
            }

            await FillStadiumOptionsAsync(vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProposeVenue(ProposeVenueVm vm)
        {
            var match = await _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .Include(x => x.VenueProposals)
                .FirstOrDefaultAsync(x => x.Id == vm.MatchId);

            if (match is null)
                return NotFound();

            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var proposedByTeamId = await GetOwnerTeamIdForMatchAsync(currentPlayerId.Value, match);

            if (!proposedByTeamId.HasValue)
                return Forbid();

            if (match.Status != MatchStatus.Scheduled)
            {
                TempData["Error"] = "Venue can only be proposed for scheduled matches.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            if (match.StartAtUtc <= DateTime.UtcNow)
            {
                TempData["Error"] = "Venue can no longer be changed because the match time has passed.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            if (match.VenueProposals.Any(x => x.Status == ProposalStatus.Pending))
            {
                TempData["Error"] = "There is already a pending venue proposal for this match.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            if (vm.VenueKind == VenueKind.Stadium)
            {
                if (!vm.ProposedStadiumId.HasValue)
                {
                    ModelState.AddModelError(nameof(vm.ProposedStadiumId), "Please select a stadium.");
                }
                else
                {
                    var stadiumExists = await _context.Stadiums
                        .AnyAsync(x => x.Id == vm.ProposedStadiumId.Value && x.IsActive);

                    if (!stadiumExists)
                        ModelState.AddModelError(nameof(vm.ProposedStadiumId), "Selected stadium was not found.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(vm.ProposedCustomVenueName))
                    ModelState.AddModelError(nameof(vm.ProposedCustomVenueName), "Venue name is required.");

                if (string.IsNullOrWhiteSpace(vm.ProposedCustomFormattedAddress))
                    ModelState.AddModelError(nameof(vm.ProposedCustomFormattedAddress), "Address is required.");

                if (!vm.ProposedCustomLatitude.HasValue)
                    ModelState.AddModelError(nameof(vm.ProposedCustomLatitude), "Please choose a location on the map.");

                if (!vm.ProposedCustomLongitude.HasValue)
                    ModelState.AddModelError(nameof(vm.ProposedCustomLongitude), "Please choose a location on the map.");
            }

            if (!ModelState.IsValid)
            {
                vm.HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team";
                vm.AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team";
                vm.StartAtUtc = match.StartAtUtc;
                vm.CurrentVenueText = BuildConfirmedVenueText(match);
                vm.VenueHeroImageUrl = BuildConfirmedVenueImageUrl(match);

                await FillStadiumOptionsAsync(vm);
                return View(vm);
            }

            var proposal = new MatchVenueProposal
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                ProposedByTeamId = proposedByTeamId.Value,
                VenueKind = vm.VenueKind,
                ProposedStadiumId = vm.VenueKind == VenueKind.Stadium ? vm.ProposedStadiumId : null,
                ProposedCustomVenueName = vm.VenueKind == VenueKind.Custom ? vm.ProposedCustomVenueName?.Trim() : null,
                ProposedCustomFormattedAddress = vm.VenueKind == VenueKind.Custom ? vm.ProposedCustomFormattedAddress?.Trim() : null,
                ProposedCustomLatitude = vm.VenueKind == VenueKind.Custom ? vm.ProposedCustomLatitude : null,
                ProposedCustomLongitude = vm.VenueKind == VenueKind.Custom ? vm.ProposedCustomLongitude : null,
                Status = ProposalStatus.Pending
            };

            _context.MatchVenueProposals.Add(proposal);

            match.VenueStatus = VenueStatus.Proposed;

            var opponentTeamId = proposedByTeamId.Value == match.HomeTeamId
                ? match.AwayTeamId
                : match.HomeTeamId;

            var opponentOwners = await _context.TeamMembers
                .Where(x =>
                    x.TeamId == opponentTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            var proposerTeamName = proposedByTeamId.Value == match.HomeTeamId
                ? match.HomeTeam?.Name ?? "Unknown Team"
                : match.AwayTeam?.Name ?? "Unknown Team";

            foreach (var ownerPlayerId in opponentOwners)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = ownerPlayerId,
                    Type = NotificationType.MatchVenueProposalReceived,
                    Title = "Venue proposal received",
                    Message = $"{proposerTeamName} proposed a venue change for the match scheduled on {match.StartAtUtc:dd MMM yyyy HH:mm}.",
                    TargetType = NotificationTargetType.MatchVenueProposal,
                    TargetId = proposal.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Venue proposal submitted successfully.";
            return RedirectToAction(nameof(Details), new { id = vm.MatchId });
        }

        private async Task<Guid?> GetOwnerTeamIdForMatchAsync(Guid playerId, Match match)
        {
            var ownerMembership = await _context.TeamMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.PlayerId == playerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive &&
                    (x.TeamId == match.HomeTeamId || x.TeamId == match.AwayTeamId));

            return ownerMembership?.TeamId;
        }

        private async Task FillStadiumOptionsAsync(ProposeVenueVm vm)
        {
            vm.StadiumOptions = await _context.Stadiums
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new StadiumSelectItemVm
                {
                    Id = x.Id,
                    Text = $"{x.Name} - {x.CityName}",
                    ImageUrl = string.IsNullOrWhiteSpace(x.ImageUrl) ? null : x.ImageUrl
                })
                .ToListAsync();
        }

        private static bool HasMeaningfulCustomVenue(Match match)
        {
            if (match.ConfirmedVenueKind != VenueKind.Custom)
                return false;

            var name = match.ConfirmedCustomVenueName?.Trim();
            var address = match.ConfirmedCustomFormattedAddress?.Trim();

            var isPlaceholderName = string.Equals(name, "Venue to be confirmed", StringComparison.OrdinalIgnoreCase);
            var isPlaceholderAddress = string.Equals(address, "To be determined", StringComparison.OrdinalIgnoreCase);

            if (isPlaceholderName && isPlaceholderAddress)
                return false;

            return !string.IsNullOrWhiteSpace(name) ||
                   !string.IsNullOrWhiteSpace(address) ||
                   match.ConfirmedCustomLatitude.HasValue ||
                   match.ConfirmedCustomLongitude.HasValue;
        }

        private static string BuildConfirmedVenueText(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium && match.ConfirmedStadium is not null)
                return $"{match.ConfirmedStadium.Name} - {match.ConfirmedStadium.FormattedAddress}";

            if (HasMeaningfulCustomVenue(match))
            {
                var name = string.IsNullOrWhiteSpace(match.ConfirmedCustomVenueName)
                    ? "Custom venue"
                    : match.ConfirmedCustomVenueName;

                var address = string.IsNullOrWhiteSpace(match.ConfirmedCustomFormattedAddress)
                    ? "Address not set"
                    : match.ConfirmedCustomFormattedAddress;

                return $"{name} - {address}";
            }

            return "Venue to be confirmed";
        }

        private static string BuildConfirmedVenueImageUrl(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium &&
                match.ConfirmedStadium is not null &&
                !string.IsNullOrWhiteSpace(match.ConfirmedStadium.ImageUrl))
            {
                return match.ConfirmedStadium.ImageUrl;
            }

            return DefaultVenueImageUrl;
        }

        private static string BuildProposalVenueText(MatchVenueProposal proposal)
        {
            if (proposal.VenueKind == VenueKind.Stadium && proposal.ProposedStadium is not null)
                return $"{proposal.ProposedStadium.Name} - {proposal.ProposedStadium.FormattedAddress}";

            var name = string.IsNullOrWhiteSpace(proposal.ProposedCustomVenueName)
                ? "Custom venue"
                : proposal.ProposedCustomVenueName;

            var address = string.IsNullOrWhiteSpace(proposal.ProposedCustomFormattedAddress)
                ? "Address not set"
                : proposal.ProposedCustomFormattedAddress;

            return $"{name} - {address}";
        }

        private static string GetVenueStatusText(VenueStatus venueStatus)
        {
            return venueStatus switch
            {
                VenueStatus.Unset => "Unset",
                VenueStatus.Proposed => "Pending Approval",
                VenueStatus.Confirmed => "Confirmed",
                _ => "Unknown"
            };
        }

        private Guid? GetCurrentPlayerId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return Guid.Parse(userId);
        }
    }
}

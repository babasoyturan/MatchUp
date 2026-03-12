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

            var isHomeOwnerView = currentPlayerId.HasValue && await _context.TeamMembers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.TeamId == match.HomeTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var isAwayOwnerView = currentPlayerId.HasValue && await _context.TeamMembers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.TeamId == match.AwayTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var hasPendingVenueProposal = match.VenueProposals.Any(x => x.Status == ProposalStatus.Pending);

            var canProposeVenue =
                (isHomeOwnerView || isAwayOwnerView) &&
                match.Status == MatchStatus.Scheduled &&
                match.StartAtUtc > DateTime.UtcNow &&
                !hasPendingVenueProposal;

            var resultProposals = await _context.MatchResultProposals
                .AsNoTracking()
                .Where(x => x.MatchId == id)
                .Include(x => x.ProposedByTeam)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync();

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
                VenueStatus = match.VenueStatus.ToString(),
                ResultStatus = match.ResultStatus.ToString(),

                CurrentVenueText = BuildCurrentVenueText(match),
                VenueHeroImageUrl = BuildVenueHeroImageUrl(match),

                IsHomeOwnerView = isHomeOwnerView,
                IsAwayOwnerView = isAwayOwnerView,

                CanProposeVenue = canProposeVenue,
                HasPendingVenueProposal = hasPendingVenueProposal,

                VenueProposals = match.VenueProposals
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => new MatchUp.ViewModels.Matches.MatchVenueProposalListItemVm
                    {
                        ProposalId = x.Id,
                        ProposedByTeamName = x.ProposedByTeam != null
                            ? x.ProposedByTeam.Name
                            : "Unknown Team",
                        VenueKindText = x.VenueKind.ToString(),
                        VenueText = BuildProposalVenueText(x),
                        StatusText = x.Status.ToString(),
                        CreatedAtUtc = x.CreatedAtUtc,
                        RespondedAtUtc = x.RespondedAtUtc,

                        CanApprove = CanRespondToVenueProposal(match, x, isHomeOwnerView, isAwayOwnerView),
                        CanDecline = CanRespondToVenueProposal(match, x, isHomeOwnerView, isAwayOwnerView)
                    })
                    .ToList()
            };

            var isMatchFinished = IsMatchFinished(match);
            var hasPendingResultProposal = resultProposals.Any(x => x.Status == ProposalStatus.Pending);

            vm.CanProposeResult =
                (vm.IsHomeOwnerView || vm.IsAwayOwnerView) &&
                isMatchFinished &&
                match.ResultStatus != ResultStatus.Confirmed &&
                match.Status != MatchStatus.Completed &&
                !hasPendingResultProposal;

            vm.HasPendingResultProposal = hasPendingResultProposal;

            vm.ConfirmedScoreText =
                match.HomeTeamScore.HasValue && match.AwayTeamScore.HasValue
                    ? $"{match.HomeTeamScore.Value} - {match.AwayTeamScore.Value}"
                    : null;

            vm.ResultProposals = resultProposals
                .Select(x => new MatchResultProposalListItemVm
                {
                    Id = x.Id,
                    ProposedByTeamName = x.ProposedByTeam != null ? x.ProposedByTeam.Name : "Unknown Team",
                    ProposedHomeTeamScore = x.ProposedHomeTeamScore,
                    ProposedAwayTeamScore = x.ProposedAwayTeamScore,
                    StatusText = x.Status.ToString(),
                    CreatedAtUtc = x.CreatedAtUtc,
                    RespondedAtUtc = x.RespondedAtUtc
                })
                .ToList();

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

            var opponentTeamId = proposal.ProposedByTeamId == match.HomeTeamId
                ? match.AwayTeamId
                : match.HomeTeamId;

            var opponentOwnerIds = await _context.TeamMembers
                .Where(x =>
                    x.TeamId == opponentTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            foreach (var ownerId in opponentOwnerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = ownerId,
                    Type = NotificationType.MatchVenueProposalReceived,
                    Title = "New venue proposal received",
                    Message = $"A new venue proposal was submitted for {match.HomeTeam!.Name} vs {match.AwayTeam!.Name}.",
                    TargetType = NotificationTargetType.MatchVenueProposal,
                    TargetId = proposal.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Venue proposal submitted successfully.";
            return RedirectToAction(nameof(Details), new { id = vm.MatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveVenueProposal(Guid proposalId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var proposal = await _context.MatchVenueProposals
                .Include(x => x.Match)
                    .ThenInclude(x => x.HomeTeam)
                .Include(x => x.Match)
                    .ThenInclude(x => x.AwayTeam)
                .Include(x => x.ProposedStadium)
                .FirstOrDefaultAsync(x => x.Id == proposalId);

            if (proposal is null)
                return NotFound();

            var match = proposal.Match;
            if (match is null)
                return NotFound();

            if (proposal.Status != ProposalStatus.Pending)
            {
                TempData["Error"] = "This venue proposal is no longer pending.";
                return RedirectToAction(nameof(Details), new { id = match.Id });
            }

            if (match.Status != MatchStatus.Scheduled || match.StartAtUtc <= DateTime.UtcNow)
            {
                TempData["Error"] = "This match is no longer available for venue approval.";
                return RedirectToAction(nameof(Details), new { id = match.Id });
            }

            var isHomeOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == match.HomeTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var isAwayOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == match.AwayTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var canRespond =
                (proposal.ProposedByTeamId == match.HomeTeamId && isAwayOwner) ||
                (proposal.ProposedByTeamId == match.AwayTeamId && isHomeOwner);

            if (!canRespond)
                return Forbid();

            proposal.Status = ProposalStatus.Accepted;
            proposal.RespondedAtUtc = DateTime.UtcNow;

            if (proposal.VenueKind == VenueKind.Stadium)
            {
                match.ConfirmedVenueKind = VenueKind.Stadium;
                match.ConfirmedStadiumId = proposal.ProposedStadiumId;

                match.ConfirmedCustomVenueName = null;
                match.ConfirmedCustomFormattedAddress = null;
                match.ConfirmedCustomLatitude = null;
                match.ConfirmedCustomLongitude = null;
            }
            else
            {
                match.ConfirmedVenueKind = VenueKind.Custom;
                match.ConfirmedStadiumId = null;

                match.ConfirmedCustomVenueName = proposal.ProposedCustomVenueName;
                match.ConfirmedCustomFormattedAddress = proposal.ProposedCustomFormattedAddress;
                match.ConfirmedCustomLatitude = proposal.ProposedCustomLatitude;
                match.ConfirmedCustomLongitude = proposal.ProposedCustomLongitude;
            }

            match.VenueStatus = VenueStatus.Confirmed;

            var otherPendingProposals = await _context.MatchVenueProposals
                .Where(x =>
                    x.MatchId == match.Id &&
                    x.Id != proposal.Id &&
                    x.Status == ProposalStatus.Pending)
                .ToListAsync();

            foreach (var item in otherPendingProposals)
            {
                item.Status = ProposalStatus.Declined;
                item.RespondedAtUtc = DateTime.UtcNow;
            }

            var proposerOwnerIds = await _context.TeamMembers
                .Where(x =>
                    x.TeamId == proposal.ProposedByTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            foreach (var ownerId in proposerOwnerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = ownerId,
                    Type = NotificationType.MatchVenueProposalApproved,
                    Title = "Venue proposal approved",
                    Message = $"Your venue proposal for {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has been approved.",
                    TargetType = NotificationTargetType.MatchVenueProposal,
                    TargetId = proposal.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Venue proposal approved successfully.";
            return RedirectToAction(nameof(Details), new { id = match.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineVenueProposal(Guid proposalId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var proposal = await _context.MatchVenueProposals
                .Include(x => x.Match)
                    .ThenInclude(x => x.HomeTeam)
                .Include(x => x.Match)
                    .ThenInclude(x => x.AwayTeam)
                .FirstOrDefaultAsync(x => x.Id == proposalId);

            if (proposal is null)
                return NotFound();

            var match = proposal.Match;
            if (match is null)
                return NotFound();

            if (proposal.Status != ProposalStatus.Pending)
            {
                TempData["Error"] = "This venue proposal is no longer pending.";
                return RedirectToAction(nameof(Details), new { id = match.Id });
            }

            if (match.Status != MatchStatus.Scheduled || match.StartAtUtc <= DateTime.UtcNow)
            {
                TempData["Error"] = "This match is no longer available for venue response.";
                return RedirectToAction(nameof(Details), new { id = match.Id });
            }

            var isHomeOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == match.HomeTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var isAwayOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == match.AwayTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            var canRespond =
                (proposal.ProposedByTeamId == match.HomeTeamId && isAwayOwner) ||
                (proposal.ProposedByTeamId == match.AwayTeamId && isHomeOwner);

            if (!canRespond)
                return Forbid();

            proposal.Status = ProposalStatus.Declined;
            proposal.RespondedAtUtc = DateTime.UtcNow;

            var anotherPendingExists = await _context.MatchVenueProposals
                .AnyAsync(x =>
                    x.MatchId == match.Id &&
                    x.Id != proposal.Id &&
                    x.Status == ProposalStatus.Pending);

            if (HasConfirmedVenue(match))
            {
                match.VenueStatus = VenueStatus.Confirmed;
            }
            else if (anotherPendingExists)
            {
                match.VenueStatus = VenueStatus.Proposed;
            }
            else
            {
                match.VenueStatus = VenueStatus.Unset;
            }

            var proposerOwnerIds = await _context.TeamMembers
                .Where(x =>
                    x.TeamId == proposal.ProposedByTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            foreach (var ownerId in proposerOwnerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = ownerId,
                    Type = NotificationType.MatchVenueProposalDeclined,
                    Title = "Venue proposal declined",
                    Message = $"Your venue proposal for {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has been declined.",
                    TargetType = NotificationTargetType.MatchVenueProposal,
                    TargetId = proposal.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Venue proposal declined.";
            return RedirectToAction(nameof(Details), new { id = match.Id });
        }

        [HttpGet]
        public async Task<IActionResult> ProposeResult(Guid matchId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var match = await _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ResultProposals)
                .FirstOrDefaultAsync(x => x.Id == matchId);

            if (match is null)
                return NotFound();

            var ownedTeamId = await GetOwnedMatchTeamIdAsync(currentPlayerId!.Value, match);

            if (!ownedTeamId.HasValue)
                return Forbid();

            if (!IsMatchFinished(match))
            {
                TempData["Error"] = "You can propose a result only after the match time has finished.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            if (match.ResultStatus == ResultStatus.Confirmed || match.Status == MatchStatus.Completed)
            {
                TempData["Error"] = "This match result has already been confirmed.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            var hasPendingProposal = match.ResultProposals.Any(x => x.Status == ProposalStatus.Pending);

            if (hasPendingProposal)
            {
                TempData["Error"] = "A pending result proposal already exists for this match.";
                return RedirectToAction(nameof(Details), new { id = matchId });
            }

            var vm = new ProposeResultVm
            {
                MatchId = match.Id,
                HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team",
                AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team",
                StartAtUtc = match.StartAtUtc,
                CurrentResultStatus = match.ResultStatus.ToString(),
                CurrentVenueText = BuildCurrentVenueText(match),
                ProposedHomeTeamScore = match.HomeTeamScore ?? 0,
                ProposedAwayTeamScore = match.AwayTeamScore ?? 0
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProposeResult(ProposeResultVm vm)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var match = await _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ResultProposals)
                .FirstOrDefaultAsync(x => x.Id == vm.MatchId);

            if (match is null)
                return NotFound();

            var proposedByTeamId = await GetOwnedMatchTeamIdAsync(currentPlayerId!.Value, match);

            if (!proposedByTeamId.HasValue)
                return Forbid();

            if (!IsMatchFinished(match))
            {
                TempData["Error"] = "You can propose a result only after the match time has finished.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            if (match.ResultStatus == ResultStatus.Confirmed || match.Status == MatchStatus.Completed)
            {
                TempData["Error"] = "This match result has already been confirmed.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            var hasPendingProposal = match.ResultProposals.Any(x => x.Status == ProposalStatus.Pending);

            if (hasPendingProposal)
            {
                TempData["Error"] = "A pending result proposal already exists for this match.";
                return RedirectToAction(nameof(Details), new { id = vm.MatchId });
            }

            if (vm.ProposedHomeTeamScore < 0 || vm.ProposedAwayTeamScore < 0)
                ModelState.AddModelError(string.Empty, "Scores cannot be negative.");

            if (!ModelState.IsValid)
            {
                vm.HomeTeamName = match.HomeTeam?.Name ?? "Unknown Team";
                vm.AwayTeamName = match.AwayTeam?.Name ?? "Unknown Team";
                vm.StartAtUtc = match.StartAtUtc;
                vm.CurrentResultStatus = match.ResultStatus.ToString();
                vm.CurrentVenueText = BuildCurrentVenueText(match);
                return View(vm);
            }

            var proposal = new MatchResultProposal
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                ProposedByTeamId = proposedByTeamId.Value,
                ProposedHomeTeamScore = vm.ProposedHomeTeamScore,
                ProposedAwayTeamScore = vm.ProposedAwayTeamScore,
                Status = ProposalStatus.Pending
            };

            _context.MatchResultProposals.Add(proposal);

            match.ResultStatus = ResultStatus.PendingApproval;

            var opponentTeamId = proposedByTeamId.Value == match.HomeTeamId
                ? match.AwayTeamId
                : match.HomeTeamId;

            var opponentOwnerIds = await GetTeamOwnerIdsAsync(opponentTeamId);

            foreach (var ownerId in opponentOwnerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = ownerId,
                    Type = NotificationType.MatchResultProposalReceived,
                    Title = "Match result proposal received",
                    Message = $"{match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: proposed score is {vm.ProposedHomeTeamScore} - {vm.ProposedAwayTeamScore}.",
                    TargetType = NotificationTargetType.MatchResultProposal,
                    TargetId = proposal.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Match result proposal submitted successfully.";
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

        private static bool HasConfirmedVenue(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium && match.ConfirmedStadiumId.HasValue)
                return true;

            if (match.ConfirmedVenueKind == VenueKind.Custom &&
                (!string.IsNullOrWhiteSpace(match.ConfirmedCustomVenueName) ||
                 !string.IsNullOrWhiteSpace(match.ConfirmedCustomFormattedAddress) ||
                 match.ConfirmedCustomLatitude.HasValue ||
                 match.ConfirmedCustomLongitude.HasValue))
                return true;

            return false;
        }

        private static string BuildCurrentVenueText(Match match)
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

            return "Venue not set";
        }

        private static string BuildVenueHeroImageUrl(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium &&
                match.ConfirmedStadium is not null &&
                !string.IsNullOrWhiteSpace(match.ConfirmedStadium.ImageUrl))
            {
                return match.ConfirmedStadium.ImageUrl;
            }

            return "/img/locations/1.jpg";
        }

        private static string BuildProposalVenueText(MatchVenueProposal proposal)
        {
            if (proposal.VenueKind == VenueKind.Stadium && proposal.ProposedStadium is not null)
                return $"{proposal.ProposedStadium.Name} - {proposal.ProposedStadium.FormattedAddress}";

            var venueName = string.IsNullOrWhiteSpace(proposal.ProposedCustomVenueName)
                ? "Custom venue"
                : proposal.ProposedCustomVenueName;

            var address = string.IsNullOrWhiteSpace(proposal.ProposedCustomFormattedAddress)
                ? "Address not set"
                : proposal.ProposedCustomFormattedAddress;

            return $"{venueName} - {address}";
        }

        private static bool CanRespondToVenueProposal(
            Match match,
            MatchVenueProposal proposal,
            bool isHomeOwnerView,
            bool isAwayOwnerView)
        {
            if (proposal.Status != ProposalStatus.Pending)
                return false;

            if (match.Status != MatchStatus.Scheduled)
                return false;

            if (match.StartAtUtc <= DateTime.UtcNow)
                return false;

            if (proposal.ProposedByTeamId == match.HomeTeamId && isAwayOwnerView)
                return true;

            if (proposal.ProposedByTeamId == match.AwayTeamId && isHomeOwnerView)
                return true;

            return false;
        }

        private static bool IsMatchFinished(Match match)
        {
            return DateTime.UtcNow >= match.StartAtUtc.AddMinutes(match.DurationMinutes);
        }

        private async Task<Guid?> GetOwnedMatchTeamIdAsync(Guid playerId, Match match)
        {
            return await _context.TeamMembers
                .Where(x =>
                    x.PlayerId == playerId &&
                    x.IsActive &&
                    x.Role == TeamRole.Owner &&
                    (x.TeamId == match.HomeTeamId || x.TeamId == match.AwayTeamId))
                .Select(x => (Guid?)x.TeamId)
                .FirstOrDefaultAsync();
        }

        private async Task<List<Guid>> GetTeamOwnerIdsAsync(Guid teamId)
        {
            return await _context.TeamMembers
                .Where(x =>
                    x.TeamId == teamId &&
                    x.IsActive &&
                    x.Role == TeamRole.Owner)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();
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

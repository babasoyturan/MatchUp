using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MatchUp.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Player> _userManager;

        public NotificationsController(AppDbContext context, UserManager<Player> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(x => x.PlayerId == currentPlayerId.Value)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync();

            var inviteIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.TeamInvite && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var invites = await _context.TeamInvites
                .AsNoTracking()
                .Where(x => inviteIds.Contains(x.Id))
                .Include(x => x.Team)
                .ToDictionaryAsync(x => x.Id);

            var submissionIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.OpenToGameSubmission && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var submissions = await _context.OpenToGameSubmissions
                .AsNoTracking()
                .Where(x => submissionIds.Contains(x.Id))
                .Include(x => x.Team)
                .Include(x => x.MemberApprovals)
                .ToDictionaryAsync(x => x.Id);

            var gameRequestIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.GameRequest && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var gameRequests = await _context.GameRequests
                .AsNoTracking()
                .Where(x => gameRequestIds.Contains(x.Id))
                .Include(x => x.FromTeam)
                .Include(x => x.ToTeam)
                .ToDictionaryAsync(x => x.Id);

            var venueProposalIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.MatchVenueProposal && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var venueProposals = await _context.MatchVenueProposals
                .AsNoTracking()
                .Where(x => venueProposalIds.Contains(x.Id))
                .Include(x => x.Match)
                    .ThenInclude(x => x.HomeTeam)
                .Include(x => x.Match)
                    .ThenInclude(x => x.AwayTeam)
                .ToDictionaryAsync(x => x.Id);

            var directMatchIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.Match && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var directMatches = await _context.Matches
                .AsNoTracking()
                .Where(x => directMatchIds.Contains(x.Id))
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .ToDictionaryAsync(x => x.Id);

            var matchesFromRequests = await _context.Matches
                .AsNoTracking()
                .Where(x => x.CreatedFromRequestId.HasValue && gameRequestIds.Contains(x.CreatedFromRequestId.Value))
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .ToListAsync();

            var requestMatchMap = matchesFromRequests
                .Where(x => x.CreatedFromRequestId.HasValue)
                .GroupBy(x => x.CreatedFromRequestId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.UtcNow;

            var vm = new NotificationsIndexVm
            {
                UnreadCount = notifications.Count(x => !x.IsRead),
                Notifications = notifications.Select(n =>
                {
                    invites.TryGetValue(n.TargetId ?? Guid.Empty, out var invite);
                    submissions.TryGetValue(n.TargetId ?? Guid.Empty, out var submission);
                    gameRequests.TryGetValue(n.TargetId ?? Guid.Empty, out var gameRequest);
                    venueProposals.TryGetValue(n.TargetId ?? Guid.Empty, out var venueProposal);
                    directMatches.TryGetValue(n.TargetId ?? Guid.Empty, out var directMatch);

                    var isInviteNotification =
                        n.TargetType == NotificationTargetType.TeamInvite &&
                        invite is not null;

                    var canAcceptOrDeclineInvite =
                        isInviteNotification &&
                        invite!.InvitedPlayerId == currentPlayerId.Value &&
                        invite.Status == InviteStatus.Pending &&
                        invite.ExpiresAtUtc > now;

                    var approvalRow = submission?.MemberApprovals
                        .FirstOrDefault(x => x.PlayerId == currentPlayerId.Value);

                    var canApproveOpenToGame =
                        n.Type == NotificationType.OpenToGameApprovalRequired &&
                        submission is not null &&
                        submission.Status == OpenToGameSubmissionStatus.PendingApprovals &&
                        approvalRow is not null &&
                        approvalRow.Status == ApprovalStatus.Pending;

                    var canDeclineOpenToGame = canApproveOpenToGame;

                    var canAcceptGameRequest =
                        n.Type == NotificationType.GameRequestReceived &&
                        gameRequest is not null &&
                        gameRequest.Status == GameRequestStatus.Pending;

                    var canDeclineGameRequest = canAcceptGameRequest;

                    Match? resolvedMatch = null;

                    if (n.TargetType == NotificationTargetType.Match && directMatch is not null)
                    {
                        resolvedMatch = directMatch;
                    }
                    else if (n.TargetType == NotificationTargetType.MatchVenueProposal && venueProposal?.Match is not null)
                    {
                        resolvedMatch = venueProposal.Match;
                    }
                    else if (n.TargetType == NotificationTargetType.GameRequest &&
                             n.TargetId.HasValue &&
                             requestMatchMap.TryGetValue(n.TargetId.Value, out var requestMatch))
                    {
                        resolvedMatch = requestMatch;
                    }

                    var matchId = resolvedMatch?.Id;
                    var matchTitle = resolvedMatch is not null
                        ? $"{resolvedMatch.HomeTeam?.Name ?? "Unknown"} vs {resolvedMatch.AwayTeam?.Name ?? "Unknown"}"
                        : null;

                    return new NotificationListItemVm
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Message = n.Message,
                        TypeText = n.Type.ToString(),
                        IsRead = n.IsRead,
                        CreatedAtUtc = n.CreatedAtUtc,

                        CanAccept = canAcceptOrDeclineInvite,
                        CanDecline = canAcceptOrDeclineInvite,

                        TeamInviteId = invite?.Id,
                        TeamInviteStatus = invite?.Status.ToString(),
                        ProposedSquadNumber = invite?.ProposedSquadNumber,
                        InviteExpiresAtUtc = invite?.ExpiresAtUtc,

                        OpenToGameSubmissionId = submission?.Id,
                        OpenToGameSubmissionStatus = submission?.Status.ToString(),

                        CanApproveOpenToGame = canApproveOpenToGame,
                        CanDeclineOpenToGame = canDeclineOpenToGame,

                        GameRequestId = gameRequest?.Id,
                        GameRequestStatus = gameRequest?.Status.ToString(),
                        GameRequestStartAtUtc = gameRequest?.StartAtUtc,
                        GameRequestFormat = gameRequest?.Format.ToString(),
                        GameRequestDurationMinutes = gameRequest?.DurationMinutes,

                        CanAcceptGameRequest = canAcceptGameRequest,
                        CanDeclineGameRequest = canDeclineGameRequest,

                        TeamName = invite?.Team?.Name
                                   ?? submission?.Team?.Name
                                   ?? gameRequest?.FromTeam?.Name
                                   ?? venueProposal?.Match?.HomeTeam?.Name,

                        IsExpired = invite is not null && invite.ExpiresAtUtc <= now,

                        MatchId = matchId,
                        MatchTitle = matchTitle,
                        CanOpenMatch = matchId.HasValue
                    };
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is null)
                return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptTeamInvite(Guid inviteId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var invite = await _context.TeamInvites
                .Include(x => x.Team)
                .FirstOrDefaultAsync(x => x.Id == inviteId);

            if (invite is null)
            {
                TempData["Error"] = "Invite not found.";
                return RedirectToAction(nameof(Index));
            }

            if (invite.InvitedPlayerId != currentPlayerId.Value)
                return Forbid();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (invite.ExpiresAtUtc <= DateTime.UtcNow)
            {
                invite.Status = InviteStatus.Expired;

                if (notification is not null && !notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAtUtc = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                TempData["Error"] = "This invite has expired.";
                return RedirectToAction(nameof(Index));
            }

            if (invite.Status != InviteStatus.Pending)
            {
                TempData["Error"] = "This invite is no longer pending.";
                return RedirectToAction(nameof(Index));
            }

            var isAlreadyMember = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.IsActive);

            if (isAlreadyMember)
            {
                TempData["Error"] = "You are already an active member of this team.";
                return RedirectToAction(nameof(Index));
            }

            var squadNumberTaken = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.IsActive &&
                    x.SquadNumber == invite.ProposedSquadNumber);

            if (squadNumberTaken)
            {
                TempData["Error"] = "This squad number is already taken in the team.";
                return RedirectToAction(nameof(Index));
            }

            var newMembership = new TeamMember
            {
                TeamId = invite.TeamId,
                PlayerId = currentPlayerId.Value,
                SquadNumber = invite.ProposedSquadNumber,
                Role = TeamRole.Member,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            };

            _context.TeamMembers.Add(newMembership);

            invite.Status = InviteStatus.Accepted;

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var teamOwner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (teamOwner is not null && teamOwner.PlayerId != currentPlayerId.Value)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = teamOwner.PlayerId,
                    Type = NotificationType.TeamInviteAccepted,
                    Title = "Team invite accepted",
                    Message = $"Your invitation for team '{invite.Team!.Name}' has been accepted.",
                    TargetType = NotificationTargetType.TeamInvite,
                    TargetId = invite.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Invite accepted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineTeamInvite(Guid inviteId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var invite = await _context.TeamInvites
                .Include(x => x.Team)
                .FirstOrDefaultAsync(x => x.Id == inviteId);

            if (invite is null)
            {
                TempData["Error"] = "Invite not found.";
                return RedirectToAction(nameof(Index));
            }

            if (invite.InvitedPlayerId != currentPlayerId.Value)
                return Forbid();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (invite.ExpiresAtUtc <= DateTime.UtcNow)
            {
                invite.Status = InviteStatus.Expired;

                if (notification is not null && !notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAtUtc = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                TempData["Error"] = "This invite has expired.";
                return RedirectToAction(nameof(Index));
            }

            if (invite.Status != InviteStatus.Pending)
            {
                TempData["Error"] = "This invite is no longer pending.";
                return RedirectToAction(nameof(Index));
            }

            invite.Status = InviteStatus.Declined;

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var teamOwner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (teamOwner is not null && teamOwner.PlayerId != currentPlayerId.Value)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = teamOwner.PlayerId,
                    Type = NotificationType.TeamInviteDeclined,
                    Title = "Team invite declined",
                    Message = $"Your invitation for team '{invite.Team!.Name}' has been declined.",
                    TargetType = NotificationTargetType.TeamInvite,
                    TargetId = invite.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Invite declined.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOpenToGame(Guid submissionId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var submission = await _context.OpenToGameSubmissions
                .Include(x => x.Team)
                .Include(x => x.MemberApprovals)
                .FirstOrDefaultAsync(x => x.Id == submissionId);

            if (submission is null)
            {
                TempData["Error"] = "Open To Game submission not found.";
                return RedirectToAction(nameof(Index));
            }

            var approval = submission.MemberApprovals
                .FirstOrDefault(x => x.PlayerId == currentPlayerId.Value);

            if (submission.Status != OpenToGameSubmissionStatus.PendingApprovals ||
                approval is null ||
                approval.Status != ApprovalStatus.Pending)
            {
                TempData["Error"] = "This approval request is no longer available.";
                return RedirectToAction(nameof(Index));
            }

            approval.Status = ApprovalStatus.Approved;
            approval.RespondedAtUtc = DateTime.UtcNow;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            if (submission.MemberApprovals.All(x => x.Status == ApprovalStatus.Approved))
            {
                submission.Status = OpenToGameSubmissionStatus.Active;
                submission.ResolvedAtUtc = DateTime.UtcNow;
                submission.Team!.IsOpenToGame = true;
                submission.Team.ActiveOpenToGameSubmissionId = submission.Id;

                var pendingNotifications = await _context.Notifications
                    .Where(x =>
                        x.Type == NotificationType.OpenToGameApprovalRequired &&
                        x.TargetType == NotificationTargetType.OpenToGameSubmission &&
                        x.TargetId == submission.Id &&
                        !x.IsRead)
                    .ToListAsync();

                foreach (var item in pendingNotifications)
                {
                    item.IsRead = true;
                    item.ReadAtUtc = DateTime.UtcNow;
                }

                var activePlayerIds = await _context.TeamMembers
                    .Where(x => x.TeamId == submission.TeamId && x.IsActive)
                    .Select(x => x.PlayerId)
                    .Distinct()
                    .ToListAsync();

                foreach (var playerId in activePlayerIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        PlayerId = playerId,
                        Type = NotificationType.OpenToGameActivated,
                        Title = "Open To Game activated",
                        Message = $"{submission.Team.Name} is now open to game.",
                        TargetType = NotificationTargetType.OpenToGameSubmission,
                        TargetId = submission.Id,
                        IsRead = false
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Approval accepted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineOpenToGame(Guid submissionId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var submission = await _context.OpenToGameSubmissions
                .Include(x => x.Team)
                .Include(x => x.MemberApprovals)
                .FirstOrDefaultAsync(x => x.Id == submissionId);

            if (submission is null)
            {
                TempData["Error"] = "Open To Game submission not found.";
                return RedirectToAction(nameof(Index));
            }

            var approval = submission.MemberApprovals
                .FirstOrDefault(x => x.PlayerId == currentPlayerId.Value);

            if (submission.Status != OpenToGameSubmissionStatus.PendingApprovals ||
                approval is null ||
                approval.Status != ApprovalStatus.Pending)
            {
                TempData["Error"] = "This approval request is no longer available.";
                return RedirectToAction(nameof(Index));
            }

            approval.Status = ApprovalStatus.Declined;
            approval.RespondedAtUtc = DateTime.UtcNow;

            submission.Status = OpenToGameSubmissionStatus.Cancelled;
            submission.ResolvedAtUtc = DateTime.UtcNow;
            submission.Team!.IsOpenToGame = false;

            if (submission.Team.ActiveOpenToGameSubmissionId == submission.Id)
                submission.Team.ActiveOpenToGameSubmissionId = null;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var otherNotifications = await _context.Notifications
                .Where(x =>
                    x.Type == NotificationType.OpenToGameApprovalRequired &&
                    x.TargetType == NotificationTargetType.OpenToGameSubmission &&
                    x.TargetId == submission.Id &&
                    !x.IsRead)
                .ToListAsync();

            foreach (var item in otherNotifications)
            {
                item.IsRead = true;
                item.ReadAtUtc = DateTime.UtcNow;
            }

            var owner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == submission.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (owner is not null && owner.PlayerId != currentPlayerId.Value)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = owner.PlayerId,
                    Type = NotificationType.OpenToGameApprovalDeclined,
                    Title = "Open To Game approval declined",
                    Message = $"A team member declined the Open To Game request for '{submission.Team.Name}'.",
                    TargetType = NotificationTargetType.OpenToGameSubmission,
                    TargetId = submission.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Approval declined.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptGameRequest(Guid gameRequestId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var gameRequest = await _context.GameRequests
                .Include(x => x.FromTeam)
                .Include(x => x.ToTeam)
                .FirstOrDefaultAsync(x => x.Id == gameRequestId);

            if (gameRequest is null)
            {
                TempData["Error"] = "Game request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (gameRequest.Status != GameRequestStatus.Pending)
            {
                TempData["Error"] = "This game request is no longer pending.";
                return RedirectToAction(nameof(Index));
            }

            var isOwnerOfTargetTeam = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == gameRequest.ToTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (!isOwnerOfTargetTeam)
                return Forbid();

            var existingMatch = await _context.Matches
                .AnyAsync(x => x.CreatedFromRequestId == gameRequest.Id);

            if (existingMatch)
            {
                TempData["Error"] = "A match has already been created from this request.";
                return RedirectToAction(nameof(Index));
            }

            var defaultStadium = await _context.Stadiums
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var match = new Match
            {
                Id = Guid.NewGuid(),
                HomeTeamId = gameRequest.FromTeamId,
                AwayTeamId = gameRequest.ToTeamId,
                StartAtUtc = gameRequest.StartAtUtc,
                DurationMinutes = gameRequest.DurationMinutes,
                Format = gameRequest.Format,
                Status = MatchStatus.Scheduled,
                CreatedFromRequestId = gameRequest.Id,
                VenueStatus = defaultStadium is not null
                    ? VenueStatus.Confirmed
                    : VenueStatus.Unset,
                ConfirmedVenueKind = defaultStadium is not null ? VenueKind.Stadium : VenueKind.Custom,
                ConfirmedStadiumId = defaultStadium?.Id,
                ConfirmedCustomVenueName = defaultStadium is null ? "Venue to be confirmed" : null,
                ConfirmedCustomFormattedAddress = defaultStadium is null ? "To be determined" : null
            };

            _context.Matches.Add(match);

            gameRequest.Status = GameRequestStatus.Accepted;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var relatedRequestNotifications = await _context.Notifications
                .Where(x =>
                    x.TargetType == NotificationTargetType.GameRequest &&
                    x.TargetId == gameRequest.Id &&
                    !x.IsRead)
                .ToListAsync();

            foreach (var item in relatedRequestNotifications)
            {
                item.IsRead = true;
                item.ReadAtUtc = DateTime.UtcNow;
            }

            await CloseTeamOpenToGameAsync(gameRequest.FromTeamId);
            await CloseTeamOpenToGameAsync(gameRequest.ToTeamId);

            var senderOwner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == gameRequest.FromTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (senderOwner is not null)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = senderOwner.PlayerId,
                    Type = NotificationType.GameRequestAccepted,
                    Title = "Game request accepted",
                    Message = $"Your game request against '{gameRequest.ToTeam!.Name}' has been accepted.",
                    TargetType = NotificationTargetType.GameRequest,
                    TargetId = gameRequest.Id,
                    IsRead = false
                });
            }

            var activePlayerIds = await _context.TeamMembers
                .Where(x =>
                    x.IsActive &&
                    (x.TeamId == gameRequest.FromTeamId || x.TeamId == gameRequest.ToTeamId))
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            foreach (var playerId in activePlayerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = playerId,
                    Type = NotificationType.MatchScheduled,
                    Title = "Match scheduled",
                    Message = $"{gameRequest.FromTeam!.Name} vs {gameRequest.ToTeam!.Name} has been scheduled for {gameRequest.StartAtUtc:dd MMM yyyy HH:mm}.",
                    TargetType = NotificationTargetType.Match,
                    TargetId = match.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Game request accepted and match created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineGameRequest(Guid gameRequestId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var gameRequest = await _context.GameRequests
                .Include(x => x.FromTeam)
                .Include(x => x.ToTeam)
                .FirstOrDefaultAsync(x => x.Id == gameRequestId);

            if (gameRequest is null)
            {
                TempData["Error"] = "Game request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (gameRequest.Status != GameRequestStatus.Pending)
            {
                TempData["Error"] = "This game request is no longer pending.";
                return RedirectToAction(nameof(Index));
            }

            var isOwnerOfTargetTeam = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == gameRequest.ToTeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (!isOwnerOfTargetTeam)
                return Forbid();

            gameRequest.Status = GameRequestStatus.Declined;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var senderOwner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == gameRequest.FromTeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (senderOwner is not null)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = senderOwner.PlayerId,
                    Type = NotificationType.GameRequestDeclined,
                    Title = "Game request declined",
                    Message = $"Your game request against '{gameRequest.ToTeam!.Name}' has been declined.",
                    TargetType = NotificationTargetType.GameRequest,
                    TargetId = gameRequest.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Game request declined.";
            return RedirectToAction(nameof(Index));
        }

        private async Task CloseTeamOpenToGameAsync(Guid teamId)
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(x => x.Id == teamId);

            if (team is null)
                return;

            team.IsOpenToGame = false;
            team.ActiveOpenToGameSubmissionId = null;
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

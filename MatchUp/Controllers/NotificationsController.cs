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
                .Include(x => x.Team)
                .Include(x => x.MemberApprovals)
                .ToDictionaryAsync(x => x.Id);

            var now = DateTime.UtcNow;

            var vm = new NotificationsIndexVm
            {
                UnreadCount = notifications.Count(x => !x.IsRead),
                Notifications = notifications.Select(n =>
                {
                    invites.TryGetValue(n.TargetId ?? Guid.Empty, out var invite);

                    var isInviteNotification =
                        n.TargetType == NotificationTargetType.TeamInvite &&
                        invite is not null;

                    var canAcceptOrDecline =
                        isInviteNotification &&
                        invite!.InvitedPlayerId == currentPlayerId.Value &&
                        invite.Status == InviteStatus.Pending &&
                        invite.ExpiresAtUtc > now;

                    submissions.TryGetValue(n.TargetId ?? Guid.Empty, out var submission);

                    var approvalRow = submission?.MemberApprovals
                        .FirstOrDefault(x => x.PlayerId == currentPlayerId.Value);

                    return new NotificationListItemVm
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Message = n.Message,
                        TypeText = n.Type.ToString(),
                        IsRead = n.IsRead,
                        CreatedAtUtc = n.CreatedAtUtc,

                        CanAccept = canAcceptOrDecline,
                        CanDecline = canAcceptOrDecline,

                        TeamInviteId = invite?.Id,
                        TeamInviteStatus = invite?.Status.ToString(),
                        ProposedSquadNumber = invite?.ProposedSquadNumber,
                        InviteExpiresAtUtc = invite?.ExpiresAtUtc,

                        OpenToGameSubmissionId = submission?.Id,
                        OpenToGameSubmissionStatus = submission?.Status.ToString(),

                        CanApproveOpenToGame =
                            n.Type == NotificationType.OpenToGameApprovalRequired &&
                            submission is not null &&
                            submission.Status == OpenToGameSubmissionStatus.PendingApprovals &&
                            approvalRow is not null &&
                            approvalRow.Status == ApprovalStatus.Pending,

                        CanDeclineOpenToGame =
                            n.Type == NotificationType.OpenToGameApprovalRequired &&
                            submission is not null &&
                            submission.Status == OpenToGameSubmissionStatus.PendingApprovals &&
                            approvalRow is not null &&
                            approvalRow.Status == ApprovalStatus.Pending,

                        TeamName = invite?.Team?.Name ?? submission?.Team?.Name,
                        IsExpired = invite is not null && invite.ExpiresAtUtc <= now
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
                .Include(x => x.Player)
                .FirstOrDefaultAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (teamOwner is not null && teamOwner.PlayerId != currentPlayerId.Value)
            {
                var acceptedNotification = new Notification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = teamOwner.PlayerId,
                    Type = NotificationType.TeamInviteAccepted,
                    Title = "Team invite accepted",
                    Message = $"Your invitation for team '{invite.Team!.Name}' has been accepted.",
                    TargetType = NotificationTargetType.TeamInvite,
                    TargetId = invite.Id,
                    IsRead = false
                };

                _context.Notifications.Add(acceptedNotification);
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
                .Include(x => x.Player)
                .FirstOrDefaultAsync(x =>
                    x.TeamId == invite.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (teamOwner is not null && teamOwner.PlayerId != currentPlayerId.Value)
            {
                var declinedNotification = new Notification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = teamOwner.PlayerId,
                    Type = NotificationType.TeamInviteDeclined,
                    Title = "Team invite declined",
                    Message = $"Your invitation for team '{invite.Team!.Name}' has been declined.",
                    TargetType = NotificationTargetType.TeamInvite,
                    TargetId = invite.Id,
                    IsRead = false
                };

                _context.Notifications.Add(declinedNotification);
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
                if (submission.Team!.ActiveOpenToGameSubmissionId.HasValue &&
                    submission.Team.ActiveOpenToGameSubmissionId.Value != submission.Id)
                {
                    var oldActiveSubmission = await _context.OpenToGameSubmissions
                        .FirstOrDefaultAsync(x => x.Id == submission.Team.ActiveOpenToGameSubmissionId.Value);

                    if (oldActiveSubmission is not null)
                    {
                        oldActiveSubmission.Status = OpenToGameSubmissionStatus.Superseded;
                        oldActiveSubmission.ResolvedAtUtc = DateTime.UtcNow;
                    }
                }

                submission.Status = OpenToGameSubmissionStatus.Active;
                submission.ResolvedAtUtc = DateTime.UtcNow;

                submission.Team.IsOpenToGame = true;
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

            if (submission.Team!.ActiveOpenToGameSubmissionId == submission.Id)
            {
                submission.Team.IsOpenToGame = false;
                submission.Team.ActiveOpenToGameSubmissionId = null;
            }

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
                    Message = $"A team member declined the Open To Game request for '{submission.Team!.Name}'.",
                    TargetType = NotificationTargetType.OpenToGameSubmission,
                    TargetId = submission.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Approval declined.";
            return RedirectToAction(nameof(Index));
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

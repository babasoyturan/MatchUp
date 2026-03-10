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

            var configIds = notifications
                .Where(x => x.TargetType == NotificationTargetType.OpenToGameConfig && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var configs = await _context.OpenToGameConfigs
                .AsNoTracking()
                .Where(x => configIds.Contains(x.Id))
                .Include(x => x.Team)
                .ToDictionaryAsync(x => x.Id);

            var respondedConfigIds = await _context.OpenToGameMemberApprovals
                .AsNoTracking()
                .Where(x => configIds.Contains(x.OpenToGameConfigId) && x.PlayerId == currentPlayerId.Value)
                .Select(x => x.OpenToGameConfigId)
                .Distinct()
                .ToListAsync();

            var activeNonOwnerTeamIds = await _context.TeamMembers
                .AsNoTracking()
                .Where(x =>
                    x.PlayerId == currentPlayerId.Value &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner)
                .Select(x => x.TeamId)
                .Distinct()
                .ToListAsync();

            var respondedConfigIdSet = respondedConfigIds.ToHashSet();
            var activeNonOwnerTeamIdSet = activeNonOwnerTeamIds.ToHashSet();

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

                    configs.TryGetValue(n.TargetId ?? Guid.Empty, out var config);

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

                        OpenToGameConfigId = config?.Id,
                        OpenToGameConfigStatus = config?.Status.ToString(),

                        CanApproveOpenToGame =
                            n.Type == NotificationType.OpenToGameApprovalRequired &&
                            config is not null &&
                            config.Status == OpenToGameConfigStatus.PendingApprovals &&
                            activeNonOwnerTeamIdSet.Contains(config.TeamId) &&
                            !respondedConfigIdSet.Contains(config.Id),

                        CanDeclineOpenToGame =
                            n.Type == NotificationType.OpenToGameApprovalRequired &&
                            config is not null &&
                            config.Status == OpenToGameConfigStatus.PendingApprovals &&
                            activeNonOwnerTeamIdSet.Contains(config.TeamId) &&
                            !respondedConfigIdSet.Contains(config.Id),

                        TeamName = invite?.Team?.Name ?? config?.Team?.Name,

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
        public async Task<IActionResult> ApproveOpenToGame(Guid configId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var config = await _context.OpenToGameConfigs
                .Include(x => x.Team)
                .Include(x => x.MemberApprovals)
                .FirstOrDefaultAsync(x => x.Id == configId);

            if (config is null)
            {
                TempData["Error"] = "Open To Game configuration not found.";
                return RedirectToAction(nameof(Index));
            }

            if (config.Status != OpenToGameConfigStatus.PendingApprovals)
            {
                TempData["Error"] = "This configuration is no longer waiting for approval.";
                return RedirectToAction(nameof(Index));
            }

            var isActiveNonOwnerMember = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == config.TeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner);

            if (!isActiveNonOwnerMember)
                return Forbid();

            var alreadyResponded = await _context.OpenToGameMemberApprovals
                .AnyAsync(x =>
                    x.OpenToGameConfigId == configId &&
                    x.PlayerId == currentPlayerId.Value);

            if (alreadyResponded)
            {
                TempData["Error"] = "You have already responded to this approval request.";
                return RedirectToAction(nameof(Index));
            }

            var approval = new OpenToGameMemberApproval
            {
                OpenToGameConfigId = configId,
                PlayerId = currentPlayerId.Value,
                IsApproved = true,
                RespondedAtUtc = DateTime.UtcNow
            };

            _context.OpenToGameMemberApprovals.Add(approval);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var owner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == config.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (owner is not null && owner.PlayerId != currentPlayerId.Value)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = owner.PlayerId,
                    Type = NotificationType.OpenToGameApprovalAccepted,
                    Title = "Open To Game approval accepted",
                    Message = $"A team member accepted the Open To Game request for '{config.Team!.Name}'.",
                    TargetType = NotificationTargetType.OpenToGameConfig,
                    TargetId = config.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            var requiredApproverIds = await _context.TeamMembers
                .Where(x =>
                    x.TeamId == config.TeamId &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner)
                .Select(x => x.PlayerId)
                .ToListAsync();

            var approvedIds = await _context.OpenToGameMemberApprovals
                .Where(x =>
                    x.OpenToGameConfigId == configId &&
                    x.IsApproved)
                .Select(x => x.PlayerId)
                .ToListAsync();

            if (requiredApproverIds.All(x => approvedIds.Contains(x)))
            {
                config.Status = OpenToGameConfigStatus.Active;
                config.ActivatedAtUtc = DateTime.UtcNow;
                config.Team!.IsOpenToGame = true;

                var activePlayerIds = await _context.TeamMembers
                    .Where(x => x.TeamId == config.TeamId && x.IsActive)
                    .Select(x => x.PlayerId)
                    .ToListAsync();

                foreach (var playerId in activePlayerIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        PlayerId = playerId,
                        Type = NotificationType.OpenToGameActivated,
                        Title = "Open To Game activated",
                        Message = $"{config.Team.Name} is now open to game.",
                        TargetType = NotificationTargetType.OpenToGameConfig,
                        TargetId = config.Id,
                        IsRead = false
                    });
                }

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Approval accepted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineOpenToGame(Guid configId, Guid notificationId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            if (!currentPlayerId.HasValue)
                return Unauthorized();

            var config = await _context.OpenToGameConfigs
                .Include(x => x.Team)
                .FirstOrDefaultAsync(x => x.Id == configId);

            if (config is null)
            {
                TempData["Error"] = "Open To Game configuration not found.";
                return RedirectToAction(nameof(Index));
            }

            if (config.Status != OpenToGameConfigStatus.PendingApprovals)
            {
                TempData["Error"] = "This configuration is no longer waiting for approval.";
                return RedirectToAction(nameof(Index));
            }

            var isActiveNonOwnerMember = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == config.TeamId &&
                    x.PlayerId == currentPlayerId.Value &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner);

            if (!isActiveNonOwnerMember)
                return Forbid();

            var alreadyResponded = await _context.OpenToGameMemberApprovals
                .AnyAsync(x =>
                    x.OpenToGameConfigId == configId &&
                    x.PlayerId == currentPlayerId.Value);

            if (alreadyResponded)
            {
                TempData["Error"] = "You have already responded to this approval request.";
                return RedirectToAction(nameof(Index));
            }

            var approval = new OpenToGameMemberApproval
            {
                OpenToGameConfigId = configId,
                PlayerId = currentPlayerId.Value,
                IsApproved = false,
                RespondedAtUtc = DateTime.UtcNow
            };

            _context.OpenToGameMemberApprovals.Add(approval);

            config.Status = OpenToGameConfigStatus.Cancelled;
            config.ActivatedAtUtc = null;
            config.Team!.IsOpenToGame = false;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.PlayerId == currentPlayerId.Value);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            var otherPendingNotifications = await _context.Notifications
                .Where(x =>
                    x.Type == NotificationType.OpenToGameApprovalRequired &&
                    x.TargetType == NotificationTargetType.OpenToGameConfig &&
                    x.TargetId == config.Id &&
                    !x.IsRead)
                .ToListAsync();

            foreach (var item in otherPendingNotifications)
            {
                item.IsRead = true;
                item.ReadAtUtc = DateTime.UtcNow;
            }

            var owner = await _context.TeamMembers
                .FirstOrDefaultAsync(x =>
                    x.TeamId == config.TeamId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (owner is not null && owner.PlayerId != currentPlayerId.Value)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = owner.PlayerId,
                    Type = NotificationType.OpenToGameApprovalDeclined,
                    Title = "Open To Game approval declined",
                    Message = $"A team member declined the Open To Game request for '{config.Team.Name}'.",
                    TargetType = NotificationTargetType.OpenToGameConfig,
                    TargetId = config.Id,
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

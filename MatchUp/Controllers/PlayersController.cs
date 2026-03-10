using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Services.Abstracts;
using MatchUp.Utilities.Constants;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MatchUp.Controllers
{
    [Authorize]
    public class PlayersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Player> _userManager;
        private readonly ICloudinaryService _cloudinaryService;

        public PlayersController(
            AppDbContext context,
            UserManager<Player> userManager,
            ICloudinaryService cloudinaryService)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] PlayersIndexFilterVm filter)
        {
            NormalizeFilter(filter);

            var vm = await BuildPlayersIndexVmAsync(filter);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ListPartial([FromQuery] PlayersIndexFilterVm filter)
        {
            NormalizeFilter(filter);

            var vm = await BuildPlayersIndexVmAsync(filter);
            return PartialView("_PlayersListPartial", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var player = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (player is null)
                return NotFound();

            var memberships = await _context.TeamMembers
                .AsNoTracking()
                .Where(x => x.PlayerId == id)
                .Include(x => x.Team)
                .ToListAsync();

            var currentUserId = GetCurrentUserId();
            var isOwnProfile = currentUserId.HasValue && currentUserId.Value == player.Id;

            var activeTeams = memberships
                .Where(x => x.IsActive)
                .ToList();

            var ownerTeamsCount = activeTeams.Count(x => x.Role == TeamRole.Owner);

            var inviteState = await BuildInviteStateAsync(player.Id, currentUserId);

            var vm = new PlayerDetailsVm
            {
                Id = player.Id,
                FullName = $"{player.FirstName} {player.LastName}",
                ImageUrl = player.ImageUrl,
                Biography = player.Biography,
                Nationality = player.Nationality,
                Height = player.Height,
                Weight = player.Weight,
                BirthDate = player.BirthDate,
                Age = CalculateAge(player.BirthDate),
                PrimaryPosition = GetPrimaryPosition(player.PlayablePositions),
                PlayablePositions = player.PlayablePositions?
                    .Select(x => x.ToString())
                    .ToList() ?? new List<string>(),
                IsFreeAgent = !activeTeams.Any(),
                ActiveTeamsCount = activeTeams.Count,
                OwnerTeamsCount = ownerTeamsCount,
                IsOwnProfile = isOwnProfile,
                Email = isOwnProfile ? player.Email : null,
                Teams = memberships
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.JoinedAtUtc)
                    .Select(x => new PlayerDetailsTeamItemVm
                    {
                        TeamId = x.TeamId,
                        TeamName = x.Team != null ? x.Team.Name : "Unknown Team",
                        TeamLogoUrl = x.Team != null ? x.Team.LogoUrl : null,
                        Role = x.Role.ToString(),
                        SquadNumber = x.SquadNumber,
                        IsActive = x.IsActive,
                        JoinedAtUtc = x.JoinedAtUtc
                    })
                    .ToList(),
                InviteState = inviteState,
                InviteRequest = new SendTeamInviteVm
                {
                    PlayerId = player.Id
                }
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Me()
        {
            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
                return Unauthorized();

            return RedirectToAction(nameof(Details), new { id = currentUserId.Value });
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
                return Unauthorized();

            var player = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentUserId.Value);

            if (player is null)
                return NotFound();

            var vm = new EditPlayerVm
            {
                FirstName = player.FirstName,
                LastName = player.LastName,
                Biography = player.Biography,
                Nationality = player.Nationality,
                Height = player.Height,
                Weight = player.Weight,
                BirthDate = player.BirthDate,
                CurrentImageUrl = player.ImageUrl,
                PlayablePositions = player.PlayablePositions?.ToList() ?? new List<PlayerPosition>(),
                NationalityOptions = GetNationalityOptions(),
                PositionOptions = GetPositionOptions()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditPlayerVm vm)
        {
            vm.NationalityOptions = GetNationalityOptions();
            vm.PositionOptions = GetPositionOptions();

            if (!ModelState.IsValid)
                return View(vm);

            if (vm.PlayablePositions is null || vm.PlayablePositions.Count == 0)
            {
                ModelState.AddModelError(nameof(vm.PlayablePositions), "At least one playable position must be selected.");
                return View(vm);
            }

            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
                return Unauthorized();

            var player = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == currentUserId.Value);

            if (player is null)
                return NotFound();

            if (vm.ImageFile is not null && vm.ImageFile.Length > 0)
            {
                var uploadResult = await _cloudinaryService.UploadImageAsync(vm.ImageFile);

                if (uploadResult is null || string.IsNullOrWhiteSpace(uploadResult.Url))
                {
                    ModelState.AddModelError(nameof(vm.ImageFile), "Image upload failed.");
                    return View(vm);
                }

                player.ImageUrl = uploadResult.Url;
            }

            player.FirstName = vm.FirstName.Trim();
            player.LastName = vm.LastName.Trim();
            player.Biography = vm.Biography.Trim();
            player.Nationality = vm.Nationality;
            player.Height = vm.Height;
            player.Weight = vm.Weight;
            player.BirthDate = vm.BirthDate!.Value;
            player.PlayablePositions = vm.PlayablePositions;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Details), new { id = player.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteToTeam(SendTeamInviteVm vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid invite request.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
                return Unauthorized();

            if (currentUserId.Value == vm.PlayerId)
            {
                TempData["Error"] = "You cannot invite yourself.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var ownedTeam = await _context.Teams
                .FirstOrDefaultAsync(t => t.Members.Any(m =>
                    m.PlayerId == currentUserId.Value &&
                    m.Role == TeamRole.Owner &&
                    m.IsActive));

            if (ownedTeam is null)
            {
                TempData["Error"] = "You do not own a team.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var invitedPlayer = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == vm.PlayerId);

            if (invitedPlayer is null)
            {
                TempData["Error"] = "Player not found.";
                return RedirectToAction(nameof(Index));
            }

            var isAlreadyMember = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.PlayerId == vm.PlayerId &&
                    x.IsActive);

            if (isAlreadyMember)
            {
                TempData["Error"] = "This player is already in your team.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var hasPendingInvite = await _context.TeamInvites
                .AnyAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.InvitedPlayerId == vm.PlayerId &&
                    x.Status == InviteStatus.Pending);

            if (hasPendingInvite)
            {
                TempData["Error"] = "An invite is already pending for this player.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var squadNumberTakenByMember = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.IsActive &&
                    x.SquadNumber == vm.ProposedSquadNumber);

            if (squadNumberTakenByMember)
            {
                TempData["Error"] = "This squad number is already used by another active team member.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var squadNumberTakenByPendingInvite = await _context.TeamInvites
                .AnyAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.Status == InviteStatus.Pending &&
                    x.ProposedSquadNumber == vm.ProposedSquadNumber);

            if (squadNumberTakenByPendingInvite)
            {
                TempData["Error"] = "This squad number is already reserved by another pending invite.";
                return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
            }

            var invite = new TeamInvite
            {
                Id = Guid.NewGuid(),
                TeamId = ownedTeam.Id,
                InvitedPlayerId = vm.PlayerId,
                ProposedSquadNumber = vm.ProposedSquadNumber,
                Status = InviteStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            };

            _context.TeamInvites.Add(invite);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                PlayerId = vm.PlayerId,
                Type = NotificationType.TeamInviteReceived,
                Title = "Team invitation received",
                Message = $"{ownedTeam.Name} invited you to join the team with squad number {vm.ProposedSquadNumber}.",
                TargetType = NotificationTargetType.TeamInvite,
                TargetId = invite.Id,
                IsRead = false
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Invite sent successfully.";
            return RedirectToAction(nameof(Details), new { id = vm.PlayerId });
        }

        private Guid? GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return Guid.Parse(userId);
        }

        private static List<SelectListItem> GetNationalityOptions()
        {
            return NationalityOptions.All;
        }

        private static List<SelectListItem> GetPositionOptions()
        {
            return Enum.GetValues<PlayerPosition>()
                .Select(x => new SelectListItem
                {
                    Value = ((int)x).ToString(),
                    Text = x.ToString()
                })
                .ToList();
        }

        private async Task<PlayersIndexVm> BuildPlayersIndexVmAsync(PlayersIndexFilterVm filter)
        {
            var players = await _context.Users
                .AsNoTracking()
                .ToListAsync();

            var activeTeamPlayerIds = await _context.TeamMembers
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync();

            var activeTeamPlayerIdSet = activeTeamPlayerIds.ToHashSet();

            IEnumerable<Player> query = players;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();

                query = query.Where(x =>
                    ($"{x.FirstName} {x.LastName}")
                    .Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.Position.HasValue)
            {
                query = query.Where(x =>
                    x.PlayablePositions != null &&
                    x.PlayablePositions.Contains(filter.Position.Value));
            }

            if (filter.FreeAgentOnly)
            {
                query = query.Where(x => !activeTeamPlayerIdSet.Contains(x.Id));
            }

            query = filter.SortBy switch
            {
                PlayerSortBy.NameAsc => query
                    .OrderBy(x => x.FirstName)
                    .ThenBy(x => x.LastName),

                PlayerSortBy.NameDesc => query
                    .OrderByDescending(x => x.FirstName)
                    .ThenByDescending(x => x.LastName),

                _ => query.OrderByDescending(x => x.CreatedAtUtc)
            };

            var totalCount = query.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            var pagedPlayers = query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new PlayerListItemVm
                {
                    Id = x.Id,
                    FullName = $"{x.FirstName} {x.LastName}",
                    ImageUrl = x.ImageUrl,
                    Nationality = x.Nationality,
                    Age = CalculateAge(x.BirthDate),
                    PrimaryPosition = GetPrimaryPosition(x.PlayablePositions)
                })
                .ToList();

            return new PlayersIndexVm
            {
                Filter = filter,
                Players = pagedPlayers,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        private static void NormalizeFilter(PlayersIndexFilterVm filter)
        {
            if (filter.Page < 1)
                filter.Page = 1;

            if (filter.PageSize < 1)
                filter.PageSize = 12;
        }

        private static int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.UtcNow.Date;
            var age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
                age--;

            return age;
        }

        private static string GetPrimaryPosition(List<PlayerPosition>? positions)
        {
            if (positions is null || positions.Count == 0)
                return "Unknown";

            return positions[0].ToString();
        }

        private async Task<Team?> GetOwnedTeamAsync(Guid currentUserId)
        {
            return await _context.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Members.Any(m =>
                    m.PlayerId == currentUserId &&
                    m.Role == TeamRole.Owner &&
                    m.IsActive));
        }

        private async Task<PlayerInviteStateVm> BuildInviteStateAsync(Guid viewedPlayerId, Guid? currentUserId)
        {
            var state = new PlayerInviteStateVm();

            if (!currentUserId.HasValue)
            {
                state.Message = "You must be logged in.";
                return state;
            }

            if (currentUserId.Value == viewedPlayerId)
            {
                state.IsOwnProfile = true;
                state.Message = "You cannot invite yourself.";
                return state;
            }

            var ownedTeam = await _context.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Members.Any(m =>
                    m.PlayerId == currentUserId.Value &&
                    m.Role == TeamRole.Owner &&
                    m.IsActive));

            if (ownedTeam is null)
            {
                state.IsOwnerOfAnyTeam = false;
                state.Message = "You do not own a team.";
                return state;
            }

            state.IsOwnerOfAnyTeam = true;
            state.OwnerTeamId = ownedTeam.Id;
            state.OwnerTeamName = ownedTeam.Name;

            var isAlreadyMember = await _context.TeamMembers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.PlayerId == viewedPlayerId &&
                    x.IsActive);

            if (isAlreadyMember)
            {
                state.IsAlreadyMemberOfOwnedTeam = true;
                state.Message = "This player is already in your team.";
                return state;
            }

            var pendingInvite = await _context.TeamInvites
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.TeamId == ownedTeam.Id &&
                    x.InvitedPlayerId == viewedPlayerId &&
                    x.Status == InviteStatus.Pending);

            if (pendingInvite is not null)
            {
                state.HasPendingInvite = true;
                state.PendingProposedSquadNumber = pendingInvite.ProposedSquadNumber;
                state.Message = $"Invite already sent. Proposed squad number: {pendingInvite.ProposedSquadNumber}";
                return state;
            }

            state.CanInvite = true;
            return state;
        }
    }
}

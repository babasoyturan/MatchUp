using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Services.Abstracts;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchUp.Controllers
{
    [Authorize]
    public class TeamsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Player> _userManager;
        private readonly ICloudinaryService _cloudinaryService;

        public TeamsController(
            AppDbContext context,
            UserManager<Player> userManager,
            ICloudinaryService cloudinaryService)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] TeamsIndexFilterVm filter)
        {
            NormalizeFilter(filter);

            var vm = await BuildTeamsIndexVmAsync(filter);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ListPartial([FromQuery] TeamsIndexFilterVm filter)
        {
            NormalizeFilter(filter);

            var vm = await BuildTeamsIndexVmAsync(filter);
            return PartialView("_TeamsListPartial", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var team = await _context.Teams
                .AsNoTracking()
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.Areas)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.TimeWindows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (team is null)
                return NotFound();

            var memberships = await _context.TeamMembers
                .AsNoTracking()
                .Where(x => x.TeamId == id)
                .Include(x => x.Player)
                .ToListAsync();

            var activeMemberships = memberships
                .Where(x => x.IsActive)
                .ToList();

            var ownerMembership = activeMemberships
                .FirstOrDefault(x => x.Role == TeamRole.Owner);

            var squad = activeMemberships
                .OrderBy(x => GetTeamRoleOrder(x.Role))
                .ThenBy(x => x.SquadNumber)
                .Select(x => new TeamDetailsSquadItemVm
                {
                    PlayerId = x.PlayerId,
                    FullName = x.Player != null
                        ? $"{x.Player.FirstName} {x.Player.LastName}"
                        : "Unknown Player",
                    ImageUrl = x.Player?.ImageUrl ?? string.Empty,
                    Nationality = x.Player?.Nationality ?? "Unknown",
                    Age = x.Player != null ? CalculateAge(x.Player.BirthDate) : 0,
                    SquadNumber = x.SquadNumber,
                    Role = x.Role.ToString(),
                    PrimaryPosition = x.Player != null
                        ? GetPrimaryPosition(x.Player.PlayablePositions)
                        : "Unknown",
                    PlayablePositions = x.Player?.PlayablePositions?
                        .Select(p => p.ToString())
                        .ToList() ?? new List<string>()
                })
                .ToList();

            var allMatches = await _context.Matches
                .AsNoTracking()
                .Where(x => x.HomeTeamId == id || x.AwayTeamId == id)
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.ConfirmedStadium)
                .ToListAsync();

            var fixtures = allMatches
                .Where(x => x.Status == MatchStatus.Scheduled)
                .OrderBy(x => x.StartAtUtc)
                .Select(x => MapTeamMatchItem(x, id))
                .ToList();

            var results = allMatches
                .Where(x =>
                    x.Status == MatchStatus.Completed &&
                    x.HomeTeamScore.HasValue &&
                    x.AwayTeamScore.HasValue)
                .OrderByDescending(x => x.StartAtUtc)
                .Select(x => MapTeamMatchItem(x, id))
                .ToList();

            var isOwnerView = activeMemberships.Any(x =>
                x.PlayerId == currentPlayerId &&
                x.Role == TeamRole.Owner);

            var isMemberView = activeMemberships.Any(x =>
                x.PlayerId == currentPlayerId);

            var vm = new TeamDetailsVm
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                LogoUrl = team.LogoUrl,
                ImageUrl = team.ImageUrl,
                OwnerFullName = ownerMembership?.Player != null
                    ? $"{ownerMembership.Player.FirstName} {ownerMembership.Player.LastName}"
                    : "Unknown Owner",
                OwnerPlayerId = ownerMembership?.PlayerId,
                MemberCount = memberships.Count,
                ActiveMemberCount = activeMemberships.Count,
                CreatedAtUtc = team.CreatedAtUtc,
                IsOpenToGame = team.IsOpenToGame,
                IsOwnerView = isOwnerView,
                IsMemberView = isMemberView,
                OpenToGame = BuildOpenToGameVm(team),
                Stats = BuildTeamStats(allMatches, id),
                Squad = squad,
                Fixtures = fixtures,
                Results = results,
                UpcomingMatchesPreview = fixtures.Take(3).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Mine([FromQuery] MyTeamsFilterBy filterBy = MyTeamsFilterBy.All)
        {
            var vm = await BuildMineTeamsVmAsync(filterBy);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> MinePartial([FromQuery] MyTeamsFilterBy filterBy = MyTeamsFilterBy.All)
        {
            var vm = await BuildMineTeamsVmAsync(filterBy);
            return PartialView("_MineTeamsListPartial", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ManageOpenToGame(Guid id)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var team = await _context.Teams
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.Areas)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.TimeWindows)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.MemberApprovals)
                        .ThenInclude(x => x.Player)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (team is null)
                return NotFound();

            var isOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == id &&
                    x.PlayerId == currentPlayerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (!isOwner)
                return Forbid();

            var activeNonOwnerMembers = await _context.TeamMembers
                .Include(x => x.Player)
                .Where(x =>
                    x.TeamId == id &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner)
                .ToListAsync();

            var vm = new ManageOpenToGameVm
            {
                TeamId = team.Id,
                TeamName = team.Name,
                IsCurrentlyOpenToGame = team.IsOpenToGame,
                StatusText = team.OpenToGameConfig?.Status.ToString() ?? "NotConfigured",
                SelectedFormats = team.OpenToGameConfig?.Formats?.ToList() ?? new List<GameFormat>(),
                CityName = team.OpenToGameConfig?.Areas?.FirstOrDefault()?.CityName ?? string.Empty,
                RegionName = team.OpenToGameConfig?.Areas?.FirstOrDefault()?.RegionName,
                DayWindows = CreateDefaultDayWindows(),
                ApprovalStatuses = BuildApprovalStatusList(activeNonOwnerMembers, team.OpenToGameConfig)
            };

            if (team.OpenToGameConfig?.TimeWindows is not null)
            {
                foreach (var existingWindow in team.OpenToGameConfig.TimeWindows)
                {
                    var vmWindow = vm.DayWindows.FirstOrDefault(x => x.Day == existingWindow.Day);

                    if (vmWindow is null)
                        continue;

                    vmWindow.IsActive = existingWindow.IsActive;
                    vmWindow.StartTime = MinuteToTimeText(existingWindow.StartMinute);
                    vmWindow.EndTime = MinuteToTimeText(existingWindow.EndMinute);
                }
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageOpenToGame(ManageOpenToGameVm vm)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var team = await _context.Teams
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.Areas)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.TimeWindows)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.MemberApprovals)
                .FirstOrDefaultAsync(x => x.Id == vm.TeamId);

            if (team is null)
                return NotFound();

            var isOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == vm.TeamId &&
                    x.PlayerId == currentPlayerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (!isOwner)
                return Forbid();

            if (vm.SelectedFormats is null || vm.SelectedFormats.Count == 0)
                ModelState.AddModelError(nameof(vm.SelectedFormats), "At least one format must be selected.");

            if (string.IsNullOrWhiteSpace(vm.CityName))
                ModelState.AddModelError(nameof(vm.CityName), "City is required.");

            var activeWindows = vm.DayWindows.Where(x => x.IsActive).ToList();

            if (activeWindows.Count == 0)
                ModelState.AddModelError(nameof(vm.DayWindows), "At least one active time window is required.");

            var parsedWindows = new List<OpenToGameTimeWindow>();

            foreach (var window in activeWindows)
            {
                if (!TryParseTimeToMinute(window.StartTime, out var startMinute))
                {
                    ModelState.AddModelError(nameof(vm.DayWindows), $"{window.Day}: invalid start time.");
                    continue;
                }

                if (!TryParseTimeToMinute(window.EndTime, out var endMinute))
                {
                    ModelState.AddModelError(nameof(vm.DayWindows), $"{window.Day}: invalid end time.");
                    continue;
                }

                if (startMinute >= endMinute)
                {
                    ModelState.AddModelError(nameof(vm.DayWindows), $"{window.Day}: start time must be less than end time.");
                    continue;
                }

                parsedWindows.Add(new OpenToGameTimeWindow
                {
                    Day = window.Day,
                    StartMinute = startMinute,
                    EndMinute = endMinute,
                    IsActive = true
                });
            }

            var activeNonOwnerMembers = await _context.TeamMembers
                .Include(x => x.Player)
                .Where(x =>
                    x.TeamId == vm.TeamId &&
                    x.IsActive &&
                    x.Role != TeamRole.Owner)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                vm.TeamName = team.Name;
                vm.IsCurrentlyOpenToGame = team.IsOpenToGame;
                vm.StatusText = team.OpenToGameConfig?.Status.ToString() ?? "NotConfigured";
                vm.ApprovalStatuses = BuildApprovalStatusList(activeNonOwnerMembers, team.OpenToGameConfig);
                return View(vm);
            }

            var config = team.OpenToGameConfig;

            if (config is null)
            {
                config = new OpenToGameConfig
                {
                    TeamId = team.Id
                };

                _context.OpenToGameConfigs.Add(config);
                team.OpenToGameConfig = config;
            }
            else
            {
                if (config.Areas.Any())
                    _context.OpenToGameAreas.RemoveRange(config.Areas);

                if (config.TimeWindows.Any())
                    _context.OpenToGameTimeWindows.RemoveRange(config.TimeWindows);

                if (config.MemberApprovals.Any())
                    _context.OpenToGameMemberApprovals.RemoveRange(config.MemberApprovals);
            }

            config.Formats = vm.SelectedFormats.Distinct().ToList();
            config.SubmittedAtUtc = DateTime.UtcNow;
            config.ActivatedAtUtc = null;

            config.Areas = new List<OpenToGameArea>
            {
                new OpenToGameArea
                {
                    CityName = vm.CityName.Trim(),
                    RegionName = string.IsNullOrWhiteSpace(vm.RegionName) ? null : vm.RegionName.Trim()
                }
            };

            config.TimeWindows = parsedWindows;

            var oldApprovalNotifications = await _context.Notifications
                .Where(x =>
                    activeNonOwnerMembers.Select(m => m.PlayerId).Contains(x.PlayerId) &&
                    x.Type == NotificationType.OpenToGameApprovalRequired &&
                    x.TargetType == NotificationTargetType.OpenToGameConfig &&
                    x.TargetId == config.Id &&
                    !x.IsRead)
                .ToListAsync();

            foreach (var oldNotification in oldApprovalNotifications)
            {
                oldNotification.IsRead = true;
                oldNotification.ReadAtUtc = DateTime.UtcNow;
            }

            if (activeNonOwnerMembers.Count == 0)
            {
                config.Status = OpenToGameConfigStatus.Active;
                config.ActivatedAtUtc = DateTime.UtcNow;
                team.IsOpenToGame = true;

                var activePlayerIds = await _context.TeamMembers
                    .Where(x => x.TeamId == team.Id && x.IsActive)
                    .Select(x => x.PlayerId)
                    .ToListAsync();

                foreach (var playerId in activePlayerIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        PlayerId = playerId,
                        Type = NotificationType.OpenToGameActivated,
                        Title = "Open To Game activated",
                        Message = $"{team.Name} is now open to game.",
                        TargetType = NotificationTargetType.OpenToGameConfig,
                        TargetId = config.Id,
                        IsRead = false
                    });
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Open To Game activated successfully.";
                return RedirectToAction(nameof(Details), new { id = team.Id });
            }

            config.Status = OpenToGameConfigStatus.PendingApprovals;
            team.IsOpenToGame = false;

            foreach (var approver in activeNonOwnerMembers)
            {
                _context.Notifications.Add(new Notification
                {
                    PlayerId = approver.PlayerId,
                    Type = NotificationType.OpenToGameApprovalRequired,
                    Title = "Open To Game approval required",
                    Message = $"{team.Name} submitted an Open To Game configuration and is waiting for your approval.",
                    TargetType = NotificationTargetType.OpenToGameConfig,
                    TargetId = config.Id,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Configuration submitted for member approvals.";
            return RedirectToAction(nameof(ManageOpenToGame), new { id = team.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableOpenToGame(Guid teamId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var team = await _context.Teams
                .Include(x => x.OpenToGameConfig)
                .FirstOrDefaultAsync(x => x.Id == teamId);

            if (team is null)
                return NotFound();

            var isOwner = await _context.TeamMembers
                .AnyAsync(x =>
                    x.TeamId == teamId &&
                    x.PlayerId == currentPlayerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (!isOwner)
                return Forbid();

            if (team.OpenToGameConfig is null)
            {
                TempData["Error"] = "Open To Game configuration not found.";
                return RedirectToAction(nameof(Details), new { id = teamId });
            }

            team.IsOpenToGame = false;
            team.OpenToGameConfig.Status = OpenToGameConfigStatus.Cancelled;
            team.OpenToGameConfig.ActivatedAtUtc = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Open To Game disabled.";
            return RedirectToAction(nameof(Details), new { id = teamId });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var playerId = GetCurrentPlayerId();

            var alreadyOwnsTeam = await _context.TeamMembers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PlayerId == playerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (alreadyOwnsTeam)
            {
                TempData["Error"] = "You already own a team.";
                return RedirectToAction(nameof(Index));
            }

            return View(new CreateTeamVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTeamVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var playerId = GetCurrentPlayerId();

            var alreadyOwnsTeam = await _context.TeamMembers
                .AnyAsync(x =>
                    x.PlayerId == playerId &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (alreadyOwnsTeam)
            {
                TempData["Error"] = "You already own a team.";
                return RedirectToAction(nameof(Index));
            }

            var duplicateNameExists = await _context.Teams
                .AnyAsync(x => !x.IsDeleted && x.Name == vm.Name);

            if (duplicateNameExists)
            {
                ModelState.AddModelError(nameof(vm.Name), "A team with this name already exists.");
                return View(vm);
            }

            if (vm.LogoFile is null || vm.LogoFile.Length == 0)
            {
                ModelState.AddModelError(nameof(vm.LogoFile), "Logo is required.");
                return View(vm);
            }

            if (vm.ImageFile is null || vm.ImageFile.Length == 0)
            {
                ModelState.AddModelError(nameof(vm.ImageFile), "Banner image is required.");
                return View(vm);
            }

            var logoUploadResult = await _cloudinaryService.UploadImageAsync(vm.LogoFile);
            if (logoUploadResult is null || string.IsNullOrWhiteSpace(logoUploadResult.Url))
            {
                ModelState.AddModelError(nameof(vm.LogoFile), "Logo upload failed.");
                return View(vm);
            }

            var imageUploadResult = await _cloudinaryService.UploadImageAsync(vm.ImageFile);
            if (imageUploadResult is null || string.IsNullOrWhiteSpace(imageUploadResult.Url))
            {
                ModelState.AddModelError(nameof(vm.ImageFile), "Banner image upload failed.");
                return View(vm);
            }

            var team = new Team
            {
                Id = Guid.NewGuid(),
                Name = vm.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                LogoUrl = logoUploadResult.Url,
                ImageUrl = imageUploadResult.Url,
                IsOpenToGame = false
            };

            var ownerMembership = new TeamMember
            {
                TeamId = team.Id,
                PlayerId = playerId,
                SquadNumber = vm.OwnerSquadNumber,
                Role = TeamRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            };

            _context.Teams.Add(team);
            _context.TeamMembers.Add(ownerMembership);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Team created successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<TeamsIndexVm> BuildTeamsIndexVmAsync(TeamsIndexFilterVm filter)
        {
            var playerId = GetCurrentPlayerId();

            var ownerOpenTeam = await GetOwnedActiveOpenToGameTeamAsync(playerId);
            var showCompatibleButton = ownerOpenTeam is not null;

            List<TeamProjection> teams;

            if (filter.CompatibleOnly && ownerOpenTeam is not null)
            {
                var blockedOpponentIds = await GetBlockedOpponentIdsAsync(ownerOpenTeam.Id);

                var candidateTeams = await _context.Teams
                    .AsNoTracking()
                    .Include(x => x.OpenToGameConfig)
                        .ThenInclude(x => x.Areas)
                    .Include(x => x.OpenToGameConfig)
                        .ThenInclude(x => x.TimeWindows)
                    .Where(x =>
                        x.IsOpenToGame &&
                        x.OpenToGameConfig != null &&
                        x.OpenToGameConfig.Status == OpenToGameConfigStatus.Active &&
                        x.Id != ownerOpenTeam.Id)
                    .ToListAsync();

                candidateTeams = candidateTeams
                    .Where(x =>
                        !blockedOpponentIds.Contains(x.Id) &&
                        IsCompatible(ownerOpenTeam, x))
                    .ToList();

                teams = candidateTeams
                    .Select(x => new TeamProjection
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ImageUrl = x.ImageUrl,
                        LogoUrl = x.LogoUrl,
                        CreatedAtUtc = x.CreatedAtUtc
                    })
                    .ToList();
            }
            else
            {
                var query = _context.Teams
                    .AsNoTracking()
                    .Select(x => new TeamProjection
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ImageUrl = x.ImageUrl,
                        LogoUrl = x.LogoUrl,
                        CreatedAtUtc = x.CreatedAtUtc
                    })
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var search = filter.Search.Trim();
                    query = query.Where(x => x.Name.Contains(search));
                }

                teams = await query.ToListAsync();
            }

            if (filter.CompatibleOnly && !string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                teams = teams
                    .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var teamIds = teams.Select(x => x.Id).ToList();

            var memberCounts = await GetTeamMemberCountsAsync(teamIds);
            var stats = await GetTeamStatsAsync(teamIds);

            var cards = teams.Select(x =>
            {
                memberCounts.TryGetValue(x.Id, out var memberCount);
                stats.TryGetValue(x.Id, out var stat);

                return new TeamListItemVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImageUrl = x.ImageUrl,
                    LogoUrl = x.LogoUrl,
                    MemberCount = memberCount,
                    WinRatio = stat?.WinRatio ?? 0
                };
            });

            cards = filter.SortBy switch
            {
                TeamSortBy.NameAsc => cards.OrderBy(x => x.Name),
                TeamSortBy.NameDesc => cards.OrderByDescending(x => x.Name),
                TeamSortBy.WinRatio => cards
                    .OrderByDescending(x => x.WinRatio)
                    .ThenBy(x => x.Name),
                _ => cards.OrderByDescending(x =>
                    teams.First(t => t.Id == x.Id).CreatedAtUtc)
            };

            var totalCount = cards.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            var pagedCards = cards
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return new TeamsIndexVm
            {
                Filter = filter,
                Teams = pagedCards,
                ShowCompatibleButton = showCompatibleButton,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        private async Task<MineTeamsVm> BuildMineTeamsVmAsync(MyTeamsFilterBy filterBy)
        {
            var playerId = GetCurrentPlayerId();

            var memberships = await _context.TeamMembers
                .AsNoTracking()
                .Where(x => x.PlayerId == playerId && x.IsActive)
                .Select(x => new
                {
                    x.TeamId,
                    x.Role
                })
                .ToListAsync();

            var ownedTeamIds = memberships
                .Where(x => x.Role == TeamRole.Owner)
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            var memberOnlyTeamIds = memberships
                .Where(x => x.Role != TeamRole.Owner)
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            var allTeamIds = memberships
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            List<Guid> targetTeamIds = filterBy switch
            {
                MyTeamsFilterBy.Owner => ownedTeamIds,
                MyTeamsFilterBy.Member => memberOnlyTeamIds,
                _ => allTeamIds
            };

            var teams = await _context.Teams
                .AsNoTracking()
                .Where(x => targetTeamIds.Contains(x.Id))
                .Select(x => new TeamProjection
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImageUrl = x.ImageUrl,
                    LogoUrl = x.LogoUrl,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();

            var memberCounts = await GetTeamMemberCountsAsync(targetTeamIds);
            var stats = await GetTeamStatsAsync(targetTeamIds);

            var cards = teams.Select(x =>
            {
                memberCounts.TryGetValue(x.Id, out var memberCount);
                stats.TryGetValue(x.Id, out var stat);

                return new
                {
                    Card = new TeamListItemVm
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ImageUrl = x.ImageUrl,
                        LogoUrl = x.LogoUrl,
                        MemberCount = memberCount,
                        WinRatio = stat?.WinRatio ?? 0
                    },
                    IsOwnerTeam = ownedTeamIds.Contains(x.Id)
                };
            });

            cards = filterBy switch
            {
                MyTeamsFilterBy.Owner => cards.OrderBy(x => x.Card.Name),
                MyTeamsFilterBy.Member => cards.OrderBy(x => x.Card.Name),
                _ => cards
                    .OrderByDescending(x => x.IsOwnerTeam)
                    .ThenBy(x => x.Card.Name)
            };

            return new MineTeamsVm
            {
                FilterBy = filterBy,
                HasOwnedTeam = ownedTeamIds.Any(),
                Teams = cards.Select(x => x.Card).ToList()
            };
        }

        private static void NormalizeFilter(TeamsIndexFilterVm filter)
        {
            if (filter.Page < 1)
                filter.Page = 1;

            if (filter.PageSize < 1)
                filter.PageSize = 12;
        }

        private async Task<Team?> GetOwnedActiveOpenToGameTeamAsync(Guid playerId)
        {
            return await _context.Teams
                .AsNoTracking()
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.Areas)
                .Include(x => x.OpenToGameConfig)
                    .ThenInclude(x => x.TimeWindows)
                .Include(x => x.Members)
                .FirstOrDefaultAsync(x =>
                    x.IsOpenToGame &&
                    x.OpenToGameConfig != null &&
                    x.OpenToGameConfig.Status == OpenToGameConfigStatus.Active &&
                    x.Members.Any(m =>
                        m.PlayerId == playerId &&
                        m.Role == TeamRole.Owner &&
                        m.IsActive));
        }

        private async Task<Dictionary<Guid, int>> GetTeamMemberCountsAsync(List<Guid> teamIds)
        {
            if (teamIds.Count == 0)
                return new Dictionary<Guid, int>();

            return await _context.TeamMembers
                .AsNoTracking()
                .Where(x => x.IsActive && teamIds.Contains(x.TeamId))
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.TeamId, x => x.Count);
        }

        private async Task<Dictionary<Guid, TeamStatsResult>> GetTeamStatsAsync(List<Guid> teamIds)
        {
            var result = teamIds
                .Distinct()
                .ToDictionary(x => x, _ => new TeamStatsResult());

            if (teamIds.Count == 0)
                return result;

            var matches = await _context.Matches
                .AsNoTracking()
                .Where(x =>
                    x.Status == MatchStatus.Completed &&
                    x.HomeTeamScore.HasValue &&
                    x.AwayTeamScore.HasValue &&
                    (teamIds.Contains(x.HomeTeamId) || teamIds.Contains(x.AwayTeamId)))
                .ToListAsync();

            foreach (var match in matches)
            {
                if (result.TryGetValue(match.HomeTeamId, out var homeStats))
                {
                    homeStats.CompletedMatches++;

                    if (match.HomeTeamScore > match.AwayTeamScore)
                        homeStats.Wins++;
                }

                if (result.TryGetValue(match.AwayTeamId, out var awayStats))
                {
                    awayStats.CompletedMatches++;

                    if (match.AwayTeamScore > match.HomeTeamScore)
                        awayStats.Wins++;
                }
            }

            foreach (var item in result.Values)
            {
                item.WinRatio = item.CompletedMatches == 0
                    ? 0
                    : Math.Round((decimal)item.Wins / item.CompletedMatches * 100, 0);
            }

            return result;
        }

        private async Task<HashSet<Guid>> GetBlockedOpponentIdsAsync(Guid ownerTeamId)
        {
            var pendingRequestOpponentIds = await _context.GameRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status == GameRequestStatus.Pending &&
                    (x.FromTeamId == ownerTeamId || x.ToTeamId == ownerTeamId))
                .Select(x => x.FromTeamId == ownerTeamId ? x.ToTeamId : x.FromTeamId)
                .ToListAsync();

            var scheduledMatchOpponentIds = await _context.Matches
                .AsNoTracking()
                .Where(x =>
                    x.Status == MatchStatus.Scheduled &&
                    (x.HomeTeamId == ownerTeamId || x.AwayTeamId == ownerTeamId))
                .Select(x => x.HomeTeamId == ownerTeamId ? x.AwayTeamId : x.HomeTeamId)
                .ToListAsync();

            return pendingRequestOpponentIds
                .Concat(scheduledMatchOpponentIds)
                .ToHashSet();
        }

        private static bool IsCompatible(Team ownerTeam, Team candidateTeam)
        {
            var ownerConfig = ownerTeam.OpenToGameConfig;
            var candidateConfig = candidateTeam.OpenToGameConfig;

            if (ownerConfig is null || candidateConfig is null)
                return false;

            var formatOverlap = ownerConfig.Formats.Intersect(candidateConfig.Formats).Any();

            var areaOverlap = ownerConfig.Areas.Any(ownerArea =>
                candidateConfig.Areas.Any(candidateArea =>
                    string.Equals(ownerArea.CityName, candidateArea.CityName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ownerArea.RegionName ?? string.Empty, candidateArea.RegionName ?? string.Empty, StringComparison.OrdinalIgnoreCase)));

            var timeOverlap = ownerConfig.TimeWindows
                .Where(x => x.IsActive)
                .Any(ownerWindow =>
                    candidateConfig.TimeWindows
                        .Where(x => x.IsActive)
                        .Any(candidateWindow =>
                            ownerWindow.Day == candidateWindow.Day &&
                            ownerWindow.StartMinute < candidateWindow.EndMinute &&
                            candidateWindow.StartMinute < ownerWindow.EndMinute));

            return formatOverlap && areaOverlap && timeOverlap;
        }

        private Guid GetCurrentPlayerId()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("Current user is not available.");

            return Guid.Parse(userId);
        }

        private static int GetTeamRoleOrder(TeamRole role)
        {
            return role switch
            {
                TeamRole.Owner => 1,
                TeamRole.Captain => 2,
                TeamRole.Member => 3,
                _ => 99
            };
        }

        private static TeamDetailsMatchItemVm MapTeamMatchItem(Match match, Guid teamId)
        {
            var isHome = match.HomeTeamId == teamId;

            var opponentTeam = isHome
                ? match.AwayTeam
                : match.HomeTeam;

            var teamScore = isHome
                ? match.HomeTeamScore
                : match.AwayTeamScore;

            var opponentScore = isHome
                ? match.AwayTeamScore
                : match.HomeTeamScore;

            return new TeamDetailsMatchItemVm
            {
                MatchId = match.Id,
                OpponentTeamId = opponentTeam?.Id ?? Guid.Empty,
                OpponentTeamName = opponentTeam?.Name ?? "Unknown Team",
                OpponentTeamLogoUrl = opponentTeam?.LogoUrl,
                IsHome = isHome,
                StartAtUtc = match.StartAtUtc,
                DurationMinutes = match.DurationMinutes,
                Format = match.Format.ToString(),
                Status = match.Status.ToString(),
                VenueText = GetVenueText(match),
                TeamScore = teamScore,
                OpponentScore = opponentScore
            };
        }

        private static string GetVenueText(Match match)
        {
            if (match.ConfirmedVenueKind == VenueKind.Stadium && match.ConfirmedStadium is not null)
                return match.ConfirmedStadium.Name;

            if (match.ConfirmedVenueKind == VenueKind.Custom)
            {
                if (!string.IsNullOrWhiteSpace(match.ConfirmedCustomVenueName))
                    return match.ConfirmedCustomVenueName;

                if (!string.IsNullOrWhiteSpace(match.ConfirmedCustomFormattedAddress))
                    return match.ConfirmedCustomFormattedAddress;
            }

            return "Venue not set";
        }

        private static TeamDetailsStatsVm BuildTeamStats(List<Match> allMatches, Guid teamId)
        {
            var completedMatches = allMatches
                .Where(x =>
                    x.Status == MatchStatus.Completed &&
                    x.HomeTeamScore.HasValue &&
                    x.AwayTeamScore.HasValue)
                .ToList();

            var scheduledMatches = allMatches
                .Where(x => x.Status == MatchStatus.Scheduled)
                .ToList();

            var played = 0;
            var wins = 0;
            var draws = 0;
            var losses = 0;
            var goalsScored = 0;
            var goalsConceded = 0;
            var cleanSheets = 0;

            foreach (var match in completedMatches)
            {
                var isHome = match.HomeTeamId == teamId;

                var teamScore = isHome
                    ? match.HomeTeamScore!.Value
                    : match.AwayTeamScore!.Value;

                var opponentScore = isHome
                    ? match.AwayTeamScore!.Value
                    : match.HomeTeamScore!.Value;

                played++;
                goalsScored += teamScore;
                goalsConceded += opponentScore;

                if (teamScore > opponentScore)
                    wins++;
                else if (teamScore == opponentScore)
                    draws++;
                else
                    losses++;

                if (opponentScore == 0)
                    cleanSheets++;
            }

            return new TeamDetailsStatsVm
            {
                MatchesPlayed = played,
                Wins = wins,
                Draws = draws,
                Losses = losses,
                GoalsScored = goalsScored,
                GoalsConceded = goalsConceded,
                GoalDifference = goalsScored - goalsConceded,
                CleanSheets = cleanSheets,
                WinRatio = played == 0
                    ? 0
                    : Math.Round((decimal)wins / played * 100, 0),
                ScheduledMatchesCount = scheduledMatches.Count,
                CompletedMatchesCount = completedMatches.Count,
                NextMatchAtUtc = scheduledMatches
                    .OrderBy(x => x.StartAtUtc)
                    .Select(x => (DateTime?)x.StartAtUtc)
                    .FirstOrDefault(),
                LastMatchAtUtc = completedMatches
                    .OrderByDescending(x => x.StartAtUtc)
                    .Select(x => (DateTime?)x.StartAtUtc)
                    .FirstOrDefault()
            };
        }

        private static TeamDetailsOpenToGameVm BuildOpenToGameVm(Team team)
        {
            var config = team.OpenToGameConfig;

            if (config is null)
            {
                return new TeamDetailsOpenToGameVm
                {
                    IsEnabled = false,
                    Status = "Not configured"
                };
            }

            return new TeamDetailsOpenToGameVm
            {
                IsEnabled = team.IsOpenToGame && config.Status == OpenToGameConfigStatus.Active,
                Status = config.Status.ToString(),
                Formats = config.Formats?
                    .Select(x => x.ToString())
                    .ToList() ?? new List<string>(),
                Areas = config.Areas?
                    .Select(x => string.IsNullOrWhiteSpace(x.RegionName)
                        ? x.CityName
                        : $"{x.CityName}, {x.RegionName}")
                    .ToList() ?? new List<string>(),
                TimeWindows = config.TimeWindows?
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Day)
                    .ThenBy(x => x.StartMinute)
                    .Select(x => $"{x.Day}: {MinuteToText(x.StartMinute)} - {MinuteToText(x.EndMinute)}")
                    .ToList() ?? new List<string>(),
                ActivatedAtUtc = config.ActivatedAtUtc
            };
        }

        private static string MinuteToText(int minute)
        {
            var hour = minute / 60;
            var min = minute % 60;

            return $"{hour:D2}:{min:D2}";
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

        private static List<OpenToGameDayWindowVm> CreateDefaultDayWindows()
        {
            return Enum.GetValues<WeekDay>()
                .Select(day => new OpenToGameDayWindowVm
                {
                    Day = day,
                    IsActive = false,
                    StartTime = "18:00",
                    EndTime = "20:00"
                })
                .ToList();
        }

        private static string MinuteToTimeText(int minute)
        {
            var hour = minute / 60;
            var min = minute % 60;
            return $"{hour:D2}:{min:D2}";
        }

        private static bool TryParseTimeToMinute(string? timeText, out int minute)
        {
            minute = 0;

            if (string.IsNullOrWhiteSpace(timeText))
                return false;

            if (!TimeSpan.TryParse(timeText, out var time))
                return false;

            minute = (int)time.TotalMinutes;
            return true;
        }

        private static List<OpenToGameApprovalMemberVm> BuildApprovalStatusList(
            List<TeamMember> activeNonOwnerMembers,
            OpenToGameConfig? config)
        {
            var result = new List<OpenToGameApprovalMemberVm>();

            var approvalRows = config?.MemberApprovals?.ToList() ?? new List<OpenToGameMemberApproval>();
            var respondedPlayerIds = approvalRows.Select(x => x.PlayerId).ToHashSet();

            foreach (var approval in approvalRows.OrderBy(x => x.Player!.FirstName).ThenBy(x => x.Player!.LastName))
            {
                result.Add(new OpenToGameApprovalMemberVm
                {
                    PlayerId = approval.PlayerId,
                    FullName = approval.Player is not null
                        ? $"{approval.Player.FirstName} {approval.Player.LastName}"
                        : "Unknown Player",
                    StatusText = approval.IsApproved ? "Accepted" : "Declined",
                    RespondedAtUtc = approval.RespondedAtUtc
                });
            }

            var pendingMembers = activeNonOwnerMembers
                .Where(x => !respondedPlayerIds.Contains(x.PlayerId))
                .OrderBy(x => x.Player!.FirstName)
                .ThenBy(x => x.Player!.LastName)
                .ToList();

            foreach (var member in pendingMembers)
            {
                result.Add(new OpenToGameApprovalMemberVm
                {
                    PlayerId = member.PlayerId,
                    FullName = member.Player is not null
                        ? $"{member.Player.FirstName} {member.Player.LastName}"
                        : "Unknown Player",
                    StatusText = "Pending",
                    RespondedAtUtc = null
                });
            }

            return result;
        }

        private sealed class TeamProjection
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = default!;
            public string? ImageUrl { get; set; }
            public string? LogoUrl { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        private sealed class TeamStatsResult
        {
            public int Wins { get; set; }
            public int CompletedMatches { get; set; }
            public decimal WinRatio { get; set; }
        }
    }
}

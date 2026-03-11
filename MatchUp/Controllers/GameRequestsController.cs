using MatchUp.Data;
using MatchUp.Models.Concretes;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.GameRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatchUp.Controllers
{
    [Authorize]
    public class GameRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Player> _userManager;

        public GameRequestsController(AppDbContext context, UserManager<Player> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Create(Guid opponentTeamId)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var fromTeam = await GetOwnedActiveOpenToGameTeamAsync(currentPlayerId);
            if (fromTeam is null)
            {
                TempData["Error"] = "You do not have an active Open To Game team.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            var opponentTeam = await _context.Teams
                .AsNoTracking()
                .Include(x => x.ActiveOpenToGameSubmission)
                    .ThenInclude(x => x.TimeWindows)
                .FirstOrDefaultAsync(x => x.Id == opponentTeamId);

            if (opponentTeam is null)
                return NotFound();

            if (fromTeam.Id == opponentTeam.Id)
            {
                TempData["Error"] = "You cannot send a game request to your own team.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            if (!opponentTeam.IsOpenToGame || opponentTeam.ActiveOpenToGameSubmissionId is null)
            {
                TempData["Error"] = "Opponent team is not open to game.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            var hasBlockedRelation = await HasBlockedRelationAsync(fromTeam.Id, opponentTeam.Id);
            if (hasBlockedRelation)
            {
                TempData["Error"] = "A pending request or scheduled match already exists between these teams.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            if (!IsCompatible(fromTeam, opponentTeam))
            {
                TempData["Error"] = "These teams are not compatible for game requests.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            var vm = new CreateGameRequestVm
            {
                OpponentTeamId = opponentTeam.Id,
                OpponentTeamName = opponentTeam.Name,
                FromTeamId = fromTeam.Id,
                FromTeamName = fromTeam.Name
            };

            PopulateCreateVm(vm, fromTeam, opponentTeam);

            if (!vm.FormatOptions.Any() || !vm.SlotOptions.Any())
            {
                TempData["Error"] = "No compatible slot was found for the next 7 days.";
                return RedirectToAction("Details", "Teams", new { id = opponentTeamId });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGameRequestVm vm)
        {
            var currentPlayerId = GetCurrentPlayerId();

            var fromTeam = await GetOwnedActiveOpenToGameTeamAsync(currentPlayerId);
            if (fromTeam is null)
                return Forbid();

            var opponentTeam = await _context.Teams
                .AsNoTracking()
                .Include(x => x.ActiveOpenToGameSubmission)
                    .ThenInclude(x => x.TimeWindows)
                .FirstOrDefaultAsync(x => x.Id == vm.OpponentTeamId);

            if (opponentTeam is null)
                return NotFound();

            vm.FromTeamId = fromTeam.Id;
            vm.FromTeamName = fromTeam.Name;
            vm.OpponentTeamName = opponentTeam.Name;

            PopulateCreateVm(vm, fromTeam, opponentTeam);

            if (!ModelState.IsValid)
                return View(vm);

            if (vm.SelectedFormat is null)
            {
                ModelState.AddModelError(nameof(vm.SelectedFormat), "Please select a format.");
                return View(vm);
            }

            var commonFormats = GetCommonFormats(fromTeam, opponentTeam);
            if (!commonFormats.Contains(vm.SelectedFormat.Value))
            {
                ModelState.AddModelError(nameof(vm.SelectedFormat), "Selected format is not valid.");
                return View(vm);
            }

            if (!DateTime.TryParse(vm.SelectedSlotStartAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var selectedStartAtUtc))
            {
                ModelState.AddModelError(nameof(vm.SelectedSlotStartAtUtc), "Selected slot is invalid.");
                return View(vm);
            }

            var availableSlots = GenerateAvailableSlots(fromTeam, opponentTeam, DateTime.UtcNow, 7, 60);
            var isValidSlot = availableSlots.Any(x => x == selectedStartAtUtc);

            if (!isValidSlot)
            {
                ModelState.AddModelError(nameof(vm.SelectedSlotStartAtUtc), "Selected slot is no longer available.");
                return View(vm);
            }

            var hasBlockedRelation = await HasBlockedRelationAsync(fromTeam.Id, opponentTeam.Id);
            if (hasBlockedRelation)
            {
                TempData["Error"] = "A pending request or scheduled match already exists between these teams.";
                return RedirectToAction("Details", "Teams", new { id = vm.OpponentTeamId });
            }

            var opponentOwner = await _context.TeamMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.TeamId == opponentTeam.Id &&
                    x.Role == TeamRole.Owner &&
                    x.IsActive);

            if (opponentOwner is null)
            {
                TempData["Error"] = "Opponent team owner was not found.";
                return RedirectToAction("Details", "Teams", new { id = vm.OpponentTeamId });
            }

            var request = new GameRequest
            {
                Id = Guid.NewGuid(),
                FromTeamId = fromTeam.Id,
                ToTeamId = opponentTeam.Id,
                RequestedByPlayerId = currentPlayerId,
                StartAtUtc = DateTime.SpecifyKind(selectedStartAtUtc, DateTimeKind.Utc),
                DurationMinutes = 60,
                Format = vm.SelectedFormat.Value,
                Message = string.IsNullOrWhiteSpace(vm.Message) ? null : vm.Message.Trim(),
                Status = GameRequestStatus.Pending,
                ExpiresAtUtc = DateTime.SpecifyKind(selectedStartAtUtc, DateTimeKind.Utc)
            };

            _context.GameRequests.Add(request);

            _context.Notifications.Add(new Notification
            {
                PlayerId = opponentOwner.PlayerId,
                Type = NotificationType.GameRequestReceived,
                Title = "New game request received",
                Message = $"{fromTeam.Name} sent a game request for {selectedStartAtUtc:dd MMM yyyy HH:mm}.",
                TargetType = NotificationTargetType.GameRequest,
                TargetId = request.Id,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Game request sent successfully.";
            return RedirectToAction("Details", "Teams", new { id = vm.OpponentTeamId });
        }

        private void PopulateCreateVm(CreateGameRequestVm vm, Team fromTeam, Team opponentTeam)
        {
            vm.FormatOptions = GetCommonFormats(fromTeam, opponentTeam)
                .Select(x => new SelectListItem
                {
                    Value = ((int)x).ToString(),
                    Text = x.ToString()
                })
                .ToList();

            vm.SlotOptions = GenerateAvailableSlots(fromTeam, opponentTeam, DateTime.UtcNow, 7, 60)
                .Select(x => new SelectListItem
                {
                    Value = x.ToString("O"),
                    Text = $"{x:ddd, dd MMM yyyy HH:mm} - {x.AddMinutes(60):HH:mm}"
                })
                .ToList();
        }

        private List<GameFormat> GetCommonFormats(Team fromTeam, Team opponentTeam)
        {
            var fromSubmission = fromTeam.ActiveOpenToGameSubmission;
            var opponentSubmission = opponentTeam.ActiveOpenToGameSubmission;

            if (fromSubmission is null || opponentSubmission is null)
                return new List<GameFormat>();

            return fromSubmission.Formats
                .Intersect(opponentSubmission.Formats)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private List<DateTime> GenerateAvailableSlots(
            Team fromTeam,
            Team opponentTeam,
            DateTime nowUtc,
            int daysAhead,
            int durationMinutes)
        {
            var result = new List<DateTime>();

            var fromSubmission = fromTeam.ActiveOpenToGameSubmission;
            var opponentSubmission = opponentTeam.ActiveOpenToGameSubmission;

            if (fromSubmission is null || opponentSubmission is null)
                return result;

            for (var i = 0; i < daysAhead; i++)
            {
                var date = nowUtc.Date.AddDays(i);
                var weekDay = MapToWeekDay(date.DayOfWeek);

                var fromWindows = fromSubmission.TimeWindows
                    .Where(x => x.IsActive && x.Day == weekDay)
                    .ToList();

                var opponentWindows = opponentSubmission.TimeWindows
                    .Where(x => x.IsActive && x.Day == weekDay)
                    .ToList();

                foreach (var fromWindow in fromWindows)
                {
                    foreach (var opponentWindow in opponentWindows)
                    {
                        var overlapStart = Math.Max(fromWindow.StartMinute, opponentWindow.StartMinute);
                        var overlapEnd = Math.Min(fromWindow.EndMinute, opponentWindow.EndMinute);

                        if (overlapEnd - overlapStart < durationMinutes)
                            continue;

                        for (var minute = overlapStart; minute + durationMinutes <= overlapEnd; minute += 60)
                        {
                            var slotStartUtc = DateTime.SpecifyKind(date.AddMinutes(minute), DateTimeKind.Utc);

                            if (slotStartUtc <= nowUtc)
                                continue;

                            result.Add(slotStartUtc);
                        }
                    }
                }
            }

            return result
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private async Task<bool> HasBlockedRelationAsync(Guid fromTeamId, Guid opponentTeamId)
        {
            var hasPendingRequest = await _context.GameRequests
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Status == GameRequestStatus.Pending &&
                    ((x.FromTeamId == fromTeamId && x.ToTeamId == opponentTeamId) ||
                     (x.FromTeamId == opponentTeamId && x.ToTeamId == fromTeamId)));

            if (hasPendingRequest)
                return true;

            var hasScheduledMatch = await _context.Matches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Status == MatchStatus.Scheduled &&
                    ((x.HomeTeamId == fromTeamId && x.AwayTeamId == opponentTeamId) ||
                     (x.HomeTeamId == opponentTeamId && x.AwayTeamId == fromTeamId)));

            return hasScheduledMatch;
        }

        private async Task<Team?> GetOwnedActiveOpenToGameTeamAsync(Guid playerId)
        {
            return await _context.Teams
                .AsNoTracking()
                .Include(x => x.ActiveOpenToGameSubmission)
                    .ThenInclude(x => x.TimeWindows)
                .Include(x => x.Members)
                .FirstOrDefaultAsync(x =>
                    x.IsOpenToGame &&
                    x.ActiveOpenToGameSubmissionId != null &&
                    x.Members.Any(m =>
                        m.PlayerId == playerId &&
                        m.Role == TeamRole.Owner &&
                        m.IsActive));
        }

        private static bool IsCompatible(Team fromTeam, Team opponentTeam)
        {
            var fromSubmission = fromTeam.ActiveOpenToGameSubmission;
            var opponentSubmission = opponentTeam.ActiveOpenToGameSubmission;

            if (fromSubmission is null || opponentSubmission is null)
                return false;

            var formatOverlap = fromSubmission.Formats.Intersect(opponentSubmission.Formats).Any();

            var hasOneHourTimeOverlap = fromSubmission.TimeWindows
                .Where(x => x.IsActive)
                .Any(fromWindow =>
                    opponentSubmission.TimeWindows
                        .Where(x => x.IsActive)
                        .Any(opponentWindow =>
                            fromWindow.Day == opponentWindow.Day &&
                            Math.Min(fromWindow.EndMinute, opponentWindow.EndMinute) -
                            Math.Max(fromWindow.StartMinute, opponentWindow.StartMinute) >= 60));

            return formatOverlap && hasOneHourTimeOverlap;
        }

        private static WeekDay MapToWeekDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => WeekDay.Monday,
                DayOfWeek.Tuesday => WeekDay.Tuesday,
                DayOfWeek.Wednesday => WeekDay.Wednesday,
                DayOfWeek.Thursday => WeekDay.Thursday,
                DayOfWeek.Friday => WeekDay.Friday,
                DayOfWeek.Saturday => WeekDay.Saturday,
                _ => WeekDay.Sunday
            };
        }

        private Guid GetCurrentPlayerId()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("Current user is not available.");

            return Guid.Parse(userId);
        }
    }
}

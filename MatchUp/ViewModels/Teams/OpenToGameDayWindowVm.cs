using MatchUp.Utilities.Enums;

namespace MatchUp.ViewModels.Teams
{
    public class OpenToGameDayWindowVm
    {
        public WeekDay Day { get; set; }
        public bool IsActive { get; set; }

        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
    }
}

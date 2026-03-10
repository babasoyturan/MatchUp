using MatchUp.Utilities.Enums;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Teams
{
    public class ManageOpenToGameVm
    {
        [Required]
        public Guid TeamId { get; set; }

        public string TeamName { get; set; } = default!;

        public bool IsCurrentlyOpenToGame { get; set; }
        public string StatusText { get; set; } = default!;

        [Required]
        public List<GameFormat> SelectedFormats { get; set; } = new();

        [Required]
        [StringLength(100)]
        public string CityName { get; set; } = default!;

        [StringLength(100)]
        public string? RegionName { get; set; }

        public List<OpenToGameDayWindowVm> DayWindows { get; set; } = new();
        public List<OpenToGameApprovalMemberVm> ApprovalStatuses { get; set; } = new();
    }
}

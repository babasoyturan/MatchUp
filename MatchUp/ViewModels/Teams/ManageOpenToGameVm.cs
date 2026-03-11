using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Teams
{
    public class ManageOpenToGameVm
    {
        [Required]
        public Guid TeamId { get; set; }

        [ValidateNever]
        public string? TeamName { get; set; }

        [ValidateNever]
        public bool IsCurrentlyOpenToGame { get; set; }

        [ValidateNever]
        public string? StatusText { get; set; }

        [Required]
        public List<GameFormat> SelectedFormats { get; set; } = new();

        public List<OpenToGameDayWindowVm> DayWindows { get; set; } = new();

        [ValidateNever]
        public List<OpenToGameApprovalMemberVm> ApprovalStatuses { get; set; } = new();
    }
}

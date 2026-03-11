using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.GameRequests
{
    public class CreateGameRequestVm
    {
        [Required]
        public Guid OpponentTeamId { get; set; }

        [ValidateNever]
        public string OpponentTeamName { get; set; } = default!;

        [ValidateNever]
        public Guid FromTeamId { get; set; }

        [ValidateNever]
        public string FromTeamName { get; set; } = default!;

        [Required(ErrorMessage = "Please select a format.")]
        public GameFormat? SelectedFormat { get; set; }

        [Required(ErrorMessage = "Please select a game slot.")]
        public string? SelectedSlotStartAtUtc { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; }

        [ValidateNever]
        public List<SelectListItem> FormatOptions { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> SlotOptions { get; set; } = new();
    }
}

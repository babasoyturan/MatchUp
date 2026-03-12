using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Matches
{
    public class ProposeResultVm
    {
        [Required]
        public Guid MatchId { get; set; }

        [ValidateNever]
        public string? HomeTeamName { get; set; }

        [ValidateNever]
        public string? AwayTeamName { get; set; }

        [ValidateNever]
        public DateTime StartAtUtc { get; set; }

        [ValidateNever]
        public string? CurrentResultStatus { get; set; }

        [ValidateNever]
        public string? CurrentVenueText { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Score cannot be negative.")]
        public int ProposedHomeTeamScore { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Score cannot be negative.")]
        public int ProposedAwayTeamScore { get; set; }
    }
}

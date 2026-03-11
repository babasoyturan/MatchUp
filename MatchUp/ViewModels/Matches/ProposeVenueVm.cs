using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Matches
{
    public class ProposeVenueVm
    {
        [Required]
        public Guid MatchId { get; set; }

        [ValidateNever]
        public string HomeTeamName { get; set; } = default!;

        [ValidateNever]
        public string AwayTeamName { get; set; } = default!;

        [ValidateNever]
        public DateTime StartAtUtc { get; set; }

        [ValidateNever]
        public string CurrentVenueText { get; set; } = default!;

        [ValidateNever]
        public string VenueHeroImageUrl { get; set; } = default!;

        [Required]
        public VenueKind VenueKind { get; set; } = VenueKind.Stadium;

        public Guid? ProposedStadiumId { get; set; }

        [StringLength(150)]
        public string? ProposedCustomVenueName { get; set; }

        [StringLength(300)]
        public string? ProposedCustomFormattedAddress { get; set; }

        public double? ProposedCustomLatitude { get; set; }
        public double? ProposedCustomLongitude { get; set; }

        [ValidateNever]
        public List<StadiumSelectItemVm> StadiumOptions { get; set; } = new();
    }
}
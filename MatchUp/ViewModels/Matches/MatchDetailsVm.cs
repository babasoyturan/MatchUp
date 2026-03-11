namespace MatchUp.ViewModels.Matches
{
    public class MatchDetailsVm
    {
        public Guid Id { get; set; }

        public Guid HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = default!;
        public string? HomeTeamLogoUrl { get; set; }

        public Guid AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = default!;
        public string? AwayTeamLogoUrl { get; set; }

        public DateTime StartAtUtc { get; set; }
        public int DurationMinutes { get; set; }

        public string Format { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string VenueStatus { get; set; } = default!;
        public string ResultStatus { get; set; } = default!;

        public string CurrentVenueText { get; set; } = default!;
        public string VenueHeroImageUrl { get; set; } = default!;

        public bool IsHomeOwnerView { get; set; }
        public bool IsAwayOwnerView { get; set; }
        public bool CanProposeVenue { get; set; }
        public bool HasPendingVenueProposal { get; set; }

        public List<MatchVenueProposalListItemVm> VenueProposals { get; set; } = new();
    }
}

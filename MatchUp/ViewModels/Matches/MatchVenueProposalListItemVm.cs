namespace MatchUp.ViewModels.Matches
{
    public class MatchVenueProposalListItemVm
    {
        public Guid ProposalId { get; set; }

        public string ProposedByTeamName { get; set; } = default!;
        public string VenueKindText { get; set; } = default!;
        public string VenueText { get; set; } = default!;
        public string StatusText { get; set; } = default!;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? RespondedAtUtc { get; set; }

        public bool CanApprove { get; set; }
        public bool CanDecline { get; set; }
    }
}

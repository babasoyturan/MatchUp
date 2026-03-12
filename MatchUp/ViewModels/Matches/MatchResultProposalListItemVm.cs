namespace MatchUp.ViewModels.Matches
{
    public class MatchResultProposalListItemVm
    {
        public Guid Id { get; set; }

        public string ProposedByTeamName { get; set; } = default!;
        public int ProposedHomeTeamScore { get; set; }
        public int ProposedAwayTeamScore { get; set; }

        public string StatusText { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? RespondedAtUtc { get; set; }
    }
}

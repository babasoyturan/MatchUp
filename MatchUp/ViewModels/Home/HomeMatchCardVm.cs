namespace MatchUp.ViewModels.Home
{
    public class HomeMatchCardVm
    {
        public Guid MatchId { get; set; }

        public string HomeTeamName { get; set; } = default!;
        public string? HomeTeamLogoUrl { get; set; }

        public string AwayTeamName { get; set; } = default!;
        public string? AwayTeamLogoUrl { get; set; }

        public DateTime StartAtUtc { get; set; }
        public string Format { get; set; } = default!;
        public string VenueText { get; set; } = default!;

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}

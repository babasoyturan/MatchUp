namespace MatchUp.ViewModels.Teams
{
    public class TeamDetailsMatchItemVm
    {
        public Guid MatchId { get; set; }

        public Guid OpponentTeamId { get; set; }
        public string OpponentTeamName { get; set; } = default!;
        public string? OpponentTeamLogoUrl { get; set; }

        public bool IsHome { get; set; }

        public DateTime StartAtUtc { get; set; }
        public int DurationMinutes { get; set; }

        public string Format { get; set; } = default!;
        public string Status { get; set; } = default!;

        public string VenueText { get; set; } = default!;

        public int? TeamScore { get; set; }
        public int? OpponentScore { get; set; }
    }
}

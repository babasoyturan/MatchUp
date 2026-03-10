namespace MatchUp.ViewModels.Teams
{
    public class TeamDetailsStatsVm
    {
        public int MatchesPlayed { get; set; }

        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }

        public int GoalsScored { get; set; }
        public int GoalsConceded { get; set; }
        public int GoalDifference { get; set; }

        public int CleanSheets { get; set; }

        public decimal WinRatio { get; set; }

        public int ScheduledMatchesCount { get; set; }
        public int CompletedMatchesCount { get; set; }

        public DateTime? NextMatchAtUtc { get; set; }
        public DateTime? LastMatchAtUtc { get; set; }
    }
}

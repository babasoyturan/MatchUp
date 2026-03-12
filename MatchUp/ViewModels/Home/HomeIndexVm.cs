namespace MatchUp.ViewModels.Home
{
    public class HomeIndexVm
    {
        public int TeamCount { get; set; }
        public int PlayerCount { get; set; }
        public int ScheduledMatchCount { get; set; }
        public int CompletedMatchCount { get; set; }
        public int OpenToGameTeamCount { get; set; }

        public HomeMatchCardVm? HighlightMatch { get; set; }

        public List<HomeMatchCardVm> RecentResults { get; set; } = new();
        public List<HomeMatchCardVm> UpcomingMatches { get; set; } = new();
        public List<HomeTeamCardVm> OpenToGameTeams { get; set; } = new();
        public List<HomePlayerCardVm> FeaturedPlayers { get; set; } = new();
        public List<HomeStadiumCardVm> FeaturedStadiums { get; set; } = new();
    }
}

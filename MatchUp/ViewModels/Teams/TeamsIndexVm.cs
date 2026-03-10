namespace MatchUp.ViewModels.Teams
{
    public class TeamsIndexVm
    {
        public TeamsIndexFilterVm Filter { get; set; } = new();

        public List<TeamListItemVm> Teams { get; set; } = new();

        public bool ShowCompatibleButton { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}

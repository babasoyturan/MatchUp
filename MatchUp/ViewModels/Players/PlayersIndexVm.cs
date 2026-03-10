namespace MatchUp.ViewModels.Players
{
    public class PlayersIndexVm
    {
        public PlayersIndexFilterVm Filter { get; set; } = new();

        public List<PlayerListItemVm> Players { get; set; } = new();

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }
    }
}

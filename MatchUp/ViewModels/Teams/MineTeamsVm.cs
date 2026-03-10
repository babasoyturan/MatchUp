using MatchUp.Utilities.Enums;

namespace MatchUp.ViewModels.Teams
{
    public class MineTeamsVm
    {
        public MyTeamsFilterBy FilterBy { get; set; } = MyTeamsFilterBy.All;

        public bool HasOwnedTeam { get; set; }

        public List<TeamListItemVm> Teams { get; set; } = new();
    }
}

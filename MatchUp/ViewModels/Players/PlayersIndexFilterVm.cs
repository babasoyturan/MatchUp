using MatchUp.Utilities.Enums;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Players
{
    public class PlayersIndexFilterVm
    {
        public string? Search { get; set; }

        public PlayerSortBy SortBy { get; set; } = PlayerSortBy.Newest;

        public PlayerPosition? Position { get; set; }

        public bool FreeAgentOnly { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 12;
    }
}

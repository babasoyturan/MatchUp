using MatchUp.Utilities.Enums;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Teams
{
    public class TeamsIndexFilterVm
    {
        public string? Search { get; set; }

        public TeamSortBy SortBy { get; set; } = TeamSortBy.Newest;

        public bool CompatibleOnly { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 12;
    }
}

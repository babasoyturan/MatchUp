namespace MatchUp.ViewModels.Teams
{
    public class TeamDetailsOpenToGameVm
    {
        public bool IsEnabled { get; set; }

        public string Status { get; set; } = default!;

        public List<string> Formats { get; set; } = new();
        public List<string> TimeWindows { get; set; } = new();

        public DateTime? ActivatedAtUtc { get; set; }
    }
}

namespace MatchUp.ViewModels.Players
{
    public class PlayerDetailsTeamItemVm
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = default!;
        public string? TeamLogoUrl { get; set; }

        public string Role { get; set; } = default!;
        public byte SquadNumber { get; set; }

        public bool IsActive { get; set; }
        public DateTime JoinedAtUtc { get; set; }
    }
}

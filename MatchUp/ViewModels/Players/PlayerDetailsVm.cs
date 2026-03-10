namespace MatchUp.ViewModels.Players
{
    public class PlayerDetailsVm
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = default!;
        public string ImageUrl { get; set; } = default!;
        public string Biography { get; set; } = default!;
        public string Nationality { get; set; } = default!;

        public short Height { get; set; }
        public short Weight { get; set; }

        public DateTime BirthDate { get; set; }
        public int Age { get; set; }

        public string PrimaryPosition { get; set; } = default!;
        public List<string> PlayablePositions { get; set; } = new();

        public bool IsFreeAgent { get; set; }
        public int ActiveTeamsCount { get; set; }
        public int OwnerTeamsCount { get; set; }

        public bool IsOwnProfile { get; set; }
        public string? Email { get; set; }

        public List<PlayerDetailsTeamItemVm> Teams { get; set; } = new();

        public PlayerInviteStateVm InviteState { get; set; } = new();
        public SendTeamInviteVm InviteRequest { get; set; } = new();
    }
}

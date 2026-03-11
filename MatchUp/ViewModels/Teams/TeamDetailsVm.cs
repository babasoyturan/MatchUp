namespace MatchUp.ViewModels.Teams
{
    public class TeamDetailsVm
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = default!;
        public string? Description { get; set; }

        public string? LogoUrl { get; set; }
        public string? ImageUrl { get; set; }

        public string OwnerFullName { get; set; } = default!;
        public Guid? OwnerPlayerId { get; set; }

        public int MemberCount { get; set; }
        public int ActiveMemberCount { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public bool IsOpenToGame { get; set; }

        public bool IsOwnerView { get; set; }
        public bool IsMemberView { get; set; }

        public bool CanSendGameRequest { get; set; }
        public Guid? SendGameRequestOpponentTeamId { get; set; }

        public TeamDetailsOpenToGameVm OpenToGame { get; set; } = new();
        public TeamDetailsStatsVm Stats { get; set; } = new();

        public List<TeamDetailsSquadItemVm> Squad { get; set; } = new();
        public List<TeamDetailsMatchItemVm> Fixtures { get; set; } = new();
        public List<TeamDetailsMatchItemVm> Results { get; set; } = new();

        public List<TeamDetailsMatchItemVm> UpcomingMatchesPreview { get; set; } = new();
    }
}

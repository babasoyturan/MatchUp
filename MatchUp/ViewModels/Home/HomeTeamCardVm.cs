namespace MatchUp.ViewModels.Home
{
    public class HomeTeamCardVm
    {
        public Guid TeamId { get; set; }
        public string Name { get; set; } = default!;
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
        public string OwnerName { get; set; } = default!;
        public int ActiveMemberCount { get; set; }
    }
}
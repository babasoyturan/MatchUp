namespace MatchUp.ViewModels.Teams
{
    public class TeamListItemVm
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = default!;

        public string? ImageUrl { get; set; }
        public string? LogoUrl { get; set; }

        public int MemberCount { get; set; }

        public decimal WinRatio { get; set; }
    }
}

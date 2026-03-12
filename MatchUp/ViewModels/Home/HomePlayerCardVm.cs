namespace MatchUp.ViewModels.Home
{
    public class HomePlayerCardVm
    {
        public Guid PlayerId { get; set; }
        public string FullName { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public string Nationality { get; set; } = default!;
    }
}

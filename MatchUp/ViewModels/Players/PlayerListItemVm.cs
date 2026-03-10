namespace MatchUp.ViewModels.Players
{
    public class PlayerListItemVm
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = default!;

        public string ImageUrl { get; set; } = default!;

        public string Nationality { get; set; } = default!;

        public int Age { get; set; }

        public string PrimaryPosition { get; set; } = default!;
    }
}

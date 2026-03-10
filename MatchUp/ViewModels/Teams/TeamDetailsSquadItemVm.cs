namespace MatchUp.ViewModels.Teams
{
    public class TeamDetailsSquadItemVm
    {
        public Guid PlayerId { get; set; }

        public string FullName { get; set; } = default!;
        public string ImageUrl { get; set; } = default!;
        public string Nationality { get; set; } = default!;

        public int Age { get; set; }

        public byte SquadNumber { get; set; }

        public string Role { get; set; } = default!;
        public string PrimaryPosition { get; set; } = default!;

        public List<string> PlayablePositions { get; set; } = new();
    }
}

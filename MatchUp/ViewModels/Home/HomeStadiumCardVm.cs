namespace MatchUp.ViewModels.Home
{
    public class HomeStadiumCardVm
    {
        public Guid StadiumId { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public string CityName { get; set; } = default!;
        public string FormattedAddress { get; set; } = default!;
    }
}

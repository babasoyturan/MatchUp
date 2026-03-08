using MatchUp.Models.Abstracts;

namespace MatchUp.Models.Concretes
{
    public class Stadium : EntityBase
    {
        public string? ImageUrl { get; set; }
        public string Name { get; set; } = default!;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string FormattedAddress { get; set; } = default!;
        public string CountryCode { get; set; } = "AZ";
        public string CityName { get; set; } = default!;
        public string? RegionName { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Match> Matches { get; set; } = new List<Match>();
        public ICollection<MatchVenueProposal> VenueProposals { get; set; } = new List<MatchVenueProposal>();
    }
}

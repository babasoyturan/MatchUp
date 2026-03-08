using MatchUp.Models.Abstracts;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameArea : EntityBase
    {
        public Guid OpenToGameConfigId { get; set; }
        public OpenToGameConfig? Config { get; set; }

        public string CityName { get; set; } = default!;
        public string? RegionName { get; set; }
    }
}

using MatchUp.Models.Abstracts;
using System.Numerics;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameMemberApproval : IEntityBase
    {
        public Guid OpenToGameConfigId { get; set; }
        public OpenToGameConfig? Config { get; set; }

        public Guid PlayerId { get; set; }
        public Player? Player { get; set; }

        public bool IsApproved { get; set; }
        public DateTime RespondedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

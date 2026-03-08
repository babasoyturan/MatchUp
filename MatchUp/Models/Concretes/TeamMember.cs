using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class TeamMember : IEntityBase
    {
        public Guid TeamId { get; set; }
        public Team? Team { get; set; }

        public Guid PlayerId { get; set; }
        public Player? Player { get; set; }

        public TeamRole Role { get; set; } = TeamRole.Member;

        public bool IsActive { get; set; } = true;
        public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

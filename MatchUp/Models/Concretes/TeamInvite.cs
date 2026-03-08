using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class TeamInvite : EntityBase
    {
        public Guid TeamId { get; set; }
        public Team? Team { get; set; }

        public Guid InvitedPlayerId { get; set; }
        public Player? InvitedPlayer { get; set; }

        public InviteStatus Status { get; set; } = InviteStatus.Pending;
        public DateTime ExpiresAtUtc { get; set; }
    }
}

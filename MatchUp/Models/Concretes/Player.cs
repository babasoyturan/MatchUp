using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Identity;

namespace MatchUp.Models.Concretes
{
    public class Player : IdentityUser<Guid>, IEntityBase
    {
        public string ImageUrl { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Biography { get; set; } = default!;
        public string Nationality { get; set; } = default!;
        public short Height { get; set; }
        public short Weight { get; set; }
        public DateTime BirthDate { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<PlayerPosition> PlayablePositions { get; set; } = new();

        public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
        public ICollection<TeamInvite> IncomingInvites { get; set; } = new List<TeamInvite>();
        public ICollection<OpenToGameMemberApproval> OpenToGameApprovals { get; set; } = new List<OpenToGameMemberApproval>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}

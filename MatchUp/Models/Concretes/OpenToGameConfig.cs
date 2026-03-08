using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;
using Microsoft.VisualBasic;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameConfig : EntityBase
    {
        public Guid TeamId { get; set; }
        public Team? Team { get; set; }

        public OpenToGameConfigStatus Status { get; set; } = OpenToGameConfigStatus.Draft;

        public List<GameFormat> Formats { get; set; } = new();

        public ICollection<OpenToGameTimeWindow> TimeWindows { get; set; } = new List<OpenToGameTimeWindow>();
        public ICollection<OpenToGameArea> Areas { get; set; } = new List<OpenToGameArea>();
        public ICollection<OpenToGameMemberApproval> MemberApprovals { get; set; } = new List<OpenToGameMemberApproval>();

        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime? ActivatedAtUtc { get; set; }
    }
}

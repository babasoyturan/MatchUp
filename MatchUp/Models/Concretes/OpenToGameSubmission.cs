using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameSubmission : EntityBase
    {
        public Guid TeamId { get; set; }
        public Team? Team { get; set; }

        public Guid SubmittedByPlayerId { get; set; }
        public Player? SubmittedByPlayer { get; set; }

        public OpenToGameSubmissionStatus Status { get; set; } = OpenToGameSubmissionStatus.PendingApprovals;

        public DateTime SubmittedAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }

        public List<GameFormat> Formats { get; set; } = new();

        public List<OpenToGameSubmissionTimeWindow> TimeWindows { get; set; } = new();
        public List<OpenToGameMemberApproval> MemberApprovals { get; set; } = new();
    }
}

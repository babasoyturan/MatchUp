using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameMemberApproval : EntityBase
    {
        public Guid OpenToGameSubmissionId { get; set; }
        public OpenToGameSubmission? OpenToGameSubmission { get; set; }

        public Guid PlayerId { get; set; }
        public Player? Player { get; set; }

        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public DateTime? RespondedAtUtc { get; set; }
    }
}

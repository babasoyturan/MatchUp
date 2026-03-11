using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameSubmissionTimeWindow : EntityBase
    {
        public Guid OpenToGameSubmissionId { get; set; }
        public OpenToGameSubmission? OpenToGameSubmission { get; set; }

        public WeekDay Day { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public bool IsActive { get; set; }
    }
}

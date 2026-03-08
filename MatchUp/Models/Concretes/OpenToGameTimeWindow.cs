using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class OpenToGameTimeWindow : EntityBase
    {
        public Guid OpenToGameConfigId { get; set; }
        public OpenToGameConfig? Config { get; set; }

        public WeekDay Day { get; set; }

        public int StartMinute { get; set; }
        public int EndMinute { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

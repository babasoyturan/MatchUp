using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;
using System.Numerics;

namespace MatchUp.Models.Concretes
{
    public class Notification : EntityBase
    {
        public Guid PlayerId { get; set; }
        public Player? Player { get; set; }

        public NotificationType Type { get; set; }
        public string Title { get; set; } = default!;
        public string? Message { get; set; }
        public NotificationTargetType? TargetType { get; set; }
        public Guid? TargetId { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAtUtc { get; set; }
    }
}

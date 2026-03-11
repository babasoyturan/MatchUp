using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;
using Microsoft.VisualBasic;

namespace MatchUp.Models.Concretes
{
    public class GameRequest : EntityBase
    {
        public Guid FromTeamId { get; set; }
        public Team? FromTeam { get; set; }

        public Guid ToTeamId { get; set; }
        public Team? ToTeam { get; set; }

        public Guid RequestedByPlayerId { get; set; }
        public Player? RequestedByPlayer { get; set; }

        public DateTime StartAtUtc { get; set; }
        public int DurationMinutes { get; set; } = 60;

        public GameFormat Format { get; set; }

        public string? Message { get; set; }
        public GameRequestStatus Status { get; set; } = GameRequestStatus.Pending;

        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RespondedAtUtc { get; set; }
    }
}

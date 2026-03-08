using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class MatchResultProposal : EntityBase
    {
        public Guid MatchId { get; set; }
        public Match? Match { get; set; }

        public Guid ProposedByTeamId { get; set; }
        public Team? ProposedByTeam { get; set; }

        public int HomeTeamScore { get; set; }
        public int AwayTeamScore { get; set; }

        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

        public DateTime? RespondedAtUtc { get; set; }
    }
}

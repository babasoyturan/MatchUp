using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;

namespace MatchUp.Models.Concretes
{
    public class MatchVenueProposal : EntityBase
    {
        public Guid MatchId { get; set; }
        public Match? Match { get; set; }

        public Guid ProposedByTeamId { get; set; }
        public Team? ProposedByTeam { get; set; }

        public VenueKind VenueKind { get; set; } = VenueKind.Stadium;

        public Guid? ProposedStadiumId { get; set; }
        public Stadium? ProposedStadium { get; set; }

        public string? ProposedCustomVenueName { get; set; }
        public string? ProposedCustomFormattedAddress { get; set; }
        public double? ProposedCustomLatitude { get; set; }
        public double? ProposedCustomLongitude { get; set; }

        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

        public DateTime? RespondedAtUtc { get; set; }
    }
}

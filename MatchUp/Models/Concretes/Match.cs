using MatchUp.Models.Abstracts;
using MatchUp.Utilities.Enums;
using Microsoft.VisualBasic;

namespace MatchUp.Models.Concretes
{
    public class Match : EntityBase
    {
        public Guid HomeTeamId { get; set; }
        public Team? HomeTeam { get; set; }

        public Guid AwayTeamId { get; set; }
        public Team? AwayTeam { get; set; }

        public DateTime StartAtUtc { get; set; }
        public int DurationMinutes { get; set; } = 60;

        public GameFormat Format { get; set; }
        public MatchStatus Status { get; set; } = MatchStatus.Scheduled;

        public Guid? CreatedFromRequestId { get; set; }
        public GameRequest? CreatedFromRequest { get; set; }

        public VenueStatus VenueStatus { get; set; } = VenueStatus.Unset;
        public VenueKind? ConfirmedVenueKind { get; set; }

        public Guid? ConfirmedStadiumId { get; set; }
        public Stadium? ConfirmedStadium { get; set; }

        public string? ConfirmedCustomVenueName { get; set; }
        public string? ConfirmedCustomFormattedAddress { get; set; }
        public double? ConfirmedCustomLatitude { get; set; }
        public double? ConfirmedCustomLongitude { get; set; }


        public ResultStatus ResultStatus { get; set; } = ResultStatus.Unset;
        public int? HomeTeamScore { get; set; }
        public int? AwayTeamScore { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public ICollection<MatchVenueProposal> VenueProposals { get; set; } = new List<MatchVenueProposal>();
        public ICollection<MatchResultProposal> ResultProposals { get; set; } = new List<MatchResultProposal>();
    }
}

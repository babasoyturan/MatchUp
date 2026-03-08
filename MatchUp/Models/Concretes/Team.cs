using MatchUp.Models.Abstracts;

namespace MatchUp.Models.Concretes
{
    public class Team : EntityBase
    {
        public string Name { get; set; } = default!;
        public string? LogoUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }

        public OpenToGameConfig? OpenToGameConfig { get; set; }

        public bool IsOpenToGame { get; set; }

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

        public ICollection<TeamInvite> Invites { get; set; } = new List<TeamInvite>();
        public ICollection<GameRequest> OutgoingGameRequests { get; set; } = new List<GameRequest>();
        public ICollection<GameRequest> IncomingGameRequests { get; set; } = new List<GameRequest>();

        public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
        public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
    }
}

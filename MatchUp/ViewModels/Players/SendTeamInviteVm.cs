using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Players
{
    public class SendTeamInviteVm
    {
        [Required]
        public Guid PlayerId { get; set; }

        [Range(1, 99)]
        public byte ProposedSquadNumber { get; set; }
    }
}

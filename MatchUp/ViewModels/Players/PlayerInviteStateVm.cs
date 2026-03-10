namespace MatchUp.ViewModels.Players
{
    public class PlayerInviteStateVm
    {
        public bool CanInvite { get; set; }

        public bool IsOwnProfile { get; set; }
        public bool IsOwnerOfAnyTeam { get; set; }
        public bool IsAlreadyMemberOfOwnedTeam { get; set; }
        public bool HasPendingInvite { get; set; }

        public Guid? OwnerTeamId { get; set; }
        public string? OwnerTeamName { get; set; }

        public byte? PendingProposedSquadNumber { get; set; }

        public string? Message { get; set; }
    }
}

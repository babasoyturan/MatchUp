namespace MatchUp.ViewModels.Notifications
{
    public class NotificationListItemVm
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = default!;
        public string? Message { get; set; }
        public string TypeText { get; set; } = default!;
        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public bool CanAccept { get; set; }
        public bool CanDecline { get; set; }

        public Guid? TeamInviteId { get; set; }
        public string? TeamInviteStatus { get; set; }
        public int? ProposedSquadNumber { get; set; }
        public DateTime? InviteExpiresAtUtc { get; set; }

        public Guid? OpenToGameSubmissionId { get; set; }
        public string? OpenToGameSubmissionStatus { get; set; }

        public bool CanApproveOpenToGame { get; set; }
        public bool CanDeclineOpenToGame { get; set; }

        public Guid? GameRequestId { get; set; }
        public string? GameRequestStatus { get; set; }
        public DateTime? GameRequestStartAtUtc { get; set; }
        public string? GameRequestFormat { get; set; }
        public int? GameRequestDurationMinutes { get; set; }

        public bool CanAcceptGameRequest { get; set; }
        public bool CanDeclineGameRequest { get; set; }

        public Guid? MatchResultProposalId { get; set; }
        public string? MatchResultProposalStatus { get; set; }
        public int? ProposedHomeTeamScore { get; set; }
        public int? ProposedAwayTeamScore { get; set; }

        public bool CanApproveMatchResultProposal { get; set; }
        public bool CanDeclineMatchResultProposal { get; set; }

        public string? TeamName { get; set; }
        public bool IsExpired { get; set; }

        public Guid? MatchId { get; set; }
        public string? MatchTitle { get; set; }
        public bool CanOpenMatch { get; set; }
    }
}

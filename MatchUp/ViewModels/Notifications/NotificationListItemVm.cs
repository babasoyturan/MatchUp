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

        public string? TeamName { get; set; }
        public byte? ProposedSquadNumber { get; set; }
        public DateTime? InviteExpiresAtUtc { get; set; }

        public bool CanApproveOpenToGame { get; set; }
        public bool CanDeclineOpenToGame { get; set; }

        public Guid? OpenToGameSubmissionId { get; set; }
        public string? OpenToGameSubmissionStatus { get; set; }

        public bool IsExpired { get; set; }
    }
}

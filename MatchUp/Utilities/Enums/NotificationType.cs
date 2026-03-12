namespace MatchUp.Utilities.Enums
{
    public enum NotificationType
    {
        TeamInviteReceived = 1,
        TeamInviteAccepted,

        OpenToGameApprovalRequired,
        OpenToGameActivated,

        GameRequestReceived,
        GameRequestAccepted,
        GameRequestDeclined,

        VenueProposed,
        VenueConfirmed,

        ResultProposed,
        ResultConfirmed,

        MatchScheduled,
        MatchCancelled,

        TeamInviteDeclined,

        OpenToGameApprovalAccepted,
        OpenToGameApprovalDeclined,

        MatchVenueProposalReceived,
        MatchVenueProposalApproved,
        MatchVenueProposalDeclined,

        MatchResultProposalReceived,
        MatchResultProposalApproved,
        MatchResultProposalDeclined,
        MatchResultConfirmed
    }
}

namespace MatchUp.Utilities.Enums
{
    public enum NotificationType
    {
        TeamInviteReceived = 1,
        TeamInviteAccepted = 2,

        OpenToGameApprovalRequired = 3,
        OpenToGameActivated = 4,

        GameRequestReceived = 5,
        GameRequestAccepted = 6,
        GameRequestDeclined = 7,

        VenueProposed = 8,
        VenueConfirmed = 9,

        ResultProposed = 10,
        ResultConfirmed = 11,

        MatchScheduled = 12,
        MatchCancelled = 13
    }
}

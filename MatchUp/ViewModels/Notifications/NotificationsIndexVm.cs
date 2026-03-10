namespace MatchUp.ViewModels.Notifications
{
    public class NotificationsIndexVm
    {
        public int UnreadCount { get; set; }
        public List<NotificationListItemVm> Notifications { get; set; } = new();
    }
}

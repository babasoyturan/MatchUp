namespace MatchUp.ViewModels.Notifications
{
    public class NotificationsIndexVm
    {
        public string ActiveTab { get; set; } = "unread";

        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }

        public int UnreadPage { get; set; } = 1;
        public int ReadPage { get; set; } = 1;

        public int UnreadTotalPages { get; set; }
        public int ReadTotalPages { get; set; }

        public List<NotificationListItemVm> UnreadNotifications { get; set; } = new();
        public List<NotificationListItemVm> ReadNotifications { get; set; } = new();
    }
}

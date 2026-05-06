namespace PropertyLeasing.MVC.ViewModels;

public class NotificationViewModel
{
    public int      NotificationId   { get; set; }
    public string   Message          { get; set; } = string.Empty;
    public string?  NotificationType { get; set; }
    public string   Status           { get; set; } = string.Empty;
    public DateTime CreatedAt        { get; set; }
    public bool     IsUnread => Status == "Unread";
}

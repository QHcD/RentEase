using PropertyLeasing.API.Models;

namespace PropertyLeasing.MVC.Services;

public class NotificationService
{
    private readonly PropertyLeasingDbContext _db;

    public NotificationService(PropertyLeasingDbContext db)
    {
        _db = db;
    }

    public async Task SendAsync(int userId, string message, string type = "General")
    {
        _db.Notifications.Add(new Notification
        {
            UserId           = userId,
            Message          = message,
            NotificationType = type,
            Status           = "Unread",
            CreatedAt        = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}

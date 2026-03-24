using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropertyLeasing.API.Hubs;

public class MaintenanceHub : Hub
{
    // When a staff member or manager connects, add them to the "Staff" group
    // so they receive real-time updates on the maintenance board
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user != null &&
            (user.IsInRole("PropertyManager") || user.IsInRole("MaintenanceStaff")))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
        }

        await base.OnConnectedAsync();
    }

    // Tenants can join to get updates on their own requests
    public async Task JoinRequestGroup(string ticketNumber)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket_{ticketNumber}");
    }

    public async Task LeaveRequestGroup(string ticketNumber)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket_{ticketNumber}");
    }
}

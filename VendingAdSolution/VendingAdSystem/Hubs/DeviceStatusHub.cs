using Microsoft.AspNetCore.SignalR;

namespace VendingAdSystem.Hubs;

public class DeviceStatusHub : Hub
{
    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
    }

    public async Task LeaveDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
    }
}

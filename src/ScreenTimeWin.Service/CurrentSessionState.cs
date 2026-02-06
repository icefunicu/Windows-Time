using ScreenTimeWin.Core.Entities;

namespace ScreenTimeWin.Service;

public class CurrentSessionState
{
    // Single active focus (for compatibility)
    public UsageSession? CurrentSession { get; set; }
    
    // All running sessions (background + foreground)
    // Key: AppId or ProcessId? ProcessId is better for tracking lifecycle.
    public System.Collections.Concurrent.ConcurrentDictionary<int, UsageSession> ActiveSessions { get; set; } = new();
}
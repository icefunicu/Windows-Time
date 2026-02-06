using ScreenTimeWin.Core.Entities;

namespace ScreenTimeWin.Core;

public static class TimeHelper
{
    public static List<UsageSession> SplitSessionByMidnight(UsageSession session)
    {
        var result = new List<UsageSession>();
        var start = session.StartUtc;
        var end = session.EndUtc;
        
        // Convert to Local to check midnight crossing in Local time
        var startLocal = start.ToLocalTime();
        var endLocal = end.ToLocalTime();
        
        if (startLocal.Date == endLocal.Date)
        {
            result.Add(session);
            return result;
        }
        
        // It crosses midnight.
        // E.g. 23:50 to 00:10
        // Split point is 00:00 Local, which is MidnightUtc
        
        // We need to find the midnight point in UTC.
        // Next midnight local
        var midnightLocal = startLocal.Date.AddDays(1);
        var midnightUtc = midnightLocal.ToUniversalTime();
        
        var firstPart = new UsageSession
        {
            Id = Guid.NewGuid(),
            AppId = session.AppId,
            App = session.App,
            WindowTitle = session.WindowTitle,
            SiteDomain = session.SiteDomain,
            StartUtc = start,
            EndUtc = midnightUtc,
            DurationSeconds = (int)(midnightUtc - start).TotalSeconds
        };
        
        var secondPart = new UsageSession
        {
            Id = Guid.NewGuid(),
            AppId = session.AppId,
            App = session.App,
            WindowTitle = session.WindowTitle,
            SiteDomain = session.SiteDomain,
            StartUtc = midnightUtc,
            EndUtc = end,
            DurationSeconds = (int)(end - midnightUtc).TotalSeconds
        };
        
        // Recursive check if it spans multiple days (rare for active usage, but possible for sleep mode)
        // For MVP assuming 1 day crossing max or handling iteratively
        result.Add(firstPart);
        result.Add(secondPart);
        
        return result;
    }
}

using ScreenTimeWin.Core;
using ScreenTimeWin.Core.Entities;

namespace ScreenTimeWin.Tests;

public class TimeHelperTests
{
    [Fact]
    public void SplitSessionByMidnight_NoSplit_WhenSameDay()
    {
        var start = DateTime.UtcNow; // Assume mid-day
        var end = start.AddMinutes(10);
        
        var session = new UsageSession { StartUtc = start, EndUtc = end, DurationSeconds = 600 };
        var result = TimeHelper.SplitSessionByMidnight(session);
        
        Assert.Single(result);
        Assert.Equal(600, result[0].DurationSeconds);
    }

    [Fact]
    public void SplitSessionByMidnight_Splits_WhenCrossingMidnight()
    {
        // Setup: 23:55 to 00:05 Local Time
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        
        var startLocal = today.AddHours(23).AddMinutes(55);
        var endLocal = tomorrow.AddMinutes(5);
        
        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();
        
        var session = new UsageSession { StartUtc = startUtc, EndUtc = endUtc, DurationSeconds = 600 };
        var result = TimeHelper.SplitSessionByMidnight(session);
        
        Assert.Equal(2, result.Count);
        Assert.Equal(300, result[0].DurationSeconds); // 5 mins
        Assert.Equal(300, result[1].DurationSeconds); // 5 mins
        
        // Verify continuity
        Assert.Equal(result[0].EndUtc, result[1].StartUtc);
    }
}

using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Core.Models;

namespace ScreenTimeWin.Tests;

public class ModelTests
{
    [Fact]
    public void LimitRule_Defaults_AreCorrect()
    {
        var rule = new LimitRule();
        Assert.True(rule.Enabled);
        Assert.Equal(ActionOnLimit.NotifyOnly, rule.ActionOnLimit);
    }

    [Fact]
    public void DailyAggregate_KeyProperties_AreSet()
    {
        var date = "2023-01-01";
        var appId = Guid.NewGuid();
        var agg = new DailyAggregate
        {
            DateLocal = date,
            AppId = appId,
            TotalSeconds = 100
        };

        Assert.Equal(date, agg.DateLocal);
        Assert.Equal(appId, agg.AppId);
        Assert.Equal(100, agg.TotalSeconds);
    }
}
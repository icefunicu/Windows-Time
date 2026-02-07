using ScreenTimeWin.Core.Services;
using Serilog;

namespace ScreenTimeWin.App.Services;

/// <summary>
/// 调试遥测服务 - 将事件记录到日志
/// </summary>
public class DebugTelemetryService : ITelemetryService
{
    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        var props = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "None";
        Log.Information("[Telemetry] Event: {EventName}, Properties: {Properties}", eventName, props);
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        var props = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "None";
        Log.Error(exception, "[Telemetry] Exception: {Message}, Properties: {Properties}", exception.Message, props);
    }

    public void TrackPageView(string pageName)
    {
        Log.Information("[Telemetry] PageView: {PageName}", pageName);
    }

    public void TrackUserAction(string action, string target, IDictionary<string, string>? properties = null)
    {
        var props = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "None";
        Log.Information("[Telemetry] UserAction: {Action} on {Target}, Properties: {Properties}", action, target, props);
    }
}

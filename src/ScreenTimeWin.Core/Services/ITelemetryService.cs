using System.Collections.Generic;

namespace ScreenTimeWin.Core.Services;

/// <summary>
/// 遥测服务接口
/// </summary>
public interface ITelemetryService
{
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);
    void TrackPageView(string pageName);
    void TrackUserAction(string action, string target, IDictionary<string, string>? properties = null);
}

/// <summary>
/// 用户行为事件常量
/// </summary>
public static class TelemetryEvents
{
    // 导航事件
    public const string PageView = "page_view";
    public const string NavigateTo = "navigate_to";

    // 功能使用事件
    public const string StartFocus = "start_focus";
    public const string StopFocus = "stop_focus";
    public const string SetLimit = "set_limit";
    public const string RemoveLimit = "remove_limit";
    public const string ExportData = "export_data";
    public const string ClearData = "clear_data";
    public const string ChangeTheme = "change_theme";
    public const string ChangePin = "change_pin";

    // 用户交互事件
    public const string ButtonClick = "button_click";
    public const string ToggleSwitch = "toggle_switch";
    public const string SelectOption = "select_option";

    // 系统事件
    public const string AppStart = "app_start";
    public const string AppClose = "app_close";
    public const string ServiceConnect = "service_connect";
    public const string ServiceDisconnect = "service_disconnect";
    public const string Error = "error";

    // 使用行为事件
    public const string ViewDashboard = "view_dashboard";
    public const string ViewWeeklyReport = "view_weekly_report";
    public const string ViewAppDetails = "view_app_details";
    public const string AddCategoryRule = "add_category_rule";
    public const string RemoveCategoryRule = "remove_category_rule";
}

/// <summary>
/// 遥测服务扩展方法
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// 记录页面浏览
    /// </summary>
    public static void TrackPageView(this ITelemetryService telemetry, string pageName)
    {
        telemetry.TrackEvent(TelemetryEvents.PageView, new Dictionary<string, string>
        {
            { "page_name", pageName },
            { "timestamp", DateTime.Now.ToString("o") }
        });
    }

    /// <summary>
    /// 记录用户操作
    /// </summary>
    public static void TrackUserAction(this ITelemetryService telemetry, string action, string target)
    {
        telemetry.TrackEvent(action, new Dictionary<string, string>
        {
            { "target", target },
            { "timestamp", DateTime.Now.ToString("o") }
        });
    }

    /// <summary>
    /// 记录功能使用
    /// </summary>
    public static void TrackFeatureUsage(this ITelemetryService telemetry, string feature, IDictionary<string, string>? properties = null)
    {
        var props = properties == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(properties);

        props["feature"] = feature;
        props["timestamp"] = DateTime.Now.ToString("o");

        telemetry.TrackEvent($"feature_{feature}", props);
    }

    /// <summary>
    /// 记录专注模式使用
    /// </summary>
    public static void TrackFocusSession(this ITelemetryService telemetry, int durationMinutes, string focusMode, bool completed)
    {
        telemetry.TrackEvent(completed ? "focus_completed" : "focus_stopped", new Dictionary<string, string>
        {
            { "duration_minutes", durationMinutes.ToString() },
            { "focus_mode", focusMode },
            { "completed", completed.ToString().ToLower() }
        });
    }
}

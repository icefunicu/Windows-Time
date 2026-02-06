namespace ScreenTimeWin.IPC.Models;

/// <summary>
/// IPC操作常量定义
/// </summary>
public static class IpcActions
{
    public const string Ping = "Ping";
    public const string GetTodaySummary = "GetTodaySummary";
    public const string GetUsageByDate = "GetUsageByDate";
    public const string GetLimitRules = "GetLimitRules";
    public const string UpsertLimitRule = "UpsertLimitRule";
    public const string StartFocus = "StartFocus";
    public const string StopFocus = "StopFocus";
    public const string ExportData = "ExportData";
    public const string ClearData = "ClearData";
    public const string GetNotifications = "GetNotifications";
    public const string VerifyPin = "VerifyPin";
    public const string SetPin = "SetPin";
    
    // 新增接口
    public const string GetWeeklySummary = "GetWeeklySummary";
    public const string GetAppDetails = "GetAppDetails";
    public const string GetRecentSessions = "GetRecentSessions";
    public const string GetFocusStatus = "GetFocusStatus";
    public const string AddExtraTime = "AddExtraTime";
    public const string RequestUnlock = "RequestUnlock";
}

#region 请求DTOs

public class PinRequest
{
    public string Pin { get; set; } = string.Empty;
}

public class SetPinRequest
{
    public string OldPin { get; set; } = string.Empty;
    public string NewPin { get; set; } = string.Empty;
}

public class UsageByDateRequest
{
    public DateTime DateLocal { get; set; }
}

public class WeeklySummaryRequest
{
    /// <summary>
    /// 周开始日期（周日）
    /// </summary>
    public DateTime WeekStartDate { get; set; }
}

public class AppDetailsRequest
{
    public Guid AppId { get; set; }
}

public class RecentSessionsRequest
{
    public Guid? AppId { get; set; }
    public int MaxCount { get; set; } = 20;
}

public class StartFocusRequest
{
    public int DurationMinutes { get; set; }
    public List<Guid> WhitelistAppIds { get; set; } = new();
    public string? Label { get; set; }
}

public class AddExtraTimeRequest
{
    public Guid AppId { get; set; }
    public int ExtraMinutes { get; set; } = 5;
}

public class RequestUnlockRequest
{
    public Guid AppId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region 响应DTOs

public class PingResponse
{
    public bool Running { get; set; }
    public string Version { get; set; } = "1.0.0";
    public TimeSpan Uptime { get; set; }
}

public class TodaySummaryResponse
{
    public long TotalSeconds { get; set; }
    public long TotalSecondsYesterday { get; set; }
    public int AppSwitches { get; set; }
    public List<AppUsageDto> TopApps { get; set; } = new();
    public List<long> HourlyUsage { get; set; } = new();
    public Dictionary<string, long> CategoryUsage { get; set; } = new();
}

/// <summary>
/// 周报响应
/// </summary>
public class WeeklySummaryResponse
{
    public long TotalSeconds { get; set; }
    public long TotalSecondsLastWeek { get; set; }
    public double ChangePercent { get; set; }
    
    /// <summary>
    /// 每日使用秒数（7天，从周日开始）
    /// </summary>
    public List<long> DailyUsage { get; set; } = new();
    
    /// <summary>
    /// 分类使用秒数
    /// </summary>
    public Dictionary<string, long> CategoryUsage { get; set; } = new();
    
    /// <summary>
    /// 本周Top应用
    /// </summary>
    public List<AppUsageDto> TopApps { get; set; } = new();
    
    /// <summary>
    /// 专注会话完成数
    /// </summary>
    public int FocusSessionsCompleted { get; set; }
    
    /// <summary>
    /// 专注总时长（秒）
    /// </summary>
    public long FocusTotalSeconds { get; set; }
}

/// <summary>
/// 应用详情响应
/// </summary>
public class AppDetailsResponse
{
    public Guid AppId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string IconBase64 { get; set; } = string.Empty;
    
    /// <summary>
    /// 今日使用秒数
    /// </summary>
    public long TodaySeconds { get; set; }
    
    /// <summary>
    /// 7日平均使用秒数
    /// </summary>
    public long SevenDayAverageSeconds { get; set; }
    
    /// <summary>
    /// 本周总使用秒数
    /// </summary>
    public long WeekTotalSeconds { get; set; }
    
    /// <summary>
    /// 限制规则
    /// </summary>
    public LimitRuleDto? LimitRule { get; set; }
    
    /// <summary>
    /// 最近会话
    /// </summary>
    public List<SessionDto> RecentSessions { get; set; } = new();
    
    /// <summary>
    /// 常用窗口标题
    /// </summary>
    public List<TitleUsageDto> TopTitles { get; set; } = new();
}

/// <summary>
/// 会话记录DTO
/// </summary>
public class SessionDto
{
    public Guid SessionId { get; set; }
    public Guid AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime StartTimeLocal { get; set; }
    public DateTime? EndTimeLocal { get; set; }
    public long DurationSeconds { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
}

/// <summary>
/// 窗口标题使用统计DTO
/// </summary>
public class TitleUsageDto
{
    public string Title { get; set; } = string.Empty;
    public long TotalSeconds { get; set; }
    public int SessionCount { get; set; }
}

/// <summary>
/// 专注状态响应
/// </summary>
public class FocusStatusResponse
{
    public bool IsActive { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int RemainingSeconds { get; set; }
    public string? Label { get; set; }
    public List<Guid> WhitelistAppIds { get; set; } = new();
}

#endregion

#region 基础DTOs

public class AppUsageDto
{
    public Guid AppId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string IconBase64 { get; set; } = string.Empty;
    public long TotalSeconds { get; set; }
}

public class LimitRuleDto
{
    public Guid AppId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? DailyLimitMinutes { get; set; }
    public TimeSpan? CurfewStartLocal { get; set; }
    public TimeSpan? CurfewEndLocal { get; set; }
    public string ActionOnLimit { get; set; } = "NotifyOnly";
    public bool Enabled { get; set; }
}

public class NotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info"; // Info, Warning, Error
    public DateTime Timestamp { get; set; }
}

#endregion

#region IPC通信

public class IpcRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public class IpcResponse
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
}

#endregion

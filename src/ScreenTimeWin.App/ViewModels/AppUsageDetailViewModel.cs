using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Windows;

namespace ScreenTimeWin.App.ViewModels;

/// <summary>
/// 应用使用详情ViewModel
/// </summary>
public partial class AppUsageDetailViewModel : ObservableObject
{
    private readonly IAppService _appService;

    public AppUsageDetailViewModel(IAppService appService)
    {
        _appService = appService;
        InitializeMockData();
    }

    #region 应用列表

    /// <summary>
    /// 应用使用列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AppUsageListItem> _appList = new();

    /// <summary>
    /// 选中的应用详情
    /// </summary>
    [ObservableProperty]
    private AppDetailInfo? _selectedAppDetail;

    /// <summary>
    /// 是否显示详情弹窗
    /// </summary>
    [ObservableProperty]
    private bool _isDetailPopupVisible = false;

    #endregion

    #region 命令

    [RelayCommand]
    private async Task LoadAppsAsync()
    {
        try
        {
            var summary = await _appService.GetTodaySummaryAsync();
            AppList.Clear();

            foreach (var app in summary.TopApps.Take(10))
            {
                // 获取7日平均数据（简化处理）
                var weeklyData = await _appService.GetAppDetailsAsync(app.AppId);

                AppList.Add(new AppUsageListItem
                {
                    AppId = app.AppId,
                    DisplayName = app.DisplayName,
                    ProcessName = app.ProcessName,
                    IconBase64 = app.IconBase64,
                    TodaySeconds = app.TotalSeconds,
                    SevenDayAverageSeconds = weeklyData.SevenDayAverageSeconds,
                    Category = app.Category
                });
            }
        }
        catch
        {
            // 使用Mock数据
            InitializeMockData();
        }
    }

    [RelayCommand]
    private async Task SelectAppAsync(AppUsageListItem? item)
    {
        if (item == null) return;

        try
        {
            var detail = await _appService.GetAppDetailsAsync(item.AppId);
            SelectedAppDetail = new AppDetailInfo
            {
                AppId = item.AppId,
                DisplayName = item.DisplayName,
                IconBase64 = item.IconBase64,
                TodaySeconds = item.TodaySeconds,
                SevenDayAverageSeconds = item.SevenDayAverageSeconds,
                RecentSessions = new ObservableCollection<SessionDisplayItem>(
                    detail.RecentSessions.Take(5).Select(s => new SessionDisplayItem
                    {
                        DateLabel = s.StartTimeLocal.ToString("MMM dd"),
                        Duration = FormatDuration(s.DurationSeconds),
                        StartTime = s.StartTimeLocal.ToString("HH:mm"),
                        WindowTitle = s.WindowTitle
                    })
                ),
                TopTitles = new ObservableCollection<string>(
                    detail.TopTitles.Take(3).Select(t => $"\"{t.Title}\"")
                ),
                HasLimit = detail.LimitRule?.Enabled ?? false,
                DailyLimitMinutes = detail.LimitRule?.DailyLimitMinutes
            };
        }
        catch
        {
            // 使用Mock数据
            SelectedAppDetail = CreateMockAppDetail(item);
        }

        IsDetailPopupVisible = true;
    }

    [RelayCommand]
    private void CloseDetailPopup()
    {
        IsDetailPopupVisible = false;
        SelectedAppDetail = null;
    }

    [RelayCommand]
    private async Task SetDailyLimitAsync()
    {
        if (SelectedAppDetail == null) return;

        // 默认设置2小时限制
        var rule = new LimitRuleDto
        {
            AppId = SelectedAppDetail.AppId,
            DailyLimitMinutes = 120,
            Enabled = true,
            ActionOnLimit = "NotifyOnly"
        };

        await _appService.UpsertLimitRuleAsync(rule);
        SelectedAppDetail.HasLimit = true;
        SelectedAppDetail.DailyLimitMinutes = 120;
    }

    [RelayCommand]
    private void BlockInFocus()
    {
        MessageBox.Show("Please configure Focus Mode whitelist in the Focus tab.", "Focus Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        CloseDetailPopup();
    }

    [RelayCommand]
    private void AlwaysAllow()
    {
        MessageBox.Show("To always allow this app, please ensure no limits are set and it is added to the Focus whitelist if needed.", "Always Allow", MessageBoxButton.OK, MessageBoxImage.Information);
        CloseDetailPopup();
    }

    #endregion

    #region 辅助方法

    private void InitializeMockData()
    {
        AppList.Clear();
        AppList.Add(new AppUsageListItem
        {
            AppId = Guid.NewGuid(),
            DisplayName = "YouTube",
            ProcessName = "chrome",
            TodaySeconds = 4500,
            SevenDayAverageSeconds = 3900,
            Category = "Entertainment",
            CategoryColor = "#EA4335"
        });
        AppList.Add(new AppUsageListItem
        {
            AppId = Guid.NewGuid(),
            DisplayName = "Google Chrome",
            ProcessName = "chrome",
            TodaySeconds = 2700,
            SevenDayAverageSeconds = 2100,
            Category = "Browser",
            CategoryColor = "#4285F4"
        });
        AppList.Add(new AppUsageListItem
        {
            AppId = Guid.NewGuid(),
            DisplayName = "Microsoft Teams",
            ProcessName = "teams",
            TodaySeconds = 1920,
            SevenDayAverageSeconds = 1800,
            Category = "Work",
            CategoryColor = "#7B83EB"
        });
        AppList.Add(new AppUsageListItem
        {
            AppId = Guid.NewGuid(),
            DisplayName = "Excel",
            ProcessName = "excel",
            TodaySeconds = 1680,
            SevenDayAverageSeconds = 1500,
            Category = "Work",
            CategoryColor = "#217346"
        });
        AppList.Add(new AppUsageListItem
        {
            AppId = Guid.NewGuid(),
            DisplayName = "Steam",
            ProcessName = "steam",
            TodaySeconds = 1080,
            SevenDayAverageSeconds = 900,
            Category = "Entertainment",
            CategoryColor = "#171A21"
        });
    }

    private AppDetailInfo CreateMockAppDetail(AppUsageListItem item)
    {
        return new AppDetailInfo
        {
            AppId = item.AppId,
            DisplayName = item.DisplayName,
            IconBase64 = item.IconBase64,
            TodaySeconds = item.TodaySeconds,
            SevenDayAverageSeconds = item.SevenDayAverageSeconds,
            RecentSessions = new ObservableCollection<SessionDisplayItem>
            {
                new() { DateLabel = "Today", Duration = "17m", StartTime = "1h 4m", WindowTitle = "Video 1" },
                new() { DateLabel = "Today", Duration = "13m", StartTime = "2h 3m", WindowTitle = "Video 2" },
                new() { DateLabel = "Today", Duration = "22m", StartTime = "2h 0m", WindowTitle = "Video 3" }
            },
            TopTitles = new ObservableCollection<string> { "\"Home\"", "\"Trending\"" },
            HasLimit = false
        };
    }

    private string FormatDuration(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }

    #endregion
}

#region 数据模型

/// <summary>
/// 应用使用列表项
/// </summary>
public class AppUsageListItem
{
    public Guid AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string IconBase64 { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#4285F4";
    public long TodaySeconds { get; set; }
    public long SevenDayAverageSeconds { get; set; }

    /// <summary>
    /// 今日使用时长文本
    /// </summary>
    public string TodayText => FormatTime(TodaySeconds);

    /// <summary>
    /// 7日平均使用时长文本
    /// </summary>
    public string SevenDayAvgText => FormatTime(SevenDayAverageSeconds);

    /// <summary>
    /// 今日使用百分比（相对最大值）
    /// </summary>
    public double TodayPercent => TodaySeconds > 0 ? Math.Min(100, TodaySeconds / 72.0) : 0; // 2小时=100%

    /// <summary>
    /// 7日平均百分比
    /// </summary>
    public double SevenDayPercent => SevenDayAverageSeconds > 0 ? Math.Min(100, SevenDayAverageSeconds / 72.0) : 0;

    private static string FormatTime(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }
}

/// <summary>
/// 应用详情信息
/// </summary>
public partial class AppDetailInfo : ObservableObject
{
    public Guid AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string IconBase64 { get; set; } = string.Empty;
    public long TodaySeconds { get; set; }
    public long SevenDayAverageSeconds { get; set; }

    public string TodayText => FormatTime(TodaySeconds);

    public ObservableCollection<SessionDisplayItem> RecentSessions { get; set; } = new();
    public ObservableCollection<string> TopTitles { get; set; } = new();

    [ObservableProperty]
    private bool _hasLimit;

    [ObservableProperty]
    private int? _dailyLimitMinutes;

    private static string FormatTime(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }
}

/// <summary>
/// 会话显示项
/// </summary>
public class SessionDisplayItem
{
    public string DateLabel { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
}

#endregion

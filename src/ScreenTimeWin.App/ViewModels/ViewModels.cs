using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ScreenTimeWin.App.ViewModels;

/// <summary>
/// 分类图例项模型
/// </summary>
public partial class CategoryLegendItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Brush _colorBrush = Brushes.Gray;

    [ObservableProperty]
    private long _seconds;

    public string TimeText => TimeSpan.FromSeconds(Seconds).ToString(@"h\h\ m\m");
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private readonly LocalAppMonitorService _monitorService;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _totalTimeText = "Loading...";

    [ObservableProperty]
    private string _growthText = "+0% from yesterday";

    [ObservableProperty]
    private string _appSwitchesText = "0";

    [ObservableProperty]
    private ObservableCollection<AppUsageViewModel> _topApps = new();

    // 分类图例列表
    [ObservableProperty]
    private ObservableCollection<CategoryLegendItem> _categoryLegends = new();

    // Charts - 图表数据
    [ObservableProperty]
    private ISeries[] _hourlySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] _categorySeries = Array.Empty<ISeries>();

    // 分类颜色映射（匹配原型图配色）
    private static readonly Dictionary<string, SKColor> CategoryColors = new()
    {
        { "Work", new SKColor(66, 133, 244) },         // 蓝色
        { "Productivity", new SKColor(66, 133, 244) }, // 蓝色
        { "Development", new SKColor(66, 133, 244) },  // 蓝色
        { "Social", new SKColor(52, 168, 83) },        // 绿色
        { "Communication", new SKColor(52, 168, 83) }, // 绿色
        { "Entertainment", new SKColor(234, 67, 53) }, // 红色
        { "Media", new SKColor(234, 67, 53) },         // 红色
        { "Games", new SKColor(234, 67, 53) },         // 红色
        { "Learning", new SKColor(66, 133, 244) },     // 蓝色
        { "System", new SKColor(154, 160, 166) },      // 灰色
        { "Browser", new SKColor(251, 188, 5) },       // 黄色
        { "Other", new SKColor(251, 188, 5) },         // 黄色
        { "Uncategorized", new SKColor(154, 160, 166) } // 灰色
    };

    public DashboardViewModel(IAppService appService, LocalAppMonitorService monitorService)
    {
        _appService = appService;
        _monitorService = monitorService;

        // 初始化图表（使用Mock数据以匹配设计）
        InitializeCharts();

        // 订阅监控数据更新事件
        _monitorService.AppsUpdated += OnMonitorDataUpdated;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (s, e) => await LoadDataAsync();
        _timer.Start();

        // 初始加载
        Task.Run(LoadDataAsync);
    }

    private void OnMonitorDataUpdated(object? sender, EventArgs e)
    {
        // 当监控数据更新时，刷新UI
        App.Current.Dispatcher.BeginInvoke(() => LoadLocalDataAsync());
    }

    /// <summary>
    /// 从本地监控服务加载数据
    /// </summary>
    private void LoadLocalDataAsync()
    {
        try
        {
            var totalSeconds = _monitorService.GetTotalSeconds();
            var time = TimeSpan.FromSeconds(totalSeconds);

            var newTimeText = $"{time.Hours}h {time.Minutes}m";
            if (TotalTimeText != newTimeText)
                TotalTimeText = newTimeText;

            var newSwitchText = _monitorService.GetAppSwitchCount().ToString();
            if (AppSwitchesText != newSwitchText)
                AppSwitchesText = newSwitchText;

            // 更新Top Apps列表 - 增量更新避免闪烁
            var trackedApps = _monitorService.GetRunningApps().Take(5).ToList();

            // 智能更新：只更新变化的项，不重建整个集合
            for (int i = 0; i < trackedApps.Count; i++)
            {
                var app = trackedApps[i];
                var dto = new IPC.Models.AppUsageDto
                {
                    AppId = Guid.NewGuid(),
                    DisplayName = app.DisplayName,
                    ProcessName = app.ProcessName,
                    TotalSeconds = (long)app.TotalSeconds,
                    Category = app.Category,
                    IconBase64 = app.IconBase64 ?? ""
                };

                if (i < TopApps.Count)
                {
                    // 更新现有项（仅更新时长避免重绘）
                    if (TopApps[i].ProcessName == app.ProcessName)
                    {
                        TopApps[i].TotalSeconds = (long)app.TotalSeconds;
                    }
                    else
                    {
                        TopApps[i] = new AppUsageViewModel(dto);
                    }
                }
                else
                {
                    TopApps.Add(new AppUsageViewModel(dto));
                }
            }

            // 移除多余项
            while (TopApps.Count > trackedApps.Count)
            {
                TopApps.RemoveAt(TopApps.Count - 1);
            }

            // 更新分类统计 - 使用增量更新
            var categoryUsage = _monitorService.GetCategoryUsage();
            if (categoryUsage.Count > 0)
            {
                UpdateCategoryLegends(categoryUsage);
                UpdateCategorySeries(categoryUsage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadLocalDataAsync error: {ex.Message}");
        }
    }

    private void InitializeCharts()
    {
        // 面积图（蓝色渐变）
        HourlySeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = new double[] { 2, 5, 4, 6, 8, 3, 5, 7, 6, 8, 5, 3 },
                Fill = new LinearGradientPaint(
                    new SKColor(0, 122, 255, 100),
                    new SKColor(0, 122, 255, 0),
                    new SKPoint(0.5f, 0),
                    new SKPoint(0.5f, 1)),
                Stroke = new SolidColorPaint(new SKColor(0, 122, 255)) { StrokeThickness = 3 },
                GeometrySize = 0, // 不显示数据点
                LineSmoothness = 0.5
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "6 AM", "", "12 PM", "", "6 PM", "", "12 AM" },
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11,
                SeparatorsPaint = null
            }
        };

        YAxes = new Axis[]
        {
             new Axis
             {
                 IsVisible = false
             }
        };

        // 初始化分类图例（占位）
        UpdateCategoryLegends(new Dictionary<string, long>
        {
            { "Work", 45 },
            { "Social", 25 },
            { "Entertainment", 20 },
            { "Other", 10 }
        });
    }

    /// <summary>
    /// 更新分类图例
    /// </summary>
    private void UpdateCategoryLegends(Dictionary<string, long> categoryUsage)
    {
        CategoryLegends.Clear();
        foreach (var kvp in categoryUsage.OrderByDescending(x => x.Value))
        {
            var color = GetCategoryColor(kvp.Key);
            CategoryLegends.Add(new CategoryLegendItem
            {
                Name = kvp.Key,
                Seconds = kvp.Value,
                ColorBrush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue))
            });
        }
    }

    /// <summary>
    /// 更新分类饼图数据
    /// </summary>
    private void UpdateCategorySeries(Dictionary<string, long> categoryUsage)
    {
        var newSeries = new List<ISeries>();

        foreach (var kvp in categoryUsage.OrderByDescending(x => x.Value))
        {
            var color = GetCategoryColor(kvp.Key);
            newSeries.Add(new PieSeries<double>
            {
                Values = new double[] { kvp.Value },
                Name = kvp.Key,
                Fill = new SolidColorPaint(color),
                InnerRadius = 50
            });
        }

        CategorySeries = newSeries.ToArray();
    }

    /// <summary>
    /// 获取分类对应的颜色
    /// </summary>
    private static SKColor GetCategoryColor(string category)
    {
        return CategoryColors.TryGetValue(category, out var color)
            ? color
            : new SKColor(154, 160, 166); // 默认灰色
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var summary = await _appService.GetTodaySummaryAsync();

        // 1. 今日总时长
        var time = TimeSpan.FromSeconds(summary.TotalSeconds);
        TotalTimeText = $"{time.Hours}h {time.Minutes}m";

        // 2. 增长百分比
        if (summary.TotalSecondsYesterday > 0)
        {
            double growth = ((double)summary.TotalSeconds - summary.TotalSecondsYesterday) / summary.TotalSecondsYesterday;
            string sign = growth >= 0 ? "+" : "";
            GrowthText = $"{sign}{growth:P0} from yesterday";
        }
        else
        {
            GrowthText = "N/A from yesterday";
        }

        // 3. App 切换次数
        AppSwitchesText = summary.AppSwitches.ToString();

        App.Current.Dispatcher.Invoke(() =>
        {
            // 4. Top Apps 列表
            TopApps.Clear();
            foreach (var app in summary.TopApps)
            {
                TopApps.Add(new AppUsageViewModel(app));
            }

            // 5. 每小时使用图表
            var hourlyValues = summary.HourlyUsage.Select(x => (double)x / 60.0).ToArray();

            if (HourlySeries.FirstOrDefault() is LineSeries<double> lineSeries)
            {
                lineSeries.Values = hourlyValues;
            }

            // 6. 分类饼图
            var newSeries = new List<ISeries>();
            int colorIdx = 0;

            foreach (var kvp in summary.CategoryUsage.OrderByDescending(x => x.Value))
            {
                var color = GetCategoryColor(kvp.Key);
                newSeries.Add(new PieSeries<double>
                {
                    Values = new double[] { kvp.Value },
                    Name = kvp.Key,
                    Fill = new SolidColorPaint(color),
                    InnerRadius = 50 // 环形图效果
                });
                colorIdx++;
            }

            CategorySeries = newSeries.ToArray();

            // 7. 更新分类图例
            UpdateCategoryLegends(summary.CategoryUsage);
        });
    }
}

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppService _appService;
    private readonly DispatcherTimer _notificationTimer;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _notificationMessage = "";

    [ObservableProperty]
    private bool _isNotificationVisible;

    public MainViewModel(IServiceProvider serviceProvider, IAppService appService)
    {
        _serviceProvider = serviceProvider;
        _appService = appService;
        NavigateToDashboard();

        // 通知轮询器
        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _notificationTimer.Tick += async (s, e) => await CheckNotificationsAsync();
        _notificationTimer.Start();
    }

    private async Task CheckNotificationsAsync()
    {
        try
        {
            var notes = await _appService.GetNotificationsAsync();
            if (notes != null && notes.Any())
            {
                // MVP阶段，只显示最后一条
                var last = notes.Last();
                ShowNotification(last.Title, last.Message);
            }
        }
        catch { }
    }

    private void ShowNotification(string title, string message)
    {
        NotificationMessage = $"{title}: {message}";
        IsNotificationVisible = true;

        // 5秒后自动隐藏
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            IsNotificationVisible = false;
            timer.Stop();
        };
        timer.Start();
    }

    [RelayCommand]
    public void NavigateToDashboard() => CurrentView = _serviceProvider.GetService(typeof(DashboardViewModel));

    [RelayCommand]
    public void NavigateToAnalytics() => CurrentView = _serviceProvider.GetService(typeof(AnalyticsViewModel));

    [RelayCommand]
    public void NavigateToLimits() => CurrentView = _serviceProvider.GetService(typeof(LimitsViewModel));

    [RelayCommand]
    public void NavigateToFocus() => CurrentView = _serviceProvider.GetService(typeof(FocusViewModel));

    [RelayCommand]
    public void NavigateToSettings() => CurrentView = _serviceProvider.GetService(typeof(SettingsViewModel));
}

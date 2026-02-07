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
using Microsoft.Extensions.DependencyInjection;

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
    [NotifyPropertyChangedFor(nameof(TimeText))] // Ensure UI updates when Seconds changes
    private long _seconds;

    [ObservableProperty]
    private double _percentage;

    public string TimeText => TimeSpan.FromSeconds(Seconds).ToString(@"h\h\ m\m");
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private readonly LocalAppMonitorService _monitorService;

    [ObservableProperty]
    private string _totalTimeText = ScreenTimeWin.App.Properties.Resources.Loading;

    [ObservableProperty]
    private string _growthText = string.Format(ScreenTimeWin.App.Properties.Resources.GrowthFromYesterday, "...");

    private long _totalSecondsYesterday; // Store locally for calculation

    [ObservableProperty]
    private string _appSwitchesText = "0";

    [ObservableProperty]
    private string _motivationText = ScreenTimeWin.App.Properties.Resources.MotivationDefault;

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

    // 是否正在加载（用于首次加载指示器）
    [ObservableProperty]
    private bool _isLoading = true;

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

        // REMOVED: Timer polling to avoid conflict with local monitor
        // _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        // _timer.Tick += async (s, e) => await LoadDataAsync();
        // _timer.Start();

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

            // Calculate Growth accurately with live data
            if (_totalSecondsYesterday > 0)
            {
                double growth = ((double)totalSeconds - _totalSecondsYesterday) / _totalSecondsYesterday;
                string sign = growth >= 0 ? "+" : "";
                GrowthText = string.Format(ScreenTimeWin.App.Properties.Resources.GrowthFromYesterday, $"{sign}{growth:P0}");
            }
            else
            {
                GrowthText = string.Format(ScreenTimeWin.App.Properties.Resources.GrowthFromYesterday, "N/A");
            }

            // Update App Switches
            var newSwitchText = _monitorService.GetAppSwitchCount().ToString();
            if (AppSwitchesText != newSwitchText)
                AppSwitchesText = newSwitchText;

            // Update Motivation Text based on live data
            double zScore = (totalSeconds - 7200.0) / 3600.0;
            double percentile = 1.0 / (1.0 + Math.Exp(-1.7 * zScore));
            int beatPercent = (int)(percentile * 100);
            beatPercent = Math.Max(5, Math.Min(99, beatPercent));

            if (totalSeconds < 60)
            {
                MotivationText = ScreenTimeWin.App.Properties.Resources.MotivationStart;
            }
            else
            {
                MotivationText = string.Format(ScreenTimeWin.App.Properties.Resources.MotivationBetterThan, beatPercent);
            }

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
        finally
        {
            // 首次加载完成后隐藏加载指示器
            if (IsLoading) IsLoading = false;
        }
    }

    private void InitializeCharts()
    {
        // 面积图（蓝色渐变）- 初始化为空，等待真实数据
        HourlySeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = Array.Empty<double>(),
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

        // 分类图例等待真实数据加载，不使用硬编码占位数据
        // CategoryLegends 将由 LoadLocalDataAsync() 填充
    }

    /// <summary>
    /// 更新分类图例
    /// </summary>
    /// <summary>
    /// 更新分类图例 (增量更新，避免闪烁)
    /// </summary>
    private void UpdateCategoryLegends(Dictionary<string, long> categoryUsage)
    {
        long totalSeconds = categoryUsage.Values.Sum();
        var sortedUsage = categoryUsage.OrderByDescending(x => x.Value).ToList();

        // 1. Update or Add
        for (int i = 0; i < sortedUsage.Count; i++)
        {
            var kvp = sortedUsage[i];
            var color = GetCategoryColor(kvp.Key);
            var name = Helpers.CategoryHelper.GetLocalizedCategory(kvp.Key);
            var percentage = totalSeconds > 0 ? (double)kvp.Value / totalSeconds : 0;
            var brush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));

            if (brush.CanFreeze) brush.Freeze(); // Optimize brush performance

            if (i < CategoryLegends.Count)
            {
                var item = CategoryLegends[i];
                // Update properties if changed
                if (item.Name != name) item.Name = name;
                if (item.Seconds != kvp.Value) item.Seconds = kvp.Value;
                if (item.Percentage != percentage) item.Percentage = percentage;

                // Brush equality check is tricky, but we can assign it if needed or check equality
                // Simple optimization: only update if color actually changed (unlikely for same category)
                // For now, let's assume color is constant for a category.
            }
            else
            {
                CategoryLegends.Add(new CategoryLegendItem
                {
                    Name = name,
                    Seconds = kvp.Value,
                    Percentage = percentage,
                    ColorBrush = brush
                });
            }
        }

        // 2. Remove excess
        while (CategoryLegends.Count > sortedUsage.Count)
        {
            CategoryLegends.RemoveAt(CategoryLegends.Count - 1);
        }
    }

    /// <summary>
    /// 更新分类饼图数据
    /// </summary>
    /// <summary>
    /// 更新分类饼图数据 (智能更新，重用现有的Series对象)
    /// </summary>
    private void UpdateCategorySeries(Dictionary<string, long> categoryUsage)
    {
        var sortedUsage = categoryUsage.OrderByDescending(x => x.Value).ToList();
        var newSeriesList = new List<ISeries>();
        bool needsReassignment = false;

        // Current series map
        var currentSeriesMap = CategorySeries.OfType<PieSeries<double>>().ToDictionary(s => s.Name ?? "", s => s);

        foreach (var kvp in sortedUsage)
        {
            var localizedName = kvp.Key;
            // NOTE: PieSeries.Name is used for matching. 
            // In InitializeCharts we set Name = kvp.Key (which is English category key). 
            // But in LoadLocalData/LoadData we might be passing English keys. 
            // Ensure we are consistent. Keys in dictionary are English keys (e.g. "Work").

            if (currentSeriesMap.TryGetValue(kvp.Key, out var existingSeries))
            {
                // Update existing
                if (existingSeries.Values is IEnumerable<double> values)
                {
                    var arr = values.ToArray();
                    if (arr.Length > 0 && Math.Abs(arr[0] - kvp.Value) > 0.01)
                    {
                        existingSeries.Values = new double[] { kvp.Value };
                    }
                }
                else
                {
                    existingSeries.Values = new double[] { kvp.Value };
                }

                newSeriesList.Add(existingSeries);
                currentSeriesMap.Remove(kvp.Key);
            }
            else
            {
                // Create new
                var color = GetCategoryColor(kvp.Key);
                newSeriesList.Add(new PieSeries<double>
                {
                    Values = new double[] { kvp.Value },
                    Name = kvp.Key,
                    Fill = new SolidColorPaint(color),
                    InnerRadius = 50,
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {TimeSpan.FromSeconds(point.Coordinate.PrimaryValue).TotalMinutes:F0}m"
                });
                needsReassignment = true;
            }
        }

        // If items left in map, they are removed
        if (currentSeriesMap.Count > 0) needsReassignment = true;

        if (needsReassignment || CategorySeries.Length != newSeriesList.Count)
        {
            CategorySeries = newSeriesList.ToArray();
        }
        else
        {
            // Check order
            bool sequenceChanged = false;
            for (int i = 0; i < CategorySeries.Length; i++)
            {
                if (CategorySeries[i] != newSeriesList[i])
                {
                    sequenceChanged = true;
                    break;
                }
            }

            if (sequenceChanged)
            {
                CategorySeries = newSeriesList.ToArray();
            }
        }
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

    // Chart Selection
    [ObservableProperty]
    private string _selectedChart = "Today";

    [RelayCommand]
    public void SwitchChart(string chartType)
    {
        SelectedChart = chartType;
        // Trigger data update to reflect chart change
        Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    public void NavigateToAppUsage()
    {
        var mainVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(App.Current.Host.Services);
        mainVM.NavigateToAppUsageDetail();
    }

    [RelayCommand]
    public void NavigateToWeeklyReport()
    {
        var mainVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(App.Current.Host.Services);
        mainVM.NavigateToWeeklyReport();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        // One-time load for historical data baseline
        var summary = await _appService.GetTodaySummaryAsync();

        // Cache yesterday's data for local calculation
        _totalSecondsYesterday = summary.TotalSecondsYesterday;

        // Initialize Hourly Chart (Historical/Baseline)
        App.Current.Dispatcher.Invoke(() =>
        {
            // 5. 每小时使用图表
            var hourlyValues = summary.HourlyUsage.Select(x => (double)x / 60.0).ToArray();
            if (HourlySeries.FirstOrDefault() is LineSeries<double> lineSeries)
            {
                lineSeries.Values = hourlyValues;
            }

            // Note: We DO NOT update TopApps or CategorySeries here anymore to avoid conflicts
            // LocalAppMonitorService (via LoadLocalDataAsync) is the single source of truth for live data.
        });

        // Trigger an immediate local update to mix historical + live data
        App.Current.Dispatcher.Invoke(() => LoadLocalDataAsync());
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

    [RelayCommand]
    public void NavigateToAppUsageDetail() => CurrentView = _serviceProvider.GetService(typeof(AppUsageDetailViewModel));

    [RelayCommand]
    public void NavigateToWeeklyReport() => CurrentView = _serviceProvider.GetService(typeof(WeeklyReportViewModel));
}

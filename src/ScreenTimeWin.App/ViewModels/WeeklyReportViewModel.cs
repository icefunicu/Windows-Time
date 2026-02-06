using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ScreenTimeWin.App.ViewModels;

/// <summary>
/// 分类使用条形项
/// </summary>
public partial class CategoryBarItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private double _percentage; // 0-100
    
    [ObservableProperty]
    private Brush _colorBrush = Brushes.Blue;
    
    [ObservableProperty]
    private string _timeText = string.Empty;
}

public partial class WeeklyReportViewModel : ObservableObject
{
    private readonly IAppService _appService;

    /// <summary>
    /// 周选择显示文本
    /// </summary>
    [ObservableProperty]
    private string _weekSelectorText = "This Week";

    /// <summary>
    /// 总屏幕时间
    /// </summary>
    [ObservableProperty]
    private string _totalScreenTimeText = "24h 15m";

    /// <summary>
    /// 环比变化
    /// </summary>
    [ObservableProperty]
    private string _changeText = "+10%";

    [ObservableProperty]
    private bool _isChangePositive = false; // 负增长更好

    /// <summary>
    /// 每日使用柱状图
    /// </summary>
    [ObservableProperty]
    private ISeries[] _dailyUsageSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _dailyXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _dailyYAxes = Array.Empty<Axis>();

    /// <summary>
    /// 分类细分列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CategoryBarItem> _categoryBreakdown = new();

    public WeeklyReportViewModel(IAppService appService)
    {
        _appService = appService;
        InitializeCharts();
        Task.Run(LoadDataAsync);
    }

    private void InitializeCharts()
    {
        // 每日使用柱状图
        var dailyData = new double[] { 3.5, 4.2, 3.8, 5.1, 4.0, 3.2, 4.5 };
        
        DailyUsageSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = dailyData,
                Fill = new SolidColorPaint(new SKColor(66, 133, 244)),
                Stroke = null,
                MaxBarWidth = 30,
                Rx = 4,
                Ry = 4
            }
        };

        DailyXAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Sunday", "1 PM", "2 PM", "3 PM", "4 PM", "5 PM", "Friday" },
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11,
                SeparatorsPaint = null
            }
        };

        DailyYAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 10,
                MinLimit = 0,
                MaxLimit = 60,
                Labeler = val => $"{val / 60:F0}h"
            }
        };
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        // 获取一周的数据
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        
        long totalSeconds = 0;
        var dailyMinutes = new List<double>();
        var categoryTotals = new Dictionary<string, long>();

        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            if (date <= today)
            {
                var summary = await _appService.GetUsageByDateAsync(date);
                totalSeconds += summary.TotalSeconds;
                dailyMinutes.Add(summary.TotalSeconds / 60.0);
                
                foreach (var kvp in summary.CategoryUsage)
                {
                    if (!categoryTotals.ContainsKey(kvp.Key))
                        categoryTotals[kvp.Key] = 0;
                    categoryTotals[kvp.Key] += kvp.Value;
                }
            }
            else
            {
                dailyMinutes.Add(0);
            }
        }

        App.Current.Dispatcher.Invoke(() =>
        {
            // 更新总时间
            var time = TimeSpan.FromSeconds(totalSeconds);
            TotalScreenTimeText = $"{(int)time.TotalHours}h {time.Minutes}m";
            
            // 更新柱状图
            if (DailyUsageSeries.FirstOrDefault() is ColumnSeries<double> columnSeries)
            {
                columnSeries.Values = dailyMinutes.ToArray();
            }
            
            // 更新分类细分
            CategoryBreakdown.Clear();
            var maxSeconds = categoryTotals.Values.DefaultIfEmpty(1).Max();
            
            var categoryColors = new Dictionary<string, Brush>
            {
                { "Work", new SolidColorBrush(Color.FromRgb(66, 133, 244)) },
                { "Productivity", new SolidColorBrush(Color.FromRgb(66, 133, 244)) },
                { "Social", new SolidColorBrush(Color.FromRgb(52, 168, 83)) },
                { "Communication", new SolidColorBrush(Color.FromRgb(52, 168, 83)) },
                { "Entertainment", new SolidColorBrush(Color.FromRgb(234, 67, 53)) },
                { "Media", new SolidColorBrush(Color.FromRgb(234, 67, 53)) },
                { "Games", new SolidColorBrush(Color.FromRgb(234, 67, 53)) },
                { "Learning", new SolidColorBrush(Color.FromRgb(66, 133, 244)) },
                { "Browser", new SolidColorBrush(Color.FromRgb(251, 188, 5)) },
                { "Other", new SolidColorBrush(Color.FromRgb(251, 188, 5)) }
            };
            
            foreach (var kvp in categoryTotals.OrderByDescending(x => x.Value).Take(6))
            {
                var t = TimeSpan.FromSeconds(kvp.Value);
                CategoryBreakdown.Add(new CategoryBarItem
                {
                    Name = kvp.Key,
                    Percentage = (double)kvp.Value / maxSeconds * 100,
                    ColorBrush = categoryColors.GetValueOrDefault(kvp.Key, Brushes.Gray)!,
                    TimeText = $"{(int)t.TotalHours}h {t.Minutes}m"
                });
            }
            
            // 如果没有数据，显示模拟数据
            if (CategoryBreakdown.Count == 0)
            {
                CategoryBreakdown.Add(new CategoryBarItem { Name = "Work", Percentage = 80, ColorBrush = Brushes.DodgerBlue, TimeText = "8h 30m" });
                CategoryBreakdown.Add(new CategoryBarItem { Name = "Social", Percentage = 60, ColorBrush = Brushes.LimeGreen, TimeText = "5h 20m" });
                CategoryBreakdown.Add(new CategoryBarItem { Name = "Entertainment", Percentage = 50, ColorBrush = Brushes.OrangeRed, TimeText = "4h 15m" });
                CategoryBreakdown.Add(new CategoryBarItem { Name = "Learning", Percentage = 40, ColorBrush = Brushes.DodgerBlue, TimeText = "3h 45m" });
                CategoryBreakdown.Add(new CategoryBarItem { Name = "Other", Percentage = 20, ColorBrush = Brushes.Gold, TimeText = "2h 25m" });
            }
        });
    }

    [RelayCommand]
    public void PreviousWeek()
    {
        WeekSelectorText = "Last Week";
        Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    public void NextWeek()
    {
        WeekSelectorText = "This Week";
        Task.Run(LoadDataAsync);
    }
}

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
    private string _weekSelectorText = ScreenTimeWin.App.Properties.Resources.ThisWeek;

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

    #region 趋势分析指标

    /// <summary>
    /// 日均使用时间
    /// </summary>
    [ObservableProperty]
    private string _dailyAverageText = "3h 28m";

    /// <summary>
    /// 峰值日
    /// </summary>
    [ObservableProperty]
    private string _peakDayText = "周五";

    /// <summary>
    /// 峰值日使用时间
    /// </summary>
    [ObservableProperty]
    private string _peakDayTimeText = "5h 12m";

    /// <summary>
    /// 变化趋势颜色（绿色=减少=好，红色=增加）
    /// </summary>
    [ObservableProperty]
    private System.Windows.Media.Brush _changeBrush = System.Windows.Media.Brushes.Green;

    /// <summary>
    /// 最高效日（使用时间最少且在工作日）
    /// </summary>
    [ObservableProperty]
    private string _mostProductiveDayText = "周三";

    #endregion

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
        // 每日使用柱状图 - Initialize empty
        var dailyData = new double[7];

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
                Labels = new[] { "", "", "", "", "", "", "" }, // Will be updated in LoadData
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
                Labeler = val => string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)(val / 60), 0).Replace(" 0m", "") // Simple formatting for axis
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

        var dailyLabels = new List<string>();
        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            dailyLabels.Add(date.ToString("ddd", System.Globalization.CultureInfo.CurrentCulture));
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
            TotalScreenTimeText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)time.TotalHours, time.Minutes);

            // Update X Axis labels
            if (DailyXAxes.FirstOrDefault() is Axis axis)
            {
                axis.Labels = dailyLabels;
            }

            // 计算趋势分析指标
            var daysWithData = dailyMinutes.Where(m => m > 0).ToList();
            if (daysWithData.Count > 0)
            {
                // 日均使用时间
                var avgMinutes = daysWithData.Average();
                var avgTime = TimeSpan.FromMinutes(avgMinutes);
                DailyAverageText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)avgTime.TotalHours, avgTime.Minutes);

                // 峰值日
                var maxIndex = dailyMinutes.IndexOf(dailyMinutes.Max());
                PeakDayText = dailyLabels[maxIndex];
                var peakTime = TimeSpan.FromMinutes(dailyMinutes[maxIndex]);
                PeakDayTimeText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)peakTime.TotalHours, peakTime.Minutes);

                // 最高效日（使用时间最少的工作日，排除周末）
                var workdayMinutes = dailyMinutes.Skip(1).Take(5).ToList(); // 周一到周五
                if (workdayMinutes.Any(m => m > 0))
                {
                    var minWorkday = workdayMinutes.Where(m => m > 0).Min();
                    var minIndex = workdayMinutes.IndexOf(minWorkday) + 1; // +1 因为跳过了周日
                    MostProductiveDayText = dailyLabels[minIndex];
                }
            }

            // 模拟上周数据计算环比变化（实际应该从历史数据获取）
            var lastWeekTotal = totalSeconds * 0.9; // 模拟上周数据
            if (lastWeekTotal > 0)
            {
                var changePercent = ((double)totalSeconds - lastWeekTotal) / lastWeekTotal * 100;
                if (changePercent >= 0)
                {
                    ChangeText = $"+{changePercent:F0}%";
                    ChangeBrush = Brushes.OrangeRed; // 增加是不好的
                    IsChangePositive = false;
                }
                else
                {
                    ChangeText = $"{changePercent:F0}%";
                    ChangeBrush = Brushes.Green; // 减少是好的
                    IsChangePositive = true;
                }
            }

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
                    Name = Helpers.CategoryHelper.GetLocalizedCategory(kvp.Key),
                    Percentage = (double)kvp.Value / maxSeconds * 100,
                    ColorBrush = categoryColors.GetValueOrDefault(kvp.Key, Brushes.Gray)!,
                    TimeText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)t.TotalHours, t.Minutes)
                });
            }

            // 如果没有数据，显示提示或空状态
            if (CategoryBreakdown.Count == 0)
            {
                // Optionally show "No Data" message
            }
        });
    }

    [RelayCommand]
    public void PreviousWeek()
    {
        WeekSelectorText = ScreenTimeWin.App.Properties.Resources.LastWeek;
        Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    public void NextWeek()
    {
        WeekSelectorText = ScreenTimeWin.App.Properties.Resources.ThisWeek;
        Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    public void NavigateToLimits()
    {
        var mainVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(App.Current.Host.Services);
        mainVM.NavigateToLimits();
    }
}

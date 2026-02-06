using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace ScreenTimeWin.App.ViewModels;

/// <summary>
/// 限制类别项模型
/// </summary>
public partial class LimitCategoryItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _limitText = "No Limit";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isWarning; // 快要达到限制

    public string CategoryIcon => Name switch
    {
        "社交应用" => "💬",
        "游戏应用" => "🎮",
        "学习时间" => "📚",
        "娱乐" => "🎬",
        _ => "📱"
    };
}

public partial class LimitsViewModel : ObservableObject
{
    private readonly IAppService _appService;

    /// <summary>
    /// 今日已使用时间文本
    /// </summary>
    [ObservableProperty]
    private string _todayUsedText = "2h 10m";

    /// <summary>
    /// 剩余时间文本
    /// </summary>
    [ObservableProperty]
    private string _remainingText = "1h 20m";

    /// <summary>
    /// 当前最紧迫的限制提示
    /// </summary>
    [ObservableProperty]
    private string _urgentLimitText = "Chrome Limit: 12 mins left";

    /// <summary>
    /// 限制规则列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LimitRuleDto> _rules = new();

    /// <summary>
    /// 分类限制列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LimitCategoryItem> _categoryLimits = new();

    public LimitsViewModel(IAppService appService)
    {
        _appService = appService;

        // 初始化分类限制
        InitializeCategoryLimits();

        Task.Run(LoadRulesAsync);
    }

    private void InitializeCategoryLimits()
    {
        CategoryLimits.Add(new LimitCategoryItem
        {
            Name = "社交应用",
            LimitText = "1小时/天",
            IsActive = false  // 默认关闭
        });
        CategoryLimits.Add(new LimitCategoryItem
        {
            Name = "游戏应用",
            LimitText = "1.5小时/天",
            IsActive = false,  // 默认关闭
            IsWarning = false
        });
        CategoryLimits.Add(new LimitCategoryItem
        {
            Name = "学习时间",
            LimitText = "无限制",
            IsActive = false  // 默认关闭
        });
    }

    [RelayCommand]
    public async Task LoadRulesAsync()
    {
        var rules = await _appService.GetLimitRulesAsync();
        var summary = await _appService.GetTodaySummaryAsync();

        App.Current.Dispatcher.Invoke(() =>
        {
            Rules.Clear();
            foreach (var r in rules) Rules.Add(r);

            // 更新今日使用统计
            var time = TimeSpan.FromSeconds(summary.TotalSeconds);
            TodayUsedText = $"{time.Hours}h {time.Minutes}m";

            // 计算剩余时间（模拟总限额3.5小时）
            var totalLimit = TimeSpan.FromHours(3.5);
            var remaining = totalLimit - time;
            if (remaining.TotalSeconds > 0)
            {
                RemainingText = $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            }
            else
            {
                RemainingText = "0m";
            }
        });
    }

    [RelayCommand]
    public async Task SaveRuleAsync(LimitRuleDto rule)
    {
        if (rule == null) return;
        await _appService.UpsertLimitRuleAsync(rule);
        MessageBox.Show($"规则 {rule.DisplayName} 已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void AddLimit()
    {
        MessageBox.Show("To add a new limit, please go to the Dashboard, click on an app to view details, and set a limit there.", "Add Limit", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ToggleCategoryLimit(LimitCategoryItem item)
    {
        if (item == null) return;
        item.IsActive = !item.IsActive;
    }
}

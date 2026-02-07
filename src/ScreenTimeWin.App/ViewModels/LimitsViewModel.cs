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

    [ObservableProperty]
    private string _key = string.Empty;

    public string CategoryIcon => Key switch
    {
        "Social" => "💬",
        "Games" => "🎮",
        "Learning" => "📚",
        "Entertainment" => "🎬",
        _ => "📱"
    };
}

public partial class LimitsViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private readonly LocalAppMonitorService _monitorService;

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
    private string _urgentLimitText = ScreenTimeWin.App.Properties.Resources.UrgentLimitExample;

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

    public LimitsViewModel(IAppService appService, LocalAppMonitorService monitorService)
    {
        _appService = appService;
        _monitorService = monitorService;

        // 初始化分类限制
        InitializeCategoryLimits();

        Task.Run(LoadRulesAsync);
    }

    private void InitializeCategoryLimits()
    {
        CategoryLimits.Add(new LimitCategoryItem
        {
            Key = "Social",
            Name = ScreenTimeWin.App.Properties.Resources.CategorySocial,
            LimitText = "1h/d",
            IsActive = false  // 默认关闭
        });
        CategoryLimits.Add(new LimitCategoryItem
        {
            Key = "Games",
            Name = ScreenTimeWin.App.Properties.Resources.CategoryGames,
            LimitText = "1.5h/d",
            IsActive = false,  // 默认关闭
            IsWarning = false
        });
        CategoryLimits.Add(new LimitCategoryItem
        {
            Key = "Learning",
            Name = ScreenTimeWin.App.Properties.Resources.CategoryLearning,
            LimitText = ScreenTimeWin.App.Properties.Resources.NoLimit,
            IsActive = false  // 默认关闭
        });
    }

    [RelayCommand]
    public async Task LoadRulesAsync()
    {
        var rules = await _appService.GetLimitRulesAsync();
        var summary = await _appService.GetTodaySummaryAsync();

        // Update local monitor rules
        _monitorService.UpdateRules(rules);

        App.Current.Dispatcher.Invoke(() =>
        {
            Rules.Clear();
            foreach (var r in rules) Rules.Add(r);

            // 更新今日使用统计
            var time = TimeSpan.FromSeconds(summary.TotalSeconds);
            TodayUsedText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, time.Hours, time.Minutes);

            // 计算剩余时间（模拟总限额3.5小时）
            var totalLimit = TimeSpan.FromHours(3.5);
            var remaining = totalLimit - time;
            if (remaining.TotalSeconds > 0)
            {
                RemainingText = string.Format(ScreenTimeWin.App.Properties.Resources.TimeFormatHM, (int)remaining.TotalHours, remaining.Minutes);
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

        // Reload to sync monitor
        await LoadRulesAsync();

        MessageBox.Show(rule.DisplayName + " " + ScreenTimeWin.App.Properties.Resources.SuccessTitle, ScreenTimeWin.App.Properties.Resources.SuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public async Task AddLimit()
    {
        try
        {
            // 获取可用应用列表
            var apps = _monitorService.GetRunningApps();

            // 创建并显示对话框
            var dialog = new Views.AddLimitDialog();
            dialog.SetAvailableApps(apps);
            dialog.Owner = App.Current.MainWindow;

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                // 保存规则
                await _appService.UpsertLimitRuleAsync(dialog.Result);

                // 刷新列表
                await LoadRulesAsync();

                MessageBox.Show(
                    dialog.Result.DisplayName + " " + ScreenTimeWin.App.Properties.Resources.SuccessTitle,
                    ScreenTimeWin.App.Properties.Resources.SuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddLimit error: {ex.Message}");
            MessageBox.Show(
                ScreenTimeWin.App.Properties.Resources.ErrorTitle + ": " + ex.Message,
                ScreenTimeWin.App.Properties.Resources.ErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ToggleCategoryLimit(LimitCategoryItem item)
    {
        if (item == null) return;
        item.IsActive = !item.IsActive;
    }

    [RelayCommand]
    public void NavigateToFocus()
    {
        var mainVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(App.Current.Host.Services);
        mainVM.NavigateToFocus();
    }
}

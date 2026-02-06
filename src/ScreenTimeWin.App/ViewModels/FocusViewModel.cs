using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace ScreenTimeWin.App.ViewModels;

public partial class FocusViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private readonly DispatcherTimer _timer;
    private DateTime? _endTime;
    private DateTime? _startTime;

    [ObservableProperty]
    private int _durationMinutes = 25;

    [ObservableProperty]
    private bool _isFocusActive;

    [ObservableProperty]
    private string _remainingTimeText = "25:00";

    [ObservableProperty]
    private string _focusLabel = "Focus on Coding";

    /// <summary>
    /// 进度百分比 (0-100)，用于圆形进度条
    /// </summary>
    [ObservableProperty]
    private double _progressPercent = 100;

    /// <summary>
    /// 今日完成的专注会话数
    /// </summary>
    [ObservableProperty]
    private int _sessionsCompletedToday = 0;

    /// <summary>
    /// 勿扰模式开关
    /// </summary>
    [ObservableProperty]
    private bool _doNotDisturb = true;

    /// <summary>
    /// 允许的应用列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SelectableAppDto> _allowedApps = new();

    /// <summary>
    /// 被阻止的应用列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SelectableAppDto> _blockedApps = new();

    public FocusViewModel(IAppService appService)
    {
        _appService = appService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateRemainingTime();
        
        // 初始化默认时间显示
        UpdateTimeDisplay(TimeSpan.FromMinutes(DurationMinutes));
        
        Task.Run(LoadAppsAsync);
    }

    [RelayCommand]
    public async Task LoadAppsAsync()
    {
        // 复用 GetLimitRules 获取应用列表
        var apps = await _appService.GetLimitRulesAsync();
        App.Current.Dispatcher.Invoke(() =>
        {
            AllowedApps.Clear();
            BlockedApps.Clear();
            
            // 模拟一些默认的允许/阻止应用
            var allowedNames = new[] { "code", "devenv", "notepad", "spotify" };
            var blockedNames = new[] { "chrome", "steam", "discord" };
            
            foreach (var app in apps)
            {
                var item = new SelectableAppDto 
                { 
                    AppId = app.AppId, 
                    DisplayName = app.DisplayName, 
                    ProcessName = app.ProcessName,
                    IsSelected = true
                };
                
                if (allowedNames.Any(n => app.ProcessName.ToLower().Contains(n)))
                {
                    AllowedApps.Add(item);
                }
                else if (blockedNames.Any(n => app.ProcessName.ToLower().Contains(n)))
                {
                    item.IsSelected = false;
                    BlockedApps.Add(item);
                }
            }
            
            // 如果没有数据，添加一些模拟数据
            if (AllowedApps.Count == 0)
            {
                AllowedApps.Add(new SelectableAppDto { DisplayName = "VS Code", ProcessName = "code", IsSelected = true });
                AllowedApps.Add(new SelectableAppDto { DisplayName = "Notepad", ProcessName = "notepad", IsSelected = true });
                AllowedApps.Add(new SelectableAppDto { DisplayName = "Spotify", ProcessName = "spotify", IsSelected = true });
            }
            if (BlockedApps.Count == 0)
            {
                BlockedApps.Add(new SelectableAppDto { DisplayName = "Social Media", ProcessName = "social", IsSelected = false });
                BlockedApps.Add(new SelectableAppDto { DisplayName = "Games", ProcessName = "games", IsSelected = false });
            }
        });
    }

    [RelayCommand]
    public async Task StartFocusAsync()
    {
        var whitelist = AllowedApps.Where(a => a.IsSelected).Select(a => a.AppId).ToList();
        var request = new StartFocusRequest
        {
            DurationMinutes = DurationMinutes,
            WhitelistAppIds = whitelist
        };

        await _appService.StartFocusAsync(request);
        
        _startTime = DateTime.Now;
        _endTime = DateTime.Now.AddMinutes(DurationMinutes);
        IsFocusActive = true;
        _timer.Start();
        UpdateRemainingTime();
    }

    [RelayCommand]
    public async Task StopFocusAsync()
    {
        await _appService.StopFocusAsync();
        
        // 如果完成了至少一半时间，算作完成一个会话
        if (_startTime.HasValue && _endTime.HasValue)
        {
            var elapsed = DateTime.Now - _startTime.Value;
            var total = _endTime.Value - _startTime.Value;
            if (elapsed.TotalSeconds >= total.TotalSeconds * 0.5)
            {
                SessionsCompletedToday++;
            }
        }
        
        IsFocusActive = false;
        _timer.Stop();
        ProgressPercent = 100;
        UpdateTimeDisplay(TimeSpan.FromMinutes(DurationMinutes));
    }

    /// <summary>
    /// 调整专注时长
    /// </summary>
    [RelayCommand]
    public void AdjustDuration(string adjustment)
    {
        if (IsFocusActive) return; // 进行中不能调整
        
        int delta = int.TryParse(adjustment, out var d) ? d : 0;
        DurationMinutes = Math.Max(5, Math.Min(120, DurationMinutes + delta));
        UpdateTimeDisplay(TimeSpan.FromMinutes(DurationMinutes));
    }

    private void UpdateRemainingTime()
    {
        if (_endTime == null || _startTime == null) return;
        
        var remaining = _endTime.Value - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            // 完成专注会话
            SessionsCompletedToday++;
            StopFocusCommand.Execute(null);
            return;
        }
        
        // 更新进度百分比
        var total = _endTime.Value - _startTime.Value;
        ProgressPercent = (remaining.TotalSeconds / total.TotalSeconds) * 100;
        
        UpdateTimeDisplay(remaining);
    }

    private void UpdateTimeDisplay(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            RemainingTimeText = $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
        else
        {
            RemainingTimeText = $"{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }
}

/// <summary>
/// 可选择的应用DTO
/// </summary>
public class SelectableAppDto : ObservableObject
{
    public Guid AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

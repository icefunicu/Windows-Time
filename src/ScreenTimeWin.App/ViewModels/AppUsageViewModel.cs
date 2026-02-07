using CommunityToolkit.Mvvm.ComponentModel;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Windows.Media;

namespace ScreenTimeWin.App.ViewModels;

public partial class AppUsageViewModel : ObservableObject
{
    private readonly AppUsageDto _dto;

    public string ProcessName => _dto.ProcessName;
    public string DisplayName => _dto.DisplayName;
    public string Category => Helpers.CategoryHelper.GetLocalizedCategory(_dto.Category);

    /// <summary>
    /// 可更新的总秒数，用于增量刷新避免闪烁
    /// </summary>
    [ObservableProperty]
    private long _totalSeconds;

    [ObservableProperty]
    private ImageSource? _icon;

    public AppUsageViewModel(AppUsageDto dto)
    {
        _dto = dto;
        _totalSeconds = dto.TotalSeconds;
        Icon = IconHelper.GetIcon(dto.ProcessName, dto.IconBase64);
    }
}


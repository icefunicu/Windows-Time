using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.Core.Services;
using System.Windows.Media;
using Microsoft.Win32;
using System.Reflection;

namespace ScreenTimeWin.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private readonly IUpdateService _updateService;
    private const string RegistryKeyName = "ScreenTimeWin";

    [ObservableProperty]
    private string _statusText = ScreenTimeWin.App.Properties.Resources.Checking;

    [ObservableProperty]
    private Brush _statusColor = Brushes.Gray;

    [ObservableProperty]
    private string _uptimeText = "";

    [ObservableProperty]
    private bool _isMockModeEnabled;

    [ObservableProperty]
    private bool _isStartWithWindowsEnabled;

    #region 更新检查

    [ObservableProperty]
    private string _updateStatusText = ScreenTimeWin.App.Properties.Resources.UpdateNotChecked;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _latestVersionText = "";

    [ObservableProperty]
    private string _releaseNotes = "";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadProgress;

    private string _downloadUrl = "";

    #endregion

    public SettingsViewModel(IAppService appService)
    {
        _appService = appService;
        _updateService = new GitHubUpdateService();
        IsMockModeEnabled = appService is MockAppService;
        CheckStartWithWindows();
        Task.Run(CheckStatusAsync);
    }

    private void CheckStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            IsStartWithWindowsEnabled = key?.GetValue(RegistryKeyName) != null;
        }
        catch { }
    }

    partial void OnIsStartWithWindowsEnabledChanged(bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (value)
            {
                // Point to the executable
                var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    key?.SetValue(RegistryKeyName, $"\"{path}\"");
                }
            }
            else
            {
                key?.DeleteValue(RegistryKeyName, false);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to update registry: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public void SetPin()
    {
        // Simple Input Dialog (using VB or custom window, for MVP we use custom small window logic or just prompt)
        // Since we don't have a DialogService, let's assume we can't easily pop up a password box here without View code.
        // But we can implement a simple "Change PIN" UI in the SettingsView directly.
        // Let's toggle visibility of a PIN section.
        IsPinChangeVisible = !IsPinChangeVisible;
    }

    [ObservableProperty]
    private bool _isPinChangeVisible;

    [ObservableProperty]
    private string _oldPin = "";

    [ObservableProperty]
    private string _newPin = "";

    [RelayCommand]
    public async Task ConfirmSetPinAsync()
    {
        var success = await _appService.SetPinAsync(OldPin, NewPin);
        if (success)
        {
            System.Windows.MessageBox.Show(ScreenTimeWin.App.Properties.Resources.PinUpdateSuccess, ScreenTimeWin.App.Properties.Resources.SuccessTitle);
            IsPinChangeVisible = false;
            OldPin = "";
            NewPin = "";
        }
        else
        {
            System.Windows.MessageBox.Show(ScreenTimeWin.App.Properties.Resources.PinUpdateError, ScreenTimeWin.App.Properties.Resources.ErrorTitle);
        }
    }

    [RelayCommand]
    public async Task CheckStatusAsync()
    {
        StatusText = ScreenTimeWin.App.Properties.Resources.Checking;
        StatusColor = Brushes.Orange;

        try
        {
            var status = await _appService.PingAsync();
            if (status.Running)
            {
                StatusText = ScreenTimeWin.App.Properties.Resources.Running;
                StatusColor = Brushes.Green;
                UptimeText = string.Format(ScreenTimeWin.App.Properties.Resources.UptimeFormat, status.Uptime.Hours, status.Uptime.Minutes);
            }
            else
            {
                StatusText = ScreenTimeWin.App.Properties.Resources.Stopped;
                StatusColor = Brushes.Red;
                UptimeText = "";
            }
        }
        catch
        {
            StatusText = ScreenTimeWin.App.Properties.Resources.StatusError;
            StatusColor = Brushes.Red;
        }
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        ThemeManager.ToggleTheme();
    }
    [RelayCommand]
    public async Task ClearDataAsync()
    {
        try
        {
            if (System.Windows.MessageBox.Show(ScreenTimeWin.App.Properties.Resources.ClearDataConfirm, ScreenTimeWin.App.Properties.Resources.ClearDataTitle, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
            {
                await _appService.ClearDataAsync();
                System.Windows.MessageBox.Show(ScreenTimeWin.App.Properties.Resources.ClearDataSuccess, ScreenTimeWin.App.Properties.Resources.SuccessTitle);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(ScreenTimeWin.App.Properties.Resources.ClearDataError, ex.Message), ScreenTimeWin.App.Properties.Resources.ErrorTitle);
        }
    }

    #region 更新检查命令

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdate) return;

        IsCheckingUpdate = true;
        UpdateStatusText = ScreenTimeWin.App.Properties.Resources.UpdateChecking;

        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            IsUpdateAvailable = updateInfo.IsUpdateAvailable;

            if (updateInfo.IsUpdateAvailable)
            {
                LatestVersionText = $"v{updateInfo.LatestVersion}";
                ReleaseNotes = updateInfo.ReleaseNotes;
                _downloadUrl = updateInfo.DownloadUrl;
                UpdateStatusText = string.Format(ScreenTimeWin.App.Properties.Resources.UpdateFound, updateInfo.LatestVersion);
            }
            else
            {
                UpdateStatusText = string.Format(ScreenTimeWin.App.Properties.Resources.UpdateLatest, updateInfo.CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = string.Format(ScreenTimeWin.App.Properties.Resources.UpdateError, ex.Message);
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_downloadUrl) || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<int>(p => DownloadProgress = p);
            var installerPath = await _updateService.DownloadUpdateAsync(_downloadUrl, progress);

            var result = System.Windows.MessageBox.Show(
                ScreenTimeWin.App.Properties.Resources.UpdateDownloadComplete,
                ScreenTimeWin.App.Properties.Resources.UpdateInstallTitle,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _updateService.InstallUpdateAsync(installerPath);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(ScreenTimeWin.App.Properties.Resources.UpdateDownloadError, ex.Message), ScreenTimeWin.App.Properties.Resources.ErrorTitle);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    #endregion

    #region 数据导出

    [ObservableProperty]
    private string _selectedExportFormat = "CSV";

    public string[] ExportFormats { get; } = new[] { "CSV", "JSON" };

    [RelayCommand]
    public async Task ExportDataAsync()
    {
        try
        {
            var path = await _appService.ExportDataAsync(SelectedExportFormat);
            if (!string.IsNullOrEmpty(path))
            {
                System.Windows.MessageBox.Show(string.Format(ScreenTimeWin.App.Properties.Resources.ExportSuccess, path), ScreenTimeWin.App.Properties.Resources.ExportSuccessTitle);
            }
            else
            {
                System.Windows.MessageBox.Show(ScreenTimeWin.App.Properties.Resources.ExportFail, ScreenTimeWin.App.Properties.Resources.ErrorTitle);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(ScreenTimeWin.App.Properties.Resources.ExportError, ex.Message), ScreenTimeWin.App.Properties.Resources.ErrorTitle);
        }
    }

    #endregion

    #region 分类规则管理

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<CategoryRuleItem> _categoryRules = new();

    [ObservableProperty]
    private string _selectedCategory = ScreenTimeWin.App.Properties.Resources.CategoryDevelopment;

    [ObservableProperty]
    private string _newProcessName = "";

    [ObservableProperty]
    private bool _isCategoryEditorVisible;

    /// <summary>
    /// 可用分类列表
    /// </summary>
    public string[] AvailableCategories { get; } = new[]
    {
        ScreenTimeWin.App.Properties.Resources.CategoryDevelopment,
        ScreenTimeWin.App.Properties.Resources.CategoryWork,
        ScreenTimeWin.App.Properties.Resources.CategoryBrowser,
        ScreenTimeWin.App.Properties.Resources.CategorySocial,
        ScreenTimeWin.App.Properties.Resources.CategoryEntertainment,
        ScreenTimeWin.App.Properties.Resources.CategoryOther
    };

    [RelayCommand]
    public void ToggleCategoryEditor()
    {
        IsCategoryEditorVisible = !IsCategoryEditorVisible;
        if (IsCategoryEditorVisible && CategoryRules.Count == 0)
        {
            LoadCategoryRulesFromJson();
        }
    }

    /// <summary>
    /// 从 JSON 文件加载分类规则
    /// </summary>
    private void LoadCategoryRulesFromJson()
    {
        try
        {
            var jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app-categories.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var json = System.IO.File.ReadAllText(jsonPath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
                if (dict != null)
                {
                    CategoryRules.Clear();
                    foreach (var kvp in dict)
                    {
                        var category = TranslateCategory(kvp.Key);
                        foreach (var process in kvp.Value)
                        {
                            CategoryRules.Add(new CategoryRuleItem { Category = category, ProcessName = process });
                        }
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 翻译分类名称
    /// </summary>
    private string TranslateCategory(string englishName) => Helpers.CategoryHelper.GetLocalizedCategory(englishName);

    /// <summary>
    /// 反向翻译（中文转英文）
    /// </summary>
    private string ReverseTranslateCategory(string chineseName) => Helpers.CategoryHelper.GetEnglishCategory(chineseName);

    [RelayCommand]
    public void AddProcessToCategory()
    {
        if (string.IsNullOrWhiteSpace(NewProcessName)) return;

        var existing = CategoryRules.FirstOrDefault(r => r.ProcessName.Equals(NewProcessName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            // 更新现有规则的分类
            existing.Category = SelectedCategory;
        }
        else
        {
            CategoryRules.Add(new CategoryRuleItem { Category = SelectedCategory, ProcessName = NewProcessName.ToLower() });
        }

        NewProcessName = "";
        SaveCategoryRulesToJson();
    }

    [RelayCommand]
    public void RemoveProcessFromCategory(CategoryRuleItem item)
    {
        CategoryRules.Remove(item);
        SaveCategoryRulesToJson();
    }

    /// <summary>
    /// 保存分类规则到 JSON 文件
    /// </summary>
    private void SaveCategoryRulesToJson()
    {
        try
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var rule in CategoryRules)
            {
                var englishCategory = ReverseTranslateCategory(rule.Category);
                if (!dict.ContainsKey(englishCategory))
                    dict[englishCategory] = new List<string>();
                dict[englishCategory].Add(rule.ProcessName);
            }

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(dict, options);
            var jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app-categories.json");
            System.IO.File.WriteAllText(jsonPath, json);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(ScreenTimeWin.App.Properties.Resources.SaveCategoryError, ex.Message), ScreenTimeWin.App.Properties.Resources.ErrorTitle);
        }
    }

    #endregion
}

/// <summary>
/// 分类规则项模型
/// </summary>
public partial class CategoryRuleItem : ObservableObject
{
    [ObservableProperty]
    private string _category = "";

    [ObservableProperty]
    private string _processName = "";
}

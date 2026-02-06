using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTimeWin.App.Services;
using System.Windows.Media;
using Microsoft.Win32;
using System.Reflection;

namespace ScreenTimeWin.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppService _appService;
    private const string RegistryKeyName = "ScreenTimeWin";

    [ObservableProperty]
    private string _statusText = "Checking...";

    [ObservableProperty]
    private Brush _statusColor = Brushes.Gray;

    [ObservableProperty]
    private string _uptimeText = "";

    [ObservableProperty]
    private bool _isMockModeEnabled;

    [ObservableProperty]
    private bool _isStartWithWindowsEnabled;

    public SettingsViewModel(IAppService appService)
    {
        _appService = appService;
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
            System.Windows.MessageBox.Show("PIN updated successfully.", "Success");
            IsPinChangeVisible = false;
            OldPin = "";
            NewPin = "";
        }
        else
        {
            System.Windows.MessageBox.Show("Failed to update PIN. Check old PIN.", "Error");
        }
    }

    [RelayCommand]
    public async Task CheckStatusAsync()
    {
        StatusText = "Checking...";
        StatusColor = Brushes.Orange;

        try
        {
            var status = await _appService.PingAsync();
            if (status.Running)
            {
                StatusText = "Running";
                StatusColor = Brushes.Green;
                UptimeText = $"Uptime: {status.Uptime.Hours}h {status.Uptime.Minutes}m";
            }
            else
            {
                StatusText = "Stopped / Not Reachable";
                StatusColor = Brushes.Red;
                UptimeText = "";
            }
        }
        catch
        {
            StatusText = "Error";
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
            if (System.Windows.MessageBox.Show("Are you sure you want to clear all usage data? This cannot be undone.", "Confirm Clear", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
            {
                await _appService.ClearDataAsync();
                System.Windows.MessageBox.Show("Data cleared successfully.", "Success");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to clear data: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task ExportDataAsync()
    {
        try 
        {
            var path = await _appService.ExportDataAsync();
            if (!string.IsNullOrEmpty(path))
            {
                System.Windows.MessageBox.Show($"Data exported to:\n{path}", "Export Success");
                // Optional: Open folder
                // Process.Start("explorer.exe", "/select," + path);
            }
            else
            {
                System.Windows.MessageBox.Show("Export failed or returned empty path.", "Error");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Export error: {ex.Message}", "Error");
        }
    }
}

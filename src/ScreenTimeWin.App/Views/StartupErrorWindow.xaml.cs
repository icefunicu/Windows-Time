using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ScreenTimeWin.App.Views;

public partial class StartupErrorWindow : Window
{
    private readonly string _errorDetail;

    public StartupErrorWindow(string errorDetail)
    {
        InitializeComponent();
        _errorDetail = errorDetail;
        ErrorTextBox.Text = errorDetail;
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTimeWin", "Logs");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Process.Start("explorer.exe", folder);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open logs folder: {ex.Message}");
        }
    }

    private void CopyError_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_errorDetail);
            MessageBox.Show("Error details copied to clipboard.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy: {ex.Message}");
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

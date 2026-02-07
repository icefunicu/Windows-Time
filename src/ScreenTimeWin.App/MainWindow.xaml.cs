using Hardcodet.Wpf.TaskbarNotification;
using ScreenTimeWin.App.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Drawing; // Requires System.Drawing.Common or use an icon file resource

namespace ScreenTimeWin.App;

public partial class MainWindow : Window
{
    private TaskbarIcon? _taskbarIcon;

    public MainWindow()
    {
        InitializeComponent();

        // Try load icon from file system
        try
        {
            if (System.IO.File.Exists("app.ico"))
            {
                var iconUri = new Uri(System.IO.Path.GetFullPath("app.ico"));
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            }
        }
        catch { }

        InitializeTaskbarIcon();
    }

    private void InitializeTaskbarIcon()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "ScreenTimeWin",
            MenuActivation = PopupActivationMode.RightClick
        };

        try
        {
            if (System.IO.File.Exists("app.ico"))
            {
                _taskbarIcon.Icon = new System.Drawing.Icon("app.ico");
            }
            else
            {
                _taskbarIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _taskbarIcon.Icon = SystemIcons.Application;
        }

        // Context Menu
        var contextMenu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open Dashboard" };
        openItem.Click += (s, e) => ShowWindow();
        contextMenu.Items.Add(openItem);

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) =>
        {
            _taskbarIcon?.Dispose();
            Application.Current.Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            this.DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (button != null) button.Content = "☐"; // 最大化图标
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (button != null) button.Content = "❐"; // 还原图标
        }
    }

    /// <summary>
    /// 处理滚轮事件，确保 ScrollViewer 能够正确响应滚轮
    /// </summary>
    private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}

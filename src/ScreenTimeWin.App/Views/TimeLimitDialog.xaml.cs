using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTimeWin.App.Views
{
    /// <summary>
    /// TimeLimitDialog.xaml 的交互逻辑 - "Time's Up!" 限时到达弹窗
    /// </summary>
    public partial class TimeLimitDialog : Window
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public Guid AppId { get; set; }
        
        /// <summary>
        /// 用户选择的操作结果
        /// </summary>
        public TimeLimitAction SelectedAction { get; private set; } = TimeLimitAction.CloseApp;

        public TimeLimitDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置弹窗信息
        /// </summary>
        public void SetAppInfo(string appName, BitmapSource? icon = null)
        {
            AppNameRun.Text = appName;
            if (icon != null)
            {
                AppIcon.Source = icon;
            }
        }

        private void CloseAppButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = TimeLimitAction.CloseApp;
            DialogResult = true;
            Close();
        }

        private void MoreTimeButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = TimeLimitAction.MoreTime;
            DialogResult = true;
            Close();
        }

        private void RequestUnlockButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = TimeLimitAction.RequestUnlock;
            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// 时间限制弹窗操作枚举
    /// </summary>
    public enum TimeLimitAction
    {
        CloseApp,
        MoreTime,
        RequestUnlock
    }
}

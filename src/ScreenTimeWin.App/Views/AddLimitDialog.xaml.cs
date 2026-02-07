using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Windows;
using System.Windows.Controls;

namespace ScreenTimeWin.App.Views
{
    /// <summary>
    /// AddLimitDialog.xaml 的交互逻辑 - 添加限制规则对话框
    /// </summary>
    public partial class AddLimitDialog : Window
    {
        /// <summary>
        /// 创建的限制规则结果
        /// </summary>
        public LimitRuleDto? Result { get; private set; }

        /// <summary>
        /// 可用应用列表
        /// </summary>
        private List<TrackedApp> _availableApps = new();

        public AddLimitDialog()
        {
            InitializeComponent();
            InitializeTimeComboBoxes();

            // 监听宵禁开关变化
            CurfewToggle.Checked += (s, e) => CurfewTimePanel.Visibility = Visibility.Visible;
            CurfewToggle.Unchecked += (s, e) => CurfewTimePanel.Visibility = Visibility.Collapsed;

            // 更新滑块显示
            UpdateLimitValueText();
        }

        /// <summary>
        /// 设置可选的应用列表
        /// </summary>
        public void SetAvailableApps(IEnumerable<TrackedApp> apps)
        {
            _availableApps = apps.ToList();
            AppComboBox.ItemsSource = _availableApps;
            if (_availableApps.Count > 0)
            {
                AppComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 初始化时间选择器
        /// </summary>
        private void InitializeTimeComboBoxes()
        {
            var times = new List<string>();
            for (int h = 0; h < 24; h++)
            {
                times.Add($"{h:D2}:00");
                times.Add($"{h:D2}:30");
            }

            CurfewStartCombo.ItemsSource = times;
            CurfewEndCombo.ItemsSource = times;

            // 默认宵禁时间 22:00 - 07:00
            CurfewStartCombo.SelectedItem = "22:00";
            CurfewEndCombo.SelectedItem = "07:00";
        }

        private void LimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateLimitValueText();
        }

        private void UpdateLimitValueText()
        {
            if (LimitValueText == null) return;

            int minutes = (int)LimitSlider.Value;
            int hours = minutes / 60;
            int mins = minutes % 60;

            if (hours > 0)
            {
                LimitValueText.Text = $"{hours}h {mins}m";
            }
            else
            {
                LimitValueText.Text = $"{mins}m";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedApp = AppComboBox.SelectedItem as TrackedApp;
            if (selectedApp == null)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectApp, Properties.Resources.ErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rule = new LimitRuleDto
            {
                AppId = Guid.NewGuid(), // 新规则生成新ID
                ProcessName = selectedApp.ProcessName,
                DisplayName = selectedApp.DisplayName,
                DailyLimitMinutes = (int)LimitSlider.Value,
                Enabled = true,
                ActionOnLimit = "NotifyOnly"
            };

            // 如果启用宵禁模式
            if (CurfewToggle.IsChecked == true)
            {
                if (CurfewStartCombo.SelectedItem is string startStr &&
                    CurfewEndCombo.SelectedItem is string endStr)
                {
                    if (TimeSpan.TryParse(startStr, out var start))
                        rule.CurfewStartLocal = start;
                    if (TimeSpan.TryParse(endStr, out var end))
                        rule.CurfewEndLocal = end;
                }
            }

            Result = rule;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

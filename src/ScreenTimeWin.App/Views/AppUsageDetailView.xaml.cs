using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenTimeWin.App.Views;

/// <summary>
/// App Usage 详情视图代码后置
/// </summary>
public partial class AppUsageDetailView : UserControl
{
    public AppUsageDetailView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击遮罩层关闭弹窗
    /// </summary>
    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.AppUsageDetailViewModel vm)
        {
            vm.CloseDetailPopupCommand.Execute(null);
        }
    }
}

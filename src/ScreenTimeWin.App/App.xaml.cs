using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.App.ViewModels;
using ScreenTimeWin.App.Views;
using System.Windows;

namespace ScreenTimeWin.App;

public partial class App : Application
{
    public IHost Host { get; private set; }

    /// <summary>
    /// 本地应用监控服务（全局单例）
    /// </summary>
    public LocalAppMonitorService MonitorService { get; } = new();

    public App()
    {
        try
        {
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
               .ConfigureServices((context, services) =>
               {
                   // 注册本地监控服务为单例
                   services.AddSingleton(MonitorService);

                   // 其他服务
                   services.AddSingleton<IAppService, AppService>();
                   services.AddSingleton<MainWindow>();
                   services.AddSingleton<MainViewModel>();
                   services.AddTransient<DashboardViewModel>();
                   services.AddTransient<AnalyticsViewModel>();
                   services.AddTransient<LimitsViewModel>();
                   services.AddTransient<FocusViewModel>();
                   services.AddTransient<SettingsViewModel>();
                   services.AddTransient<WeeklyReportViewModel>();
                   services.AddTransient<AppUsageDetailViewModel>();
               })
               .Build();
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("app_ctor_error.txt", ex.ToString());
            throw;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Temp: Generate Icon if missing
        try
        {
            if (!System.IO.File.Exists("app.ico"))
            {
                IconGenerator.Program.MainGen();
            }
        }
        catch { }

        try
        {
            await Host.StartAsync();

            // 启动本地应用监控
            MonitorService.Start(2000); // 每2秒扫描一次

            var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Host.Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("startup_error.txt", ex.ToString());
            MessageBox.Show($"Startup Error: {ex.Message}");
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // 停止监控服务
        MonitorService.Stop();
        MonitorService.Dispose();

        await Host.StopAsync();
        base.OnExit(e);
    }

    public new static App Current => (App)Application.Current;
}


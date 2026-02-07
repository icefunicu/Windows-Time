using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.App.ViewModels;
using ScreenTimeWin.Core.Services;
using ScreenTimeWin.App.Views;
using Serilog;
using System.Windows;

namespace ScreenTimeWin.App;

public partial class App : Application
{
    public IHost Host { get; private set; }

    /// <summary>
    /// 本地应用监控服务（全局单例） - initialized in ConfigureServices now
    /// </summary>
    public LocalAppMonitorService MonitorService { get; private set; }

    public App()
    {
        // Initialize Serilog first
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logPath = System.IO.Path.Combine(localData, "ScreenTimeWin", "Logs", "log-.txt");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
               .UseSerilog()
               .ConfigureServices((context, services) =>
               {
                   // Data Layer
                   services.AddDbContextFactory<ScreenTimeWin.Data.ScreenTimeDbContext>();
                   services.AddSingleton<ScreenTimeWin.Data.DataRepository>();

                   // 注册本地监控服务为单例
                   services.AddSingleton<LocalAppMonitorService>();

                   // Telemetry
                   services.AddSingleton<ITelemetryService, DebugTelemetryService>();

                   // 其他服务
                   // Switch to Embedded AppService for standalone "Real Data" mode
                   services.AddSingleton<IAppService, EmbeddedAppService>(); 
                   
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
               
            MonitorService = Host.Services.GetRequiredService<LocalAppMonitorService>();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed (Constructor)");
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

            // Ensure DB is created
            var repo = Host.Services.GetRequiredService<ScreenTimeWin.Data.DataRepository>();
            await repo.EnsureCreatedAsync();

            // 启动本地应用监控
            MonitorService.Start(2000); // 每2秒扫描一次

            // Wire up Limit Enforcement
            var appService = Host.Services.GetRequiredService<IAppService>();
            try
            {
                var rules = await appService.GetLimitRulesAsync();
                MonitorService.UpdateRules(rules);
            }
            catch { /* Ignore if service not ready */ }

            MonitorService.LimitReached += (s, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new TimeLimitDialog();
                    // Convert Base64 icon to BitmapSource if needed, or just set name
                    System.Windows.Media.Imaging.BitmapSource? icon = null;
                    if (!string.IsNullOrEmpty(args.IconBase64))
                    {
                        try {
                            var bytes = Convert.FromBase64String(args.IconBase64);
                            using var stream = new System.IO.MemoryStream(bytes);
                            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                            icon = decoder.Frames[0];
                        } catch { }
                    }

                    dialog.SetAppInfo(args.AppName, icon);
                    
                    // Show top-most overlay
                    dialog.Topmost = true;
                    if (dialog.ShowDialog() == true)
                    {
                        if (dialog.SelectedAction == TimeLimitAction.CloseApp)
                        {
                            try
                            {
                                System.Diagnostics.Process.GetProcessById(args.ProcessId).Kill();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to kill process {Pid}", args.ProcessId);
                            }
                        }
                        else if (dialog.SelectedAction == TimeLimitAction.MoreTime)
                        {
                            MonitorService.ExtendLimit(args.ProcessName, 15); // 15 mins extra
                        }
                        else if (dialog.SelectedAction == TimeLimitAction.RequestUnlock)
                        {
                            // Just extend for now in prototype, or show message
                            MessageBox.Show("Unlock request sent to admin.", "Request Sent");
                        }
                    }
                });
            };

            var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Host.Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            // Ensure we are on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var errorWindow = new StartupErrorWindow(ex.ToString());
                errorWindow.ShowDialog();
            });
            Log.Fatal(ex, "Application start-up failed (OnStartup)");
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // 停止监控服务
        MonitorService.Stop();
        MonitorService.Dispose();

        await Host.StopAsync();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    public new static App Current => (App)Application.Current;
}


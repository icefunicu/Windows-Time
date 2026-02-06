using Microsoft.EntityFrameworkCore;
using ScreenTimeWin.Data;
using Serilog;
using System.Runtime.Versioning;

namespace ScreenTimeWin.Service;

[SupportedOSPlatform("windows")]
public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/service.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "ScreenTimeWin Service";
            });

            builder.Services.AddSerilog();
            
            builder.Services.AddDbContextFactory<ScreenTimeDbContext>(options =>
            {
                var folder = Environment.SpecialFolder.LocalApplicationData;
                var path = Environment.GetFolderPath(folder);
                var dbFolder = System.IO.Path.Join(path, "ScreenTimeWin");
                System.IO.Directory.CreateDirectory(dbFolder);
                var dbPath = System.IO.Path.Join(dbFolder, "ScreenTimeWin.db");
                options.UseSqlite($"Data Source={dbPath}");
            });

            builder.Services.AddSingleton<DataRepository>();
            builder.Services.AddSingleton<FocusManager>();
            builder.Services.AddSingleton<NotificationQueue>();
            builder.Services.AddSingleton<CurrentSessionState>();
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddHostedService<IpcServer>();

            var host = builder.Build();
            host.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Service terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

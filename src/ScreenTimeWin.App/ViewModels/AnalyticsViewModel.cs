using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using ScreenTimeWin.App.Services;
using ScreenTimeWin.IPC.Models;
using System.Collections.ObjectModel;

namespace ScreenTimeWin.App.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    private readonly IAppService _appService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private ObservableCollection<AppUsageDto> _topApps = new();

    [ObservableProperty]
    private ISeries[] _hourlySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _hourlyXAxes = new Axis[] { new Axis { Labels = Enumerable.Range(0, 24).Select(i => $"{i}:00").ToList() } };

    [ObservableProperty]
    private ISeries[] _categorySeries = Array.Empty<ISeries>();

    public AnalyticsViewModel(IAppService appService)
    {
        _appService = appService;
        Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var data = await _appService.GetUsageByDateAsync(SelectedDate);

        App.Current.Dispatcher.Invoke(() =>
        {
            TopApps.Clear();
            foreach (var app in data.TopApps) TopApps.Add(app);

            HourlySeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = data.HourlyUsage.Select(x => (double)x).ToArray(),
                    Name = Properties.Resources.Seconds
                }
            };

            // Category Pie Chart
            var categories = data.TopApps
                .GroupBy(a => a.Category ?? "Uncategorized")
                .Select(g => new PieSeries<double>
                {
                    Values = new double[] { g.Sum(a => a.TotalSeconds) },
                    Name = Helpers.CategoryHelper.GetLocalizedCategory(g.Key),
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {TimeSpan.FromSeconds(point.Coordinate.PrimaryValue).TotalMinutes:F0}m"
                })
                .ToArray();

            CategorySeries = categories;
        });
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        LoadDataCommand.Execute(null);
    }
}

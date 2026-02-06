using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Data;
using ScreenTimeWin.IPC.Models;

namespace ScreenTimeWin.Service;

public class IpcServer : BackgroundService
{
    private readonly ILogger<IpcServer> _logger;
    private readonly DataRepository _repository;
    private readonly FocusManager _focusManager;
    private readonly NotificationQueue _notificationQueue;
    private readonly CurrentSessionState _currentSessionState;
    private const string PipeName = "ScreenTimeWinPipe";

    public IpcServer(ILogger<IpcServer> logger, DataRepository repository, FocusManager focusManager, NotificationQueue notificationQueue, CurrentSessionState currentSessionState)
    {
        _logger = logger;
        _repository = repository;
        _focusManager = focusManager;
        _notificationQueue = notificationQueue;
        _currentSessionState = currentSessionState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(stoppingToken);

                // Read length
                var lengthBytes = new byte[4];
                await ReadExactAsync(server, lengthBytes, 4, stoppingToken);
                var length = BitConverter.ToInt32(lengthBytes, 0);

                // Read request
                var requestBytes = new byte[length];
                await ReadExactAsync(server, requestBytes, length, stoppingToken);
                var requestJson = Encoding.UTF8.GetString(requestBytes);
                var request = JsonSerializer.Deserialize<IpcRequest>(requestJson);

                if (request != null)
                {
                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response);
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    var respLengthBytes = BitConverter.GetBytes(responseBytes.Length);

                    await server.WriteAsync(respLengthBytes, 0, 4, stoppingToken);
                    await server.WriteAsync(responseBytes, 0, responseBytes.Length, stoppingToken);
                    await server.FlushAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IPC Server");
            }
        }
    }

    private async Task<IpcResponse> HandleRequestAsync(IpcRequest request)
    {
        var response = new IpcResponse { RequestId = request.RequestId, Success = true };

        try
        {
            switch (request.Action)
            {
                case IpcActions.Ping:
                    response.DataJson = JsonSerializer.Serialize(new PingResponse { Running = true, Uptime = TimeSpan.FromMinutes(10) });
                    break;

                case IpcActions.GetTodaySummary:
                    var now = DateTime.Now;

                    // 1. Fetch DB Data
                    var aggs = await _repository.GetAggregatesByDateAsync(now);
                    var hourlyUsage = await _repository.GetHourlyUsageAsync(now);
                    var categoryUsage = await _repository.GetCategoryUsageAsync(now);
                    var totalSecondsYesterday = await _repository.GetTotalSecondsByDateAsync(now.AddDays(-1));
                    var appSwitches = await _repository.GetAppSwitchesCountAsync(now);

                    // 2. Merge Active Sessions (Real-time)
                    var currentSessions = _currentSessionState.ActiveSessions.Values.ToList();

                    foreach (var current in currentSessions)
                    {
                        if (current != null && current.StartUtc.ToLocalTime().Date == now.Date)
                        {
                            // Duration so far
                            var duration = (long)(DateTime.UtcNow - current.StartUtc).TotalSeconds;
                            if (duration > 0)
                            {
                                // A. Update Aggregates (Top Apps)
                                var agg = aggs.FirstOrDefault(a => a.AppId == current.AppId);
                                if (agg != null)
                                {
                                    agg.TotalSeconds += (int)duration;
                                }
                                else
                                {
                                    aggs.Add(new DailyAggregate
                                    {
                                        AppId = current.AppId,
                                        App = current.App,
                                        TotalSeconds = (int)duration,
                                        DateLocal = now.ToString("yyyy-MM-dd")
                                    });
                                }

                                // B. Update Hourly Usage (Add to current hour)
                                int currentHour = now.Hour;
                                if (hourlyUsage.ContainsKey(currentHour))
                                {
                                    hourlyUsage[currentHour] += duration;
                                }
                                else
                                {
                                    hourlyUsage[currentHour] = duration;
                                }

                                // C. Update Category Usage
                                var cat = current.App?.Category ?? "Uncategorized";
                                if (categoryUsage.ContainsKey(cat))
                                {
                                    categoryUsage[cat] += duration;
                                }
                                else
                                {
                                    categoryUsage[cat] = duration;
                                }
                            }
                        }
                    }

                    // 3. Construct Response
                    var summary = new TodaySummaryResponse
                    {
                        TotalSeconds = aggs.Sum(a => (long)a.TotalSeconds),
                        TotalSecondsYesterday = totalSecondsYesterday,
                        AppSwitches = appSwitches,

                        TopApps = aggs.GroupBy(a => a.AppId)
                            .Select(g => new AppUsageDto
                            {
                                AppId = g.Key,
                                ProcessName = g.First().App?.ProcessName ?? "",
                                DisplayName = g.First().App?.DisplayName ?? "",
                                Category = g.First().App?.Category ?? "Uncategorized",
                                IconBase64 = g.First().App?.IconBase64 ?? "",
                                TotalSeconds = g.Sum(x => x.TotalSeconds)
                            })
                            .OrderByDescending(a => a.TotalSeconds)
                            .ToList(),

                        HourlyUsage = hourlyUsage.OrderBy(k => k.Key).Select(k => k.Value).ToList(),
                        CategoryUsage = categoryUsage
                    };
                    response.DataJson = JsonSerializer.Serialize(summary);
                    break;

                case IpcActions.GetUsageByDate:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var reqDto = JsonSerializer.Deserialize<UsageByDateRequest>(request.PayloadJson);
                        if (reqDto != null)
                        {
                            var dateAggs = await _repository.GetAggregatesByDateAsync(reqDto.DateLocal);
                            // Simple hourly distribution not implemented in DB yet, returning empty for MVP or mock it
                            var result = new TodaySummaryResponse
                            {
                                TotalSeconds = dateAggs.Sum(a => a.TotalSeconds),
                                TopApps = dateAggs.OrderByDescending(a => a.TotalSeconds).Select(a => new AppUsageDto
                                {
                                    AppId = a.AppId,
                                    ProcessName = a.App?.ProcessName ?? "",
                                    DisplayName = a.App?.DisplayName ?? "",
                                    TotalSeconds = a.TotalSeconds
                                }).ToList(),
                                HourlyUsage = new List<long>(new long[24]) // Placeholder
                            };
                            response.DataJson = JsonSerializer.Serialize(result);
                        }
                    }
                    break;

                case IpcActions.GetLimitRules:
                    var rules = await _repository.GetRulesAsync();
                    var allApps = await _repository.GetAllAppsAsync();

                    // Merge rules with apps
                    var ruleDtos = allApps.Select(app =>
                    {
                        var r = rules.FirstOrDefault(x => x.AppId == app.Id);
                        return new LimitRuleDto
                        {
                            AppId = app.Id,
                            DisplayName = app.DisplayName, // Add DisplayName to DTO if needed, currently DTO doesn't have it, let's assume UI fetches apps or we add it
                            ProcessName = app.ProcessName,
                            DailyLimitMinutes = r?.DailyLimitMinutes,
                            Enabled = r?.Enabled ?? false,
                            ActionOnLimit = r?.ActionOnLimit.ToString() ?? "NotifyOnly"
                        };
                    }).ToList();

                    response.DataJson = JsonSerializer.Serialize(ruleDtos);
                    break;

                case IpcActions.UpsertLimitRule:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var ruleDto = JsonSerializer.Deserialize<LimitRuleDto>(request.PayloadJson);
                        if (ruleDto != null)
                        {
                            var rule = new LimitRule
                            {
                                AppId = ruleDto.AppId,
                                DailyLimitMinutes = ruleDto.DailyLimitMinutes,
                                Enabled = ruleDto.Enabled,
                                ActionOnLimit = Enum.TryParse<Core.Models.ActionOnLimit>(ruleDto.ActionOnLimit, out var action) ? action : Core.Models.ActionOnLimit.NotifyOnly
                            };
                            await _repository.UpsertRuleAsync(rule);
                        }
                    }
                    break;

                case IpcActions.StartFocus:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var focusDto = JsonSerializer.Deserialize<StartFocusRequest>(request.PayloadJson);
                        if (focusDto != null)
                        {
                            _focusManager.StartFocus(focusDto.DurationMinutes, focusDto.WhitelistAppIds);
                        }
                    }
                    break;

                case IpcActions.StopFocus:
                    _focusManager.StopFocus();
                    break;

                case IpcActions.ClearData:
                    await _repository.ClearDataAsync();
                    break;

                case IpcActions.ExportData:
                    var path = await _repository.ExportDataAsync("csv");
                    response.DataJson = JsonSerializer.Serialize(path);
                    break;

                case IpcActions.GetNotifications:
                    var notifications = _notificationQueue.DequeueAll();
                    response.DataJson = JsonSerializer.Serialize(notifications);
                    break;

                case IpcActions.VerifyPin:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var req = JsonSerializer.Deserialize<PinRequest>(request.PayloadJson);
                        var isValid = await _repository.VerifyPinAsync(req?.Pin ?? "");
                        response.DataJson = JsonSerializer.Serialize(isValid);
                    }
                    break;

                case IpcActions.SetPin:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var req = JsonSerializer.Deserialize<SetPinRequest>(request.PayloadJson);
                        var success = await _repository.SetPinAsync(req?.OldPin ?? "", req?.NewPin ?? "");
                        response.DataJson = JsonSerializer.Serialize(success);
                    }
                    break;

                case IpcActions.GetWeeklySummary:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var weekReq = JsonSerializer.Deserialize<WeeklySummaryRequest>(request.PayloadJson);
                        if (weekReq != null)
                        {
                            var weekStart = weekReq.WeekStartDate;
                            var dailyUsage = new List<long>();
                            var weekCategoryUsage = new Dictionary<string, long>();
                            long weekTotalSeconds = 0;

                            // 获取一周的数据
                            for (int i = 0; i < 7; i++)
                            {
                                var date = weekStart.AddDays(i);
                                var dayAggs = await _repository.GetAggregatesByDateAsync(date);
                                var dayTotal = dayAggs.Sum(a => (long)a.TotalSeconds);
                                dailyUsage.Add(dayTotal);
                                weekTotalSeconds += dayTotal;

                                // 累加分类使用
                                foreach (var agg in dayAggs)
                                {
                                    var cat = agg.App?.Category ?? "Uncategorized";
                                    if (!weekCategoryUsage.ContainsKey(cat))
                                        weekCategoryUsage[cat] = 0;
                                    weekCategoryUsage[cat] += agg.TotalSeconds;
                                }
                            }

                            // 获取上周数据用于比较
                            var lastWeekStart = weekStart.AddDays(-7);
                            long lastWeekTotalSeconds = 0;
                            for (int i = 0; i < 7; i++)
                            {
                                var date = lastWeekStart.AddDays(i);
                                var dayAggs = await _repository.GetAggregatesByDateAsync(date);
                                lastWeekTotalSeconds += dayAggs.Sum(a => (long)a.TotalSeconds);
                            }

                            double changePercent = lastWeekTotalSeconds > 0
                                ? ((double)weekTotalSeconds - lastWeekTotalSeconds) / lastWeekTotalSeconds * 100
                                : 0;

                            var weekSummary = new WeeklySummaryResponse
                            {
                                TotalSeconds = weekTotalSeconds,
                                TotalSecondsLastWeek = lastWeekTotalSeconds,
                                ChangePercent = changePercent,
                                DailyUsage = dailyUsage,
                                CategoryUsage = weekCategoryUsage
                            };
                            response.DataJson = JsonSerializer.Serialize(weekSummary);
                        }
                    }
                    break;

                case IpcActions.GetAppDetails:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var appReq = JsonSerializer.Deserialize<AppDetailsRequest>(request.PayloadJson);
                        if (appReq != null)
                        {
                            var app = await _repository.GetAppByIdAsync(appReq.AppId);
                            var todayAgg = await _repository.GetAggregatesByDateAsync(DateTime.Today);
                            var todaySeconds = todayAgg.Where(a => a.AppId == appReq.AppId).Sum(a => (long)a.TotalSeconds);

                            // 7日平均
                            long weekTotal = 0;
                            for (int i = 0; i < 7; i++)
                            {
                                var date = DateTime.Today.AddDays(-i);
                                var dayAggs = await _repository.GetAggregatesByDateAsync(date);
                                weekTotal += dayAggs.Where(a => a.AppId == appReq.AppId).Sum(a => (long)a.TotalSeconds);
                            }

                            var rule = (await _repository.GetRulesAsync()).FirstOrDefault(r => r.AppId == appReq.AppId);

                            var appDetails = new AppDetailsResponse
                            {
                                AppId = appReq.AppId,
                                ProcessName = app?.ProcessName ?? "",
                                DisplayName = app?.DisplayName ?? "",
                                Category = app?.Category ?? "Uncategorized",
                                IconBase64 = app?.IconBase64 ?? "",
                                TodaySeconds = todaySeconds,
                                SevenDayAverageSeconds = weekTotal / 7,
                                WeekTotalSeconds = weekTotal,
                                LimitRule = rule != null ? new LimitRuleDto
                                {
                                    AppId = rule.AppId,
                                    DailyLimitMinutes = rule.DailyLimitMinutes,
                                    Enabled = rule.Enabled,
                                    ActionOnLimit = rule.ActionOnLimit.ToString()
                                } : null
                            };
                            response.DataJson = JsonSerializer.Serialize(appDetails);
                        }
                    }
                    break;

                case IpcActions.GetFocusStatus:
                    var focusStatus = new FocusStatusResponse
                    {
                        IsActive = _focusManager.IsFocusActive,
                        StartTime = _focusManager.FocusStartTime,
                        EndTime = _focusManager.FocusEndTime,
                        RemainingSeconds = _focusManager.RemainingSeconds,
                        WhitelistAppIds = _focusManager.WhitelistAppIds?.ToList() ?? new List<Guid>()
                    };
                    response.DataJson = JsonSerializer.Serialize(focusStatus);
                    break;

                case IpcActions.AddExtraTime:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var extraReq = JsonSerializer.Deserialize<AddExtraTimeRequest>(request.PayloadJson);
                        if (extraReq != null)
                        {
                            // 延长应用的限制时间
                            var existingRule = (await _repository.GetRulesAsync()).FirstOrDefault(r => r.AppId == extraReq.AppId);
                            if (existingRule != null && existingRule.DailyLimitMinutes.HasValue)
                            {
                                existingRule.DailyLimitMinutes += extraReq.ExtraMinutes;
                                await _repository.UpsertRuleAsync(existingRule);
                            }
                        }
                    }
                    break;

                default:
                    response.Success = false;
                    response.ErrorMessage = "Unknown Action";
                    break;
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }
    private async Task ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}

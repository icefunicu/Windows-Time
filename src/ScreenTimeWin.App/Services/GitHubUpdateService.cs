using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using ScreenTimeWin.Core.Services;

namespace ScreenTimeWin.App.Services;

/// <summary>
/// GitHub Releases API 更新服务实现
/// </summary>
public class GitHubUpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private bool _isDownloading;

    /// <summary>
    /// 上次检查更新时间
    /// </summary>
    public DateTime? LastCheckTime { get; private set; }

    /// <summary>
    /// 是否正在下载
    /// </summary>
    public bool IsDownloading => _isDownloading;

    /// <summary>
    /// 创建 GitHub 更新服务
    /// </summary>
    /// <param name="owner">仓库所有者</param>
    /// <param name="repo">仓库名称</param>
    public GitHubUpdateService(string owner = "icefunicu", string repo = "Windows-Time")
    {
        _owner = owner;
        _repo = repo;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenTimeWin/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>
    /// 检查是否有可用更新
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            LastCheckTime = DateTime.Now;
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);

            if (release == null)
            {
                return new UpdateInfo { IsUpdateAvailable = false, CurrentVersion = GetCurrentVersion() };
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
            var isUpdateAvailable = CompareVersions(latestVersion, currentVersion) > 0;

            // 查找安装包资产
            var installerAsset = release.Assets?.FirstOrDefault(a =>
                a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true ||
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

            return new UpdateInfo
            {
                IsUpdateAvailable = isUpdateAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseNotes = release.Body ?? string.Empty,
                DownloadUrl = installerAsset?.BrowserDownloadUrl ?? string.Empty,
                ReleaseDate = release.PublishedAt,
                FileSizeBytes = installerAsset?.Size ?? 0
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = GetCurrentVersion()
            };
        }
    }

    /// <summary>
    /// 下载更新包
    /// </summary>
    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentException("下载链接不能为空", nameof(downloadUrl));

        _isDownloading = true;
        try
        {
            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var tempPath = Path.Combine(Path.GetTempPath(), "ScreenTimeWin_Update");
            Directory.CreateDirectory(tempPath);
            var filePath = Path.Combine(tempPath, fileName);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            var bytesRead = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            int read;
            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var percent = (int)((bytesRead * 100) / totalBytes);
                    progress?.Report(percent);
                }
            }

            progress?.Report(100);
            return filePath;
        }
        finally
        {
            _isDownloading = false;
        }
    }

    /// <summary>
    /// 安装更新
    /// </summary>
    public Task InstallUpdateAsync(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("安装包文件不存在", installerPath);

        // 启动安装程序
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(startInfo);

        // 退出当前应用
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前版本
    /// </summary>
    private static string GetCurrentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
    /// <returns>正数表示 v1 > v2，负数表示 v1 < v2，0 表示相等</returns>
    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

        var maxLen = Math.Max(parts1.Length, parts2.Length);
        for (int i = 0; i < maxLen; i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 != p2) return p1 - p2;
        }
        return 0;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

#region GitHub API Models

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

#endregion

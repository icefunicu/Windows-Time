namespace ScreenTimeWin.Core.Services;

/// <summary>
/// 应用程序更新信息
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// 是否有可用更新
    /// </summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>
    /// 当前版本
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// 最新版本
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// 发布说明
    /// </summary>
    public string ReleaseNotes { get; init; } = string.Empty;

    /// <summary>
    /// 下载 URL
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// 发布日期
    /// </summary>
    public DateTime ReleaseDate { get; init; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSizeBytes { get; init; }
}

/// <summary>
/// 更新服务接口
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查是否有可用更新
    /// </summary>
    Task<UpdateInfo> CheckForUpdatesAsync();

    /// <summary>
    /// 下载更新包
    /// </summary>
    /// <param name="downloadUrl">下载链接</param>
    /// <param name="progress">下载进度回调（0-100）</param>
    /// <returns>下载的文件路径</returns>
    Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null);

    /// <summary>
    /// 安装更新（会关闭当前应用）
    /// </summary>
    /// <param name="installerPath">安装包路径</param>
    Task InstallUpdateAsync(string installerPath);

    /// <summary>
    /// 上次检查更新时间
    /// </summary>
    DateTime? LastCheckTime { get; }

    /// <summary>
    /// 是否正在下载
    /// </summary>
    bool IsDownloading { get; }
}

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ScreenTimeWin.IPC.Models;

namespace ScreenTimeWin.IPC;

/// <summary>
/// IPC 客户端 - 支持自动重试和增强错误处理
/// </summary>
public class IpcClient
{
    private const string PipeName = "ScreenTimeWinPipe";
    private const int DefaultTimeoutMs = 5000;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; private set; }

    public async Task<TResponse?> SendAsync<TResponse>(string action, object? payload = null, int timeoutMs = DefaultTimeoutMs)
    {
        LastError = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await SendInternalAsync<TResponse>(action, payload, timeoutMs);
                IsConnected = true;
                return result;
            }
            catch (TimeoutException ex)
            {
                LastError = $"连接超时（尝试 {attempt}/{MaxRetries}）: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"IPC 超时: {LastError}");

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt); // 指数退避
                }
            }
            catch (IOException ex)
            {
                LastError = $"IO 错误（尝试 {attempt}/{MaxRetries}）: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"IPC IO 错误: {LastError}");

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
            catch (Exception ex)
            {
                LastError = $"通信错误: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"IPC 错误: {LastError}");
                IsConnected = false;
                return default;
            }
        }

        IsConnected = false;
        return default;
    }

    private async Task<TResponse?> SendInternalAsync<TResponse>(string action, object? payload, int timeoutMs)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);

        // 使用 CancellationToken 来实现更可控的超时
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            await client.ConnectAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"连接到管道 {PipeName} 超时（{timeoutMs}ms）");
        }

        var request = new IpcRequest
        {
            Action = action,
            PayloadJson = payload != null ? JsonSerializer.Serialize(payload) : "{}"
        };

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        var lengthBytes = BitConverter.GetBytes(requestBytes.Length);

        await client.WriteAsync(lengthBytes, 0, 4, cts.Token);
        await client.WriteAsync(requestBytes, 0, requestBytes.Length, cts.Token);
        await client.FlushAsync(cts.Token);

        // 读取响应长度
        var responseLengthBytes = new byte[4];
        var bytesRead = await client.ReadAsync(responseLengthBytes, 0, 4, cts.Token);
        if (bytesRead != 4)
        {
            throw new IOException("读取响应长度失败");
        }

        var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);

        // 防止恶意大响应
        if (responseLength <= 0 || responseLength > 10 * 1024 * 1024) // 最大 10MB
        {
            throw new IOException($"无效的响应长度: {responseLength}");
        }

        var responseBytes = new byte[responseLength];
        var totalRead = 0;
        while (totalRead < responseLength)
        {
            var read = await client.ReadAsync(responseBytes, totalRead, responseLength - totalRead, cts.Token);
            if (read == 0)
            {
                throw new IOException("连接意外关闭");
            }
            totalRead += read;
        }

        var responseJson = Encoding.UTF8.GetString(responseBytes);
        var response = JsonSerializer.Deserialize<IpcResponse>(responseJson);

        if (response != null && response.Success && !string.IsNullOrEmpty(response.DataJson))
        {
            return JsonSerializer.Deserialize<TResponse>(response.DataJson);
        }

        if (response != null && !response.Success)
        {
            LastError = $"服务端错误: {response.ErrorMessage}";
        }

        return default;
    }

    /// <summary>
    /// 测试连接是否可用
    /// </summary>
    public async Task<bool> TestConnectionAsync(int timeoutMs = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(cts.Token);
            IsConnected = true;
            return true;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }
}

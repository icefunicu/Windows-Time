using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ScreenTimeWin.IPC.Models;

namespace ScreenTimeWin.IPC;

public class IpcClient
{
    private const string PipeName = "ScreenTimeWinPipe";

    public async Task<TResponse?> SendAsync<TResponse>(string action, object? payload = null, int timeoutMs = 5000)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
        try
        {
            await client.ConnectAsync(timeoutMs);

            var request = new IpcRequest
            {
                Action = action,
                PayloadJson = payload != null ? JsonSerializer.Serialize(payload) : "{}"
            };

            var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            var lengthBytes = BitConverter.GetBytes(requestBytes.Length);

            await client.WriteAsync(lengthBytes, 0, 4);
            await client.WriteAsync(requestBytes, 0, requestBytes.Length);
            await client.FlushAsync();

            // Read response length
            var responseLengthBytes = new byte[4];
            var bytesRead = await client.ReadAsync(responseLengthBytes, 0, 4);
            if (bytesRead != 4) return default;

            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            var responseBytes = new byte[responseLength];
            var totalRead = 0;
            while (totalRead < responseLength)
            {
                var read = await client.ReadAsync(responseBytes, totalRead, responseLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            var responseJson = Encoding.UTF8.GetString(responseBytes);
            var response = JsonSerializer.Deserialize<IpcResponse>(responseJson);

            if (response != null && response.Success && !string.IsNullOrEmpty(response.DataJson))
            {
                return JsonSerializer.Deserialize<TResponse>(response.DataJson);
            }

            return default;
        }
        catch (Exception ex)
        {
            // Simple logging for observability
            System.Diagnostics.Debug.WriteLine($"IPC Error: {ex.Message}");
            return default;
        }
    }
}

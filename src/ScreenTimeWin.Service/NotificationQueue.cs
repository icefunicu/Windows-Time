using System.Collections.Concurrent;
using ScreenTimeWin.IPC.Models;

namespace ScreenTimeWin.Service;

public class NotificationQueue
{
    private readonly ConcurrentQueue<NotificationDto> _queue = new();

    public void Enqueue(string title, string message, string type = "Info")
    {
        _queue.Enqueue(new NotificationDto
        {
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        });
        
        // Limit queue size
        while (_queue.Count > 50)
        {
            _queue.TryDequeue(out _);
        }
    }

    public List<NotificationDto> DequeueAll()
    {
        var list = new List<NotificationDto>();
        while (_queue.TryDequeue(out var item))
        {
            list.Add(item);
        }
        return list;
    }
}

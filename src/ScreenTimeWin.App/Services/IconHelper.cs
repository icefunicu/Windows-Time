using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ScreenTimeWin.App.Services;

public static class IconHelper
{
    // Simple memory cache
    private static readonly Dictionary<string, ImageSource> _iconCache = new();

    public static ImageSource? GetIcon(string processName, string? iconBase64 = null)
    {
        if (_iconCache.TryGetValue(processName, out var cached))
        {
            return cached;
        }

        if (!string.IsNullOrEmpty(iconBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(iconBase64!);
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                _iconCache[processName] = image;
                return image;
            }
            catch { }
        }

        // Fallback: try to find executable in path if running locally (App side)
        // But IPC provides base64, so usually we don't need this unless self-contained.
        // Let's just return null or default.
        return null;
    }
}

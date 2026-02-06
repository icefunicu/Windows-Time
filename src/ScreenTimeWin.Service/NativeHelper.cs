using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

using System.Drawing;
using System.IO;

using System.Runtime.Versioning;

namespace ScreenTimeWin.Service;

[SupportedOSPlatform("windows")]
public static class NativeHelper
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));
        [MarshalAs(UnmanagedType.U4)]
        public int cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    public static List<(IntPtr Handle, string Title, string ProcessName, int ProcessId, string FilePath)> GetAllWindows()
    {
        var windows = new List<(IntPtr, string, string, int, string)>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            var title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;
            // Filter out common shell windows? Program Manager, etc.
            if (title == "Program Manager") return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            try
            {
                var process = Process.GetProcessById((int)pid);
                string path = "";
                try { path = process.MainModule?.FileName ?? ""; } catch { }
                
                // Avoid duplicates if multiple windows belong to same process?
                // Or track windows individually?
                // User said "open windows", so maybe process level is better for aggregation.
                // But let's collect all windows first.
                
                windows.Add((hWnd, title, process.ProcessName, process.Id, path));
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static string GetActiveWindowTitle()
    {
        const int nChars = 256;
        var buff = new StringBuilder(nChars);
        var handle = GetForegroundWindow();

        if (GetWindowText(handle, buff, nChars) > 0)
        {
            return buff.ToString();
        }
        return string.Empty;
    }

    public static (string ProcessName, int ProcessId, string FilePath) GetActiveProcessInfo()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero) return ("Unknown", 0, "");

        GetWindowThreadProcessId(handle, out var pid);
        try
        {
            var process = Process.GetProcessById((int)pid);
            // FilePath might require higher privileges or not be available for some system processes
            string path = "";
            try { path = process.MainModule?.FileName ?? ""; } catch { }
            
            return (process.ProcessName, process.Id, path);
        }
        catch
        {
            return ("Unknown", 0, "");
        }
    }

    public static bool KillProcess(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = Marshal.SizeOf(lastInputInfo);
        lastInputInfo.dwTime = 0;

        var envTicks = (uint)Environment.TickCount;

        if (GetLastInputInfo(ref lastInputInfo))
        {
            var lastInputTick = lastInputInfo.dwTime;
            var idleTicks = envTicks - lastInputTick;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        return TimeSpan.Zero;
    }

    public static string? ExtractIconBase64(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            // Use ExtractAssociatedIcon which is lightweight
            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            using var stream = new MemoryStream();
            // Convert to bitmap then save as PNG
            using var bitmap = icon.ToBitmap();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }
}

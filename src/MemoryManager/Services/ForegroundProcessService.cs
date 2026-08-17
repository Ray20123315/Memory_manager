using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ray.MemoryManager.Services;

public sealed record ForegroundProcessInfo(int Pid, string Name, string Path);

public sealed class ForegroundProcessService
{
    public ForegroundProcessInfo GetForegroundProcess()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return new(0, string.Empty, string.Empty);
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return new(0, string.Empty, string.Empty);
            using var p = Process.GetProcessById((int)pid);
            var path = string.Empty;
            try { path = p.MainModule?.FileName ?? string.Empty; } catch { }
            return new((int)pid, p.ProcessName, path);
        }
        catch
        {
            return new(0, string.Empty, string.Empty);
        }
    }

    public string GetForegroundProcessName() => GetForegroundProcess().Name;

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}

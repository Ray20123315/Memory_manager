using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ray.MemoryManager.Services;

public sealed class ForegroundProcessService
{
    public string GetForegroundProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return string.Empty;
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}

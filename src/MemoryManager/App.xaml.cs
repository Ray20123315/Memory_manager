using Ray.MemoryManager.Services;
using System.IO;

namespace Ray.MemoryManager;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && string.Equals(e.Args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
        {
            var output = e.Args.Length > 1 && !string.IsNullOrWhiteSpace(e.Args[1])
                ? e.Args[1]
                : Path.Combine(Path.GetTempPath(), "memory-manager-selftest.json");
            var code = SelfTestService.Run(output);
            Shutdown(code);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.ContentRendered += (_, _) => UiVersionService.ApplyReleaseLabels(window);
        window.Show();
    }
}
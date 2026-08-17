using System;
using System.IO;
using System.Linq;

namespace Ray.MemoryManagerSetup;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var operation = e.Args.FirstOrDefault(a => a is "--install" or "--repair" or "--uninstall");
        var quiet = e.Args.Any(a => string.Equals(a, "--quiet", StringComparison.OrdinalIgnoreCase));
        var target = ArgumentValue(e.Args, "--target");
        var noShell = e.Args.Any(a => string.Equals(a, "--no-shell", StringComparison.OrdinalIgnoreCase));

        if (operation is not null && (quiet || !string.IsNullOrWhiteSpace(target)))
        {
            var service = new InstallerService();
            var dir = string.IsNullOrWhiteSpace(target) ? InstallerService.DefaultInstallDir : Path.GetFullPath(target!);
            InstallerOperationResult result;
            if (operation == "--uninstall")
            {
                result = service.Uninstall(dir, integrateShell: !noShell);
            }
            else
            {
                result = service.InstallOrRepair(dir, desktopShortcut: false, integrateShell: !noShell);
                if (result.Ok && !service.AuditInstalled(dir, out var audit)) result = new(false, audit, dir);
            }
            Shutdown(result.Ok ? 0 : 1);
            return;
        }

        var w = new MainWindow();
        MainWindow = w;
        w.Show();
        if (operation == "--uninstall") _ = w.Uninstall(quiet);
    }

    static string? ArgumentValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}

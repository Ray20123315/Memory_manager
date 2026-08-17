using System;
using System.Linq;
namespace Ray.MemoryManagerSetup;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var w=new MainWindow();
        MainWindow=w;
        w.Show();
        if(e.Args.Any(a=>string.Equals(a,"--uninstall",StringComparison.OrdinalIgnoreCase)))
            _ = w.Uninstall(e.Args.Any(a=>string.Equals(a,"--quiet",StringComparison.OrdinalIgnoreCase)));
    }
}

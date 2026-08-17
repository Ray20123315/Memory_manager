namespace Ray.MemoryManagerSetup;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Any(a=>string.Equals(a,"--uninstall",StringComparison.OrdinalIgnoreCase)))
        {
            var w=new MainWindow();
            w.Show();
            _ = w.Uninstall(e.Args.Any(a=>string.Equals(a,"--quiet",StringComparison.OrdinalIgnoreCase)));
        }
    }
}

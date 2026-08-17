using System.Collections.ObjectModel;
using System.Drawing;
using Forms = System.Windows.Forms;
using Ray.MemoryManager.Models;
namespace Ray.MemoryManager.Services;
public sealed class NotificationService : IDisposable
{
    readonly Forms.NotifyIcon _tray;
    public ObservableCollection<AppNotification> Items { get; } = new();
    public NotificationService() { _tray = new Forms.NotifyIcon { Visible = true, Text = "Memory Manager", Icon = SystemIcons.Information }; }
    public void Add(string title, string message, string kind="info", bool desktop=false)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => Items.Insert(0,new(DateTimeOffset.Now,title,message,kind)));
        if (desktop) _tray.ShowBalloonTip(5000,title,message,Forms.ToolTipIcon.Info);
    }
    public void Dispose() { _tray.Visible=false; _tray.Dispose(); }
}

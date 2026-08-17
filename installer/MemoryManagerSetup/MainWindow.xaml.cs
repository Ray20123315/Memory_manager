using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace Ray.MemoryManagerSetup;
public partial class MainWindow : Window
{
    readonly string installDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Ray20123315","Memory Manager");
    string AppExe=>Path.Combine(installDir,"MemoryManager.exe");
    string Uninstaller=>Path.Combine(installDir,"MemoryManagerSetup.exe");
    public MainWindow(){InitializeComponent();PathText.Text=installDir;UninstallButton.IsEnabled=System.IO.File.Exists(AppExe);}

    async void Install_Click(object s,RoutedEventArgs e)
    {
        SetBusy(true,"正在安裝…");
        try
        {
            Directory.CreateDirectory(installDir); Progress.Value=15;
            using var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("MemoryManager.exe") ?? throw new InvalidOperationException("安裝包內沒有 MemoryManager.exe");
            using(var output=System.IO.File.Create(AppExe)){await input.CopyToAsync(output);} Progress.Value=50;
            if(!string.Equals(Environment.ProcessPath,Uninstaller,StringComparison.OrdinalIgnoreCase)) System.IO.File.Copy(Environment.ProcessPath!,Uninstaller,true); Progress.Value=65;
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","Memory Manager.lnk"),AppExe);
            if(DesktopCheck.IsChecked==true)CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Memory Manager.lnk"),AppExe); Progress.Value=80;
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RayMemoryManager")){
                key.SetValue("DisplayName","Memory Manager / 記憶體管理器"); key.SetValue("DisplayVersion","0.9.0-beta.28"); key.SetValue("Publisher","Ray20123315"); key.SetValue("InstallLocation",installDir); key.SetValue("DisplayIcon",AppExe); key.SetValue("UninstallString",$"\"{Uninstaller}\" --uninstall"); key.SetValue("QuietUninstallString",$"\"{Uninstaller}\" --uninstall --quiet");
            }
            Progress.Value=100; StatusText.Text="安裝完成。Windows 已有真正的程式檔、捷徑與解除安裝項目。"; UninstallButton.IsEnabled=true;
            if(MessageBox.Show("安裝完成，要現在開啟 Memory Manager 嗎？","完成",MessageBoxButton.YesNo)==MessageBoxResult.Yes)Process.Start(new ProcessStartInfo(AppExe){UseShellExecute=true});
        }catch(Exception ex){StatusText.Text="安裝失敗："+ex.Message;MessageBox.Show(StatusText.Text,"安裝失敗");}
        finally{SetBusy(false,StatusText.Text);}
    }

    async void Uninstall_Click(object s,RoutedEventArgs e)
    {
        if(MessageBox.Show("確定解除安裝 Memory Manager？Log 與診斷資料會保留，避免誤刪你的除錯紀錄。","解除安裝",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        await Uninstall();
    }

    public async Task Uninstall(bool quiet=false)
    {
        SetBusy(true,"正在解除安裝…");
        try
        {
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","Memory Manager.lnk"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Memory Manager.lnk"));
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RayMemoryManager",false);
            var self=Environment.ProcessPath!; var dir=installDir; Progress.Value=90;
            if(self.StartsWith(dir,StringComparison.OrdinalIgnoreCase)){
                Process.Start(new ProcessStartInfo("cmd.exe",$"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{dir}\""){CreateNoWindow=true,UseShellExecute=false});
                Application.Current.Shutdown(); return;
            }
            if(Directory.Exists(dir))Directory.Delete(dir,true); Progress.Value=100; StatusText.Text="解除安裝完成。";
            if(!quiet)MessageBox.Show("解除安裝完成。Log 與診斷資料依照安全策略保留。","完成");
        }catch(Exception ex){StatusText.Text="解除安裝失敗："+ex.Message;if(!quiet)MessageBox.Show(StatusText.Text,"失敗");}
        finally{SetBusy(false,StatusText.Text);}
        await Task.CompletedTask;
    }

    static void CreateShortcut(string path,string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType=Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("無法使用 Windows Shortcut API");
        dynamic shell=Activator.CreateInstance(shellType)!; dynamic link=shell.CreateShortcut(path); link.TargetPath=target; link.WorkingDirectory=Path.GetDirectoryName(target); link.IconLocation=target+",0"; link.Save();
    }
    static void TryDelete(string p){try{if(System.IO.File.Exists(p))System.IO.File.Delete(p);}catch{}}
    void SetBusy(bool busy,string text){InstallButton.IsEnabled=!busy;UninstallButton.IsEnabled=!busy && System.IO.File.Exists(AppExe);StatusText.Text=text;}
}

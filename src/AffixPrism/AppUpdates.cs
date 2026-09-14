using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AffixPrism.Core;

namespace AffixPrism;
public partial class MainWindow
{
    private readonly HttpClient updateHttp=new() { Timeout=TimeSpan.FromMinutes(5) };
    private AppRelease? availableUpdate;
    private string? updateStage;
    private bool updateBusy;
    private string? notifiedUpdate;
    private readonly System.Windows.Threading.DispatcherTimer updateTimer=new() { Interval=TimeSpan.FromMinutes(30) };
    private readonly TextBlock updateStatus=new() { Text="Check GitHub for a newer release.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,6,0,6) };
    private readonly TextBlock updateNotes=new() { TextWrapping=TextWrapping.Wrap };
    private readonly Button updateDownload=new() { Content="Download update", Visibility=Visibility.Collapsed };
    private readonly Button updateInstall=new() { Content="Install update and restart", Visibility=Visibility.Collapsed };
    private void InitializeUpdates()
    {
        updateHttp.DefaultRequestHeaders.UserAgent.ParseAdd("AffixPrism-Updater/1.0");
        var panel=new StackPanel { Margin=new Thickness(0,16,0,16) };
        ((StackPanel)SettingsScroll.Content).Children.Add(panel);
        panel.Children.Add(new TextBlock { Text="Application updates", FontSize=18 });
        void Option(string label,bool value,Action<bool> changed) { var box=new CheckBox { Content=label,IsChecked=value,Margin=new Thickness(0,5,0,0) }; box.Click+=(_,_)=> { changed(box.IsChecked==true); if(!smoke)Persist(); }; panel.Children.Add(box); }
        Option("Check for updates at startup and every 30 minutes",settings.CheckUpdates,v=>settings.CheckUpdates=v);
        Option("Automatically download updates",settings.DownloadUpdates,v=>settings.DownloadUpdates=v);
        Option("Include beta releases",settings.BetaUpdates,v=>settings.BetaUpdates=v);
        var check=new Button { Content="Check for updates",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0) };
        check.Click+=async(_,_)=>await CheckAppUpdates(); panel.Children.Add(check); panel.Children.Add(updateStatus);
        panel.Children.Add(new ScrollViewer { Content=updateNotes,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        updateDownload.Click+=async(_,_)=>await DownloadAppUpdate(); updateInstall.Click+=async(_,_)=>await InstallAppUpdate();
        panel.Children.Add(updateDownload); panel.Children.Add(updateInstall);
        updateTimer.Tick+=async(_,_)=> { if(settings.CheckUpdates && updateStage==null)await CheckAppUpdates(); };
        if(!smoke)updateTimer.Start(); Closed+=(_,_)=>updateTimer.Stop();
        UpdateBannerAction.Click+=async(_,_)=> { if(updateStage!=null)await InstallAppUpdate(); else await DownloadAppUpdate(); };
        if(!smoke) Loaded+=async(_,_)=> { if(RestorePending()) { await InstallAppUpdate(); return; } if(updateStage==null && settings.CheckUpdates)await CheckAppUpdates(); };
    }
    private async Task CheckAppUpdates()
    {
        if(updateBusy||smoke)return; updateBusy=true;
        try
        {
            updateStatus.Text="Checking GitHub releases…";
            using var response=await updateHttp.GetAsync("https://api.github.com/repos/blacksheep25/AffixPrism/releases?per_page=100",lifetime.Token); response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));
            string version=typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
            var release=GitHubUpdates.Select(json.RootElement,version,settings.BetaUpdates);
            if(updateStage!=null) { updateStatus.Text="A verified update is ready. Click Install update and restart."; return; }
            availableUpdate=release;
            updateStatus.Text=release==null ? $"You are up to date ({version}) for this release channel." : $"{release.Version} is available. Installed: {version}.";
            updateNotes.Text=release?.Notes??""; updateDownload.Visibility=release==null ? Visibility.Collapsed : Visibility.Visible;
            if(release!=null && notifiedUpdate!=release.Version) { notifiedUpdate=release.Version; tray?.ShowBalloonTip(8000,"AffixPrism update available",$"Version {release.Version} is available. Open AffixPrism to download and install.",System.Windows.Forms.ToolTipIcon.Info); }
            UpdateBanner.Visibility=release==null ? Visibility.Collapsed : Visibility.Visible; UpdateBannerText.Text=updateStatus.Text; UpdateBannerAction.Content="Download update";
            if(release!=null)Notice.Text=$"AffixPrism {release.Version} available · open Settings → Application updates.";
        }
        catch(Exception ex) when(ex is HttpRequestException or IOException or JsonException or OperationCanceledException or FormatException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException) { availableUpdate=null; updateDownload.Visibility=Visibility.Collapsed; updateStatus.Text="Could not check for updates: "+ex.Message; }
        finally { updateBusy=false; }
        if(availableUpdate!=null&&settings.DownloadUpdates&&updateStage==null)await DownloadAppUpdate();
    }
    private async Task DownloadAppUpdate()
    {
        if(updateBusy||availableUpdate==null||smoke)return; updateBusy=true; updateDownload.IsEnabled=false;
        try
        {
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AffixPrism","updates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string archive=Path.Combine(directory,"update.zip");
            using var response=await updateHttp.GetAsync(availableUpdate.Zip,HttpCompletionOption.ResponseHeadersRead,lifetime.Token); response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>300_000_000)throw new IOException("Update archive is too large.");
            await using(var input=await response.Content.ReadAsStreamAsync(lifetime.Token))
            await using(var output=File.Create(archive))
            {
                var buffer=new byte[81920]; long total=0; int count;
                while((count=await input.ReadAsync(buffer,lifetime.Token))>0) { total+=count; if(total>300_000_000)throw new IOException("Update archive is too large."); await output.WriteAsync(buffer.AsMemory(0,count),lifetime.Token); updateStatus.Text=$"Downloading {availableUpdate.Version}: {total/1048576} MB"; }
            }
            string checksum=await updateHttp.GetStringAsync(availableUpdate.Checksum,lifetime.Token); GitHubUpdates.Verify(archive,checksum);
            string target=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            string stage=Path.Combine(Path.GetDirectoryName(target)!,".AffixPrism-update-"+Guid.NewGuid().ToString("N"));
            await Task.Run(()=>GitHubUpdates.Extract(archive,stage),lifetime.Token); updateStage=stage; pendingJob=null; SavePending(); File.Delete(archive);
            updateStatus.Text=$"{availableUpdate.Version} downloaded and verified. Click Install update and restart, or exit and reopen AffixPrism to apply it.";
            updateInstall.Visibility=Visibility.Visible; updateDownload.Visibility=Visibility.Collapsed; Notice.Text=updateStatus.Text;
            UpdateBanner.Visibility=Visibility.Visible; UpdateBannerText.Text=$"AffixPrism {availableUpdate.Version} is ready to install."; UpdateBannerAction.Content="Install and restart";
        }
        catch(Exception ex) when(ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or InvalidDataException) { updateStatus.Text="Update download failed: "+ex.Message; }
        finally { updateBusy=false; updateDownload.IsEnabled=true; }
    }
    private async Task InstallAppUpdate()
    {
        if(updateStage==null||smoke||updateBusy)return;
        updateBusy=true; UpdateBannerAction.IsEnabled=false; updateInstall.IsEnabled=false;
        try
        {
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AffixPrism","updates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string helper=Path.Combine(directory,"install.ps1"), manifest=Path.Combine(directory,"job.json");
            File.Copy(Path.Combine(AppContext.BaseDirectory,"UpdateHelper.ps1"),helper);
            File.WriteAllText(manifest,JsonSerializer.Serialize(new { Target=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),Stage=updateStage,ProcessId=Environment.ProcessId }));
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=directory };
            foreach(string arg in new[]{"-NoProfile","-NonInteractive","-File",helper,"-Manifest",manifest})start.ArgumentList.Add(arg);
            using var helperProcess=Process.Start(start) ?? throw new IOException("Updater did not start.");
            for(int i=0;i<50&&!File.Exists(manifest+".ready")&&!helperProcess.HasExited;i++)await Task.Delay(100);
            if(!File.Exists(manifest+".ready"))throw new IOException("The update helper could not start. Check Windows script policy; the app remains open.");
            pendingJob=manifest; SavePending();
            ExitApplication();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { updateStatus.Text="Could not start updater: "+ex.Message; UpdateBannerText.Text=updateStatus.Text; }
        finally { updateBusy=false; UpdateBannerAction.IsEnabled=true; updateInstall.IsEnabled=true; }
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using AffixPrism.Core;
namespace AffixPrism;
public partial class MainWindow
{
    private sealed record PendingUpdate(AppRelease Release,string Stage,string Target,string? Job=null);
    private string PendingPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AffixPrism","updates","pending-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).ToUpperInvariant())))[..16]+".json");
    private string? pendingJob;
    private void SavePending()
    {
        if(availableUpdate==null||updateStage==null)return;
        Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
        File.WriteAllText(PendingPath+".tmp",JsonSerializer.Serialize(new PendingUpdate(availableUpdate,updateStage,Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),pendingJob)));
        File.Move(PendingPath+".tmp",PendingPath,true);
    }
    private bool RestorePending()
    {
        if(!File.Exists(PendingPath))return false;
        try
        {
            var pending=JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(PendingPath))!;
            string target=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            string stage=Path.GetFullPath(pending.Stage);
            if(!pending.Target.Equals(target,StringComparison.OrdinalIgnoreCase)||!Path.GetDirectoryName(stage)!.Equals(Path.GetDirectoryName(target),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(stage).StartsWith(".AffixPrism-update-",StringComparison.Ordinal))throw new IOException("Invalid pending update location.");
            string current=typeof(MainWindow).Assembly.GetName().Version!.ToString();
            current=System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(MainWindow).Assembly)!.InformationalVersion.Split('+')[0];
            if(GitHubUpdates.Compare(pending.Release.Version,current)<=0) { File.Delete(PendingPath); updateStatus.Text="Update installed: "+current; return false; }
            if(!File.Exists(Path.Combine(stage,"AffixPrism.exe")))throw new IOException("Downloaded update is missing. Download it again.");
            availableUpdate=pending.Release; updateStage=stage; pendingJob=pending.Job;
            updateInstall.Visibility=Visibility.Visible; updateDownload.Visibility=Visibility.Collapsed;
            UpdateBanner.Visibility=Visibility.Visible; UpdateBannerText.Text=$"AffixPrism {pending.Release.Version} is ready to install.";
            UpdateBannerAction.Content="Install and restart";
            if(pending.Job!=null && File.Exists(Path.Combine(Path.GetDirectoryName(pending.Job)!,"install-result.txt")))
            {
                updateStatus.Text=File.ReadAllText(Path.Combine(Path.GetDirectoryName(pending.Job)!,"install-result.txt"));
                UpdateBannerText.Text=updateStatus.Text; return false;
            }
            return true;
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        { updateStatus.Text="Could not restore update: "+ex.Message; return false; }
    }
}

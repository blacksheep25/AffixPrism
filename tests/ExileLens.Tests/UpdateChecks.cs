using ExileLens.Core;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
static class UpdateChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(GitHubUpdates.Compare("v0.4.0-beta.10","0.4.0-beta.6")>0,"Updater compares numeric beta versions correctly");
        check(GitHubUpdates.Compare("0.4.0","0.4.0-beta.99")>0,"Stable release is newer than its prereleases");
        object Release(string version,bool beta,bool draft=false,string host="https://github.com/blacksheep25/ExileLens")=>new { tag_name=version,prerelease=beta,draft,body="Changes",assets=new[]{ new { name=$"ExileLens-{version}-win-x64.zip",browser_download_url=$"{host}/releases/download/{version}/ExileLens-{version}-win-x64.zip" },new { name=$"ExileLens-{version}-win-x64.zip.sha256",browser_download_url=$"{host}/releases/download/{version}/ExileLens-{version}-win-x64.zip.sha256" } } };
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new[]{Release("0.4.1",false),Release("0.5.0-beta.1",true),Release("0.6.0",false,true)}));
        check(GitHubUpdates.Select(json.RootElement,"0.4.0",false)?.Version=="0.4.1","Updater excludes beta and draft releases on stable channel");
        check(GitHubUpdates.Select(json.RootElement,"0.4.0",true)?.Version=="0.5.0-beta.1","Updater selects newer beta on beta channel");
        using var hostile=JsonDocument.Parse(JsonSerializer.Serialize(new[]{Release("9.0.0",false,false,"https://example.org")}));
        check(GitHubUpdates.Select(hostile.RootElement,"0.4.0",true)==null,"Updater rejects assets outside project release URLs");
        string root=Path.Combine(Path.GetTempPath(),"ExileLens-update-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string archive=Path.Combine(root,"update.zip");
            using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)) { using var writer=new StreamWriter(zip.CreateEntry("ExileLens.exe").Open()); writer.Write("fixture"); }
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))); GitHubUpdates.Verify(archive,hash+"  update.zip"); check(true,"Updater verifies archive checksum");
            bool rejected=false; try { GitHubUpdates.Verify(archive,new string('0',64)); } catch(IOException) { rejected=true; } check(rejected,"Updater refuses checksum mismatch");
            GitHubUpdates.Extract(archive,Path.Combine(root,"stage")); check(File.ReadAllText(Path.Combine(root,"stage","ExileLens.exe"))=="fixture","Updater stages valid archive");
            string bad=Path.Combine(root,"bad.zip"); using(var zip=ZipFile.Open(bad,ZipArchiveMode.Create)) { zip.CreateEntry("../escape.txt"); zip.CreateEntry("ExileLens.exe"); }
            rejected=false; try { GitHubUpdates.Extract(bad,Path.Combine(root,"bad-stage")); } catch(IOException) { rejected=true; } check(rejected&&!File.Exists(Path.Combine(root,"escape.txt")),"Updater rejects archive traversal before extraction");
        }
        finally { Directory.Delete(root,true); }
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AffixPrism.Core;

public sealed record AppRelease(string Version,string Notes,string Page,string Zip,string Checksum);
public static class GitHubUpdates
{
    public const string Repository="https://github.com/blacksheep25/AffixPrism";
    public static int Compare(string a,string b)
    {
        (Version Version,string[] Pre) Parse(string s) { var parts=s.TrimStart('v').Split('+')[0].Split('-',2); return (Version.Parse(parts[0]),parts.Length==1 ? [] : parts[1].Split('.')); }
        var x=Parse(a); var y=Parse(b); int order=x.Version.CompareTo(y.Version); if(order!=0) return order;
        if(x.Pre.Length==0 || y.Pre.Length==0) return x.Pre.Length==y.Pre.Length ? 0 : x.Pre.Length==0 ? 1 : -1;
        for(int i=0;i<Math.Min(x.Pre.Length,y.Pre.Length);i++) { bool nx=int.TryParse(x.Pre[i],out int vx),ny=int.TryParse(y.Pre[i],out int vy); order=nx&&ny ? vx.CompareTo(vy) : nx!=ny ? nx ? -1 : 1 : string.CompareOrdinal(x.Pre[i],y.Pre[i]); if(order!=0)return order; }
        return x.Pre.Length.CompareTo(y.Pre.Length);
    }
    public static AppRelease? Select(JsonElement releases,string current,bool betas)
    {
        AppRelease? best=null;
        foreach(var release in releases.EnumerateArray())
        {
            if(release.GetProperty("draft").GetBoolean() || (!betas && release.GetProperty("prerelease").GetBoolean()))continue;
            var tag=release.GetProperty("tag_name").GetString()!;
            if(!Regex.IsMatch(tag,@"^v?\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?$") || Compare(tag,current)<=0 || (best!=null&&Compare(tag,best.Version)<=0))continue;
            string name=$"AffixPrism-{tag.TrimStart('v')}-win-x64.zip";
            string? Asset(string n)=>release.GetProperty("assets").EnumerateArray().FirstOrDefault(a=>a.GetProperty("name").GetString()==n) is var asset && asset.ValueKind==JsonValueKind.Object ? asset.GetProperty("browser_download_url").GetString() : null;
            var zip=Asset(name); var checksum=Asset(name+".sha256");
            if(zip==null||checksum==null||!(zip.StartsWith(Repository+"/releases/download/",StringComparison.Ordinal))||checksum!=zip+".sha256")continue;
            best=new(tag,release.GetProperty("body").GetString()??"",Repository+"/releases/tag/"+tag,zip,checksum);
        }
        return best;
    }
    public static void Verify(string archive,string checksum)
    {
        var expected=checksum.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        using var stream=File.OpenRead(archive);
        if(expected==null||!Regex.IsMatch(expected,"^[a-fA-F0-9]{64}$")||!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Update checksum verification failed.");
    }
    public static void Extract(string archive,string destination)
    {
        if(Directory.Exists(destination))throw new IOException("Update staging directory already exists.");
        string root=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;
        using var zip=ZipFile.OpenRead(archive);
        long total=0;
        foreach(var entry in zip.Entries)
        {
            string path=Path.GetFullPath(Path.Combine(root,entry.FullName)); total+=entry.Length;
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||entry.FullName.Contains(':')||((entry.ExternalAttributes>>16)&0xF000)==0xA000||total>1_000_000_000)throw new IOException("Unsafe update archive.");
        }
        if(!zip.Entries.Any(e=>e.FullName=="AffixPrism.exe"))throw new IOException("Update has no application executable.");
        zip.ExtractToDirectory(destination);
    }
}

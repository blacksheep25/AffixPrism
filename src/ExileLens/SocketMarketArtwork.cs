using System;
using System.Linq;
using System.Threading.Tasks;
using ExileLens.Core;
namespace ExileLens;
public partial class MainWindow
{
    private Task? socketArtRequest;
    private DateTimeOffset socketArtLoaded;
    private void WarmSocketArtwork(CopiedItem item)
    {
        if(smoke || ItemSockets.From(item).Count==0 || socketArtRequest is { IsCompleted:false } || DateTimeOffset.UtcNow-socketArtLoaded<TimeSpan.FromMinutes(5)) return;
        socketArtRequest=LoadSocketArtwork();
    }
    private async Task LoadSocketArtwork()
    {
        try
        {
            foreach(string category in new[]{"Runes","SoulCores"})
            {
                var data=await economy.CategoryAsync(new(category,true),settings.League,lifetime.Token,TimeSpan.FromHours(1));
                SocketStrip.Observe(data.Rows.Where(r=>r.IconUrl!=null).Select(r=>new ItemSocket(0,"rune",r.Name,r.IconUrl,true)));
            }
        }
        catch(Exception ex) when(ex is System.Net.Http.HttpRequestException or System.IO.IOException or OperationCanceledException or System.Text.Json.JsonException) { /* Item checking remains available if artwork cannot load. */ }
        finally { socketArtLoaded=DateTimeOffset.UtcNow; }
    }
}

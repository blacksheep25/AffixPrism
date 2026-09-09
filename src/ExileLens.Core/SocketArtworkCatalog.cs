namespace ExileLens.Core;

// Learn only from source-confirmed socket items. Never derive URLs or borrow another tier's art.
public sealed class SocketArtworkCatalog
{
    private readonly Dictionary<string,string> icons = new(StringComparer.OrdinalIgnoreCase);
    public bool Observe(IEnumerable<ItemSocket> sockets)
    {
        bool changed=false;
        foreach(var socket in sockets)
        {
            if(socket.Inferred || socket.Occupied!=true || string.IsNullOrWhiteSpace(socket.Name) || ItemArtwork.SafeUrl(socket.IconUrl) is not {} url) continue;
            string name=socket.Name.Trim();
            if(icons.TryGetValue(name,out var old) && old==url) continue;
            if(icons.Count>=512) icons.Clear();
            icons[name]=url; changed=true;
        }
        return changed;
    }
    public ItemSocket Resolve(ItemSocket socket)
    {
        if(ItemArtwork.SafeUrl(socket.IconUrl) is {} own) return socket with { IconUrl=own };
        return socket with { IconUrl=socket.Occupied==true && socket.Name!=null && icons.TryGetValue(socket.Name.Trim(),out var url) ? url : null };
    }
}

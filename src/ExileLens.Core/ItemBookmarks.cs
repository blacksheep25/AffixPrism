using System.Text.Json;
namespace ExileLens.Core;
public sealed record ItemBookmark(CopiedItem Item, string? IconUrl, DateTimeOffset SavedAt, string Notes = "")
{
    public override string ToString() => $"{Item.Name} · {Item.BaseType} · {SavedAt.ToLocalTime():dd MMM}";
}
public sealed class ItemBookmarks(string path)
{
    public List<ItemBookmark> Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<List<ItemBookmark>>(File.ReadAllText(path)) ?? new() : new();
    public void Save(CopiedItem item, string? icon)
    {
        var rows = Read(); string notes = rows.FirstOrDefault(x=>x.Item.Details==item.Details)?.Notes ?? ""; rows.RemoveAll(x => x.Item.Details == item.Details);
        rows.Insert(0, new(item, icon, DateTimeOffset.UtcNow, notes)); Write(rows);
    }
    public void UpdateNotes(ItemBookmark bookmark,string notes)
    {
        var rows=Read(); int index=rows.FindIndex(x=>x.Item.Details==bookmark.Item.Details);
        if(index>=0) { rows[index]=rows[index] with { Notes=notes }; Write(rows); }
    }
    public void Remove(ItemBookmark item) { var rows = Read(); rows.RemoveAll(x => x.Item.Details == item.Item.Details); Write(rows); }
    private void Write(List<ItemBookmark> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(rows)); File.Move(path + ".tmp",path,true);
    }
}

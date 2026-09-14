using System;
using AffixPrism.Core;

namespace AffixPrism;
public sealed record RecentCheck(CopiedItem Item, DateTimeOffset CheckedAt)
{
    public string Label => $"{CheckedAt:HH:mm}  {Item.Name}";
}
public sealed record SessionArea(string Name, int Level, DateTimeOffset EnteredAt)
{
    public string Time => EnteredAt.ToLocalTime().ToString("HH:mm:ss");
    public string Label => $"{Name} · level {Level}";
}
public sealed record TrackedLoot(string Name, int Quantity)
{
    public string Label => $"{Quantity} × {Name}";
}

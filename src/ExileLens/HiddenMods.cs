using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ExileLens.Core;
namespace ExileLens;
public partial class EvaluationWindow
{
    public void VerifyHiddenMods()
    {
        RefreshHiddenMods();
        if(compactModRows.Any(r=>r.Row.Visibility!=Visibility.Visible)) throw new System.Exception("Modifiers hidden by default");
        ToggleHiddenMods(this,new RoutedEventArgs());
        if(!compactModRows.Any(r=>r.Row.Visibility==Visibility.Collapsed)) throw new System.Exception("No secondary modifiers collapsed");
        ToggleHiddenMods(this,new RoutedEventArgs());
        if(compactModRows.Any(r=>r.Row.Visibility!=Visibility.Visible)) throw new System.Exception("Show hidden mods failed");
    }
    private readonly List<(FrameworkElement Row,int Group,string Text)> compactModRows=new();
    private bool showHiddenMods=true;
    private void ToggleHiddenMods(object sender,RoutedEventArgs e) { showHiddenMods=!showHiddenMods; RefreshHiddenMods(); QueueContentResize(); }
    private void RefreshHiddenMods()
    {
        if(HiddenModsButton==null) return;
        var suggested=item==null ? new string[0] : DefaultItemFilters.Suggested(item).ToArray();
        int hidden=0;
        foreach(var row in compactModRows)
        {
            bool secondary=!suggested.Contains(row.Text) && !drafts.Any(d=>d.Filter.GroupId==row.Group && d.Filter.Enabled);
            if(secondary) hidden++;
            row.Row.Visibility=secondary && !showHiddenMods ? Visibility.Collapsed : Visibility.Visible;
        }
        HiddenModsButton.Visibility=hidden==0 ? Visibility.Collapsed : Visibility.Visible;
        HiddenModsButton.Content=showHiddenMods ? "Compact mods" : $"Show {hidden} hidden mods";
        HiddenModsButton.ToolTip="Only changes the display. Hidden modifiers still contribute to item comparison and valuation.";
    }
}

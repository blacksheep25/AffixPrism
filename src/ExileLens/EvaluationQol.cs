using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExileLens.Core;

namespace ExileLens;
public partial class EvaluationWindow
{
    private sealed record ProfileStat(string Signature,string Kind);
    private sealed record FilterProfile(string Name,string ItemClass,bool Broad,ProfileStat[] Stats,string[] Warnings);
    private sealed record ProfileFile(List<FilterProfile> Profiles,string? Active);
    private sealed record UndoRow(bool Enabled,string Min,string Max);
    private sealed record FilterState(bool Base,bool Broad,int Currency,int Seller,int Age,UndoRow[] Rows);
    private readonly Stack<FilterState> filterUndo=new();
    private FilterState? lastFilterState;
    private List<FilterProfile> profiles=new();
    private string? activeProfile;
    private bool profilesWritable=true;
    private Window? profileWindow;
    public event Action<int>? HistoryRequested;
    private static string ProfilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ExileLens","filter-profiles.json");
    private FilterState CaptureFilters()=>new(exactBase,BroadPreset.IsChecked==true,ResultCurrency.SelectedIndex,SellerMode.SelectedIndex,ListingAge.SelectedIndex,drafts.Select(x=>new UndoRow(x.Filter.Enabled,x.Min.Text,x.Max.Text)).ToArray());
    private void InitializeQol()
    {
        if(!IsTestMode) try
        {
            if(File.Exists(ProfilePath))
            {
                if(new FileInfo(ProfilePath).Length>1024*1024) throw new JsonException();
                var data=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(ProfilePath));
                if(data?.Profiles==null || data.Profiles.Count>30 || data.Profiles.Any(p=>p==null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>60 || p.ItemClass==null || p.Stats==null || p.Stats.Length>200 || p.Stats.Any(s=>s?.Signature==null || s.Kind==null) || p.Warnings==null || p.Warnings.Length>50 || p.Warnings.Any(w=>w==null || w.Length>160))) throw new JsonException();
                profiles=data.Profiles; activeProfile=data.Active;
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) { profilesWritable=false; QolStatus.Text="Could not load profiles; existing file preserved."; }
        PreviewKeyDown+=(_,e)=> {
            var key=e.Key==Key.System ? e.SystemKey : e.Key;
            if(Keyboard.Modifiers==ModifierKeys.Alt && key is Key.Left or Key.Right) { HistoryRequested?.Invoke(key==Key.Left ? 1 : -1); e.Handled=true; }
            if(Keyboard.Modifiers==ModifierKeys.Control && key==Key.Z && e.OriginalSource is not TextBox) { UndoFilters(this,new RoutedEventArgs()); e.Handled=true; }
        };
        Closing+=(_,_)=>profileWindow?.Close();
    }
    private void ResetFilterUndo()
    {
        filterUndo.Clear(); lastFilterState=CaptureFilters(); RefreshFilterSummary(); RefreshWarnings();
    }
    private void RecordFilterChange()
    {
        var next=CaptureFilters();
        if(lastFilterState!=null && JsonSerializer.Serialize(lastFilterState)!=JsonSerializer.Serialize(next))
        {
            if(filterUndo.Count>=50) { var keep=filterUndo.Take(49).Reverse().ToArray(); filterUndo.Clear(); foreach(var state in keep) filterUndo.Push(state); }
            filterUndo.Push(lastFilterState);
        }
        lastFilterState=next; RefreshFilterSummary();
    }
    private void UndoFilters(object sender,RoutedEventArgs e)
    {
        if(filterUndo.Count==0) return;
        var state=filterUndo.Pop(); building=true;
        exactBase=state.Base; UpdateBaseFilter(); BroadPreset.IsChecked=state.Broad; ExactPreset.IsChecked=!state.Broad;
        ResultCurrency.SelectedIndex=state.Currency; SellerMode.SelectedIndex=state.Seller; ListingAge.SelectedIndex=state.Age;
        foreach(var group in drafts.Select((d,i)=>(Draft:d,Index:i)).GroupBy(x=>x.Draft.Filter.GroupId))
        {
            var first=group.First();
            if(first.Index<state.Rows.Length && first.Draft.Filter.Enabled!=state.Rows[first.Index].Enabled && rowToggles.TryGetValue(group.Key,out var toggle)) toggle();
        }
        for(int i=0;i<Math.Min(state.Rows.Length,drafts.Count);i++) { drafts[i].Min.Text=state.Rows[i].Min; drafts[i].Max.Text=state.Rows[i].Max; }
        building=false; lastFilterState=CaptureFilters(); InvalidatePrices(); StoreDraft();
        QolStatus.Text="Previous filters restored · Search when ready.";
    }
    private void RefreshFilterSummary()
    {
        UndoFilterButton.IsEnabled=filterUndo.Count>0;
        var active=drafts.Where(x=>x.Filter.Enabled).ToArray();
        ActiveFilters.Header=$"Active filters ({active.Select(x=>x.Filter.GroupId).Distinct().Count()})";
        FilterChips.Children.Clear();
        if(item!=null) FilterChips.Children.Add(new TextBlock { Text=$"{(exactBase ? item.BaseType : item.ItemClass)} · {item.Rarity} · {(ItemAnalysis.From(item).Unidentified ? "unidentified" : "identified")} · {(ItemAnalysis.From(item).Corrupted ? "corrupted" : "uncorrupted")}",TextWrapping=TextWrapping.Wrap,Foreground=Foreground });
        foreach(var group in active.GroupBy(x=>x.Filter.GroupId))
        {
            int id=group.Key;
            var text=group.First().Filter.Text+" · "+string.Join(" / ",group.Select(x=>$"{(x.Min.Text.Length==0 ? "any" : x.Min.Text)}–{(x.Max.Text.Length==0 ? "any" : x.Max.Text)}"));
            var button=new Button { Content=new TextBlock { Text="× "+text,TextWrapping=TextWrapping.Wrap },HorizontalContentAlignment=HorizontalAlignment.Left,ToolTip="Remove this filter",Margin=new Thickness(0,2,0,2),Padding=new Thickness(5,3,5,3) };
            button.Click+=(_,_)=> { if(rowToggles.TryGetValue(id,out var toggle)) toggle(); };
            FilterChips.Children.Add(button);
        }
    }
    private void RefreshWarnings()
    {
        var profile=profiles.FirstOrDefault(p=>p.Name==activeProfile);
        bool map=item!=null && (item.ItemClass.Contains("Waystone",StringComparison.OrdinalIgnoreCase) || item.ItemClass.Contains("Map",StringComparison.OrdinalIgnoreCase));
        var hits=map && profile!=null ? ItemAnalysis.From(item!).Lines.Where(l=>l.Kind is not ("Flavour" or "Description") && profile.Warnings.Any(w=>w.Length>0 && l.Text.Contains(w,StringComparison.OrdinalIgnoreCase))).Select(l=>l.Text).Distinct().ToArray() : Array.Empty<string>();
        MapWarnings.Visibility=hits.Length==0 ? Visibility.Collapsed : Visibility.Visible;
        WarningText.Text=$"⚠ {activeProfile} · flagged map modifiers\n"+string.Join("\n",hits);
    }
    private Action<string>? profileWriterForVerification;
    private string profileSaveError = "";
    private bool SaveProfiles()
    {
        profileSaveError = "";
        if(!profilesWritable) { profileSaveError="Profiles were not loaded safely. Existing file is preserved; changes are session-only."; QolStatus.Text=profileSaveError; return false; }
        try
        {
            string json=JsonSerializer.Serialize(new ProfileFile(profiles,activeProfile));
            if(profileWriterForVerification!=null) profileWriterForVerification(json);
            else if(!IsTestMode) { Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!); File.WriteAllText(ProfilePath+".tmp",json); File.Move(ProfilePath+".tmp",ProfilePath,true); }
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        { profileSaveError="Could not save profiles. Changes are session-only; check folder access and free disk space, then try Save again."; QolStatus.Text=profileSaveError; return false; }
    }
    private void ApplyProfile(FilterProfile profile)
    {
        activeProfile=profile.Name; bool persisted=SaveProfiles(); RefreshWarnings();
        if(item==null || !item.ItemClass.Equals(profile.ItemClass,StringComparison.OrdinalIgnoreCase)) { QolStatus.Text=$"{profile.Name} warnings active. Filter template applies to {profile.ItemClass}." + (persisted ? "" : "\n"+profileSaveError); return; }
        building=true; BroadPreset.IsChecked=profile.Broad; ExactPreset.IsChecked=!profile.Broad;
        foreach(var group in drafts.GroupBy(x=>x.Filter.GroupId))
        {
            var first=group.First(); bool enabled=profile.Stats.Any(s=>s.Kind==first.Filter.Kind && s.Signature==ComparableMarket.Signature(first.Filter.Text));
            if(first.Filter.Enabled!=enabled && rowToggles.TryGetValue(group.Key,out var toggle)) toggle();
            foreach(var row in group) { row.Filter.Preset(profile.Broad); row.Min.Text=row.Filter.Minimum; row.Max.Text=row.Filter.Maximum; }
        }
        building=false; InvalidatePrices(); StoreDraft();
        QolStatus.Text=$"Applied {profile.Name} · {drafts.Where(x=>x.Filter.Enabled).Select(x=>x.Filter.GroupId).Distinct().Count()} matching stats; bounds use this item's rolls. Search when ready." + (persisted ? "" : "\n"+profileSaveError);
    }
    private void OpenProfiles(object sender,RoutedEventArgs e)
    {
        if(profileWindow!=null) { profileWindow.Activate(); return; }
        var panel=new StackPanel { Margin=new Thickness(16) };
        var choose=new ComboBox { ItemsSource=profiles.Select(p=>p.Name).ToArray(),SelectedItem=activeProfile };
        var name=new TextBox { Text=activeProfile??"",MaxLength=60 };
        var warnings=new TextBox { AcceptsReturn=true,VerticalContentAlignment=VerticalAlignment.Top,MaxLength=8000,Height=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,TextWrapping=TextWrapping.Wrap };
        var help=new TextBlock { Text="Save selected stats and Item values/Broad mode for this item class. Numeric bounds use the next item's own rolls. Map warnings match your phrases in copied waystones; no background map detection.",TextWrapping=TextWrapping.Wrap };
        panel.Children.Add(help); panel.Children.Add(choose); panel.Children.Add(new TextBlock { Text="Profile name",Margin=new Thickness(0,10,0,3) }); panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text="Map warning phrases · one per line",Margin=new Thickness(0,10,0,3) }); panel.Children.Add(warnings);
        var feedback=new TextBlock { TextWrapping=TextWrapping.Wrap, Text=profilesWritable ? "" : "Profiles could not be loaded. Existing file preserved; saving is disabled." }; panel.Children.Add(feedback);
        var actions=new WrapPanel(); panel.Children.Add(actions);
        var save=new Button { Content="Save current filters",IsEnabled=profilesWritable && item!=null }; var apply=new Button { Content="Apply selected" }; var remove=new Button { Content="Delete selected",IsEnabled=profilesWritable };
        actions.Children.Add(save); actions.Children.Add(apply); actions.Children.Add(remove);
        var saveWarnings=new Button { Content="Save warnings only",IsEnabled=profilesWritable }; actions.Children.Add(saveWarnings);
        saveWarnings.Click+=(_,_)=> {
            var existing=profiles.FirstOrDefault(p=>p.Name==choose.SelectedItem as string);
            if(existing==null) { feedback.Text="Select an existing profile first."; return; }
            var phrases=warnings.Text.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(phrases.Length>50 || phrases.Any(w=>w.Length>160)) { feedback.Text="Limit: 50 phrases, 160 characters each."; return; }
            profiles[profiles.IndexOf(existing)]=existing with { Warnings=phrases }; activeProfile=existing.Name; bool saved=SaveProfiles(); RefreshWarnings(); QueueContentResize(); feedback.Text=saved ? "Warnings saved; filter template preserved." : profileSaveError;
        };
        void Fill() { if(profiles.FirstOrDefault(p=>p.Name==choose.SelectedItem as string) is {} p) { name.Text=p.Name; warnings.Text=string.Join("\n",p.Warnings); } }
        choose.SelectionChanged+=(_,_)=>Fill(); Fill();
        save.Click+=(_,_)=> {
            if(item==null || string.IsNullOrWhiteSpace(name.Text)) { feedback.Text="Enter a profile name."; return; }
            string title=name.Text.Trim(); var phrases=warnings.Text.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(phrases.Length>50 || phrases.Any(w=>w.Length>160) || (profiles.Count>=30 && profiles.All(p=>p.Name!=title))) { feedback.Text="Limit: 30 profiles, 50 phrases per profile, 160 characters per phrase."; return; }
            profiles.RemoveAll(p=>p.Name==title);
            profiles.Add(new(title,item.ItemClass,BroadPreset.IsChecked==true,drafts.Where(x=>x.Filter.Enabled).Select(x=>new ProfileStat(ComparableMarket.Signature(x.Filter.Text),x.Filter.Kind)).Distinct().ToArray(),phrases));
            activeProfile=title; bool saved=SaveProfiles(); RefreshWarnings(); QueueContentResize(); choose.ItemsSource=profiles.Select(p=>p.Name).ToArray(); choose.SelectedItem=title; feedback.Text=saved ? "Profile saved; warning phrases are active." : profileSaveError;
        };
        apply.Click+=(_,_)=> { if(profiles.FirstOrDefault(p=>p.Name==choose.SelectedItem as string) is {} p) { ApplyProfile(p); QueueContentResize(); profileWindow?.Close(); } };
        remove.Click+=(_,_)=> { if(choose.SelectedItem is string title) { profiles.RemoveAll(p=>p.Name==title); if(activeProfile==title) activeProfile=null; bool saved=SaveProfiles(); RefreshWarnings(); choose.ItemsSource=profiles.Select(p=>p.Name).ToArray(); feedback.Text=saved ? "Profile deleted." : profileSaveError; } };
        profileWindow=new Window { Title="Filter profiles and map warnings",Owner=this,Resources=Resources,Width=470,Height=480,MinWidth=420,MinHeight=360,Background=new SolidColorBrush(Color.FromRgb(20,20,16)),Foreground=Foreground,Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },Topmost=true,ShowInTaskbar=false };
        profileWindow.WindowStyle=WindowStyle.None; profileWindow.AllowsTransparency=true; profileWindow.ResizeMode=ResizeMode.CanResizeWithGrip;
        var shell=new DockPanel(); var header=new DockPanel { Margin=new Thickness(12,8,12,0) };
        var close=new Button { Content="✕",Padding=new Thickness(8,3,8,3) }; close.Click+=(_,_)=>profileWindow?.Close(); DockPanel.SetDock(close,Dock.Right); header.Children.Add(close);
        var titleBar=new TextBlock { Text="Filter profiles & map warnings",Foreground=Brushes.Khaki,Padding=new Thickness(0,6,0,6),Cursor=Cursors.SizeAll };
        titleBar.MouseLeftButtonDown+=(_,e)=> { if(e.LeftButton==MouseButtonState.Pressed) profileWindow?.DragMove(); }; header.Children.Add(titleBar);
        DockPanel.SetDock(header,Dock.Top); shell.Children.Add(header); shell.Children.Add(new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        profileWindow.Content=new Border { Child=shell,BorderBrush=Brushes.DarkGoldenrod,BorderThickness=new Thickness(1) };
        profileWindow.Closed+=(_,_)=>profileWindow=null; profileWindow.Show();
    }
    private void PreviousCheck(object sender,RoutedEventArgs e)=>HistoryRequested?.Invoke(1);
    private void NextCheck(object sender,RoutedEventArgs e)=>HistoryRequested?.Invoke(-1);
    public void SetHistoryNavigation(bool older,bool newer) { PreviousItemButton.IsEnabled=older; NextItemButton.IsEnabled=newer; }
    public void VerifyQol(Action<string,Window> capture)
    {
        Show();
        var weapon=ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt"))!;
        SetItem(weapon,"QOL fixture");
        var before=JsonSerializer.Serialize(CaptureFilters());
        rowToggles[drafts[0].Filter.GroupId]();
        if(!UndoFilterButton.IsEnabled) throw new Exception("Filter undo did not record selection");
        UndoFilters(this,new RoutedEventArgs());
        if(JsonSerializer.Serialize(CaptureFilters())!=before) throw new Exception("Filter undo failed to restore state");
        var skills=drafts.First(x=>x.Filter.Text.Contains("Level of all Melee Skills",StringComparison.OrdinalIgnoreCase));
        var template=new FilterProfile("Melee test",weapon.ItemClass,true,new[] {new ProfileStat(ComparableMarket.Signature(skills.Filter.Text),skills.Filter.Kind)},new[] {"cannot regenerate"});
        profiles.Add(template);
        profileWriterForVerification=_=>throw new IOException("Simulated full disk");
        ApplyProfile(template);
        if(!QolStatus.Text.Contains("session-only") || !profileSaveError.Contains("Could not save")) throw new Exception("Profile failure was overwritten by success feedback");
        profilesWritable=false;
        if(SaveProfiles() || !profileSaveError.Contains("preserved")) throw new Exception("Unreadable profile protection failed");
        profilesWritable=true; profileWriterForVerification=null;
        if(!SaveProfiles() || profileSaveError.Length!=0) throw new Exception("Profile save did not recover");
        SetItem(ItemParser.Parse(weapon.Details.Replace("+4 to Level of all Melee Skills","+6 to Level of all Melee Skills")),"QOL fixture");
        ApplyProfile(template);
        var applied=drafts.Where(x=>x.Filter.Enabled).ToArray();
        if(applied.Length!=1 || applied[0].Min.Text!="6") throw new Exception("Profile did not use the current item's whole skill-level bound");
        var state=JsonSerializer.Deserialize<ProfileFile>(JsonSerializer.Serialize(new ProfileFile(profiles,activeProfile)))!;
        if(state.Active!="Melee test" || state.Profiles.Last().Warnings.Single()!="cannot regenerate") throw new Exception("Profile persistence round-trip failed");
        ActiveFilters.IsExpanded=true; ResizeToItemContent(); capture("artifacts/qol-filters.png",this);
        OpenProfiles(this,new RoutedEventArgs()); capture("artifacts/qol-profiles.png",profileWindow!); profileWindow!.Close();
        SetItem(ItemParser.Parse("Item Class: Waystones\nRarity: Rare\nDangerous Passage\nWaystone (Tier 10)\n--------\nPlayers cannot Regenerate Life\n20% increased Pack Size"),"QOL fixture");
        if(MapWarnings.Visibility!=Visibility.Visible || !WarningText.Text.Contains("cannot Regenerate")) throw new Exception("Configured map warning not shown");
        ResizeToItemContent(); capture("artifacts/qol-map-warning.png",this);
        SetItem(weapon,"QOL fixture");
        if(MapWarnings.Visibility!=Visibility.Collapsed) throw new Exception("Map warnings leaked onto non-map item");
        profiles.Remove(template); activeProfile=null; ActiveFilters.IsExpanded=false; RefreshWarnings(); Hide();
    }
}

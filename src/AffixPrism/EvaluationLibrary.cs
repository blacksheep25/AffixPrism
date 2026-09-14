using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AffixPrism.Core;
namespace AffixPrism;
public partial class EvaluationWindow
{
    public event Action<CopiedItem>? BookmarkOpened;
    private readonly List<Window> pinnedWindows = new();
    private Window? bookmarkWindow;
    private ItemBookmarks BookmarkStore => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AffixPrism", "bookmarks.json"));
    public void VerifyPinSnapshot()
    {
        var before=item;
        PinToScreen(this,new RoutedEventArgs());
        if(pinnedWindows.Count!=1 || !pinnedWindows[0].Topmost) throw new Exception("Pin window missing");
        var pin=pinnedWindows[0];
        SetItem(ItemParser.Parse("Rarity: Currency\nDivine Orb\n--------\nStack Size: 1/10"),"fixture");
        if(!pin.IsVisible || !pin.Title.Contains(before!.Name)) throw new Exception("Pin changed with active item");
        Hide(); if(!pin.IsVisible) throw new Exception("Pin hidden with checker");
        pin.Close(); if(pinnedWindows.Count!=0) throw new Exception("Pin not released");
        SetItem(before,"fixture"); Show();
    }
    private void BookmarkItem(object sender, RoutedEventArgs e)
    {
        if (item == null || IsTestMode) return;
        try { BookmarkStore.Save(item, ItemHeaderArt.Url); BookmarkButton.Content = "★ Saved"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { MessageBox.Show(this,"Could not save bookmark: " + ex.Message); }
    }
    private Window CreateItemWindow(string title, double width, double height, out DockPanel body)
    {
        body = new DockPanel { Margin = new Thickness(8) };
        var window = new Window { Title=title, Width=width, Height=height, MinWidth=320, MinHeight=220, Topmost=true, ShowInTaskbar=false, WindowStyle=WindowStyle.None, ResizeMode=ResizeMode.CanResizeWithGrip, Background=new SolidColorBrush(Theme.Colour("Background")), Foreground=Foreground, Icon=Icon, Content=new Border { BorderBrush=Theme.Brush("Muted"), BorderThickness=new Thickness(1), Child=body }, WindowStartupLocation=WindowStartupLocation.CenterScreen };
        var header = new DockPanel { Margin=new Thickness(0,0,0,8), Background=Brushes.Transparent };
        var close = new Button { Content="Close", Padding=new Thickness(8,3,8,3) }; close.Click += (_,_)=>window.Close(); DockPanel.SetDock(close,Dock.Right); header.Children.Add(close);
        var drag = new TextBlock { Text=title, Padding=new Thickness(5), Cursor=Cursors.SizeAll }; drag.MouseLeftButtonDown += (_,e)=> { if(e.ButtonState==MouseButtonState.Pressed) window.DragMove(); }; header.Children.Add(drag); DockPanel.SetDock(header,Dock.Top); body.Children.Add(header);
        window.PreviewKeyDown += (_,e)=> { if(e.Key==Key.Escape) window.Close(); };
        return window;
    }
    private void PinToScreen(object sender, RoutedEventArgs e)
    {
        if(item == null) return;
        var snapshot=item;
        var window=CreateItemWindow("Pinned · " + snapshot.Name,420,620,out var body);
        body.Children.Add(new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Content=new ItemPreviewCard { Item=snapshot, IconUrl=ItemHeaderArt.Url } });
        pinnedWindows.Add(window); window.Closed += (_,_)=>pinnedWindows.Remove(window);
        window.Show();
    }
    public void OpenBookmarks(object sender, RoutedEventArgs e)
    {
        if(bookmarkWindow != null) { bookmarkWindow.Activate(); return; }
        try
        {
            var store=BookmarkStore;
            var window=CreateItemWindow("Bookmarked items",520,500,out var body);
            var list=new ListBox { Background=Background, Foreground=Foreground, Margin=new Thickness(0,5,0,5) };
            var actions=new StackPanel { Orientation=Orientation.Horizontal };
            var open=new Button { Content="Open and refresh price",Padding=new Thickness(8,4,8,4),Margin=new Thickness(3) };
            var remove=new Button { Content="Remove",Padding=new Thickness(8,4,8,4),Margin=new Thickness(3) };
            actions.Children.Add(open); actions.Children.Add(remove); DockPanel.SetDock(actions,Dock.Bottom); body.Children.Add(actions);
            var notePanel=new StackPanel();
            notePanel.Children.Add(new TextBlock { Text="Bookmark notes",Margin=new Thickness(3) });
            var notes=new TextBox { AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=70,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var saveNotes=new Button { Content="Save notes",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(3) };
            notePanel.Children.Add(notes); notePanel.Children.Add(saveNotes); DockPanel.SetDock(notePanel,Dock.Bottom); body.Children.Add(notePanel);
            list.SelectionChanged += (_,_) => notes.Text=(list.SelectedItem as ItemBookmark)?.Notes ?? "";
            saveNotes.Click += (_,_) => { if(list.SelectedItem is ItemBookmark saved) { try { store.UpdateNotes(saved,notes.Text); var updated=store.Read(); list.ItemsSource=updated; list.SelectedItem=updated.FirstOrDefault(x=>x.Item.Details==saved.Item.Details); saveNotes.Content="Notes saved"; } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { MessageBox.Show(window,ex.Message); } } };
            var hint=new TextBlock { Text="Saved item snapshots · prices refresh in your selected league",TextWrapping=TextWrapping.Wrap }; DockPanel.SetDock(hint,Dock.Top); body.Children.Add(hint); body.Children.Add(list);
            void Refresh() { list.ItemsSource=store.Read(); list.SelectedIndex=list.Items.Count>0 ? 0 : -1; if(list.Items.Count==0) hint.Text="No bookmarks yet. Use ☆ Bookmark in the price checker."; }
            void Reopen() { if(list.SelectedItem is ItemBookmark saved) { BookmarkOpened?.Invoke(saved.Item); ItemHeaderArt.Url=saved.IconUrl; window.Close(); } }
            open.Click += (_,_)=>Reopen(); list.MouseDoubleClick += (_,_)=>Reopen();
            remove.Click += (_,_)=> { if(list.SelectedItem is ItemBookmark saved) { try { store.Remove(saved); Refresh(); } catch(IOException ex) { MessageBox.Show(window,ex.Message); } } };
            Refresh(); bookmarkWindow=window; window.Closed += (_,_)=>bookmarkWindow=null; window.Show();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { MessageBox.Show(this,"Could not read bookmarks: " + ex.Message); }
    }
}

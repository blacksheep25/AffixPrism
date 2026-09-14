using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AffixPrism.Core;
namespace AffixPrism;
public partial class MainWindow
{
    private void OpenSavedEquipment(object sender, RoutedEventArgs e) => evaluation.OpenBookmarks(sender,e);
    private void ReturnToComparison(object sender, RoutedEventArgs e)
    {
        if(currentItem == null) { Notice.Text="Hover an item in POE2 and press Alt+E, or choose a bookmarked item."; return; }
        Hide(); evaluation.Show(); evaluation.Activate();
    }
    private void GuideCategoryChanged(object sender, SelectionChangedEventArgs e) { if(ready && Tabs.SelectedItem==GuidesTab) LoadGuide(sender,e); }
    private void BrowseResearchCategory(object sender,RoutedEventArgs e)
    {
        string? type=((ComboBoxItem)OpportunityCategory.SelectedItem).Tag?.ToString();
        Tabs.SelectedItem=PricesTab;
        var entry=PriceCategory.Items.OfType<ListBoxItem>().FirstOrDefault(x=>x.Tag is EconomyCategory category && category.Type==type);
        if(entry!=null) PriceCategory.SelectedItem=entry;
    }
    private void RenderShortlist()
    {
        if(ShortlistRows==null) return;
        var rows=marketFavourites.Where(x=>x.Key.StartsWith(settings.League+"|",StringComparison.Ordinal)).Select(x=>x.Value).OrderBy(x=>x.Name).ToArray();
        ShortlistRows.ItemsSource=rows.Select(x=>new MarketViewRow(x,true,browserRates)).ToArray();
        ShortlistStatus.Text=rows.Length==0 ? "No saved market items. Open Market Prices and click ☆ beside a price." : $"{rows.Length} saved items · {settings.League} · saved reference prices; refresh to update";
    }
    private async void RefreshShortlist(object sender,RoutedEventArgs e)
    {
        if(smoke) { RenderShortlist(); return; }
        RefreshShortlistButton.IsEnabled=false;
        string league=settings.League; int updated=0,missing=0; bool stale=false;
        try
        {
            var entries=marketFavourites.Where(x=>x.Key.StartsWith(league+"|",StringComparison.Ordinal)).ToArray();
            foreach(var group in entries.GroupBy(x=>(x.Value.CategoryType,x.Value.ExchangeCategory)))
            {
                if(group.Key.CategoryType==null) { missing+=group.Count(); continue; }
                ShortlistStatus.Text="Refreshing "+group.Key.CategoryType+"…";
                var data=await economy.CategoryAsync(new(group.Key.CategoryType,group.Key.ExchangeCategory),league,lifetime.Token,TimeSpan.FromMinutes(5));
                if(settings.League!=league) return;
                stale|=data.Stale;
                foreach(var entry in group)
                {
                    var row=data.Rows.FirstOrDefault(r=>r.Name==entry.Value.Name && r.Variant==entry.Value.Variant && r.Currency==entry.Value.Currency && r.Corrupted==entry.Value.Corrupted);
                    if(row==null) { missing++; continue; }
                    marketFavourites[entry.Key]=row; updated++;
                }
            }
            try { browserRates=await economy.CategoryAsync(new("Currency",true),league,lifetime.Token,TimeSpan.FromMinutes(5)); } catch(System.Net.Http.HttpRequestException) { browserRates=null; }
            if(settings.League!=league) return;
            File.WriteAllText(FavouriteFile+".tmp",JsonSerializer.Serialize(marketFavourites)); File.Move(FavouriteFile+".tmp",FavouriteFile,true);
            RenderShortlist(); UpdateHome();
            ShortlistStatus.Text=$"{updated} prices updated · {league}"+(stale ? " · STALE cached data" : " · "+DateTimeOffset.Now.ToString("dd MMM HH:mm"))+(missing>0 ? $" · {missing} unavailable or older entries; re-save these from their market category." : "");
        }
        catch(Exception ex) when(ex is System.Net.Http.HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException) { RenderShortlist(); ShortlistStatus.Text="Refresh incomplete: "+ex.Message; }
        finally { RefreshShortlistButton.IsEnabled=true; }
    }
    private sealed record LocalCharacter(string Name,string Class,int Level,string? Portrait);
    private LocalCharacter? localCharacter;
    private string CharacterFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AffixPrism","character.json");
    private void LoadCharacter()
    {
        if(!smoke) try { if(File.Exists(CharacterFile)) localCharacter=JsonSerializer.Deserialize<LocalCharacter>(File.ReadAllText(CharacterFile)); }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException) { Notice.Text="Character profile could not be loaded."; }
        RenderCharacter();
    }
    private void RenderCharacter()
    {
        if(localCharacter==null) return;
        HomeCharacterName.Text=localCharacter.Name;
        HomeCharacterClass.Text=$"{localCharacter.Class} · Level {localCharacter.Level} · local profile";
        if(localCharacter.Portrait is { } path && File.Exists(path)) try
        {
            var image=new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.DecodePixelWidth=160; image.UriSource=new Uri(path); image.EndInit(); image.Freeze(); HomeCharacterImage.Source=image;
        }
        catch(Exception ex) when(ex is IOException or NotSupportedException or ArgumentException) { Notice.Text="Could not load the saved portrait. Choose another image in Edit character."; }
    }
    private void EditCharacter(object sender,RoutedEventArgs e)
    {
        var panel=new StackPanel { Margin=new Thickness(20) };
        var dialog=new Window { Owner=this,Title="Your character",Width=440,Height=500,ResizeMode=ResizeMode.CanResize,MinWidth=360,MinHeight=360,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new System.Windows.Media.SolidColorBrush(Theme.Colour("Surface")),Foreground=Foreground,Resources=Resources,Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto } };
        panel.Children.Add(new TextBlock { Text="Character profile",FontSize=22,Margin=new Thickness(0,0,0,10) });
        panel.Children.Add(new TextBlock { Text="Stored on this device. Automatic account/character import is not connected.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) });
        TextBox Field(string label,string value) { panel.Children.Add(new TextBlock { Text=label }); var box=new TextBox { Text=value,Margin=new Thickness(0,4,0,8) }; panel.Children.Add(box); return box; }
        var name=Field("Character name",localCharacter?.Name??""); var characterClass=Field("Class / ascendancy",localCharacter?.Class??""); var level=Field("Level",localCharacter?.Level.ToString()??"1");
        string? portrait=localCharacter?.Portrait;
        var choose=new Button { Content="Choose portrait image…",HorizontalAlignment=HorizontalAlignment.Left };
        choose.Click+=(_,_)=> { var picker=new Microsoft.Win32.OpenFileDialog { Filter="Portrait images|*.png;*.jpg;*.jpeg;*.bmp" }; if(picker.ShowDialog(dialog)==true) { portrait=picker.FileName; choose.Content=Path.GetFileName(portrait); } }; panel.Children.Add(choose);
        var status=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) }; panel.Children.Add(status);
        var save=new Button { Content="Save character",HorizontalAlignment=HorizontalAlignment.Right }; panel.Children.Add(save);
        save.Click+=(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(name.Text) || !int.TryParse(level.Text,out int n) || n<1 || n>100) { status.Text="Enter a name and a level from 1 to 100."; return; }
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(CharacterFile)!);
                if(portrait!=null) { var info=new FileInfo(portrait); if(info.Length>10*1024*1024) { status.Text="Choose an image smaller than 10 MB."; return; } var target=Path.Combine(Path.GetDirectoryName(CharacterFile)!,"character-portrait"+Path.GetExtension(portrait)); if(!Path.GetFullPath(portrait).Equals(target,StringComparison.OrdinalIgnoreCase)) File.Copy(portrait,target,true); portrait=target; }
                var next=new LocalCharacter(name.Text.Trim(),characterClass.Text.Trim(),n,portrait); File.WriteAllText(CharacterFile+".tmp",JsonSerializer.Serialize(next)); File.Move(CharacterFile+".tmp",CharacterFile,true); localCharacter=next; RenderCharacter(); dialog.Close();
            } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { status.Text="Could not save profile: "+ex.Message; }
        };
        dialog.ShowDialog();
    }
}

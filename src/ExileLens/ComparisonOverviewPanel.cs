using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileLens.Core;

namespace ExileLens;

public sealed class ComparisonOverviewPanel : Border
{
    public ComparisonOverviewPanel(CopiedItem yours,CopiedItem other,string otherLabel)
    {
        Background=new SolidColorBrush(Color.FromRgb(24,24,19)); Padding=new Thickness(12); Margin=new Thickness(5,0,5,12);
        var root=new StackPanel();
        Child=new Expander { Header="AT A GLANCE", IsExpanded=false, Foreground=Brushes.Khaki, Content=root };
        var priority=new ComboBox { ItemsSource=ComparisonOverview.Priorities, SelectedIndex=0, Width=180, HorizontalAlignment=HorizontalAlignment.Left, Margin=new Thickness(0,5,0,10) };
        root.Children.Add(priority);
        var body=new StackPanel(); root.Children.Add(body);
        void Render()
        {
            body.Children.Clear(); var result=ComparisonOverview.Create(yours,other,(string)priority.SelectedItem);
            body.Children.Add(new TextBlock { Text=result.Verdict.Replace("Other item",otherLabel), FontSize=16, FontWeight=FontWeights.SemiBold, Foreground=Brushes.Wheat, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,8) });
            var table=new Grid(); table.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(2,GridUnitType.Star) });
            for(int i=0;i<3;i++) table.ColumnDefinitions.Add(new ColumnDefinition());
            void Cell(string text,int row,int column,Brush brush) { var label=new TextBlock { Text=text,Foreground=brush,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(3,3,3,3) }; Grid.SetRow(label,row); Grid.SetColumn(label,column); table.Children.Add(label); }
            table.RowDefinitions.Add(new RowDefinition()); Cell("Key stat",0,0,Brushes.Wheat); Cell("Your item",0,1,Brushes.Wheat); Cell(otherLabel,0,2,Brushes.Wheat); Cell("Difference",0,3,Brushes.Wheat);
            int row=1;
            foreach(var stat in result.Stats)
            {
                table.RowDefinitions.Add(new RowDefinition());
                var yourColour=stat.Direction<0 ? Brushes.LightGreen : stat.Direction>0 ? Brushes.LightCoral : Brushes.LightGray;
                var otherColour=stat.Direction>0 ? Brushes.LightGreen : stat.Direction<0 ? Brushes.LightCoral : Brushes.LightGray;
                Cell(stat.Name,row,0,Brushes.LightGray);
                Cell(stat.Yours?.ToString("0.##",CultureInfo.InvariantCulture)??"—",row,1,yourColour);
                Cell(stat.Other?.ToString("0.##",CultureInfo.InvariantCulture)??"—",row,2,otherColour);
                Cell(stat.Difference,row,3,otherColour);
                row++;
            }
            body.Children.Add(table);
            foreach(var text in new[]{"Higher on other item: "+(result.Gains.Length>0 ? result.Gains : "None"),"Lower on other item: "+(result.Losses.Length>0 ? result.Losses : "None"),"Green/red means higher/lower, not necessarily better/worse. Missing stats are unknown. Choose a priority to narrow the comparison."})
                body.Children.Add(new TextBlock { Text=text,Foreground=Brushes.DarkKhaki,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0),FontSize=11 });
        }
        priority.SelectionChanged+=(_,_)=>Render(); Render();
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AffixPrism.Core;
namespace AffixPrism;
internal static class FoilVisual
{
    public static bool IsFoil(CopiedItem item) => item.Details.Replace("\r", "").Split('\n').Any(l => l.Trim().Equals("Foil Unique", StringComparison.OrdinalIgnoreCase));
    public static Brush Rainbow()
    {
        var brush = new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,1) };
        var colours = new[]{"#F3D66A","#AEE779","#85DDEC","#B5A0EE","#EEA4C7","#F3D66A"};
        for(int i=0;i<colours.Length;i++) brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colours[i]),i/(double)(colours.Length-1)));
        if(SystemParameters.ClientAreaAnimation)
        {
            var turn=new RotateTransform(0,.5,.5); brush.RelativeTransform=turn;
            turn.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(8)) { RepeatBehavior=RepeatBehavior.Forever });
        }
        return brush;
    }
    public static void Frame(Border frame)
    {
        frame.BorderBrush=Rainbow();
        frame.BorderThickness=new Thickness(2);
        frame.Background=new LinearGradientBrush(new GradientStopCollection {
            new GradientStop(Color.FromRgb(37,31,15),0),new GradientStop(Color.FromRgb(22,30,29),.3),
            new GradientStop(Color.FromRgb(31,22,37),.6),new GradientStop(Color.FromRgb(37,31,15),1)},45);
    }
}

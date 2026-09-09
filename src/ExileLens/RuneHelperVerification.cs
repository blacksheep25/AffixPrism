using System;
using System.IO;
using System.Linq;
using System.Windows;
using ExileLens.Core;

namespace ExileLens;
public partial class MainWindow
{
    private void VerifyRuneHelper()
    {
        string[] expected={ "The Runefather's Alloy","Sovereign Alloy","Mystic Alloy","Prismatic Alloy","Swift Alloy","Cyclonic Alloy","Expansive Alloy","Protective Alloy","Adaptive Alloy","Runic Alloy" };
        // Generate our own OCR fixture so a clean checkout needs no reference repository.
        using var source=new System.Drawing.Bitmap(620,500);
        using(var graphics=System.Drawing.Graphics.FromImage(source))
        using(var font=new System.Drawing.Font("Arial",22))
        {
            graphics.Clear(System.Drawing.Color.White);
            for(int i=0;i<expected.Length;i++) graphics.DrawString(expected[i],font,System.Drawing.Brushes.Black,12,8+i*48);
        }
        using var names=(System.Drawing.Bitmap)source.Clone();
        var text=RuneOcr.Read(names);
        File.WriteAllLines("artifacts/rune-ocr-check.txt",text.Select(x=>$"{x.Confidence:0} · {x.Text}"));
        var prices=expected.Select((name,i)=>new EconomyRow(name,"","",i+1,"Exalted Orb",null,null)).ToArray();
        var matches=text.Select(line=>(Text:line,Match:RuneNames.Match(line.Text,line.Confidence,prices))).Where(x=>x.Match!=null).Select(x=>(x.Text,Match:x.Match!)).ToArray();
        if(matches.Select(x=>x.Match.Row.Name).Distinct().Count()<8) throw new Exception("Rune menu OCR fixture recognised fewer than 8 of 10 choices. See rune-ocr-check.txt.");
        var helper=new RuneHelperWindow(economy,true) { Resources=Resources };
        helper.Open("UI fixture"); helper.ShowRecognitionFixture(); Capture("artifacts/rune-helper.png",helper); helper.Shutdown();
        var labels=new RunePricesOverlay();
        labels.Render(new System.Drawing.Rectangle(100,100,300,source.Height),matches,"UI fixture prices");
        Capture("artifacts/rune-price-labels.png",labels);
        labels.Close();
    }
}

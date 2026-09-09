using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Tesseract;

namespace ExileLens;
internal sealed record RuneText(string Text, float Confidence, double Y);
internal static class RuneOcr
{
    public static IReadOnlyList<RuneText> Capture(Rectangle region)
    {
        if (region.Width < 50 || region.Height < 30 || region.Width > 1600 || region.Height > 1200) throw new ArgumentException("Select only the choice list (50–1600 px wide, 30–1200 px high).");
        using var source = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(source)) g.CopyFromScreen(region.Location, Point.Empty, region.Size);
        return Read(source);
    }
    internal static IReadOnlyList<RuneText> Read(Bitmap source)
    {
        // Upscale and invert light game lettering. Nothing is written to disk or uploaded.
        using var scaled = new Bitmap(source.Width * 2, source.Height * 2, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        using (var attributes = new ImageAttributes())
        {
            double light=0; int count=0;
            for(int y=0;y<source.Height;y+=20) for(int x=0;x<source.Width;x+=20) { var c=source.GetPixel(x,y); light+=(c.R+c.G+c.B)/3.0; count++; }
            if(light/Math.Max(1,count)<127) attributes.SetColorMatrix(new ColorMatrix(new[] { new float[] {-.299f,-.299f,-.299f,0,0},new float[] {-.587f,-.587f,-.587f,0,0},new float[] {-.114f,-.114f,-.114f,0,0},new float[] {0,0,0,1,0},new float[] {1,1,1,0,1} }));
            g.DrawImage(source, new Rectangle(0,0,scaled.Width,scaled.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        }
        using var stream = new MemoryStream(); scaled.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        using var engine = new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"tessdata"),"eng",EngineMode.LstmOnly);
        using var page = engine.Process(pix, PageSegMode.SparseText);
        using var iter = page.GetIterator();
        var result = new List<RuneText>(); iter.Begin();
        do
        {
            var text = iter.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var box))
                result.Add(new(text,iter.GetConfidence(PageIteratorLevel.TextLine),box.Y1 / 2.0));
        } while (iter.Next(PageIteratorLevel.TextLine));
        return result;
    }
}

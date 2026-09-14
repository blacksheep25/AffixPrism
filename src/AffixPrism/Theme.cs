using System.Windows;
using System.Windows.Media;
namespace AffixPrism;
internal static class Theme
{
    public static Color Colour(string role) => (Color)Application.Current.FindResource("Theme."+role+"Color");
    public static Brush Brush(string role) => (Brush)Application.Current.FindResource("Theme."+role);
}

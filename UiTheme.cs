using System.Windows;
using System.Windows.Media;

namespace ArtoSalesCopilot;

internal static class UiTheme
{
    internal static readonly Dictionary<string,(string Dark,string Light)> Palette=new()
    {
        ["Bg"]=("#101418","#F4F6F5"),["Panel"]=("#191F25","#FFFFFF"),
        ["Ink"]=("#F0F3F5","#17252D"),["Muted"]=("#A8B4BF","#53636F"),
        ["Accent"]=("#A7E6CE","#176B4D"),["Line"]=("#36414C","#CBD5DB"),
        ["Input"]=("#242D35","#F8FAF9"),["Field"]=("#10161C","#FFFFFF"),
        ["Hover"]=("#3A4C50","#DDEEE6"),["FocusRing"]=("#FFFFFF","#155EA8"),
        ["PrimaryFill"]=("#A7E6CE","#176B4D"),["PrimaryInk"]=("#102C23","#FFFFFF"),
        ["Selected"]=("#243831","#E0EEE5"),["SelectedBorder"]=("#3D6052","#9BBAA9"),
        ["StatusPanel"]=("#1C2927","#E8F0EB"),["AdvicePanel"]=("#1B2B25","#EDF5F0"),
        ["AdviceBorder"]=("#416454","#AFCABC"),["VoiceBorder"]=("#435B70","#B8CDD9"),
        ["AudioAccent"]=("#A3C8F0","#246697"),["Warning"]=("#F1CD91","#79501B"),
        ["ScrollThumb"]=("#586571","#8597A2"),["TranscriptRep"]=("#243831","#E8F2EB"),
        ["TranscriptClient"]=("#222B34","#F0F3F5"),["Chip"]=("#243039","#EDF2F4")
    };
    public static void Apply(string theme)
    {
        foreach(var (key,colors) in Palette)
        {
            var color=(Color)ColorConverter.ConvertFromString(theme=="light"?colors.Light:colors.Dark);
            // Mutable brushes also refresh transcript rows created before the theme switch.
            if(Application.Current.Resources[key] is SolidColorBrush brush&&!brush.IsFrozen)brush.Color=color;
            else Application.Current.Resources[key]=new SolidColorBrush(color);
        }
    }
}

using System.Windows;
using System.Windows.Media;

namespace ArtoSalesCopilot;

internal static class UiTheme
{
    internal static readonly Dictionary<string,(string Dark,string Light)> Palette=new()
    {
        ["Bg"]=("#121B18","#F6F5F0"),["Panel"]=("#1B2621","#FFFFFF"),
        ["Ink"]=("#ECF2ED","#192F25"),["Muted"]=("#ABBAB1","#52665A"),
        ["Accent"]=("#98DEB9","#186546"),["Line"]=("#37483E","#CCD5CC"),
        ["Input"]=("#24372D","#EDF2EC"),["Field"]=("#15221B","#FAFBF7"),
        ["Hover"]=("#304B3C","#DCE9DB"),["FocusRing"]=("#B7F3D0","#155EAA"),
        ["PrimaryFill"]=("#A4E6C2","#196447"),["PrimaryInk"]=("#102C20","#FFFFFF"),
        ["Selected"]=("#254533","#DCEBDF"),["SelectedBorder"]=("#72B28E","#547C62"),
        ["StatusPanel"]=("#16271F","#EAF0E5"),["AdvicePanel"]=("#1C3227","#E8EEDF"),
        ["AdviceBorder"]=("#496B55","#B5C8AA"),["VoiceBorder"]=("#3A5143","#C4D0C1"),
        ["AudioAccent"]=("#AFCAE1","#356383"),["Warning"]=("#E1BF80","#77501A"),
        ["ScrollThumb"]=("#6F8376","#748778"),["TranscriptRep"]=("#1B3024","#E7EEE0"),
        ["TranscriptClient"]=("#121B18","#F6F5F0"),["Chip"]=("#23362A","#E7EDE2"),
        ["Rail"]=("#0D1712","#ECEEE5"),["Metal"]=("#C1A776","#80663B"),
        ["LivePill"]=("#3A221D","#F6E1DC"),["LiveDot"]=("#FF8B74","#A32E23"),
        ["ControlLine"]=("#718779","#6D8373"),["Danger"]=("#FFB5A5","#A32E23")
    };
    internal static Color ColorFor(string key,string theme,bool highContrast)
    {
        var colors=Palette[key];
        if(!highContrast)return (Color)ColorConverter.ConvertFromString(theme=="light"?colors.Light:colors.Dark);
        return key is "Ink" or "Muted" or "Accent" or "Metal" or "Line" or "ControlLine" or "ScrollThumb" or "VoiceBorder" or "AdviceBorder" or "AudioAccent" or "Warning" or "Danger"?SystemColors.WindowTextColor
            :key is "PrimaryFill" or "FocusRing" or "SelectedBorder" or "LiveDot"?SystemColors.HighlightColor
            :key=="PrimaryInk"?SystemColors.HighlightTextColor:SystemColors.WindowColor;
    }
    public static void Apply(string theme)
    {
        foreach(var (key,colors) in Palette)
        {
            var color=ColorFor(key,theme,SystemParameters.HighContrast);
            // Mutable brushes also refresh transcript rows created before the theme switch.
            if(Application.Current.Resources[key] is SolidColorBrush brush&&!brush.IsFrozen)brush.Color=color;
            else Application.Current.Resources[key]=new SolidColorBrush(color);
        }
    }
}

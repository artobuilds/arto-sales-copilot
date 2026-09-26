using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ArtoSalesCopilot;

internal static class UiText
{
    internal sealed record Entry(string Id,string Source,string Ru,string En,string Uk);
    internal static readonly Entry[] Entries=Load();
    static readonly Dictionary<string,Entry> Lookup=BuildLookup();
    static readonly List<WeakReference<Message>> Messages=[];
    public static string Language {get;private set;}="ru";
    static Entry[] Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ArtoSalesCopilot.UiStrings.json")??throw new FileNotFoundException("UI translations missing.");
        return JsonSerializer.Deserialize<Entry[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    }
    static Dictionary<string,Entry> BuildLookup()
    {
        var result=new Dictionary<string,Entry>(StringComparer.Ordinal);
        foreach(var e in Entries)result[e.Source]=e;
        foreach(var e in Entries)foreach(var text in new[]{e.Ru,e.En,e.Uk})result.TryAdd(text,e);
        return result;
    }
    public static string NormalizeLanguage(string? value)=>value is "en" or "uk"?value:"ru";
    public static bool Contains(string source)=>Lookup.ContainsKey(source);
    public static string Get(string source,params object[] args)
    {
        var text=Lookup.TryGetValue(source,out var entry)?Language switch{"en"=>entry.En,"uk"=>entry.Uk,_=>entry.Ru}:source;
        return args.Length==0?text:string.Format(CultureInfo.GetCultureInfo(Language switch{"en"=>"en-US","uk"=>"uk-UA",_=>"ru-RU"}),text,args);
    }
    public static void SetLanguage(string language)
    {
        Language=NormalizeLanguage(language);
        foreach(var e in Entries)Application.Current.Resources[e.Id]=Language switch{"en"=>e.En,"uk"=>e.Uk,_=>e.Ru};
        foreach(var weak in Messages.ToArray())if(weak.TryGetTarget(out var message))message.Refresh();
        Messages.RemoveAll(w=>!w.TryGetTarget(out _));
    }
    public static void Bind(DependencyObject target,DependencyProperty property,Func<string> value)
    {
        var message=new Message(value);Messages.Add(new(message));
        if(Messages.Count>1500)Messages.RemoveAll(w=>!w.TryGetTarget(out _));
        BindingOperations.SetBinding(target,property,new Binding(nameof(Message.Value)){Source=message,Mode=BindingMode.OneWay});
    }
    sealed class Message(Func<string> value):INotifyPropertyChanged
    {
        public string Value=>value();
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh()=>PropertyChanged?.Invoke(this,new(nameof(Value)));
    }
}

public partial class MainWindow
{
    bool appearanceChanging;
    static string T(string source,params object[] args)=>UiText.Get(source,args);
    static void SetUi(TextBlock target,Func<string> value)=>UiText.Bind(target,TextBlock.TextProperty,value);
    TextBlock TranscriptHeading(Utterance turn,string stamp)
    {
        var text=new TextBlock{FontSize=12,Margin=new(0,0,0,5)};
        text.SetResourceReference(TextBlock.ForegroundProperty,turn.Speaker=="rep"?"Accent":"Muted");
        SetUi(text,()=>T("{0} · {1}",T(turn.Speaker=="rep"?"Ты":"Клиент"),stamp));return text;
    }
    void AppearanceChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready||appearanceChanging)return;
        var language=UiText.NormalizeLanguage((InterfaceLanguageBox.SelectedItem as ComboBoxItem)?.Tag as string);
        var theme=(ThemeBox.SelectedItem as ComboBoxItem)?.Tag as string=="light"?"light":"dark";
        try
        {
            Store.SaveAppearance(language,theme);
            settings.InterfaceLanguage=language;settings.Theme=theme;
            ApplyAppearance();
        }
        catch(Exception ex){StatusText.ToolTip=ex.Message;SetUi(StatusText,()=>T("Не удалось сохранить оформление. Попробуй ещё раз."));}
    }
    void ApplyAppearance()
    {
        appearanceChanging=true;
        try
        {
            UiText.SetLanguage(settings.InterfaceLanguage);UiTheme.Apply(settings.Theme);
            RenderVoice();
            RefreshLabels(MicrophoneBox);RefreshLabels(OutputBox);RefreshLabels(VideoSourceBox);
        }
        finally{appearanceChanging=false;}
    }
    static void RefreshLabels(ComboBox box)
    {
        if(box.ItemsSource is not System.Collections.IEnumerable source)return;
        var selected=box.SelectedItem;var items=source.Cast<object>().ToArray();box.ItemsSource=items;box.SelectedItem=selected;
    }
}

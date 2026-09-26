using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    async Task CheckAppearance(string directory,List<string> checks)
    {
        feedbackTimer.Stop();FeedbackBox.Visibility=Visibility.Collapsed;
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("Appearance check failed: "+label);checks.Add("PASS "+label);}
        foreach(var entry in UiText.Entries)
        {
            if(string.IsNullOrWhiteSpace(entry.Ru)||string.IsNullOrWhiteSpace(entry.En)||string.IsNullOrWhiteSpace(entry.Uk))throw new InvalidOperationException("Missing translation: "+entry.Id);
            foreach(var text in new[]{entry.Ru,entry.En,entry.Uk})_ = string.Format(text,1,2,3);
        }
        checks.Add("PASS all embedded translations exist and format in RU/UK/EN");
        var settingsPath=Path.Combine(Store.Root,"settings.json");
        var saved=JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();saved["futureSetting"]="keep-me";File.WriteAllText(settingsPath,saved.ToJsonString());
        var originalBrief=Store.Load().Brief.Client;ClientText.Text="Unfinished client edit — must not be auto-saved";
        var keyBytes=File.ReadAllBytes(Path.Combine(Store.Root,"typesafe.key"));
        scripted=true;ClearConversation();for(int i=0;i<4;i++)AddTurn(Demo.Turn(i,"en"));ShowAdvice(Demo.Advice(3,"en"));
        await Task.Delay(220);
        SetUi(ModeText,()=>T("Текстовый пример · без микрофона"));SetUi(StatusText,()=>T("Идёт текстовый пример. Реплики и подсказки подготовлены заранее. Микрофон выключен."));
        var clientPhrase=SayText.Text;var callLanguage=LanguageCode;
        foreach(var (code,index) in new[]{("ru",0),("uk",1),("en",2)})
        {
            InterfaceLanguageBox.SelectedIndex=index;await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Check(UiText.Language==code&&((TabItem)Tabs.Items[0]).Header.ToString()==T("Звонок"),"navigation switches to "+code);
            Check(ModeText.Text==T("Текстовый пример · без микрофона")&&VoiceHeading.Text==T("Голос и интонация"),"existing status and voice panel switch to "+code);
            Check(AdviceTitle.Text==Coach.Title("value",code)&&SayText.Text==clientPhrase&&LanguageCode==callLanguage,"UI language preserves client reply and call language: "+code);
            var turnPanel=(StackPanel)((Border)TranscriptPanel.Children[0]).Child;
            Check(((TextBlock)turnPanel.Children[0]).Text.StartsWith(T("Клиент"))&&((TextBlock)turnPanel.Children[1]).Text==Demo.Turn(0,"en").Text,"transcript headings translate but client words do not: "+code);
            Check(new VideoSource("Окно · Client Zoom","window").ToString()==T("Окно · {0}","Client Zoom"),"window label translates without altering its title: "+code);
            foreach(var (theme,themeIndex) in new[]{("dark",0),("light",1)})
            {
                ThemeBox.SelectedIndex=themeIndex;await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);UpdateLayout();
                var expected=(Color)ColorConverter.ConvertFromString(theme=="light"?UiTheme.Palette["Bg"].Light:UiTheme.Palette["Bg"].Dark);
                Check(((SolidColorBrush)Background).Color==expected,"theme applies to existing window: "+code+"/"+theme);
                var oldRow=(Border)TranscriptPanel.Children[0];
                Check(((SolidColorBrush)oldRow.Background).Color==((SolidColorBrush)FindResource("TranscriptClient")).Color&&Contrast(((SolidColorBrush)((TextBlock)turnPanel.Children[1]).Foreground).Color,((SolidColorBrush)oldRow.Background).Color)>=4.5,"existing transcript rows remain readable after switching: "+code+"/"+theme);
                var persisted=Store.Load();Check(persisted.InterfaceLanguage==code&&persisted.Theme==theme&&persisted.Language==callLanguage,"appearance persisted independently: "+code+"/"+theme);
                Check(persisted.Brief.Client==originalBrief&&JsonNode.Parse(File.ReadAllText(settingsPath))!["futureSetting"]!.ToString()=="keep-me","appearance save preserves unfinished brief and unrelated settings: "+code+"/"+theme);
                foreach(var size in new[]{(1000d,720d),(1320d,900d)})
                {
                    Width=size.Item1;Height=size.Item2;Tabs.SelectedIndex=0;UpdateLayout();await Task.Delay(200);
                    var bounds=VoiceCard.TransformToAncestor(this).TransformBounds(new Rect(new Point(),VoiceCard.RenderSize));
                    Check(bounds.Bottom<=ActualHeight&&bounds.Right<=ActualWidth&&AdviceCard.ActualHeight>=160,"call layout fits "+code+"/"+theme+" "+Width+"x"+Height);
                    Capture(Path.Combine(directory,$"appearance-{code}-{theme}-{Width:F0}.png"));
                }
                for(int page=1;page<4;page++){Tabs.SelectedIndex=page;UpdateLayout();await Task.Delay(200);Capture(Path.Combine(directory,$"page-{page}-{code}-{theme}.png"));}
                Capture(Path.Combine(directory,$"settings-{code}-{theme}.png"));
            }
        }
        Check(File.ReadAllBytes(Path.Combine(Store.Root,"typesafe.key")).SequenceEqual(keyBytes),"appearance switch leaves encrypted API key unchanged");
        foreach(var theme in new[]{"dark","light"})
        {
            UiTheme.Apply(theme);
            foreach(var pair in new[]{("Ink","Panel"),("Muted","Panel"),("Ink","AdvicePanel"),("PrimaryInk","PrimaryFill")})
            {
                var ratio=Contrast(((SolidColorBrush)FindResource(pair.Item1)).Color,((SolidColorBrush)FindResource(pair.Item2)).Color);
                Check(ratio>=4.5,$"text contrast {theme} {pair.Item1}/{pair.Item2}: {ratio:F2}:1");
            }
        }
        // Load a fresh window from the isolated saved preferences, never the user's normal settings.
        var restart=new MainWindow();Check(restart.settings.InterfaceLanguage=="en"&&restart.settings.Theme=="light"&&restart.LanguageCode==callLanguage,"new window restores saved language/theme without changing call language");restart.coach.Dispose();
        InterfaceLanguageBox.SelectedIndex=0;ThemeBox.SelectedIndex=0;settings.InterfaceLanguage="ru";settings.Theme="dark";ApplyAppearance();Tabs.SelectedIndex=0;
        ClientText.Text=originalBrief;scripted=false;ClearConversation();
    }
    static double Contrast(Color a,Color b)
    {
        static double L(Color c){static double V(byte b){var v=b/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}return .2126*V(c.R)+.7152*V(c.G)+.0722*V(c.B);}
        var x=L(a);var y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
}

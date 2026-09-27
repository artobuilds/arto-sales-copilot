using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArtoSalesCopilot;
public partial class MainWindow
{
 async Task CheckDisclosureLayout(string directory,List<string> checks)
 {
  void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("Call 0.7 check failed: "+label);checks.Add("PASS "+label);}
  Rect Bounds(FrameworkElement el)=>el.TransformToAncestor(RootLayout).TransformBounds(new Rect(el.RenderSize));
  void Fits(FrameworkElement el,string label){var b=Bounds(el);Check(el.IsVisible&&b.Left>=-.5&&b.Top>=-.5&&b.Right<=RootLayout.ActualWidth+.5&&b.Bottom<=RootLayout.ActualHeight+.5,label);}
  Tabs.SelectedIndex=0;scripted=false;ClearConversation();RefreshCallPresentation();UpdateLayout();
  Check(ReadinessPanel.IsVisible&&LiveButton.IsVisible&&!StopButton.IsVisible&&VoicePitchText.Text=="—","idle checklist, Start and real empty voice values");
  Check(!HeaderMeters.IsVisible&&!MiniButton.IsEnabled,"idle hides meters and disables mini");
  string savedClient=ClientText.Text,savedGoal=GoalText.Text;
  ClientText.Clear();GoalText.Clear();RefreshReadiness();Check(BriefBadgeText.Text=="1","blank briefing is not marked ready");
  ClientText.Text=savedClient;GoalText.Text=savedGoal;
  scripted=true;ShowAdvice(Demo.Advice(3,"en"));RefreshCallPresentation();
  Check(ModeText.Text==T("Текстовый пример")&&!IsCapturing&&MicMeter.Value==0&&OutputMeter.Value==0,"demo is labeled and has no fake meter values");
  foreach(var(language,index)in new[]{("ru",0),("uk",1),("en",2)})
  foreach(var(theme,themeIndex)in new[]{("dark",0),("light",1)})
  foreach(var size in new[]{(1000d,720d),(1320d,900d)})
  {
   InterfaceLanguageBox.SelectedIndex=index;ThemeBox.SelectedIndex=themeIndex;Width=size.Item1;Height=size.Item2;
   for(int i=0;i<3;i++)AddTurn(Demo.Turn(i,"en"));
   foreach(bool open in new[]{false,true})
   {
    manualOpen=open;RefreshManualVisibility();StatusText.Text=T("Не удалось сохранить оформление. Попробуй ещё раз.");
    RefreshCallPresentation();UpdateLayout();await Task.Delay(80);
    var suffix=$"{language}/{theme}/{Width:F0}/{open}";
    foreach(var el in new FrameworkElement[]{HeaderPanel,LiveButton,VoiceCard,VoicePitchText,VoicePauseText,TopicsCard,StatusBanner})Fits(el,"visible without overlap: "+el.Name+"/"+suffix);
    Check(Bounds(VoiceCard).Top>=Bounds(AdviceCard).Bottom+15&&Bounds(AdviceCard).Top>=Bounds(StatusBanner).Bottom+10,"banner, advice and voice remain separate: "+suffix);
    Check(AdviceScroll.ViewportHeight>=80&&SayText.ActualHeight<=AdviceScroll.ViewportHeight-AdviceTitle.ActualHeight-10,"ordinary phrase fits without scrolling: "+suffix);
    if(open)Fits(ManualText,"manual text fits: "+suffix);
    Check(VoicePitchLabel.TextWrapping==TextWrapping.NoWrap&&VoicePauseText.TextWrapping==TextWrapping.NoWrap,"voice labels and values remain single-line: "+suffix);
    Check(Tabs.Items.Cast<TabItem>().All(tab=>tab.Template.FindName("RailLabel",tab) is TextBlock label&&label.TextWrapping==TextWrapping.NoWrap&&label.ActualWidth<=tab.ActualWidth+.5),"rail labels stay on one line with full-name tooltips: "+suffix);
    if(compactCall){OpenTopics(this,new());await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);Check(TopicsPopup.IsOpen&&SignalsPanel.IsVisible,"compact topics popup exposes chips: "+suffix);TopicsPopup.IsOpen=false;}
    else Check(SignalsPanel.IsVisible,"full size topics are always visible: "+suffix);
   }
  }
  manualOpen=true;RefreshManualVisibility();ManualText.Text="Fictional manual draft";ManualText.Focus();
  var escape=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(this),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};
  ManualPanel.RaiseEvent(escape);UpdateLayout();
  Check(!manualOpen&&ManualText.Text=="Fictional manual draft"&&!ManualPanel.IsVisible,"Esc collapses manual input without discarding its draft");ManualText.Clear();
  InterfaceLanguageBox.SelectedIndex=0;ThemeBox.SelectedIndex=1;Width=1320;Height=900;uiFixtureLive=true;displayedElapsed=TimeSpan.FromSeconds(768);RefreshCallPresentation();UpdateLayout();
  Check(ModeText.Text==T("Идёт звонок")&&ElapsedText.Text=="12:48"&&StopButton.IsVisible&&!LiveButton.IsVisible,"live pill and action swap");
  Check(capture is null&&worker is null,"synthetic live fixture never starts capture or recognition");
  UpdateOutputActivity(.12f);Check(SpeakingVisible&&TranscriptPanel.Children.Contains(speakingRow),"real-level presentation gate shows client activity");
  lastOutputActivity=uiClock.Elapsed.TotalSeconds-1.6;OutputMeter.Value=0;RefreshSpeakingVisibility();Check(!SpeakingVisible&&!TranscriptPanel.Children.Contains(speakingRow),"client activity disappears after 1.5 seconds of silence");
  foreach(var theme in new[]{"dark","light"})
  {
   Check(Contrast(UiTheme.ColorFor("Danger",theme,false),UiTheme.ColorFor("LivePill",theme,false))>=4.5,"live status contrast: "+theme);
   Check(UiTheme.ColorFor("LivePill",theme,true)==SystemColors.WindowColor&&UiTheme.ColorFor("LiveDot",theme,true)==SystemColors.HighlightColor,"live status uses high-contrast system colors: "+theme);
  }
  var settingsPath=Path.Combine(Store.Root,"settings.json");var before=JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
  string unfinished=ClientText.Text;ClientText.Text="Unfinished private UI draft — do not save";
  Store.SaveMiniPosition(120,140);var after=JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
  after.Remove("miniLeft");after.Remove("miniTop");before.Remove("miniLeft");before.Remove("miniTop");
  Check(JsonNode.DeepEquals(before,after),"mini position saves only coordinates; briefing and unknown settings remain intact");ClientText.Text=unfinished;
  var oldPhrase=SayText.Text;OpenMini(this,new());await Task.Delay(200);
  Check(mini is not null&&mini.Topmost&&!mini.ShowInTaskbar&&WindowState==WindowState.Minimized,"mini opens above other windows and minimizes main");
  var miniView=mini!;SayText.Text="A new shared suggestion.";await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);
  Check(miniView.Phrase.Text==SayText.Text,"mini phrase follows the same live text binding");
  Check(FrameworkElementAutomationPeer.CreatePeerForElement(miniView.ExpandButton)?.GetName()==T("Развернуть окно"),"mini expand has a localized automation name");
  var peer=new ButtonAutomationPeer(miniView.ExpandButton);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();await Task.Delay(150);
  Check(mini is null&&WindowState!=WindowState.Minimized,"mini expand restores main without stopping the call");SayText.Text=oldPhrase;
  OpenMini(this,new());await Task.Delay(80);
  var miniEscape=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(mini!),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};mini!.RaiseEvent(miniEscape);await Task.Delay(80);
  Check(mini is null&&WindowState!=WindowState.Minimized,"Escape returns from mini to the same call");
  OpenMini(this,new());await Task.Delay(80);uiFixtureLive=false;scripted=false;RefreshCallPresentation();await Task.Delay(80);
  Check(mini is null&&WindowState!=WindowState.Minimized,"ending a call closes mini and restores its status in main");
  OpenOverflow(this,new());await Task.Delay(50);Check(CallMenu.IsOpen&&NewButton.Parent==CallMenu&&DemoButton.Parent==CallMenu,"overflow exposes existing call actions");CallMenu.IsOpen=false;
  PinBox.IsChecked=true;Check(Topmost,"overflow always-on-top toggles the existing pin behavior");PinBox.IsChecked=false;
  uiFixtureLive=false;scripted=false;RefreshCallPresentation();
  await ExportCall07(directory);
 }
 async Task ExportCall07(string directory)
 {
  UiMotion.SuppressForTest=true;Tabs.SelectedIndex=0;InterfaceLanguageBox.SelectedIndex=0;VoiceEnabledBox.IsChecked=true;
  FocusManager.SetFocusedElement(this,null);Keyboard.ClearFocus();
  async Task ClientSize(double width,double height){Width=width;Height=height;UpdateLayout();Width+=width-RootLayout.ActualWidth;Height+=height-RootLayout.ActualHeight;UpdateLayout();await Task.Delay(100);}
  scripted=false;ClearConversation();LoadBrief(new Brief{Title="Client portal discovery",Client="Fictional client. Synthetic interface fixture.",Goal="Understand the client portal scope."});
  CaptureStateText.Text=T("Микрофон выключен");StatusText.Text=T("Начни с подготовки клиента и выбора звука. Или посмотри пример без микрофона.");ThemeBox.SelectedIndex=1;
  await ClientSize(1320,860);RefreshCallPresentation();UpdateLayout();Capture(Path.Combine(directory,"v07-idle-day.png"));
  for(int i=0;i<4;i++)AddTurn(Demo.Turn(i,"en"));ShowAdvice(Demo.Advice(3,"en"));uiFixtureLive=true;displayedElapsed=TimeSpan.FromSeconds(768);
  var sample=VoiceTests.Sample();clientVoice.Reset();for(int i=0;i<4;i++)clientVoice.Add(sample,i*4,1);
  voiceReadings["client"]=clientVoice.Add(sample with{PitchHz=220},16,1) with{Pitch="higher",Level="usual",Pace="slower",Pauses="longer"};
  live=true;RenderVoice();StatusText.Text=T("Слушаем разговор · Jev анализирует текст");RefreshCallPresentation();StopButton.IsEnabled=true;UpdateLayout();
  Capture(Path.Combine(directory,"v07-live-day.png"));ThemeBox.SelectedIndex=0;UpdateLayout();await Task.Delay(100);Capture(Path.Combine(directory,"v07-live-night.png"));
  ThemeBox.SelectedIndex=1;await ClientSize(1000,720);RenderVoice();RefreshCallPresentation();UpdateLayout();Capture(Path.Combine(directory,"v07-compact.png"));
  OpenMini(this,new());await Task.Delay(200);mini!.UpdateLayout();
  var root=(FrameworkElement)mini.Content;var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"v07-mini.png")))encoder.Save(file);
  mini.Close();uiFixtureLive=false;live=false;scripted=false;ClearConversation();SetButtons();UiMotion.SuppressForTest=false;
 }
}

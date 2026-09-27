using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArtoSalesCopilot;

// Presentation only: observes the existing call state; never starts audio, providers or recordings.
public partial class MainWindow
{
 readonly DispatcherTimer presentationTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
 readonly Stopwatch uiClock=Stopwatch.StartNew();
 readonly List<(DependencyPropertyDescriptor Descriptor,object Target,EventHandler Handler)> uiObservers=[];
 List<Signal> displayedSignals=[];
 bool hasAdvice,manualOpen,presentationReady,pulsing,uiFixtureLive;
 double lastOutputActivity=double.NegativeInfinity;
 TimeSpan displayedElapsed;
 MiniWindow? mini;
 WindowState beforeMini;
 Border? speakingRow;
 readonly TextBlock[] speakingBars=[new(),new(),new()];
 bool IsCapturing=>live&&capture is not null||uiFixtureLive;
 bool IsReadiness=>!hasAdvice&&turns.Count==0&&!busy&&!uiFixtureLive;
 void InitializeCallPresentation()
 {
  presentationReady=true;
  SetUi(CaptureStateText,()=>T("Микрофон выключен"));
  SetUi(SessionMeta,()=>T("Запись начнётся после «Начать звонок»"));
  void Observe(DependencyObject target,DependencyProperty property,Action action)
  {
   var d=DependencyPropertyDescriptor.FromProperty(property,target.GetType());
   EventHandler h=(_,_)=>action();d.AddValueChanged(target,h);uiObservers.Add((d,target,h));
  }
  Observe(StatusText,TextBlock.TextProperty,RefreshCallPresentation);
  Observe(CaptureStateText,TextBlock.TextProperty,RefreshCallPresentation);
  foreach(var field in new[]{BriefTitle,ClientText,GoalText})field.TextChanged+=(_,_)=>RefreshReadiness();
  MicrophoneBox.SelectionChanged+=(_,_)=>RefreshReadiness();OutputBox.SelectionChanged+=(_,_)=>RefreshReadiness();
  presentationTimer.Tick+=(_,_)=>{if(busy)displayedElapsed=callClock.Elapsed;RefreshElapsed();RefreshSpeakingVisibility();UpdateLivePulse();};
  Loaded+=(_,_)=>presentationTimer.Start();
  Closed+=(_,_)=>{presentationTimer.Stop();foreach(var(d,t,h)in uiObservers)d.RemoveValueChanged(t,h);uiObservers.Clear();if(mini is not null){mini.RestoreOnClose=false;mini.Close();}};
  RefreshCallPresentation();RenderTopics();
 }
 static void Show(UIElement element,bool show)=>element.Visibility=show?Visibility.Visible:Visibility.Collapsed;
 void RefreshCallPresentation()
 {
  if(!ready||ModeText is null)return;
  var capturing=IsCapturing;var demo=scripted;
  ModeText.Text=capturing?T("Идёт звонок"):demo?T("Текстовый пример"):IsReadiness&&!busy?T("Микрофон выключен"):CaptureStateText.Text;
  ModeText.SetResourceReference(TextBlock.ForegroundProperty,capturing?"Danger":demo?"Accent":"Muted");
  StatusPill.SetResourceReference(Border.BackgroundProperty,capturing?"LivePill":"Chip");
  StatusDot.SetResourceReference(Shape.StrokeProperty,capturing?"LiveDot":demo?"Accent":"Muted");
  StatusDot.Fill=capturing?(Brush)FindResource("LiveDot"):Brushes.Transparent;
  Show(ModeText,!(capturing&&compactCall));Show(ElapsedText,capturing);
  Show(HeaderMeters,live||demo||uiFixtureLive);
  Show(LiveButton,!busy&&!uiFixtureLive);Show(StopButton,busy||uiFixtureLive);
  MiniButton.IsEnabled=capturing||demo;IdleDemoButton.IsEnabled=DemoButton.IsEnabled;
  LanguageBox.Width=live||demo||uiFixtureLive?64:138;
  var full=new[]{"Английский","Русский","Украинский"};int i=0;
  foreach(ComboBoxItem item in LanguageBox.Items)item.Content=live||demo||uiFixtureLive?item.Tag.ToString()!.ToUpperInvariant():T(full[i++]);
  var quiet=new[]{"Начни с подготовки клиента и выбора звука. Или посмотри пример без микрофона.","Новый разговор. Проверь подготовку клиента и устройства.","Слушаем разговор. Звук других приложений на выбранном устройстве тоже попадает в запись.","Слушаем разговор · Jev анализирует текст","Идёт текстовый пример. Реплики и подсказки подготовлены заранее. Микрофон выключен."};
  Show(StatusBanner,!string.IsNullOrWhiteSpace(StatusText.Text)&&!quiet.Any(s=>T(s)==StatusText.Text));
  bool problem=StatusText.Tag is string diagnostic&&StatusText.Text.StartsWith(diagnostic,StringComparison.Ordinal);
  StatusIcon.Text=problem?"\uE7BA":"\uE946";
  StatusBanner.ToolTip=problem?StatusText.ToolTip??StatusText.Text:StatusText.Text;
  Show(ReadinessPanel,IsReadiness);Show(AdviceContent,!IsReadiness);Show(StagePill,!IsReadiness);Show(CopyButton,!IsReadiness);Show(IdleDemoButton,IsReadiness);
  AdviceLabel.Text=T(IsReadiness?"ПЕРЕД ЗВОНКОМ":"ЧТО СКАЗАТЬ");
  Show(EmptyTranscriptCard,turns.Count==0);
  StagePill.Background=settings.Theme=="light"&&!SystemParameters.HighContrast?new SolidColorBrush(Color.FromRgb(246,248,242)):(Brush)FindResource("Selected");
  AdviceCard.Effect=settings.Theme=="dark"&&!SystemParameters.HighContrast?new System.Windows.Media.Effects.DropShadowEffect{Color=((SolidColorBrush)FindResource("Accent")).Color,Opacity=.25,BlurRadius=40,ShadowDepth=0}:null;
  RefreshReadiness();RefreshTopicHeading();RefreshElapsed();RefreshSpeakingVisibility();UpdateContentInset();UpdateLivePulse();
  if(mini is not null&&!capturing&&!demo&&!live){var view=mini;mini=null;view.Close();}
  mini?.Refresh(IsCapturing,scripted,OutputMeter.Value,SpeakingVisible,VoicePresentationItems());
 }
 void RefreshElapsed()
 {
  ElapsedText.Text=$"{(int)displayedElapsed.TotalMinutes:00}:{displayedElapsed.Seconds:00}";
 }
 void RefreshReadiness()
 {
  if(!ready||ReadyHeading is null)return;
  bool brief=new[]{BriefTitle.Text,ClientText.Text,GoalText.Text}.All(s=>!string.IsNullOrWhiteSpace(s));
  bool audio=MicrophoneBox.SelectedItem is DeviceOption&&OutputBox.SelectedItem is DeviceOption;
  ReadyHeading.Text=T(brief&&audio?"Два шага готовы. Нажми «Начать звонок» — подсказка появится после первых слов клиента.":brief||audio?"Остался один шаг. Проверь подготовку и звук перед звонком.":"Подготовь бриф и звук. Затем нажми «Начать звонок».");
  ReadyBriefDetail.Text=BriefTitle.Text;ReadyBriefDetail.ToolTip=BriefTitle.Text;
  ReadyAudioDetail.Text=T("Ты: {0} · Клиент: {1}",MicrophoneBox.SelectedItem?.ToString()??"—",OutputBox.SelectedItem?.ToString()??"—");ReadyAudioDetail.ToolTip=ReadyAudioDetail.Text;
  void Badge(Border badge,TextBlock text,bool done,string number){badge.Background=done?(Brush)FindResource("PrimaryFill"):Brushes.Transparent;text.Text=done?"✓":number;text.SetResourceReference(TextBlock.ForegroundProperty,done?"PrimaryInk":"Accent");}
  Badge(BriefBadge,BriefBadgeText,brief,"1");Badge(AudioBadge,AudioBadgeText,audio,"2");
 }
 void OpenOverflow(object s,RoutedEventArgs e){CallMenu.PlacementTarget=OverflowButton;CallMenu.Placement=PlacementMode.Bottom;CallMenu.IsOpen=true;}
 void OpenTopics(object s,RoutedEventArgs e)=>TopicsPopup.IsOpen=!TopicsPopup.IsOpen;
 void OpenManual(object s,RoutedEventArgs e){manualOpen=true;RefreshManualVisibility();ManualText.Focus();}
 void ManualPanelKeyDown(object s,KeyEventArgs e){if(e.Key!=Key.Escape)return;manualOpen=false;RefreshManualVisibility();(compactCall?CompactManualButton:ManualButton).Focus();e.Handled=true;}
 void RefreshManualVisibility(){Show(ManualPanel,manualOpen);Show(ManualButton,!compactCall&&!manualOpen);Show(CompactManualButton,compactCall&&!manualOpen);}
 void RefreshTopicHeading()
 {
  int count=displayedSignals.Count(s=>s.Value>=.8),total=displayedSignals.Count;
  TopicsHeading.Text=T(compactCall?"Обсудили {0} из {1}":IsReadiness?"Что обсудить":"Что уже обсудили",count,total);
  TopicsCount.Text=T("{0} из {1}",count,total);Show(TopicsCount,!compactCall);
 }
 void RenderTopics()
 {
  if(SignalsPanel is null)return;
  var signals=displayedSignals.Count==0?Coach.Topics.Select(t=>new Signal(t.Id,t.Ru,0)).ToList():displayedSignals;
  SignalsPanel.Children.Clear();
  foreach(var signal in signals)
  {
   bool covered=signal.Value>=.8;
   var text=new TextBlock{Text=(covered?"✓ ":"")+TopicName(signal),FontSize=12,TextWrapping=TextWrapping.NoWrap};
   text.SetResourceReference(TextBlock.ForegroundProperty,covered?"Accent":"Muted");
   var grid=new Grid{Margin=new(0,0,6,6)};
   var outline=new Rectangle{RadiusX=16,RadiusY=16,StrokeThickness=covered?0:1};
   outline.SetResourceReference(Shape.FillProperty,covered?"Selected":"Bg");
   if(!covered){outline.SetResourceReference(Shape.StrokeProperty,"ControlLine");outline.StrokeDashArray=new DoubleCollection{3,3};}
   grid.Children.Add(outline);var pad=new Border{Padding=new(10,5,10,5),Child=text};grid.Children.Add(pad);
   grid.ToolTip=T("Оценка наличия сведений: {0:P0}. Это не вероятность продажи. «Не подтверждено» также может означать «не применимо».",signal.Value);
   SignalsPanel.Children.Add(grid);
  }
  if(displayedSignals.Count==0)displayedSignals=signals;
  RefreshTopicHeading();
 }
 void UpdateOutputActivity(float value)
 {
  // Same amplitude gate as the existing CaptureChannel (.009); not emotion or speaker recognition.
  if(value>.009f)lastOutputActivity=uiClock.Elapsed.TotalSeconds;
  for(int i=0;i<speakingBars.Length;i++)speakingBars[i].Height=3+Math.Clamp(value*18,0,1)*(i==1?11:i==0?7:5);
  RefreshSpeakingVisibility();
 }
 bool SpeakingVisible=>IsCapturing&&uiClock.Elapsed.TotalSeconds-lastOutputActivity<=1.5;
 void RefreshSpeakingVisibility()
 {
  if(!presentationReady)return;
  if(speakingRow is null)
  {
   var stack=new StackPanel{Orientation=Orientation.Horizontal};
   for(int i=0;i<speakingBars.Length;i++){var bar=speakingBars[i];bar.Width=3;bar.Height=3;bar.Margin=new(0,0,2,0);bar.VerticalAlignment=VerticalAlignment.Center;bar.SetResourceReference(TextBlock.BackgroundProperty,"AudioAccent");stack.Children.Add(bar);}
   var label=new TextBlock{FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new(6,0,0,0)};label.SetResourceReference(TextBlock.TextProperty,"V723");label.SetResourceReference(TextBlock.ForegroundProperty,"AudioAccent");stack.Children.Add(label);
   var box=new Grid();var stroke=new Rectangle{RadiusX=12,RadiusY=12,StrokeThickness=1,StrokeDashArray=new DoubleCollection{3,3},Opacity=.5};stroke.SetResourceReference(Shape.StrokeProperty,"AudioAccent");box.Children.Add(stroke);box.Children.Add(new Border{Padding=new(14,10,14,10),Child=stack});
   speakingRow=new Border{Child=box,Margin=new(0,0,0,12)};
  }
  bool had=TranscriptPanel.Children.Contains(speakingRow);
  if(SpeakingVisible&&!had){TranscriptPanel.Children.Add(speakingRow);TranscriptScroll.ScrollToEnd();}
  if(!SpeakingVisible&&had)TranscriptPanel.Children.Remove(speakingRow);
  mini?.Refresh(IsCapturing,scripted,OutputMeter.Value,SpeakingVisible,VoicePresentationItems());
 }
 void UpdateLivePulse()
 {
  bool enabled=IsCapturing&&UiMotion.Enabled;
  if(enabled==pulsing)return;pulsing=enabled;
  var scale=(ScaleTransform)LivePulse.RenderTransform;
  LivePulse.BeginAnimation(OpacityProperty,null);scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);LivePulse.Opacity=0;
  if(!enabled)return;
  var duration=TimeSpan.FromSeconds(1.8);
  LivePulse.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,0,duration){RepeatBehavior=RepeatBehavior.Forever});
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,2.4,duration){RepeatBehavior=RepeatBehavior.Forever});
  scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,2.4,duration){RepeatBehavior=RepeatBehavior.Forever});
 }
 void RenderVoicePresentation()
 {
  if(!ready)return;
  voiceReadings.TryGetValue(VoiceSpeaker,out var reading);
  var codes=new[]{reading?.Pitch,reading?.Level,reading?.Pace,reading?.Pauses};
  var values=new[]{VoicePitchText,VoiceLevelText,VoicePaceText,VoicePauseText};var arrows=new[]{VoicePitchArrow,VoiceLevelArrow,VoicePaceArrow,VoicePauseArrow};
  for(int i=0;i<4;i++)
  {
   var value=values[i];var code=codes[i];bool absent=value.Text=="—";
   bool up=code is "higher" or "louder" or "faster" or "longer",down=code is "lower" or "quieter" or "slower" or "shorter";
   bool changed=!absent&&(up||down);arrows[i].Text=up?"↑":"↓";Show(arrows[i],changed);
   value.FontWeight=absent||changed?FontWeights.SemiBold:FontWeights.Normal;value.SetResourceReference(TextBlock.ForegroundProperty,absent||changed?"Ink":"Muted");
   if(!absent&&code is not null)
   {
    var full=VoiceValue(code);value.ToolTip=full;
    value.Text=compactCall&&changed?T(code switch{"higher"=>"Выше","lower"=>"Ниже","louder"=>"Громче","quieter"=>"Тише","faster"=>"Быстрее","slower"=>"Медленнее","longer"=>"Длиннее",_=>"Короче"}):full;
   }
  }
  if(!VoicePersonText.Text.StartsWith("· "))VoicePersonText.Text="· "+VoicePersonText.Text;
  VoiceSummary.Text=compactCall?VoiceNote.Text:VoiceStatus.Text+" "+VoiceNote.Text;
  VoiceSummary.ToolTip=VoiceStatus.Text+"\n"+VoiceNote.Text;VoiceHeading.ToolTip=VoiceSummary.ToolTip;
  mini?.Refresh(IsCapturing,scripted,OutputMeter.Value,SpeakingVisible,VoicePresentationItems());
 }
 internal (string Label,string Value,bool Changed)[] VoicePresentationItems()
 {
  voiceReadings.TryGetValue(VoiceSpeaker,out var reading);
  var codes=new[]{reading?.Pitch,reading?.Level,reading?.Pace,reading?.Pauses};
  return new[]{(T("Тон"),VoicePitchText,VoicePitchArrow),(T("Громкость"),VoiceLevelText,VoiceLevelArrow),(T("Темп"),VoicePaceText,VoicePaceArrow),(T("Паузы"),VoicePauseText,VoicePauseArrow)}
   .Select((v,i)=>{
    bool changed=v.Item3.Visibility==Visibility.Visible;
    string value=changed?T(codes[i] switch{"higher"=>"Выше","lower"=>"Ниже","louder"=>"Громче","quieter"=>"Тише","faster"=>"Быстрее","slower"=>"Медленнее","longer"=>"Длиннее",_=>"Короче"}):v.Item2.Text;
    return(v.Item1,(changed?v.Item3.Text+" ":"")+value.ToLower(System.Globalization.CultureInfo.GetCultureInfo(UiText.Language)),changed);
   }).ToArray();
 }
 void OpenMini(object s,RoutedEventArgs e)
 {
  if(!MiniButton.IsEnabled)return;
  if(mini is not null){mini.Activate();return;}
  beforeMini=WindowState;
  mini=new MiniWindow(SayText,AdviceTitle,ElapsedText,ModeText,LivePulse,()=>CopyLine(this,new()),RestoreFromMini,SaveMiniPosition);
  if(settings.MiniLeft is double left&&settings.MiniTop is double top)mini.SetSavedPosition(left,top);
  mini.Show();mini.Refresh(IsCapturing,scripted,OutputMeter.Value,SpeakingVisible,VoicePresentationItems());
  WindowState=WindowState.Minimized;
 }
 void SaveMiniPosition(double left,double top)
 {
  try{Store.SaveMiniPosition(left,top);settings.MiniLeft=left;settings.MiniTop=top;}
  catch(Exception ex){StatusText.ToolTip=ex.Message;SetUi(StatusText,()=>T("Не удалось сохранить положение мини-окна."));}
 }
 void RestoreFromMini(){mini=null;WindowState=beforeMini==WindowState.Minimized?WindowState.Normal:beforeMini;Show();Activate();}
}

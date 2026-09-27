using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using Microsoft.Win32;

namespace ArtoSalesCopilot;

// A view over the owner's text/voice/meter state, not another call session.
internal sealed class MiniWindow:Window
{
 internal readonly Grid Surface=new();
 internal readonly TextBlock Phrase=new(),Heading=new(),Clock=new(),Mode=new();
 internal readonly Button CopyButton=new(),ExpandButton=new();
 readonly StackPanel speaking=new(){Orientation=Orientation.Horizontal};
 readonly Border dot=new(){Width=8,Height=8,CornerRadius=new(4),Margin=new(0,0,8,0)};
 readonly Border[] bars=[new(),new(),new()];
 readonly TextBlock voiceLine=new(){FontSize=11,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
 readonly Action restore;readonly Action<double,double> savePosition;
 internal bool RestoreOnClose=true;
 public MiniWindow(TextBlock phrase,TextBlock title,TextBlock elapsed,TextBlock mode,Ellipse sharedPulse,Action copy,Action restore,Action<double,double> savePosition)
 {
  this.restore=restore;this.savePosition=savePosition;
  Width=420;Height=280;MinWidth=360;MaxWidth=640;MinHeight=280;MaxHeight=280;
  WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;Topmost=true;ShowInTaskbar=false;Title="Arto";
  WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=(FontFamily)Application.Current.Resources["BodyFont"];
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new(6),CornerRadius=new(16),GlassFrameThickness=new(0),UseAeroCaptionButtons=false});
  ApplyPalette();
  var outer=new Border{CornerRadius=new(16),BorderThickness=new(1),Padding=new(18,14,16,14),Child=Surface};
  outer.SetResourceReference(Border.BackgroundProperty,"MiniSurface");outer.SetResourceReference(Border.BorderBrushProperty,"Line");
  Content=outer;
  Surface.RowDefinitions.Add(new(){Height=new(36)});Surface.RowDefinitions.Add(new(){Height=GridLength.Auto});Surface.RowDefinitions.Add(new());Surface.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var top=new DockPanel();Surface.Children.Add(top);
  var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Right);top.Children.Add(buttons);
  void IconButton(Button button,string glyph,string label,Action action)
  {
   button.Width=button.Height=36;button.MinHeight=36;button.Padding=new(0);button.Margin=new(6,0,0,0);button.Content=new TextBlock{Text=glyph,FontSize=15,FontFamily=new("Segoe Fluent Icons, Segoe MDL2 Assets")};
   button.SetResourceReference(BackgroundProperty,"Input");button.SetResourceReference(ForegroundProperty,"Ink");UiText.Bind(button,ToolTipProperty,()=>UiText.Get(label));UiText.Bind(button,AutomationProperties.NameProperty,()=>UiText.Get(label));button.Click+=(_,_)=>action();buttons.Children.Add(button);
  }
  IconButton(CopyButton,"\uE8C8","Копировать фразу",copy);IconButton(ExpandButton,"\uE740","Развернуть окно",Close);
  KeyboardNavigation.SetTabIndex(CopyButton,0);KeyboardNavigation.SetTabIndex(ExpandButton,1);
  var status=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};top.Children.Add(status);
  var dotHost=new Grid{Width=8,Height=8,Margin=new(0,0,8,0)};dot.Margin=new(0);dot.SetResourceReference(Border.BackgroundProperty,"LiveDot");dotHost.Children.Add(dot);
  var ring=new Ellipse{StrokeThickness=1,RenderTransformOrigin=new(.5,.5)};ring.SetResourceReference(Shape.StrokeProperty,"LiveDot");ring.SetBinding(OpacityProperty,new Binding("Opacity"){Source=sharedPulse});
  var scale=new ScaleTransform();BindingOperations.SetBinding(scale,ScaleTransform.ScaleXProperty,new Binding("RenderTransform.ScaleX"){Source=sharedPulse});BindingOperations.SetBinding(scale,ScaleTransform.ScaleYProperty,new Binding("RenderTransform.ScaleY"){Source=sharedPulse});ring.RenderTransform=scale;dotHost.Children.Add(ring);status.Children.Add(dotHost);
  void Bind(TextBlock target,TextBlock source)=>target.SetBinding(TextBlock.TextProperty,new Binding("Text"){Source=source});
  Bind(Clock,elapsed);Clock.FontSize=12;Clock.FontWeight=FontWeights.SemiBold;Clock.SetResourceReference(TextBlock.ForegroundProperty,"Danger");status.Children.Add(Clock);
  Bind(Mode,mode);Mode.FontSize=11;Mode.SetResourceReference(TextBlock.ForegroundProperty,"Accent");Mode.TextTrimming=TextTrimming.CharacterEllipsis;Mode.MaxWidth=132;Mode.TextWrapping=TextWrapping.NoWrap;status.Children.Add(Mode);
  speaking.Margin=new(10,0,0,0);speaking.VerticalAlignment=VerticalAlignment.Center;
  for(int i=0;i<3;i++){bars[i].Width=2;bars[i].Height=3;bars[i].CornerRadius=new(1);bars[i].Margin=new(0,0,2,0);bars[i].SetResourceReference(Border.BackgroundProperty,"AudioAccent");speaking.Children.Add(bars[i]);}
  var speakingText=new TextBlock{FontSize=11,Margin=new(5,0,0,0),TextWrapping=TextWrapping.NoWrap};speakingText.SetResourceReference(TextBlock.TextProperty,"V723");speakingText.SetResourceReference(TextBlock.ForegroundProperty,"AudioAccent");speaking.Children.Add(speakingText);status.Children.Add(speaking);
  var headingPanel=new DockPanel{Margin=new(0,10,0,10)};Grid.SetRow(headingPanel,1);Surface.Children.Add(headingPanel);
  var mark=new Border{Width=3,Height=12,Margin=new(0,0,8,0)};mark.SetResourceReference(Border.BackgroundProperty,"Metal");headingPanel.Children.Add(mark);
  Heading.SetBinding(TextBlock.TextProperty,new Binding("Text"){Source=title,Converter=new UppercaseConverter()});Heading.FontSize=11;Heading.FontWeight=FontWeights.Bold;Heading.TextWrapping=TextWrapping.NoWrap;Heading.TextTrimming=TextTrimming.CharacterEllipsis;Heading.SetResourceReference(TextBlock.ForegroundProperty,"Accent");headingPanel.Children.Add(Heading);
  Bind(Phrase,phrase);Phrase.FontFamily=(FontFamily)Application.Current.Resources["DisplayFont"];Phrase.FontSize=20;Phrase.LineHeight=27;Phrase.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;Phrase.FontWeight=FontWeights.Medium;Phrase.TextWrapping=TextWrapping.Wrap;Phrase.TextTrimming=TextTrimming.CharacterEllipsis;Phrase.MaxHeight=108;Phrase.VerticalAlignment=VerticalAlignment.Top;Phrase.SetResourceReference(TextBlock.ForegroundProperty,"Ink");Phrase.SetBinding(ToolTipProperty,new Binding("Text"){Source=phrase});Grid.SetRow(Phrase,2);Surface.Children.Add(Phrase);
  var voice=new Border{BorderThickness=new(0,1,0,0),Padding=new(0,9,0,0)};voice.SetResourceReference(Border.BorderBrushProperty,"Line");Grid.SetRow(voice,3);Surface.Children.Add(voice);
  voice.Child=voiceLine;
  PreviewKeyDown+=(_,e)=>{UiMotion.KeyboardInput=true;if(e.Key==Key.Escape){e.Handled=true;Close();}};
  PreviewMouseDown+=(_,_)=>UiMotion.KeyboardInput=false;
  MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is DependencyObject target&&AncestorButton(target) is null){try{DragMove();}catch(InvalidOperationException){}}};
  SourceInitialized+=(_,_)=>ApplyBackdrop();
  Loaded+=(_,_)=>UiMotion.Reveal(Surface,160);
  SystemEvents.UserPreferenceChanged+=PreferenceChanged;
  Closed+=(_,_)=>{SystemEvents.UserPreferenceChanged-=PreferenceChanged;savePosition(Left,Top);if(RestoreOnClose)restore();};
 }
 internal void ApplyPalette()
 {
  foreach(var (key,pair) in UiTheme.Palette)Resources[key]=new SolidColorBrush(UiTheme.ColorFor(key,"dark",SystemParameters.HighContrast));
  var bg=((SolidColorBrush)Resources["Bg"]).Color;
  Resources["MiniSurface"]=new SolidColorBrush(Color.FromArgb(SystemParameters.HighContrast?(byte)255:(byte)240,bg.R,bg.G,bg.B));
  Background=(Brush)Resources["Bg"];
 }
 void PreferenceChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{ApplyPalette();ApplyBackdrop();});
 void ApplyBackdrop()
 {
  var handle=new WindowInteropHelper(this).Handle;int dark=1;DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
  bool transparency=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",0) is int n&&n!=0;
  int backdrop=Environment.OSVersion.Version.Build>=22621&&transparency&&!SystemParameters.HighContrast?2:1;
  DwmSetWindowAttribute(handle,38,ref backdrop,sizeof(int));int corners=2;DwmSetWindowAttribute(handle,33,ref corners,sizeof(int));
 }
 internal void SetSavedPosition(double left,double top)
 {
  if(!double.IsFinite(left)||!double.IsFinite(top))return;
  WindowStartupLocation=WindowStartupLocation.Manual;
  Left=Math.Clamp(left,SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenLeft+Math.Max(0,SystemParameters.VirtualScreenWidth-Width));
  Top=Math.Clamp(top,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenTop+Math.Max(0,SystemParameters.VirtualScreenHeight-Height));
 }
 internal void Refresh(bool live,bool demo,double level,bool isSpeaking,(string Label,string Value,bool Changed)[] voice)
 {
  Clock.Visibility=live?Visibility.Visible:Visibility.Collapsed;Mode.Visibility=live?Visibility.Collapsed:Visibility.Visible;
  dot.Visibility=live?Visibility.Visible:Visibility.Collapsed;speaking.Visibility=live&&isSpeaking?Visibility.Visible:Visibility.Collapsed;
  for(int i=0;i<3;i++)bars[i].Height=3+Math.Clamp(level*18,0,1)*(i==1?8:i==0?5:3);
  voiceLine.Inlines.Clear();
  for(int i=0;i<4;i++){if(i>0)voiceLine.Inlines.Add(new Run(" · "){Foreground=(Brush)Resources["Muted"]});voiceLine.Inlines.Add(new Run(voice[i].Label+" "){Foreground=(Brush)Resources["Muted"]});voiceLine.Inlines.Add(new Run(voice[i].Value){Foreground=(Brush)Resources[voice[i].Changed?"Ink":"Muted"],FontWeight=voice[i].Changed?FontWeights.SemiBold:FontWeights.Normal});}
  voiceLine.ToolTip=string.Join(" · ",voice.Select(v=>v.Label+" "+v.Value));
 }
 static Button? AncestorButton(DependencyObject target){while(target is not null){if(target is Button b)return b;target=target is Visual?VisualTreeHelper.GetParent(target):LogicalTreeHelper.GetParent(target);}return null;}
 sealed class UppercaseConverter:IValueConverter
 {
  public object Convert(object value,Type t,object p,System.Globalization.CultureInfo c)=>value?.ToString()?.ToUpper(c)??"";
  public object ConvertBack(object value,Type t,object p,System.Globalization.CultureInfo c)=>Binding.DoNothing;
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}

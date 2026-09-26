using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    async Task CheckSignature(string directory,List<string> checks)
    {
        void Check(bool pass,string label){if(!pass)throw new InvalidOperationException("Signature check failed: "+label);checks.Add("PASS "+label);}
        Width=1320;Height=900;Tabs.SelectedIndex=0;scripted=true;
        BriefTitle.Text="Client portal discovery · fictional demonstration";
        for(int i=0;i<4;i++)AddTurn(Demo.Turn(i,"en"));ShowAdvice(Demo.Advice(3,"en"));
        SetUi(StatusText,()=>T("Идёт текстовый пример. Реплики и подсказки подготовлены заранее. Микрофон выключен."));
        SetUi(ModeText,()=>T("Текстовый пример · без микрофона"));
        CopyButton.Focus();UiMotion.KeyboardInput=true;
        var focused=Keyboard.FocusedElement;
        ShowAdvice(Demo.Advice(4,"en"));
        Check(ReferenceEquals(focused,Keyboard.FocusedElement)&&!AdviceContent.HasAnimatedProperties,"new suggestion preserves keyboard focus and keyboard actions remain instant");
        UiMotion.KeyboardInput=false;UiMotion.SuppressForTest=true;UiMotion.Reveal(AdviceContent);
        Check(!AdviceContent.HasAnimatedProperties&&AdviceContent.Opacity==1,"reduced-motion path restores final content immediately");
        UiMotion.SuppressForTest=false;
        for(int i=0;i<10;i++)UiMotion.Reveal(AdviceContent);
        // WPF starts its animation clock on a render tick, which can be delayed in a hidden test window.
        bool Settled()=>Math.Abs(AdviceContent.Opacity-1)<.001&&AdviceContent.RenderTransform is TranslateTransform t&&Math.Abs(t.Y)<.001;
        var motionWait=System.Diagnostics.Stopwatch.StartNew();
        while(!Settled()&&motionWait.ElapsedMilliseconds<1000)await Task.Delay(40);
        Check(Settled(),"repeated animation replaces previous motion and settles without a queue");
        Check(FrameworkElementAutomationPeer.CreatePeerForElement(CopyButton)?.GetName()==T("Копировать фразу"),"copy action has a native automation name");
        Tabs.SelectedIndex=2;UpdateLayout();CableExpander.IsExpanded=true;await Task.Delay(200);
        Check(CableExpander.IsExpanded&&((FrameworkElement)CableExpander.Content).IsVisible,"audio routing instructions expand without a modal");
        CableExpander.IsExpanded=false;
        foreach(var (theme,index) in new[]{("dark",0),("light",1)})
        {
            ThemeBox.SelectedIndex=index;
            foreach(var pair in new[]{("Ink","Field"),("Muted","Bg"),("Ink","AdvicePanel"),("Accent","Selected"),("ControlLine","Input"),("FocusRing","Bg")})
            {
                var ratio=Contrast(((SolidColorBrush)FindResource(pair.Item1)).Color,((SolidColorBrush)FindResource(pair.Item2)).Color);
                Check(ratio>=(pair.Item1 is "ControlLine" or "FocusRing"?3:4.5),$"Signature contrast {theme} {pair.Item1}/{pair.Item2}: {ratio:F2}:1");
            }
            foreach(var (language,indexLanguage) in new[]{("ru",0),("uk",1),("en",2)})
            {
                InterfaceLanguageBox.SelectedIndex=indexLanguage;Width=1000;Height=720;
                for(int page=0;page<4;page++)
                {
                    Tabs.SelectedIndex=page;UpdateLayout();await Task.Delay(200);
                    Check(RootLayout.ActualWidth<=ActualWidth&&RootLayout.ActualHeight<=ActualHeight,$"native page viewport {language}/{theme}/{page} at 1000x720");
                    Capture(Path.Combine(directory,$"compact-{language}-{theme}-{page}.png"));
                }
            }
        }
        InterfaceLanguageBox.SelectedIndex=2;ThemeBox.SelectedIndex=0;Tabs.SelectedIndex=0;Width=1320;Height=900;UpdateLayout();await Task.Delay(220);
        var dpi=VisualTreeHelper.GetDpi(this);checks.Add($"INFO actual window DPI {dpi.PixelsPerInchX:F0}; 100/125/150% images below are WPF raster checks, not a change to Windows display settings");
        foreach(var scale in new[]{1d,1.25,1.5})
        {
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(RootLayout.ActualWidth*scale),(int)Math.Ceiling(RootLayout.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
            bitmap.Render(RootLayout);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(directory,$"dpi-raster-{scale*100:F0}.png")))png.Save(file);
            Check(bitmap.PixelWidth==(int)Math.Ceiling(RootLayout.ActualWidth*scale),$"WPF vector raster at {scale*100:F0}%");
        }
        ShowFeedback(T("Сохранено на компьютере"));Check(FeedbackBox.IsVisible&&FeedbackText.Text==T("Сохранено на компьютере"),"save feedback is visible without stealing focus");
        await Task.Delay(2500);Check(!FeedbackBox.IsVisible,"feedback dismisses automatically");
        // Short capture of this application's own rendered UI. No desktop, microphone or provider capture.
        if(Environment.GetEnvironmentVariable("ARTO_UI_VIDEO")=="1")
        {
            var frames=Path.Combine(directory,"interaction-frames");Directory.CreateDirectory(frames);
            int frame=0;
            async Task Hold(int count){for(int i=0;i<count;i++){UpdateLayout();Capture(Path.Combine(frames,$"frame-{frame++:D4}.png"));await Task.Delay(70);}}
            await Hold(16);ShowAdvice(Demo.Advice(5,"en"));await Hold(20);ShowFeedback(T("Скопировано"));await Hold(16);
            Tabs.SelectedIndex=2;await Hold(12);CableExpander.IsExpanded=true;await Hold(18);CableExpander.IsExpanded=false;await Hold(8);
            Tabs.SelectedIndex=3;await Hold(12);ThemeBox.SelectedIndex=1;await Hold(16);InterfaceLanguageBox.SelectedIndex=1;await Hold(16);
            Tabs.SelectedIndex=0;await Hold(20);
            File.WriteAllText(Path.Combine(directory,"interaction-capture.txt"),$"{frame} frames, synthetic WPF UI only. No audio. Rendered frames, not a full-desktop screen recording.");
        }
        UiMotion.KeyboardInput=false;InterfaceLanguageBox.SelectedIndex=0;ThemeBox.SelectedIndex=0;Tabs.SelectedIndex=0;
        scripted=false;ClearConversation();
    }
}

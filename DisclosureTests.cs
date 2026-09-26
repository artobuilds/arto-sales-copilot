using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    async Task CheckDisclosureLayout(string directory,List<string> checks)
    {
        void Check(bool pass,string label){if(!pass)throw new InvalidOperationException("Disclosure check failed: "+label);checks.Add("PASS "+label);}
        Rect Bounds(FrameworkElement element)=>element.TransformToAncestor(RootLayout).TransformBounds(new Rect(element.RenderSize));
        Tabs.SelectedIndex=0;ClearConversation();
        Check(TopicsEmptyText.Visibility==Visibility.Visible,"empty topic disclosure explains when topics appear");
        ShowAdvice(Demo.Advice(3,"en"));
        Check(TopicsEmptyText.Visibility==Visibility.Collapsed,"topic explanation clears when analysis is present");
        foreach(var (language,index) in new[]{("ru",0),("uk",1),("en",2)})
        foreach(var (theme,themeIndex) in new[]{("dark",0),("light",1)})
        foreach(var size in new[]{(1000d,720d),(1320d,900d)})
        {
            InterfaceLanguageBox.SelectedIndex=index;ThemeBox.SelectedIndex=themeIndex;Width=size.Item1;Height=size.Item2;
            foreach(var expanded in new[]{false,true})
            {
                CoverageExpander.IsExpanded=ManualExpander.IsExpanded=expanded;UpdateLayout();await Task.Delay(200);
                if(expanded)CallViewport.ScrollToEnd();else CallViewport.ScrollToTop();UpdateLayout();
                var suffix=$"{language}/{theme}/{Width:F0}/"+(expanded?"open":"closed");
                Check(AdviceScroll.ActualHeight>=80,"suggested reply retains a readable scroll area: "+suffix);
                Check(Bounds(LiveButton).Top>=Bounds(HeaderPanel).Bottom&&Bounds(LiveButton).Bottom<=Bounds(CallViewport).Top,"start and stop stay visible above the scrolling conversation: "+suffix);
                Check(Bounds(ManualExpander).Bottom<=Bounds(TopStatusBox).Top-8&&Bounds(CoverageExpander).Bottom<=Bounds(TopStatusBox).Top-8&&Bounds(VoiceCard).Bottom<=Bounds(CoverageExpander).Top,"voice and disclosures do not overlap footer: "+suffix);
                Check(Bounds(ModeText).Left>=Bounds(TopStatusBox).Left+16&&Bounds(SessionMeta).Bottom<=Bounds(TopStatusBox).Bottom-12&&Bounds(TopStatusBox).Bottom<=RootLayout.ActualHeight-16,"status text and audio have complete inner and outer spacing: "+suffix);
                foreach(var expander in new[]{CoverageExpander,ManualExpander})
                {
                    expander.ApplyTemplate();var header=(ToggleButton)expander.Template.FindName("HeaderSite",expander);header.ApplyTemplate();
                    var action=(TextBlock)header.Template.FindName("ActionText",header);
                    Check(header.ActualHeight>=44&&action.Text==T(expanded?"Скрыть":"Открыть"),"disclosure action is labeled and has a full click target: "+expander.Name+"/"+suffix);
                }
                if(language=="ru")Capture(Path.Combine(directory,$"refined-{theme}-{Width:F0}-{(expanded?"open":"closed")}.png"));
            }
        }
        var toggle=(ToggleButton)ManualExpander.Template.FindName("HeaderSite",ManualExpander);
        toggle.Focus();var peer=new ToggleButtonAutomationPeer(toggle);
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();UpdateLayout();
        Check(!ManualExpander.IsExpanded&&ReferenceEquals(System.Windows.Input.Keyboard.FocusedElement,toggle),"native disclosure toggle collapses and retains keyboard focus");
        CoverageExpander.IsExpanded=ManualExpander.IsExpanded=false;
    }
}

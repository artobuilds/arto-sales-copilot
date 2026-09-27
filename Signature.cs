using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    readonly DispatcherTimer feedbackTimer = new() { Interval = TimeSpan.FromSeconds(2.4) };
    bool signatureReady;
    internal bool BackdropRequested { get; private set; }
    void SignatureLoaded(object sender, RoutedEventArgs e)
    {
        if (signatureReady) return;
        signatureReady = true;
        PreviewKeyDown += (_, _) => UiMotion.KeyboardInput = true;
        PreviewMouseDown += (_, _) => UiMotion.KeyboardInput = false;
        feedbackTimer.Tick += (_, _) => { feedbackTimer.Stop(); FeedbackBox.Visibility = Visibility.Collapsed; };
        SystemEvents.UserPreferenceChanged += SystemAppearanceChanged;
        Closed += (_, _) => { feedbackTimer.Stop(); SystemEvents.UserPreferenceChanged -= SystemAppearanceChanged; };
        UpdateSignature(); WindowSizeChanged(this, null!);
    }
    void SystemAppearanceChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { UiTheme.Apply(settings.Theme); UpdateSignature(); });
    void NavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || e.Source != Tabs) return;
        UpdateSignature();RefreshCallPresentation();
        // Page changes preserve geometry; motion is limited to advice, pulse and mini.
    }
    void UpdateSignature()
    {
        if (PageTitle is null || Tabs.SelectedItem is not TabItem tab) return;
        BrandName.Text=System.Windows.Automation.AutomationProperties.GetName(tab);
        if (Tabs.SelectedIndex == 0)
            PageTitle.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = BriefTitle, Converter = new ClientTitleConverter() });
        else PageTitle.Text=System.Windows.Automation.AutomationProperties.GetName(tab);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(UiText.Language);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = settings.Theme == "dark" ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        // Windows 11's native caption can use Mica; reading surfaces remain opaque.
        bool transparency = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0) is int enabled && enabled != 0;
        int backdrop = Environment.OSVersion.Version.Build >= 22621 && transparency && !SystemParameters.HighContrast ? 2 : 1;
        BackdropRequested = DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int)) == 0 && backdrop == 2;
    }
    void ShowFeedback(string text)
    {
        FeedbackText.Text = text; FeedbackBox.Visibility = Visibility.Visible;
        feedbackTimer.Stop(); feedbackTimer.Start();
    }
    public void PrepareUiReview()
    {
        Title="Arto Sales Copilot · UI REVIEW · NO AUDIO";
        LoadBrief(new Brief { Title="Client portal discovery", Client="Fictional client for interface review only." });
        MicrophoneBox.ItemsSource=new[]{new DeviceOption("fixture-mic","Demo microphone (not connected)")};MicrophoneBox.SelectedIndex=0;
        OutputBox.ItemsSource=new[]{new DeviceOption("fixture-output","Demo headphones (not connected)"),new DeviceOption("fixture-cable","Demo CABLE-A (not connected)")};OutputBox.SelectedIndex=0;
        // Review runs have an isolated empty store: no real keys, endpoint or audio worker.
        scripted=true;for(int i=0;i<4;i++)AddTurn(Demo.Turn(i,"en"));ShowAdvice(Demo.Advice(3,"en"));RenderVoice();
        SetUi(CaptureStateText,()=>T("Текстовый пример · без микрофона"));
        SetUi(StatusText,()=>T("Идёт текстовый пример. Реплики и подсказки подготовлены заранее. Микрофон выключен."));
        KeyStatus.Text="Jev: —\nOpenAI: —";
    }
    sealed class ClientTitleConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) => string.IsNullOrWhiteSpace(value as string) ? T("Новый разговор") : value;
        public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

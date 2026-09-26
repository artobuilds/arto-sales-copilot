using System.Windows;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    void WindowSizeChanged(object s, SizeChangedEventArgs e)
    {
        if(!ready)return;
        bool compact=ActualHeight<820 || ActualWidth<1150;
        RootLayout.Margin=new(0);
        PageTitle.FontSize=compact?22:27;
        HeaderPanel.Margin=new(212,compact?12:24,28,0);
        TopStatusBox.Padding=new(0,10,0,0);
        TopStatusBox.Margin=new(212,0,28,12);
        VoiceCard.Padding=new(0,compact?10:14,0,0);
        VoiceCard.Margin=new(0,compact?12:18,0,0);
        VoiceStatus.Margin=compact?new(0,4,0,6):new(0,7,0,10);
        VoiceStatus.FontSize=12;
        AdviceCard.Padding=compact?new(20,16,20,16):new(28);
        SayText.FontSize=compact?20:27;SayText.LineHeight=compact?27:37;
        VoiceNote.Visibility=VoicePersonText.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        VoiceHeading.ToolTip=VoiceNote.Text+"\n"+VoicePersonText.Text;
        CoverageExpander.Padding=ManualExpander.Padding=new(0);
        Tabs.ApplyTemplate();
        if(Tabs.Template.FindName("PART_SelectedContentHost",Tabs) is System.Windows.Controls.ContentPresenter host)host.Margin=new(28,compact?90:126,28,compact?76:84);
    }
    void GoAudio(object s, RoutedEventArgs e) => Tabs.SelectedIndex = 2;
    void GoCall(object s, RoutedEventArgs e) {Tabs.SelectedIndex = 0;RenderVoice();}
    static string StageName(string stage) => stage switch
    {
        "opening" => T("Знакомство"), "discovery" => T("Задача клиента"),
        "qualification" => T("Условия проекта"), "value" => T("Ценность решения"),
        "objection" => T("Сомнения клиента"), "scoping" => T("Объём работ"),
        "closing" => T("Следующий шаг"), _ => T("Слушаем клиента")
    };
    static string TopicName(Signal signal) {var topic=Coach.Topics.FirstOrDefault(t=>t.Id==signal.Id);return UiText.Language switch{"en"=>topic.En??signal.Label,"uk"=>topic.Uk??signal.Label,_=>topic.Ru??signal.Label};}
    string FriendlyError(Exception error)
    {
        // Keep provider diagnostics inspectable without letting long technical text dominate the call screen.
        StatusText.ToolTip = error.Message;
        if (error.Message.Contains("402")) return T("Jev отклонил запрос из-за биллинга (402). Сохранённый ключ не подтверждает доступ к API.");
        if (error.Message.Contains("401")) return T("Сервис не принял API-ключ (401). Проверь его в «Настройки ИИ».");
        if (error.Message.Contains("429")) return T("ИИ временно ограничил запросы (429). Подожди и повтори попытку.");
        return UiText.Contains(error.Message)?T(error.Message):T("Не удалось выполнить действие. Подробности — при наведении на это сообщение.");
    }
}

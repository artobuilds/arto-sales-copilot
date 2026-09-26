using System.Windows;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    void WindowSizeChanged(object s, SizeChangedEventArgs e)
    {
        if(!ready)return;
        bool compact=ActualHeight<820;
        RootLayout.Margin=compact?new(18,12,18,12):new(24,18,24,18);
        BrandName.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        PageTitle.FontSize=compact?22:26;
        HeaderPanel.Margin=compact?new(0,0,0,10):new(0,0,0,18);
        TopStatusBox.Padding=compact?new(12,8,12,8):new(14,10,14,10);
        TopStatusBox.Margin=compact?new(0,0,0,8):new(0,0,0,12);
        VoiceCard.Padding=compact?new(16,10,16,10):new(18,13,18,13);
        VoiceStatus.Margin=compact?new(0,5,0,6):new(0,7,0,10);
        VoiceStatus.FontSize=compact?12:13;
        AdviceCard.Padding=compact?new(16,12,16,12):new(20);
        SayText.FontSize=compact?19:22;SayText.LineHeight=compact?26:31;
        CoverageExpander.Padding=ManualExpander.Padding=compact?new(0):new(0,12,0,0);
        foreach(System.Windows.Controls.TabItem tab in Tabs.Items)tab.Padding=compact?new(16,8,16,8):new(18,12,18,12);
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

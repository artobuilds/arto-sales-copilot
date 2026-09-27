using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArtoSalesCopilot;
public partial class MainWindow
{
 bool compactCall;
 void WindowSizeChanged(object s, SizeChangedEventArgs e)
 {
  if(!ready)return;
  compactCall=ActualHeight<820||ActualWidth<1150;
  var c=compactCall;RootLayout.Margin=new(0);
  Tabs.ApplyTemplate();
  if(Tabs.Template.FindName("RailColumn",Tabs) is ColumnDefinition rail)rail.Width=new(c?64:76);
  foreach(TabItem tab in Tabs.Items){tab.Width=c?52:62;tab.Height=c?52:58;tab.FontSize=c?10:11;tab.ApplyTemplate();if(tab.Template.FindName("RailIcon",tab) is TextBlock icon)icon.FontSize=c?18:20;}
  PageTitle.FontSize=c?18:22;BrandName.Visibility=c?Visibility.Collapsed:Visibility.Visible;
  HeaderArea.Margin=new(c?84:104,c?16:20,c?20:28,0);
  HeaderMeters.Width=c?90:120;
  foreach(var el in new FrameworkElement[]{StatusPill,HeaderMeters,LanguageBox,MiniButton,OverflowButton})el.Margin=new(0,0,c?10:14,0);
  AdviceColumn.Width=new(c?1.35:1.4,GridUnitType.Star);CallGap.Width=new(c?18:24);
  AdviceCard.Padding=c?new(24,20,24,16):new(32,26,32,22);AdviceCard.CornerRadius=new(c?14:16);
  AdviceScroll.Margin=new(0,c?14:22,0,0);AdviceTitle.FontSize=c?14:16;
  SayText.FontSize=c?24:32;SayText.LineHeight=c?32:42;PhraseRule.Padding=new(c?16:20,0,0,0);
  CopyButton.Height=c?40:44;CopyLabel.Text=T(c?"Копировать":"Копировать фразу");
  VoiceCard.Padding=new(0,c?10:14,0,0);VoiceHeading.FontSize=c?14:15;VoicePersonText.Visibility=c?Visibility.Collapsed:Visibility.Visible;
  VoiceSpeakerBox.Height=ResetVoiceButton.Height=c?32:36;VoiceSpeakerBox.Width=c?124:136;
  VoiceValues.Margin=new(0,c?8:12,0,c?8:12);
  foreach(var label in new[]{VoicePitchLabel,VoiceLevelLabel,VoicePaceLabel,VoicePauseLabel})label.FontSize=c?11:12;
  foreach(var value in new[]{VoicePitchText,VoiceLevelText,VoicePaceText,VoicePauseText})value.FontSize=c?13:16;
  var current=SignalsPanel.Parent as ContentControl;
  var target=c?PopupTopicsHost:TopicsHost;
  if(current!=target){TopicsPopup.IsOpen=false;if(current is not null)current.Content=null;target.Content=SignalsPanel;}
  TopicsHost.Visibility=c?Visibility.Collapsed:Visibility.Visible;TopicsButton.Visibility=c?Visibility.Visible:Visibility.Collapsed;
  RefreshManualVisibility();RefreshCallPresentation();RenderVoicePresentation();
  UpdateContentInset();
 }
 void UpdateContentInset()
 {
  if(!ready)return;
  double top=(compactCall?16:20)+44+(StatusBanner.Visibility==Visibility.Visible?48:0)+(compactCall?14:18);
  if(Tabs.Template.FindName("PART_SelectedContentHost",Tabs) is ContentPresenter host)host.Margin=new(compactCall?20:28,top,compactCall?20:28,compactCall?18:24);
 }
 void GoAudio(object s,RoutedEventArgs e)=>Tabs.SelectedIndex=2;
 void GoBrief(object s,RoutedEventArgs e)=>Tabs.SelectedIndex=1;
 void GoCall(object s,RoutedEventArgs e){Tabs.SelectedIndex=0;RenderVoice();}
 static string StageName(string stage)=>stage switch
 {
  "opening"=>T("Знакомство"),"discovery"=>T("Задача клиента"),"qualification"=>T("Условия проекта"),
  "value"=>T("Ценность решения"),"objection"=>T("Сомнения клиента"),"scoping"=>T("Объём работ"),
  "closing"=>T("Следующий шаг"),_=>T("Слушаем клиента")
 };
 static string TopicName(Signal signal){var topic=Coach.Topics.FirstOrDefault(t=>t.Id==signal.Id);return UiText.Language switch{"en"=>topic.En??signal.Label,"uk"=>topic.Uk??signal.Label,_=>topic.Ru??signal.Label};}
 string FriendlyError(Exception error)
 {
  StatusText.ToolTip=error.Message;
  var text=error.Message.Contains("402")?T("Jev отклонил запрос из-за биллинга (402). Сохранённый ключ не подтверждает доступ к API.")
   :error.Message.Contains("401")?T("Сервис не принял API-ключ (401). Проверь его в «Настройки ИИ».")
   :error.Message.Contains("429")?T("ИИ временно ограничил запросы (429). Подожди и повтори попытку.")
   :UiText.Contains(error.Message)?T(error.Message):T("Не удалось выполнить действие. Подробности — при наведении на это сообщение.");
  StatusText.Tag=text;return text;
 }
}

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace ArtoSalesCopilot;

public partial class MainWindow : Window
{
    Settings settings = new(); readonly Coach coach = new(); readonly List<Utterance> turns = [];
    CancellationTokenSource session = new(); SpeechWorker? worker; DualCapture? capture; string? audioFolder;
    SessionRecording? recording; VideoRecorder? video; Task recordingFinalization=Task.CompletedTask; bool closingAfterStop,finalizing; string stopSummary="Остановлено. Запись этой встречи не сохранялась.";
    bool ready, busy, live, scripted; int generation, aiRequests,turnRevision; long inputTokens; readonly Stopwatch callClock = new();
    Brief activeBrief = new(); string activeLanguage = "en", typesafe = "", openai = "", activeModel="gpt-6-sol", activeEffort="low"; bool activePhrasing;
    public MainWindow(bool smoke = false)
    {
        InitializeComponent();
        try { if(!smoke)settings=Store.Load(); } catch { SetUi(StatusText,()=>T("Не удалось прочитать настройки. Загружены значения по умолчанию; сохранённый файл не изменён.")); }
        LoadBrief(settings.Brief);
        PythonPathText.Text=settings.PythonPath;CachePathText.Text=settings.ModelCache;PhrasingBox.IsChecked=settings.UsePhrasing;
        ModelBox.SelectedIndex=settings.PhrasingModel switch {"gpt-5.6-terra"=>1,"gpt-6-luna"=>2,_=>0};
        ReasoningBox.SelectedIndex=settings.ReasoningEffort=="medium"?1:0;
        SaveSessionBox.IsChecked=settings.SaveSession;FfmpegText.Text=settings.FfmpegPath;VoiceEnabledBox.IsChecked=settings.AnalyzeVoice;
        VideoSourceBox.ItemsSource=new[]{new VideoSource(T("Без видео · только звук и текст"),"none")};VideoSourceBox.SelectedIndex=0;
        LanguageBox.SelectedIndex=settings.Language switch {"ru"=>1,"uk"=>2,_=>0};
        if(!smoke) { try{RefreshDeviceList();UpdateKeyStatus();}catch{SetUi(StatusText,()=>T("Не удалось загрузить устройства или ключи. Текстовый пример доступен."));} }
        InterfaceLanguageBox.SelectedIndex=settings.InterfaceLanguage switch{"uk"=>1,"en"=>2,_=>0};ThemeBox.SelectedIndex=settings.Theme=="light"?1:0;
        ready=true;ApplyAppearance();SetButtons();RenderVoice();
    }
    string LanguageCode=>((LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string)??"en";
    void LanguageChanged(object sender,SelectionChangedEventArgs e) {if(!ready)return;settings.Language=LanguageCode;if(busy)SetUi(StatusText,()=>T("Новый язык будет применён со следующего звонка."));RenderVoice();}
    void PinChanged(object sender,RoutedEventArgs e)=>Topmost=PinBox.IsChecked==true;
    Brief ReadBrief()=>new Brief {Title=BriefTitle.Text.Trim(),Profile=ProfileText.Text.Trim(),Offer=OfferText.Text.Trim(),Client=ClientText.Text.Trim(),Goal=GoalText.Text.Trim(),Constraints=ConstraintsText.Text.Trim(),CustomChecks=CustomText.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)}.Validate();
    void LoadBrief(Brief b) {BriefTitle.Text=b.Title;ProfileText.Text=b.Profile;OfferText.Text=b.Offer;ClientText.Text=b.Client;GoalText.Text=b.Goal;ConstraintsText.Text=b.Constraints;CustomText.Text=string.Join(Environment.NewLine,b.CustomChecks);}
    void CollectSettings() {settings.Brief=ReadBrief();settings.Language=LanguageCode;settings.PythonPath=PythonPathText.Text.Trim();settings.ModelCache=CachePathText.Text.Trim();settings.UsePhrasing=PhrasingBox.IsChecked==true;settings.Microphone=(MicrophoneBox.SelectedItem as DeviceOption)?.Id??"";settings.Output=(OutputBox.SelectedItem as DeviceOption)?.Id??"";settings.SaveSession=SaveSessionBox.IsChecked==true;settings.AnalyzeVoice=VoiceEnabledBox.IsChecked==true;settings.FfmpegPath=FfmpegText.Text.Trim();settings.PhrasingModel=((ComboBoxItem)ModelBox.SelectedItem).Tag.ToString()!;settings.ReasoningEffort=((ComboBoxItem)ReasoningBox.SelectedItem).Tag.ToString()!;}
    void SaveBrief(object s,RoutedEventArgs e)=>Guard(()=>{CollectSettings();Store.Save(settings);SetUi(StatusText,()=>T("Подготовка сохранена на компьютере."));});
    void SaveSettings(object s,RoutedEventArgs e)=>Guard(()=>{CollectSettings();settings.PhrasingModel=((ComboBoxItem)ModelBox.SelectedItem).Tag.ToString()!;settings.ReasoningEffort=((ComboBoxItem)ReasoningBox.SelectedItem).Tag.ToString()!;Store.Save(settings);if(!string.IsNullOrWhiteSpace(TypeSafeKey.Password))Store.SetKey("typesafe",TypeSafeKey.Password);if(!string.IsNullOrWhiteSpace(OpenAiKey.Password))Store.SetKey("openai",OpenAiKey.Password);TypeSafeKey.Clear();OpenAiKey.Clear();UpdateKeyStatus();SetUi(StatusText,()=>T("Настройки сохранены. Ключи зашифрованы для твоей учётной записи Windows."));});
    void RemoveKeys(object s,RoutedEventArgs e)=>Guard(()=>{if(MessageBox.Show(this,T("Остановить звонок и удалить сохранённые ключи приложения? Исходные файлы ключей останутся."),T("Удаление ключей"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;StopSession();typesafe="";openai="";Store.SetKey("typesafe","");Store.SetKey("openai","");TypeSafeKey.Clear();OpenAiKey.Clear();UpdateKeyStatus();});
    void ImportOpenAiKey(object s,RoutedEventArgs e)=>Guard(()=>{if(!ImportSelectedKey("openai"))return;UpdateKeyStatus();SetUi(StatusText,()=>T("Ключ OpenAI импортирован и зашифрован. Запросов к API не было."));});
    void ImportTypeSafeKey(object s,RoutedEventArgs e)=>Guard(()=>{
        if(!ImportSelectedKey("typesafe"))return;UpdateKeyStatus();SetUi(StatusText,()=>T("Ключ Jev импортирован и зашифрован. Исходный файл не изменён. Запросов к API не было."));
    });
    bool ImportSelectedKey(string provider)
    {
        var dialog=new OpenFileDialog{Title=T(provider=="typesafe"?"Импортировать ключ Jev из файла":"Импортировать ключ OpenAI из файла"),Filter="Text / environment files|*.txt;*.env|All files|*.*"};
        if(dialog.ShowDialog(this)!=true)return false;
        KeyImport.Import(provider,dialog.FileName);return true;
    }
    internal static string ReadTypeSafeFile(string path)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Selected key file was not found.");
        if(new FileInfo(path).Length>16000)throw new InvalidDataException("Key file is unexpectedly large.");
        var lines=File.ReadLines(path).Select(x=>x.Trim()).Where(x=>x.StartsWith("TYPESAFE_API_KEY=",StringComparison.Ordinal)).ToArray();
        if(lines.Length!=1)throw new InvalidDataException("Expected exactly one TYPESAFE_API_KEY variable.");
        var key=lines[0]["TYPESAFE_API_KEY=".Length..].Trim().Trim('"','\'');
        if(key.Length<12||key.Any(char.IsWhiteSpace))throw new InvalidDataException("Paste the TypeSafe key after TYPESAFE_API_KEY= and save the file first.");
        return key;
    }
    void UpdateKeyStatus(){bool jevSaved=Store.GetKey("typesafe").Length>0,openaiSaved=Store.GetKey("openai").Length>0;SetUi(KeyStatus,()=>T("Jev: {0}\nOpenAI: {1}",T(jevSaved?"ключ сохранён":"ключ не добавлен"),T(openaiSaved?"ключ сохранён":"ключ не добавлен")));}
    void RefreshDevices(object s,RoutedEventArgs e)=>Guard(RefreshDeviceList);
    void RefreshVideoSources(object s,RoutedEventArgs e)=>Guard(()=>{if(busy)throw new InvalidOperationException(T("Останови звонок перед выбором видео."));VideoSourceBox.ItemsSource=VideoSources.List();VideoSourceBox.SelectedIndex=0;});
    void OpenSessions(object s,RoutedEventArgs e)=>Guard(()=>{var folder=Path.GetFullPath(Path.Combine(Store.Root,"..","..","sessions"));Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={folder},UseShellExecute=true});});
    void RefreshDeviceList()
    {
        if(live)throw new InvalidOperationException(T("Останови звонок перед сменой устройства."));
        var m=DualCapture.Devices(DataFlow.Capture);m.Insert(0,new("",T("Микрофон Windows для связи")));MicrophoneBox.ItemsSource=m;MicrophoneBox.SelectedItem=m.FirstOrDefault(x=>x.Id==settings.Microphone)??m[0];
        var o=DualCapture.Devices(DataFlow.Render);o.Insert(0,new("",T("Устройство вывода Windows по умолчанию")));OutputBox.ItemsSource=o;OutputBox.SelectedItem=o.FirstOrDefault(x=>x.Id==settings.Output)??o[0];
    }
    void ImportBrief(object s,RoutedEventArgs e)=>Guard(()=>{
        if(busy)throw new InvalidOperationException(T("Останови звонок перед заменой подготовки."));
        var d=new OpenFileDialog{Filter=T("Подготовка клиента|*.json;*.md;*.txt"),Title=T("Загрузить контекст клиента")};if(d.ShowDialog(this)!=true)return;
        var b=Brief.Load(d.FileName);
        if(Path.GetExtension(d.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase))LoadBrief(b);
        else {ClientText.Text=b.Client;BriefTitle.Text=b.Title;}
        SetUi(StatusText,()=>T("Данные загружены. Проверь их и нажми «Сохранить подготовку»."));
    });
    void ExportBrief(object s,RoutedEventArgs e)=>Guard(()=>{var b=ReadBrief();var d=new SaveFileDialog{Filter=T("Подготовка JSON|*.json"),FileName="client-brief.json"};if(d.ShowDialog(this)==true)File.WriteAllText(d.FileName,JsonSerializer.Serialize(b,Store.Json));});
    void ExportNotes(object s,RoutedEventArgs e)=>Guard(()=>{
        var d=new SaveFileDialog{Filter=T("Заметки Markdown|*.md"),FileName="call-notes-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".md"};if(d.ShowDialog(this)!=true)return;
        var sb=new StringBuilder("# Call notes\n\n");sb.AppendLine(scripted?"Source: scripted offline demo (not AI analysis).":"Source: local session transcript; may contain recognition errors.");
        foreach(var t in turns)sb.AppendLine($"\n**{t.Speaker} [{t.Seconds:F1}s]**: {t.Text}");
        sb.AppendLine("\n## Latest suggestion (not an agreed commitment)\n\n"+SayText.Text);File.WriteAllText(d.FileName,sb.ToString(),Encoding.UTF8);SetUi(StatusText,()=>T("Заметки сохранены в выбранный файл."));
    });
    void CopyLine(object s,RoutedEventArgs e)=>Guard(()=>Clipboard.SetText(SayText.Text));
    void SetButtons() {DemoButton.IsEnabled=!busy&&!finalizing;AiDemoButton.IsEnabled=!busy&&!finalizing;LiveButton.IsEnabled=!busy&&!finalizing;NewButton.IsEnabled=!busy&&!finalizing;LanguageBox.IsEnabled=!busy&&!finalizing;VoiceEnabledBox.IsEnabled=!busy&&!finalizing;StopButton.IsEnabled=busy&&!finalizing;MicrophoneBox.IsEnabled=OutputBox.IsEnabled=VideoSourceBox.IsEnabled=!busy&&!finalizing;}
    void ClearConversation() {turns.Clear();TranscriptPanel.Children.Clear();TranscriptPanel.Children.Add(EmptyTranscript);SetUi(TurnCount,()=>T("0 реплик"));SignalsPanel.Children.Clear();aiRequests=0;inputTokens=0;SetUi(AdviceTitle,()=>T("Подсказка появится здесь"));SetUi(SayText,()=>T("Заполни «Подготовку», выбери устройства в «Звук и запись», затем нажми «Начать звонок»."));SetUi(AdviceMeta,()=>T("Подсказок пока нет"));SetUi(StageText,()=>T("Знакомство"));ResetVoiceState();}
    void NewConversation(object s,RoutedEventArgs e) {if(busy)return;if(turns.Count>0&&MessageBox.Show(this,T("Очистить текущий разговор на экране? Сначала сохрани заметки, если они нужны."),T("Новый звонок"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;scripted=false;ClearConversation();SetUi(ModeText,()=>T("Микрофон выключен"));SetUi(StatusText,()=>T("Новый разговор. Проверь подготовку клиента и устройства."));SetUi(SessionMeta,()=>T("Запись начнётся после «Начать звонок»"));}
    void Begin(bool demo,bool isLive,bool clear)
    {
        if(finalizing)throw new InvalidOperationException(T("Подожди завершения сохранения предыдущей записи."));
        StopSession(); if(clear)ClearConversation(); session=new();busy=true;live=isLive;scripted=demo;generation++;callClock.Restart();
        CollectSettings();activeBrief=settings.Brief;activeLanguage=LanguageCode;activePhrasing=settings.UsePhrasing;SetButtons();RenderVoice();Tabs.SelectedIndex=0;
        activeModel=((ComboBoxItem)ModelBox.SelectedItem).Tag.ToString()!;activeEffort=((ComboBoxItem)ReasoningBox.SelectedItem).Tag.ToString()!;
    }
    async void PlayDemo(object s,RoutedEventArgs e)=>await DemoRun(false);
    async void PlayAiDemo(object s,RoutedEventArgs e)=>await DemoRun(true);
    async Task DemoRun(bool ai)
    {
        if(busy||!recordingFinalization.IsCompleted)return;
        if(!ConfirmReplace())return;
        int gen=generation;
        try {
            if(ai) { typesafe=Store.GetKey("typesafe");if(typesafe.Length==0)throw new InvalidOperationException(T("Добавь ключ Jev в «Настройки ИИ»."));if(MessageBox.Show(this,T("Отправить 8 вымышленных реплик в Jev? Это расходует API-кредит. Твой реальный brief и микрофон не используются."),T("Проверка Jev"),MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return; }
            Begin(!ai,false,true);gen=generation;var ct=session.Token;
            activeBrief=new Brief {Title="Fictional sales demo",Client="Fictional company with slow lead responses and manual data entry. No real client data.",CustomChecks=[]};activePhrasing=false;
            SetUi(ModeText,()=>ai?T("Проверка Jev · без микрофона"):T("Текстовый пример · без микрофона"));
            SetUi(StatusText,()=>ai?T("Проверяем Jev на вымышленном разговоре."):T("Идёт текстовый пример. Реплики и подсказки подготовлены заранее. Микрофон выключен."));
            for(int i=0;i<Demo.Turns.Length;i++) {ct.ThrowIfCancellationRequested();AddTurn(Demo.Turn(i,activeLanguage));if(ai)await Analyze(ct,gen);else ShowAdvice(Demo.Advice(i,activeLanguage));await Task.Delay(2400,ct);}
            if(gen==generation){SetUi(StatusText,()=>ai?T("Проверка Jev завершена. Посмотри полученные подсказки."):T("Пример завершён. Микрофон и ИИ не использовались. Для настоящего разговора нажми «Начать звонок»."));busy=false;SetButtons();RenderVoice();}
        }catch(OperationCanceledException){}catch(Exception ex){if(gen==generation)Fail(ex);}
    }
    async void SendText(object s,RoutedEventArgs e)=>await TypedTurn();
    async void ManualKeyDown(object s,KeyEventArgs e){if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;await TypedTurn();}}
    async Task TypedTurn()
    {
        if(busy||string.IsNullOrWhiteSpace(ManualText.Text))return;
        int gen=generation;
        try {
            typesafe=Store.GetKey("typesafe");if(typesafe.Length==0)throw new InvalidOperationException(T("Добавь ключ Jev в «Настройки ИИ». Текстовый пример работает без ключа."));
            if(ConsentBox.IsChecked!=true)throw new InvalidOperationException(T("На вкладке «Звук и запись» подтверди разрешение на передачу текста разговора и brief в ИИ."));
            bool clear=scripted;Begin(false,false,clear);gen=generation;openai=Store.GetKey("openai");
            if(activePhrasing&&openai.Length==0)throw new InvalidOperationException(T("Генерация через OpenAI включена. Добавь его ключ в «Настройки ИИ» или выключи эту функцию."));
            AddTurn(new(((SpeakerBox.SelectedItem as ComboBoxItem)?.Tag as string)??"client",ManualText.Text.Trim(),turns.Count==0?0:turns[^1].Seconds+1));ManualText.Clear();SetUi(ModeText,()=>T("Проверка текста · без микрофона"));
            await Analyze(session.Token,gen);if(gen==generation){busy=false;SetButtons();}
        }catch(OperationCanceledException){}catch(Exception ex){if(gen==generation)Fail(ex);}
    }
    async Task Analyze(CancellationToken ct,int gen,Utterance[]? supplied=null,int revision=-1)
    {
        SetUi(StatusText,()=>T("Jev анализирует разговор…"));var snapshot=supplied??turns.ToArray();
        var advice=await coach.Analyze(activeBrief,snapshot,activeLanguage,typesafe,ct);
        ct.ThrowIfCancellationRequested();if(gen!=generation)return;aiRequests++;inputTokens+=advice.InputTokens;if(revision>=0&&revision!=turnRevision)return;ShowAdvice(advice);SetUi(StatusText,()=>live?T("Слушаем разговор · Jev анализирует текст"):T("Анализ завершён."));
        SetUi(SessionMeta,()=>T("Анализов Jev: {0} · входных токенов: {1:N0}",aiRequests,inputTokens));
        if(activePhrasing&&openai.Length>0)
        {
            try {SetUi(StatusText,()=>T("OpenAI готовит фразу. Jev проверит её перед показом…"));var phrase=await coach.Phrase(activeBrief,snapshot,advice,activeLanguage,openai,typesafe,ct,activeModel,activeEffort);ct.ThrowIfCancellationRequested();if(gen!=generation||(revision>=0&&revision!=turnRevision))return;if(phrase?.Text is not null){SayText.Text=phrase.Text;recording?.Advice(advice,phrase.Text);SetUi(AdviceMeta,()=>T("Фраза {0} · проверена Jev",activeModel));UiText.Bind(AdviceMeta,FrameworkElement.ToolTipProperty,()=>T("Обдумывание: {0}; подготовка и проверка: {1:F1} с",activeEffort,phrase.Seconds));}else SetUi(AdviceMeta,()=>T("Сгенерированная фраза не прошла проверку; показан шаблон"));SetUi(StatusText,()=>live?T("Слушаем разговор · Jev анализирует текст + OpenAI"):T("Анализ завершён."));}
            catch(OperationCanceledException){throw;}catch(Exception ex){if(gen==generation){SetUi(AdviceMeta,()=>T("Генерация недоступна; показан шаблон"));SetUi(StatusText,()=>T(FriendlyError(ex)));}}
        }
    }
    async Task ProcessCoaching(ChannelReader<(Utterance[] Turns,int Revision)> reader,CancellationToken ct,int gen)
    {
        try{await foreach(var item in reader.ReadAllAsync(ct))await Analyze(ct,gen,item.Turns,item.Revision);}
        catch(OperationCanceledException){}
        catch(Exception ex){if(gen==generation){SetUi(ModeText,()=>T("Ошибка ИИ · запись продолжается"));SetUi(StatusText,()=>T(FriendlyError(ex))+T(" Запись и расшифровка продолжаются. Проверь доступ к ИИ перед новым звонком."));SetUi(AdviceTitle,()=>T("ИИ сейчас недоступен"));SetUi(SayText,()=>T("Новых подсказок пока нет. Запись и расшифровка продолжаются."));SetUi(AdviceMeta,()=>T("Ошибка сервиса · это сообщение о состоянии"));}}
    }
    async void StartLive(object s,RoutedEventArgs e)
    {
        if(busy||!recordingFinalization.IsCompleted)return;
        if(!ConfirmReplace())return;
        int gen=generation;
        try {
            if(ConsentBox.IsChecked!=true)throw new InvalidOperationException(T("На вкладке «Звук и запись» подтверди разрешение на обработку разговора."));
            typesafe=Store.GetKey("typesafe");if(typesafe.Length==0)throw new InvalidOperationException(T("Перед звонком добавь ключ Jev в «Настройки ИИ»."));
            Begin(false,true,true);gen=generation;openai=Store.GetKey("openai");var runningSession=session;var ct=runningSession.Token;
            if(activePhrasing&&openai.Length==0)throw new InvalidOperationException(T("Генерация через OpenAI включена. Добавь его ключ в «Настройки ИИ» или выключи эту функцию."));
            SetUi(ModeText,()=>T("Подготовка · микрофон выключен"));SetUi(StatusText,()=>T("Загружаем распознавание речи. Микрофон ещё выключен…"));
            worker=new();await worker.Start(settings,ct);ct.ThrowIfCancellationRequested();if(gen!=generation)return;
            await StartVoice(gen,ct);ct.ThrowIfCancellationRequested();if(gen!=generation)return;
            var source=VideoSourceBox.SelectedItem as VideoSource??new VideoSource(T("Без видео"),"none");
            if(SaveSessionBox.IsChecked==true||source.Kind!="none")recording=new SessionRecording(activeBrief,activeLanguage);
            if(source.Kind!="none"){
                video=new();await video.Start(FfmpegText.Text.Trim(),source,Path.Combine(recording!.Folder,"screen.mkv"),msg=>Dispatcher.BeginInvoke(()=>{if(gen==generation)Fail(new InvalidOperationException(msg));}),ct);
                ct.ThrowIfCancellationRequested();if(gen!=generation)return;
            }
            audioFolder=Path.Combine(Store.Root,"audio-temp",Guid.NewGuid().ToString("N"));
            var queue=Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(8){FullMode=BoundedChannelFullMode.Wait,SingleReader=true,SingleWriter=false});
            capture=new();capture.Start(settings,audioFolder,chunk=>{
                var voiceWorker=voice;
                if(voiceWorker is not null){chunk=chunk with{VoiceProfile=voiceWorker.StampProfile(chunk.Speaker)};voiceWorker.Enqueue(chunk);}
                if(ct.IsCancellationRequested||!queue.Writer.TryWrite(chunk)){TryDelete(chunk.Path);Dispatcher.BeginInvoke(()=>{if(gen==generation)SetUi(StatusText,()=>T("Распознавание не успевает: один фрагмент пропущен. Останови звонок и проверь нагрузку компьютера."));});}
            },(who,value)=>Dispatcher.BeginInvoke(()=>{if(gen==generation){if(who=="rep")MicMeter.Value=value;else OutputMeter.Value=value;}}),message=>Dispatcher.BeginInvoke(()=>{if(gen==generation)Fail(new InvalidOperationException(message));}),recording?.Folder,who=>recording?.AudioStart(who));
            SetUi(ModeText,()=>T("Слушаем тебя и клиента"));SetUi(StatusText,()=>T("Слушаем разговор. Звук других приложений на выбранном устройстве тоже попадает в запись."));
            if(recording is not null)SetUi(ModeText,()=>source.Kind=="none"?T("Записываем звук и текст"):T("Записываем видео, звук и текст"));
            var adviceQueue=Channel.CreateBounded<(Utterance[],int)>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=true});
            var coaching=ProcessCoaching(adviceQueue.Reader,ct,gen);
            try{await foreach(var chunk in queue.Reader.ReadAllAsync(ct))
                {
                    try {
                        var text=await worker.Transcribe(chunk.Path,activeLanguage,"",ct);
                        ct.ThrowIfCancellationRequested();if(gen!=generation)break;if(string.IsNullOrWhiteSpace(text))continue;
                        VoiceTranscript(chunk,text);AddTurn(new(chunk.Speaker,text,chunk.Seconds));if(!coaching.IsCompleted)adviceQueue.Writer.TryWrite((turns.ToArray(),turnRevision));
                    } finally {TryDelete(chunk.Path);}
                }
            }finally{adviceQueue.Writer.TryComplete();runningSession.Cancel();await coaching;}
        }catch(OperationCanceledException){}catch(Exception ex){if(gen==generation)Fail(ex);}
    }
    public void AddTurn(Utterance t)
    {
        recording?.Turn(t);
        turnRevision++;
        turns.Add(t);if(turns.Count>1000)turns.RemoveAt(0);
        if(EmptyTranscript is not null)TranscriptPanel.Children.Remove(EmptyTranscript);
        var stamp=$"{Math.Floor(Math.Max(0,t.Seconds)/60):00}:{Math.Max(0,t.Seconds)%60:00}";
        var panel=new StackPanel();panel.Children.Add(TranscriptHeading(t,stamp));panel.Children.Add(new TextBlock{Text=t.Text,FontSize=15,LineHeight=23});
        var row=new Border{Child=panel,Padding=new(12),Margin=new(0,0,0,10),CornerRadius=new(7)};
        row.SetResourceReference(Border.BackgroundProperty,t.Speaker=="rep"?"TranscriptRep":"TranscriptClient");TranscriptPanel.Children.Add(row);
        if(TranscriptPanel.Children.Count>200)TranscriptPanel.Children.RemoveAt(0);SetUi(TurnCount,()=>T("Реплик: {0}",turns.Count));TranscriptScroll.ScrollToEnd();
    }
    public void ShowAdvice(Advice a)
    {
        recording?.Advice(a);
        SetUi(AdviceTitle,()=>Coach.Title(a.Move,UiText.Language));SayText.Text=a.Say;SetUi(StageText,()=>StageName(a.Stage));SetUi(AdviceMeta,()=>scripted?T("Готовый пример · ИИ не используется"):T("Подсказка Jev · базовая фраза"));UiText.Bind(AdviceMeta,FrameworkElement.ToolTipProperty,()=>T("{0}; уверенность в выборе шага: {1:P0}. Это не вероятность продажи.",a.Source,a.Confidence));SignalsPanel.Children.Clear();
        foreach(var s in a.Signals) {
            var state=s.Value>=.8?T("есть в разговоре"):s.Value<=.2?T("пока не подтверждено"):T("нужно уточнить");
            var text=new TextBlock{FontSize=11};text.SetResourceReference(TextBlock.ForegroundProperty,s.Value>=.8?"Accent":"Muted");SetUi(text,()=>T("{0} · {1}",TopicName(s),T(state)));
            var chip=new Border{Child=text,Padding=new(10,7,10,7),Margin=new(0,0,7,7),CornerRadius=new(5)};
            chip.SetResourceReference(Border.BackgroundProperty,"Chip");UiText.Bind(chip,FrameworkElement.ToolTipProperty,()=>T("Оценка наличия сведений: {0:P0}. Это не вероятность продажи. «Не подтверждено» также может означать «не применимо».",s.Value));SignalsPanel.Children.Add(chip);
        }
    }
    async void StopClicked(object s,RoutedEventArgs e){StopSession();SetUi(StatusText,()=>T("Останавливаем запись и сохраняем файлы…"));await recordingFinalization;SetUi(StatusText,()=>T(stopSummary));}
    void StopSession()
    {
        generation++;session.Cancel();capture?.Dispose();capture=null;worker?.Dispose();worker=null;busy=false;live=false;MicMeter.Value=0;OutputMeter.Value=0;SetUi(ModeText,()=>T("Остановлено · микрофон выключен"));SetButtons();
        var voiceCompletion=StopVoice();var saved=recording;recording=null;var recorder=video;video=null;
        if(saved is not null||recorder is not null||!voiceCompletion.IsCompleted){finalizing=true;SetButtons();recordingFinalization=FinishRecording(saved,recorder,settings.FfmpegPath,voiceCompletion);}
        var folder=audioFolder;audioFolder=null;if(folder is not null&&Directory.Exists(folder)){foreach(var p in Directory.GetFiles(folder,"*.wav"))TryDelete(p);try{Directory.Delete(folder);}catch{}}
    }
    static void TryDelete(string path){try{File.Delete(path);}catch{}}
    bool ConfirmReplace()=>turns.Count==0||scripted||MessageBox.Show(this,T("Начать новый звонок? Несохранённые заметки текущего разговора исчезнут с экрана. Продолжить?"),T("Новый звонок"),MessageBoxButton.YesNo)==MessageBoxResult.Yes;
    void Fail(Exception ex){StopSession();SetUi(StatusText,()=>T(FriendlyError(ex)));SetUi(ModeText,()=>T("Ошибка · микрофон выключен"));}
    void Guard(Action action){try{action();}catch(Exception ex){SetUi(StatusText,()=>T(FriendlyError(ex)));}}
    async Task FinishRecording(SessionRecording? saved,VideoRecorder? recorder,string ffmpeg,Task voiceCompletion){try{var ok=recorder is null||await recorder.Stop();if(saved is not null&&recorder is not null&&ok)ok=await saved.Mux(ffmpeg);saved?.Finish(ok?"stopped":"stopped-video-incomplete");stopSummary=saved is null?T("Остановлено. Запись этой встречи не сохранялась."):ok?T("Остановлено. Файлы сохранены в папке записей."):T("Остановлено. Исходные записи сохранены, но собрать видео не удалось. Проверь папку записей.");}catch{stopSummary=T("Исходные записи сохранены, но завершить обработку не удалось. Проверь папку записей.");}finally{await voiceCompletion;finalizing=false;SetButtons();}}
    async void OnClosing(object? s,CancelEventArgs e){if(closingAfterStop){coach.Dispose();return;}StopSession();if(!recordingFinalization.IsCompleted){e.Cancel=true;await recordingFinalization;closingAfterStop=true;Close();}else coach.Dispose();}
    public async Task Smoke(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("UI check failed: "+label);checks.Add("PASS "+label);}
        void Visible(FrameworkElement element,string label)
        {
            var bounds=element.TransformToAncestor(this).TransformBounds(new Rect(new Point(),element.RenderSize));
            Check(element.IsVisible&&bounds.Top>=0&&bounds.Bottom<=ActualHeight&&bounds.Left>=0&&bounds.Right<=ActualWidth,label);
        }
        MicrophoneBox.ItemsSource=new[]{new DeviceOption("test-mic","Микрофон · тестовое устройство")};MicrophoneBox.SelectedIndex=0;
        OutputBox.ItemsSource=new[]{new DeviceOption("test-output","Наушники · тестовое устройство"),new DeviceOption("test-cable","CABLE-A Input · тестовое устройство")};OutputBox.SelectedIndex=0;
        KeyStatus.Text="Jev: тестовый ключ сохранён\nOpenAI: тестовый ключ сохранён";
        scripted=false;ClearConversation();UpdateLayout();Capture(Path.Combine(directory,"redesign-ready.png"));
        Check(!busy&&!live&&capture is null&&voice is null,"startup keeps microphone and voice worker off");
        Check(LanguageCode=="en"&&VoicePitchLabel.Text=="Тон голоса","English conversation retains Russian operator labels");
        Check(TranscriptPanel.Children.Contains(EmptyTranscript),"new conversation has useful transcript empty state");
        var demoTask=DemoRun(false);await Task.Delay(80);
        Check(busy&&!live&&capture is null&&voice is null&&StopButton.IsEnabled,"real demo action advances text without starting audio");
        StopSession();await demoTask;Check(!busy&&!StopButton.IsEnabled,"stop cancels running demo playback");
        scripted=false;ClearConversation();ModeText.Text="Микрофон выключен";StatusText.Text="Начни с подготовки клиента и выбора звука. Или посмотри пример без микрофона.";
        for(int i=0;i<4;i++){Tabs.SelectedIndex=i;UpdateLayout();Capture(Path.Combine(directory,$"screen-{i+1}.png"));}
        GoAudio(this,new());Check(Tabs.SelectedIndex==2,"preparation shortcut opens sound settings");
        GoCall(this,new());Check(Tabs.SelectedIndex==0,"sound settings shortcut returns to call");
        scripted=true;for(int i=0;i<5;i++)AddTurn(Demo.Turn(i,"en"));ShowAdvice(Demo.Advice(4,"en"));RenderVoice();
        ModeText.Text="Текстовый пример · без микрофона";StatusText.Text="Проверка интерфейса: вымышленные реплики, без микрофона и запросов к ИИ.";
        Check(VoiceStatus.Text.Contains("нет звука")&&VoicePitchText.Text=="—","text demo explains absence of intonation and shows no fake metrics");
        Check(AdviceTitle.Text==Coach.Title("objection","ru")&&SayText.Text==Demo.Advice(4,"en").Say,"Russian coaching explanation preserves English client phrase");
        Check(SayText.Foreground==FindResource("Ink")&&SayText.FontWeight==FontWeights.Normal,"selected navigation styling does not bleed into content");
        UpdateLayout();Capture(Path.Combine(directory,"redesign-demo.png"));
        foreach(var size in new[]{(1000d,720d),(1320d,900d)})
        {
            Width=size.Item1;Height=size.Item2;UpdateLayout();
            Visible(VoiceCard,$"voice panel entirely visible at {Width}x{Height}");
            Visible(VoicePauseText,$"all four voice values visible at {Width}x{Height}");
            Visible(LiveButton,$"main action visible at {Width}x{Height}");
            Check(AdviceCard.ActualHeight>=175,$"advice keeps useful height at {Width}x{Height}");
            Capture(Path.Combine(directory,$"redesign-{Width:F0}x{Height:F0}.png"));
        }
        foreach(var control in new Control[]{DemoButton,AiDemoButton,LiveButton,StopButton,LanguageBox,ManualText,ProfileText,TypeSafeKey,VoiceSpeakerBox,ResetVoiceButton,VoiceEnabledBox})
            Check(control.Focusable,"native keyboard target: "+control.Name);
        Tabs.SelectedIndex=2;UpdateLayout();OutputBox.IsDropDownOpen=true;await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);UpdateLayout();
        var popup=(System.Windows.Controls.Primitives.Popup)OutputBox.Template.FindName("PART_Popup",OutputBox);
        Check(popup.IsOpen&&popup.Child.IsVisible,"custom device dropdown opens a visible popup");
        OutputBox.SelectedIndex=1;OutputBox.IsDropDownOpen=false;Check((OutputBox.SelectedItem as DeviceOption)?.Id=="test-cable","device selection retains endpoint identity");
        Tabs.SelectedIndex=0;UpdateLayout();LanguageBox.Focus();
        Check(LanguageBox.IsKeyboardFocusWithin,"keyboard focus reaches language selection");
        StopSession();Check(ModeText.Text=="Остановлено · микрофон выключен"&&!busy&&!live&&!StopButton.IsEnabled,"stop disables capture and stop action");
        scripted=false;var fake=VoiceTests.Sample();clientVoice.Reset();for(int i=0;i<3;i++)clientVoice.Add(fake,i*4,1);
        clientVoice.Add(fake with{PitchHz=220},12,1);voiceReadings["client"]=clientVoice.Add(fake with{PitchHz=220},16,1);
        RenderVoice();Check(VoicePitchText.Text.Contains("Выше")&&!ResetVoiceButton.IsEnabled,"changed voice is readable; speaker reset disabled while stopped");
        foreach(var index in new[]{0,1,2}){LanguageBox.SelectedIndex=index;RenderVoice();Check(VoiceHeading.Text=="Голос и интонация",$"operator language independent of conversation index {index}");}
        VoiceSpeakerBox.SelectedIndex=1;Check(VoicePitchText.Text=="—","speaker channels do not share measurements");VoiceSpeakerBox.SelectedIndex=0;
        LanguageBox.SelectedIndex=0;AdviceTitle.Text="Уточни, что смущает клиента";StatusText.Text="Проверка интерфейса: голосовые показатели ниже получены из синтетического теста.";
        UpdateLayout();Capture(Path.Combine(directory,"redesign-voice-example.png"));
        voiceProblem="worker_failed";RenderVoice();Check(VoiceStatus.Text.Contains("недоступен")&&VoicePitchText.Text=="—","voice error hides stale readings and gives a useful explanation");
        voiceProblem="";scripted=false;ClearConversation();Check(voiceReadings.Count==0&&!clientVoice.Ready&&TranscriptPanel.Children.Contains(EmptyTranscript),"new call clears voice reference and restores empty state");
        var originalRoot=Store.Root;var testRoot=Path.Combine(directory,"ui-settings-"+Guid.NewGuid().ToString("N"));
        try
        {
            Store.Root=testRoot;Store.SetKey("typesafe","fictional-typesafe-test-key");Store.SetKey("openai","fictional-openai-test-key");
            LoadBrief(new Brief{Title="UI test client",Client="Fictional context"});TypeSafeKey.Clear();OpenAiKey.Clear();
            ModelBox.SelectedIndex=0;ReasoningBox.SelectedIndex=0;PhrasingBox.IsChecked=true;VoiceEnabledBox.IsChecked=false;
            SaveSettings(this,new());var restored=Store.Load();
            Check(restored.Brief.Title=="UI test client"&&restored.Microphone=="test-mic"&&restored.Output=="test-cable","save preserves brief and exact audio endpoint selection");
            Check(restored.Language=="en"&&restored.PhrasingModel=="gpt-6-sol"&&restored.ReasoningEffort=="low"&&!restored.AnalyzeVoice,"model, language and voice preference round trip");
            Check(Store.GetKey("typesafe")=="fictional-typesafe-test-key"&&Store.GetKey("openai")=="fictional-openai-test-key","empty API fields preserve encrypted existing keys");
            Check(KeyStatus.Text.Contains("ключ сохранён"),"stored key status is explicit");
            await CheckAppearance(directory,checks);
        }
        finally{Store.Root=originalRoot;}
        StatusText.Text=FriendlyError(new InvalidOperationException("TypeSafe HTTP 402 billing_error"));
        Check(StatusText.Text.Contains("биллинга")&&StatusText.ToolTip.ToString()!.Contains("402"),"billing error is understandable with original diagnostic available");
        ModeText.Text="Ошибка ИИ · запись продолжается";AdviceTitle.Text="ИИ сейчас недоступен";SayText.Text="Новых подсказок пока нет. Запись и расшифровка продолжаются.";
        UpdateLayout();Capture(Path.Combine(directory,"redesign-error.png"));
        File.WriteAllLines(Path.Combine(directory,"ui-checks.txt"),checks);
    }
    void Capture(string path){var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);}
}

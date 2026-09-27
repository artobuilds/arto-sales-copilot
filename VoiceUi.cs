using System.Windows;
using System.Windows.Controls;

namespace ArtoSalesCopilot;

public partial class MainWindow
{
    LiveVoice? voice;
    readonly VoiceBaseline clientVoice=new(),repVoice=new();
    readonly Dictionary<string,VoiceReading> voiceReadings=[];
    string voiceProblem="";
    string VoiceSpeaker=>((VoiceSpeakerBox.SelectedItem as ComboBoxItem)?.Tag as string)??"client";
    void VoiceSpeakerChanged(object s,SelectionChangedEventArgs e){if(ready)RenderVoice();}
    void ResetVoiceState(){clientVoice.Reset();repVoice.Reset();voiceReadings.Clear();voiceProblem="";if(ready)RenderVoice();}
    void ResetVoice(object s,RoutedEventArgs e)
    {
        if(!live||voice is null||VoiceSpeaker!="client")return;
        voice.ResetClient();clientVoice.Reset();voiceReadings.Remove("client");voiceProblem="";RenderVoice();
        // New profile number is saved with the next measurement, no identifying voiceprint.
    }
    async Task StartVoice(int gen,CancellationToken ct)
    {
        if(!settings.AnalyzeVoice){voiceProblem="off";RenderVoice();return;}
        var next=new LiveVoice((who,f,at,profile)=>Dispatcher.BeginInvoke(()=>{
            if(gen!=generation||!live||voice is null||profile!=voice.Profile(who))return;
            var baseline=who=="rep"?repVoice:clientVoice;
            var reading=baseline.Add(f,at,profile);voiceReadings[who]=reading;
            if(voiceProblem!="save_failed")voiceProblem="";
            try{recording?.Voice(who,reading);}catch{voiceProblem="save_failed";}
            RenderVoice();
        }),error=>Dispatcher.BeginInvoke(()=>{if(gen==generation&&live){voiceProblem=error;RenderVoice();}}));
        voice=next;
        try{await next.Start(settings.PythonPath,ct);ct.ThrowIfCancellationRequested();RenderVoice();}
        catch(OperationCanceledException){next.Dispose();throw;}
        catch{next.Dispose();if(ct.IsCancellationRequested||gen!=generation)throw new OperationCanceledException(ct);if(voice==next)voice=null;voiceProblem="worker_failed";RenderVoice();}
    }
    Task StopVoice(){var old=voice;voice=null;old?.Dispose();RenderVoice();return old?.Completion??Task.CompletedTask;}
    void VoiceTranscript(AudioChunk chunk,string text)
    {
        if(voice is null||chunk.VoiceProfile!=voice.Profile(chunk.Speaker))return;
        var baseline=chunk.Speaker=="rep"?repVoice:clientVoice;
        var pace=baseline.AddText(text,chunk.Duration);
        if(voiceReadings.TryGetValue(chunk.Speaker,out var r)&&r.State is "ready" or "calibrating")voiceReadings[chunk.Speaker]=r with{Pace=pace};
        RenderVoice();
    }
    void RenderVoice()
    {
        if(!ready)return;
        VoiceHeading.Text=T("Голос и интонация");
        ClientVoiceItem.Content=T("Голос клиента");RepVoiceItem.Content=T("Мой голос");
        ResetVoiceButton.Content=T("Другой собеседник");
        ResetVoiceButton.ToolTip=T("Нажми, если говорить начал другой человек или изменилось устройство звука. Обучение голосу начнётся заново, история разговора сохранится.");
        ResetVoiceButton.IsEnabled=live&&voice is not null&&VoiceSpeaker=="client";
        VoicePitchLabel.Text=T("Тон голоса");VoiceLevelLabel.Text=T("Громкость");
        VoicePaceLabel.Text=T("Скорость речи");VoicePauseLabel.Text=T("Паузы");
        VoiceNote.Text=T("Изменения речи, а не эмоции или намерения. Анализ остаётся на компьютере.");
        VoicePaceText.ToolTip=T("Приблизительная оценка по количеству распознанных слов и длине аудиофрагмента.");
        VoicePauseText.ToolTip=T("Сравниваются только паузы внутри фраз. Время, пока говорит другой человек, сюда не входит.");
        voiceReadings.TryGetValue(VoiceSpeaker,out var reading);
        var profile=voice?.Profile(VoiceSpeaker)??reading?.Profile??1;
        VoicePersonText.Text=VoiceSpeaker=="rep"?T("Твой голос · этот звонок"):T("Собеседник {0} · этот звонок",profile);
        if(reading is null)
        {
            VoicePitchText.Text=VoiceLevelText.Text=VoicePaceText.Text=VoicePauseText.Text="—";
            VoiceStatus.Text=scripted
                ?T("В текстовом примере нет звука. Интонация появится при настоящем звонке.")
                :live?T("Ждём речь. Сначала изучим обычный голос, затем покажем изменения.")
                :T("Включится после «Начать звонок». Сравниваем речь каждого клиента с его обычным голосом.");
        }
        else
        {
            VoicePitchText.Text=VoiceValue(reading.Pitch);VoiceLevelText.Text=VoiceValue(reading.Level);
            VoicePaceText.Text=VoiceValue(reading.Pace);VoicePauseText.Text=VoiceValue(reading.Pauses);
            VoiceStatus.Text=reading.State switch
            {
                "calibrating"=>T("Изучаем обычную речь: {0:F0} из 12 секунд. Нужно несколько фраз.",Math.Min(12,reading.CollectedSeconds)),
                "ready"=>T("Сравниваем с обычной речью этого собеседника в начале звонка."),
                "clipped"=>T("Звук перегружен. Уменьши уровень микрофона или источника."),
                "noisy"=>T("Слишком много шума. Для сравнения нужна более чистая речь."),
                "too_quiet"=>T("Очень тихий звук. Проверь выбранное устройство и громкость."),
                _=>T("Пока мало разборчивой речи. Дождись нескольких фраз.")
            };
            if(!live)VoiceStatus.Text=T("Последние измерения · ")+VoiceStatus.Text;
            VoiceStatus.ToolTip=T("Последний фрагмент: {0:F0} с от начала звонка. Оценки могут меняться из-за настроек звука.",reading.Seconds);
        }
        if(VoiceEnabledBox.IsChecked!=true && reading is null && !scripted)VoiceStatus.Text=T("Анализ голоса выключен. Включи его во вкладке «Звук и запись».");
        if(voiceProblem.Length>0)
        {
            VoiceStatus.Text=voiceProblem switch
            {
                "off"=>T("Анализ голоса выключен во вкладке «Звук и запись»."),
                "save_failed"=>T("Не удалось сохранить журнал голоса. На экране измерения продолжаются."),
                "backlog"=>T("Анализ голоса не успевает. Один фрагмент пропущен."),
                _=>T("Анализ голоса недоступен. Расшифровка и запись продолжаются.")
            };
            if(voiceProblem!="save_failed")VoicePitchText.Text=VoiceLevelText.Text=VoicePaceText.Text=VoicePauseText.Text="—";
        }
        RenderVoicePresentation();
    }
    static string VoiceValue(string value)=>value switch
    {
        "higher"=>T("Выше обычного"),"lower"=>T("Ниже обычного"),
        "louder"=>T("Громче обычного"),"quieter"=>T("Тише обычного"),
        "faster"=>T("Быстрее обычного"),"slower"=>T("Медленнее обычного"),
        "longer"=>T("Длиннее обычного"),"shorter"=>T("Короче обычного"),
        "usual"=>T("Как обычно"),"checking"=>T("Проверяем…"),"collecting"=>T("Изучаем голос…"),
        "insufficient"=>T("Мало данных"),_=>T("Нет оценки")
    };
}

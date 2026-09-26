using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ArtoSalesCopilot;

public sealed record VoiceFeatures
{
    public double Duration { get; init; }
    public double SpeechSeconds { get; init; }
    public double LevelDb { get; init; }
    public double? PitchHz { get; init; }
    public double? PitchRangeSemitones { get; init; }
    public double VoicedFraction { get; init; }
    public double ClippingFraction { get; init; }
    public double? SnrDb { get; init; }
    public double[] Pauses { get; init; } = [];
    public string Quality { get; init; } = "insufficient";
    public bool Valid => double.IsFinite(Duration) && Duration is > 0 and <= 14 &&
        double.IsFinite(SpeechSeconds) && SpeechSeconds >= 0 && SpeechSeconds <= Duration &&
        double.IsFinite(LevelDb) && LevelDb is >= -120 and <= 20 &&
        (PitchHz is null || double.IsFinite(PitchHz.Value) && PitchHz is >= 60 and <= 500) &&
        (PitchRangeSemitones is null || double.IsFinite(PitchRangeSemitones.Value) && PitchRangeSemitones >= 0) &&
        double.IsFinite(VoicedFraction) && VoicedFraction is >= 0 and <= 1 &&
        double.IsFinite(ClippingFraction) && ClippingFraction is >= 0 and <= 1 &&
        (SnrDb is null || double.IsFinite(SnrDb.Value)) && Pauses is not null &&
        Pauses.Length <= 70 && Pauses.All(p => double.IsFinite(p) && p is >= .2 and <= 14) &&
        (Quality != "ok" || SpeechSeconds >= .65);
}

public sealed record VoiceReading(string State, double CollectedSeconds, string Pitch, string Level,
    string Pace, string Pauses, double Seconds, int Profile, VoiceFeatures? Features = null);

/// <summary>Session-only reference for ONE channel/speaker. Rules are experimental, not emotion scores.</summary>
internal sealed class VoiceBaseline
{
    readonly List<VoiceFeatures> reference = [];
    readonly List<double> paceReference = [];
    readonly List<double> pitchReference = [], pauseReference = [];
    readonly Dictionary<string,(string Direction,int Count)> streaks = [];
    double seconds, paceSeconds;
    string pace = "collecting";
    public bool Ready => reference.Count >= 3 && seconds >= 12;
    public void Reset() { reference.Clear(); paceReference.Clear(); pitchReference.Clear(); pauseReference.Clear(); streaks.Clear(); seconds=paceSeconds=0; pace="collecting"; }
    public VoiceReading Add(VoiceFeatures f, double at, int profile)
    {
        if (!f.Valid) throw new InvalidDataException("Invalid acoustic measurement.");
        if (f.Quality != "ok") {
            streaks.Clear();pace="unavailable";
            return new(f.Quality,seconds,"unavailable","unavailable","unavailable","unavailable",at,profile,f);
        }
        var pitchWasReady=pitchReference.Count>=3;
        if(!pitchWasReady && f.PitchHz is not null)pitchReference.Add(12*Math.Log2(f.PitchHz.Value));
        var pausesWereReady=pauseReference.Count>=3;
        if(!pausesWereReady)pauseReference.AddRange(f.Pauses.Take(20-pauseReference.Count));
        if (!Ready) {
            reference.Add(f); seconds+=f.SpeechSeconds;
            return new(Ready?"ready":"calibrating",seconds,"collecting","collecting",pace,"collecting",at,profile,f);
        }
        // Fixed reference until explicit reset: a sustained change cannot redefine itself as normal.
        var levels=reference.Select(x=>x.LevelDb).ToArray();
        if(f.PitchHz is null)streaks.Remove("pitch");
        var pauses=pauseReference.ToArray();
        return new("ready",seconds,
            f.PitchHz is null?"insufficient":!pitchWasReady?"collecting":Trend("pitch",12*Math.Log2(f.PitchHz.Value),pitchReference.ToArray(),2,"higher","lower"),
            Trend("level",f.LevelDb,levels,6,"louder","quieter"),pace,
            pausesWereReady && f.Pauses.Length>0
                ? Trend("pause",Median(f.Pauses),pauses,Math.Max(.25,Median(pauses)*.5),"longer","shorter")
                : NoPauses(at), at,profile,f);
    }
    string NoPauses(double _) { streaks.Remove("pause"); return "insufficient"; }
    public string AddText(string text,double duration)
    {
        var words=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length;
        if(duration<1 || duration>14 || words<3)return pace;
        var rate=words*60/duration;
        if(rate is < 20 or > 400)return pace;
        if(paceReference.Count<3 || paceSeconds<12){paceReference.Add(rate);paceSeconds+=duration;return pace="collecting";}
        return pace=Trend("pace",rate,paceReference.ToArray(),Math.Max(30,Median(paceReference)*.3),"faster","slower");
    }
    string Trend(string key,double value,double[] baseline,double floor,string up,string down)
    {
        var median=Median(baseline);var mad=Median(baseline.Select(v=>Math.Abs(v-median)));
        var threshold=Math.Max(floor,3*1.4826*mad);
        var direction=value-median>threshold?up:median-value>threshold?down:"usual";
        var old=streaks.GetValueOrDefault(key);var count=old.Direction==direction?old.Count+1:1;
        streaks[key]=(direction,count);
        return direction=="usual"?"usual":count>=2?direction:"checking";
    }
    static double Median(IEnumerable<double> values){var a=values.Order().ToArray();return a.Length==0?0:(a[(a.Length-1)/2]+a[a.Length/2])/2;}
}

/// <summary>Independent CPU worker; bounded queue, private temporary copies, no network.</summary>
internal sealed class LiveVoice : IDisposable
{
    sealed record Job(AudioChunk Chunk,int Profile);
    readonly Channel<Job> queue=Channel.CreateBounded<Job>(new BoundedChannelOptions(4){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    readonly CancellationTokenSource stop=new(); readonly object sync=new();
    readonly string folder=Path.Combine(Store.Root,"voice-temp",Guid.NewGuid().ToString("N"));
    readonly Action<string,VoiceFeatures,double,int> deliver;
    readonly Action<string> failed;
    Process? process; Task completion=Task.CompletedTask; bool disposed,running;
    int clientProfile=1;
    int discardClientChunk;
    public Task Completion=>completion;
    public int Profile(string who)=>who=="rep"?1:Volatile.Read(ref clientProfile);
    public int StampProfile(string who)=>who=="client" && Interlocked.Exchange(ref discardClientChunk,0)==1?0:Profile(who);
    public int ResetClient(){Interlocked.Exchange(ref discardClientChunk,1);return Interlocked.Increment(ref clientProfile);}
    public LiveVoice(Action<string,VoiceFeatures,double,int> deliver,Action<string> failed){this.deliver=deliver;this.failed=failed;}
    public async Task Start(string python,CancellationToken ct)
    {
        if(!File.Exists(python))throw new FileNotFoundException("Local voice runtime unavailable.");
        var psi=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8};
        psi.ArgumentList.Add("-u");psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"prosody_worker.py"));
        psi.Environment["PYTHONIOENCODING"]="utf-8";psi.Environment["HF_HUB_OFFLINE"]="1";psi.Environment["OMP_NUM_THREADS"]="1";
        var p=Process.Start(psi)??throw new InvalidOperationException("Local voice worker could not start.");
        lock(sync){if(disposed){p.Kill();p.Dispose();throw new OperationCanceledException();}process=p;}
        p.ErrorDataReceived+=(_,_)=>{};p.BeginErrorReadLine();
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,stop.Token);
        try {
            var line=await p.StandardOutput.ReadLineAsync(linked.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(25),linked.Token);
            using var d=JsonDocument.Parse(line??"{}");
            if(!d.RootElement.TryGetProperty("ready",out var ready)||!ready.GetBoolean())throw new InvalidOperationException("Local voice model unavailable.");
            lock(sync){if(disposed)throw new OperationCanceledException();Directory.CreateDirectory(folder);running=true;completion=Run(p);}
        }catch{Dispose();throw;}
    }
    public void Enqueue(AudioChunk chunk)
    {
        lock(sync){
            if(disposed||stop.IsCancellationRequested)return;
            var copy=Path.Combine(folder,Guid.NewGuid().ToString("N")+".wav");
            try{File.Copy(chunk.Path,copy);if(!queue.Writer.TryWrite(new(chunk with{Path=copy},chunk.VoiceProfile))){File.Delete(copy);failed("backlog");}}
            catch{TryDelete(copy);failed("copy_failed");}
        }
    }
    async Task Run(Process p)
    {
        try {
            await foreach(var job in queue.Reader.ReadAllAsync(stop.Token).ConfigureAwait(false)){
                try {
                    if(job.Profile!=Profile(job.Chunk.Speaker))continue;
                    await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{path=job.Chunk.Path})).ConfigureAwait(false);
                    await p.StandardInput.FlushAsync(stop.Token).ConfigureAwait(false);
                    var line=await p.StandardOutput.ReadLineAsync(stop.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(15),stop.Token).ConfigureAwait(false);
                    var f=JsonSerializer.Deserialize<VoiceFeatures>(line??"{}",Store.Json);
                    if(f is null||!f.Valid){failed("measurement_failed");continue;}
                    if(!stop.IsCancellationRequested && job.Profile==Profile(job.Chunk.Speaker))deliver(job.Chunk.Speaker,f,job.Chunk.Seconds,job.Profile);
                } finally {TryDelete(job.Chunk.Path);}
            }
        } catch(OperationCanceledException){}catch{if(!stop.IsCancellationRequested)failed("worker_failed");}
        finally{
            Dispose();
            // Windows can keep the current WAV open until the killed worker exits.
            // Wait for its handle release before the final cleanup pass.
            try{await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);}catch{}
            p.Dispose();
            while(queue.Reader.TryRead(out var job))TryDelete(job.Chunk.Path);
            try{foreach(var path in Directory.GetFiles(folder,"*.wav"))TryDelete(path);Directory.Delete(folder);}catch{}
        }
    }
    static void TryDelete(string path){try{File.Delete(path);}catch{}}
    public void Dispose()
    {
        lock(sync){if(disposed)return;disposed=true;stop.Cancel();queue.Writer.TryComplete();try{if(process is not null&&!process.HasExited)process.Kill(entireProcessTree:true);}catch{}if(!running)process?.Dispose();process=null;}
    }
}

internal static class VoiceText
{
    public static string Pick(string language,string en,string ru,string uk)=>language switch{"ru"=>ru,"uk"=>uk,_=>en};
    public static string Term(string key,string l)=>key switch {
        "higher"=>Pick(l,"Higher than usual","Выше обычного","Вище звичного"),"lower"=>Pick(l,"Lower than usual","Ниже обычного","Нижче звичного"),
        "louder"=>Pick(l,"Signal level increased","Уровень звука вырос","Рівень звуку зріс"),"quieter"=>Pick(l,"Signal level decreased","Уровень звука снизился","Рівень звуку знизився"),
        "faster"=>Pick(l,"Faster than usual","Быстрее обычного","Швидше звичного"),"slower"=>Pick(l,"Slower than usual","Медленнее обычного","Повільніше звичного"),
        "longer"=>Pick(l,"Longer within phrases","Длиннее внутри фраз","Довші всередині фраз"),"shorter"=>Pick(l,"Shorter within phrases","Короче внутри фраз","Коротші всередині фраз"),
        "usual"=>Pick(l,"Within usual range","В обычном диапазоне","У звичному діапазоні"),"checking"=>Pick(l,"Checking a change…","Проверяем изменение…","Перевіряємо зміну…"),
        "collecting"=>Pick(l,"Learning this voice…","Изучаем этот голос…","Вивчаємо цей голос…"),
        "insufficient"=>Pick(l,"Not enough samples","Мало данных","Мало даних"),
        _=>Pick(l,"Unavailable","Недоступно","Недоступно")};
    public static string State(VoiceReading r,string l)=>r.State switch{
        "calibrating"=>Pick(l,$"Learning voice · {Math.Min(12,r.CollectedSeconds):F0}/12s of clear speech (at least 3 chunks)",$"Настройка · {Math.Min(12,r.CollectedSeconds):F0}/12 с чистой речи (минимум 3 фрагмента)",$"Налаштування · {Math.Min(12,r.CollectedSeconds):F0}/12 с чистого мовлення (мінімум 3 фрагменти)"),
        "ready"=>Pick(l,"Compared with this voice in this call","Сравнение с этим голосом в текущем звонке","Порівняння з цим голосом у поточному дзвінку"),
        "clipped"=>Pick(l,"Audio clipping · check input level","Перегрузка звука · проверь уровень","Перевантаження звуку · перевір рівень"),
        "noisy"=>Pick(l,"Noisy audio · comparison withheld","Шумная запись · сравнение приостановлено","Шумний запис · порівняння призупинено"),
        "too_quiet"=>Pick(l,"Audio too quiet","Слишком тихая запись","Надто тихий запис"),
        _=>Pick(l,"Not enough reliable speech · no inference","Недостаточно надёжной речи · без выводов","Недостатньо надійного мовлення · без висновків")};
}

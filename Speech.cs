using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ArtoSalesCopilot;

public sealed record DeviceOption(string Id, string Name) { public override string ToString()=>string.IsNullOrEmpty(Id)?UiText.Get(Name):Name; }
public sealed record AudioChunk(string Path, string Speaker, double Seconds, double Duration=0, int VoiceProfile=1);

public sealed class SpeechWorker : IDisposable
{
    Process? process;
    readonly SemaphoreSlim gate = new(1,1);
    public async Task Start(Settings s, CancellationToken ct)
    {
        if(!File.Exists(s.PythonPath)) throw new FileNotFoundException("Local Whisper Python was not found. Check Settings.");
        if(!Directory.Exists(s.ModelCache)) throw new DirectoryNotFoundException("Local Whisper model cache was not found. Check Settings.");
        var psi = new ProcessStartInfo(s.PythonPath) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        psi.ArgumentList.Add("-u"); psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"speech_worker.py")); psi.ArgumentList.Add("--cache"); psi.ArgumentList.Add(s.ModelCache);
        psi.Environment["PYTHONIOENCODING"]="utf-8"; psi.Environment["HF_HUB_OFFLINE"]="1";
        var venv=Directory.GetParent(Path.GetDirectoryName(s.PythonPath)!)!.FullName;
        var nvidia=Path.Combine(venv,"Lib","site-packages","nvidia");
        if(Directory.Exists(nvidia)) { var bins=Directory.GetDirectories(nvidia).Select(d=>Path.Combine(d,"bin")).Where(Directory.Exists); psi.Environment["PATH"]=string.Join(";",bins)+";"+Environment.GetEnvironmentVariable("PATH"); }
        process=Process.Start(psi) ?? throw new InvalidOperationException("Could not start local speech worker.");
        process.ErrorDataReceived += (_,_) => { /* Drain; do not log private paths/transcripts. */ }; process.BeginErrorReadLine();
        try {
            var first=await process.StandardOutput.ReadLineAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(90),ct);
            using var status=JsonDocument.Parse(first??"{}");
            if(!status.RootElement.TryGetProperty("ready",out var ready) || !ready.GetBoolean()) throw new InvalidOperationException("Local Whisper failed to initialize. Check model path and available GPU memory.");
        } catch { Dispose(); throw; }
    }
    public async Task<string> Transcribe(string path,string language,string hotwords,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try {
            var p=process ?? throw new InvalidOperationException("Speech worker is not ready.");
            await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new {path,language,hotwords}).AsMemory(),ct); await p.StandardInput.FlushAsync(ct);
            var line=await p.StandardOutput.ReadLineAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(45),ct);
            using var result=JsonDocument.Parse(line??"{}");
            if(result.RootElement.TryGetProperty("error",out _)) throw new InvalidOperationException("Local transcription failed. Stop and check the speech settings.");
            return result.RootElement.GetProperty("text").GetString()??"";
        } catch { Dispose(); throw; }
        finally { gate.Release(); }
    }
    public void Dispose() { var p=Interlocked.Exchange(ref process,null); if(p is null)return; try { if(!p.HasExited)p.Kill(entireProcessTree:true); } catch{} p.Dispose(); }
}

public sealed class DualCapture : IDisposable
{
    readonly List<CaptureChannel> channels=[];
    public static List<DeviceOption> Devices(DataFlow flow)
    {
        using var e=new MMDeviceEnumerator(); var list=new List<DeviceOption>();
        foreach(var d in e.EnumerateAudioEndPoints(flow,DeviceState.Active)) { using(d) list.Add(new(d.ID,d.FriendlyName)); }
        return list;
    }
    public void Start(Settings settings,string folder,Action<AudioChunk> deliver,Action<string,float> meter,Action<string> failed,string? recordingFolder=null,Action<string>? recordingStarted=null)
    {
        Directory.CreateDirectory(folder);
        try {
            using var devices=new MMDeviceEnumerator();
            var mic=string.IsNullOrEmpty(settings.Microphone)?devices.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Communications):devices.GetDevice(settings.Microphone);
            var output=string.IsNullOrEmpty(settings.Output)?devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia):devices.GetDevice(settings.Output);
            channels.Add(new(new WasapiCapture(mic),mic,"rep",folder,deliver,meter,failed,recordingFolder,recordingStarted));
            channels.Add(new(new WasapiLoopbackCapture(output),output,"client",folder,deliver,meter,failed,recordingFolder,recordingStarted));
            foreach(var ch in channels) ch.Start();
        } catch { Dispose(); throw; }
    }
    public void Dispose() { foreach(var c in channels)c.Dispose(); channels.Clear(); }
}

sealed class CaptureChannel : IDisposable
{
    readonly WasapiCapture capture; readonly MMDevice device; readonly string speaker,folder;
    readonly Action<AudioChunk> deliver; readonly Action<string,float> meter; readonly Action<string> failed;
    readonly object sync=new(); readonly Stopwatch clock=Stopwatch.StartNew();
    MemoryStream bytes=new(); System.Threading.Timer? timer; bool stopped; bool speech; double firstTime,lastLoud,lastMeter;
    TimelineWaveWriter? recording;readonly Stopwatch recordingClock=new();readonly string? recordingFolder;readonly Action<string>? recordingStarted;
    public CaptureChannel(WasapiCapture c,MMDevice d,string who,string dir,Action<AudioChunk> callback,Action<string,float> level,Action<string> fail,string? archive=null,Action<string>? started=null)
    {capture=c;device=d;speaker=who;folder=dir;deliver=callback;meter=level;failed=fail;recordingFolder=archive;recordingStarted=started;}
    public void Start()
    {
        capture.DataAvailable+=OnData;
        capture.RecordingStopped+=(_,e)=> { if(!stopped)failed(e.Exception is null?"Audio device stopped unexpectedly. Select devices again.":"Audio device error. Stop and reselect your devices."); };
        if(recordingFolder is not null){recording=new TimelineWaveWriter(Path.Combine(recordingFolder,speaker=="rep"?"microphone.wav":"computer.wav"),capture.WaveFormat);recordingStarted?.Invoke(speaker);recordingClock.Start();}
        capture.StartRecording(); timer=new(_=>Tick(),null,250,250);
    }
    void OnData(object? sender,WaveInEventArgs e)
    {
        try {
            var now=clock.Elapsed.TotalSeconds; var peak=Peak(e.Buffer,e.BytesRecorded,capture.WaveFormat);
            lock(sync) {
                if(stopped)return;
                recording?.Append(e.Buffer,e.BytesRecorded,recordingClock.Elapsed.TotalSeconds-(double)e.BytesRecorded/capture.WaveFormat.AverageBytesPerSecond);
                if(now-lastMeter>.1){lastMeter=now;meter(speaker,peak);}
                if(peak>.009f){speech=true;lastLoud=now;}
                if(bytes.Length==0)firstTime=now;
                // Keep a small silent lead-in, but bound silent buffers as well.
                bytes.Write(e.Buffer,0,e.BytesRecorded);
                if(!speech && bytes.Length>capture.WaveFormat.AverageBytesPerSecond/3) {bytes.SetLength(0);firstTime=now;}
                if(bytes.Length>capture.WaveFormat.AverageBytesPerSecond*12) Flush();
            }
        } catch { failed("Audio capture failed. Stop and check the selected devices."); }
    }
    void Tick()
    {
        try { lock(sync) {if(stopped||!speech)return;var now=clock.Elapsed.TotalSeconds;var duration=(double)bytes.Length/capture.WaveFormat.AverageBytesPerSecond;
            if(duration>=.45 && (now-lastLoud>.75 || duration>=8))Flush();} }
        catch {failed("Could not buffer audio. Check free disk space.");}
    }
    void Flush()
    {
        if(speech && bytes.Length>capture.WaveFormat.AverageBytesPerSecond/5)
        {
            var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".wav");
            using(var w=new WaveFileWriter(path,capture.WaveFormat)) { var raw=bytes.ToArray(); w.Write(raw,0,raw.Length); }
            deliver(new(path,speaker,firstTime,(double)bytes.Length/capture.WaveFormat.AverageBytesPerSecond));
        }
        bytes.SetLength(0);speech=false;firstTime=clock.Elapsed.TotalSeconds;
    }
    public static float Peak(byte[] data,int count,WaveFormat f)
    {
        bool floating=f.Encoding==WaveFormatEncoding.IeeeFloat || f is WaveFormatExtensible ext && ext.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71");
        float peak=0;int stride=f.BitsPerSample/8;
        for(int i=0;stride>0&&i+stride<=count;i+=stride)
        {
            float value=0;
            if(floating&&stride==4)value=BitConverter.ToSingle(data,i);
            else if(stride==2)value=BitConverter.ToInt16(data,i)/32768f;
            else if(stride==3){int v=data[i]|data[i+1]<<8|data[i+2]<<16;if((v&0x800000)!=0)v|=unchecked((int)0xff000000);value=v/8388608f;}
            else if(stride==4)value=BitConverter.ToInt32(data,i)/2147483648f;
            if(float.IsFinite(value))peak=Math.Max(peak,Math.Abs(value));
        }
        return Math.Clamp(peak,0,1);
    }
    public void Dispose()
    {
        lock(sync){if(stopped)return;stopped=true;timer?.Dispose();bytes.Dispose();try{recording?.Pad(recordingClock.Elapsed.TotalSeconds);recording?.Dispose();}catch{try{recording?.Dispose();}catch{}}finally{recording=null;}}
        capture.DataAvailable-=OnData;
        try{capture.StopRecording();}catch{}
        capture.Dispose();device.Dispose();
    }
}

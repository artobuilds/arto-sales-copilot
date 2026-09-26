using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Globalization;
using NAudio.Wave;

namespace ArtoSalesCopilot;

public sealed record VideoSource(string Label,string Kind,long Handle=0,int X=0,int Y=0,int Width=0,int Height=0)
{
    public override string ToString()=>Kind switch
    {
        "none"=>UiText.Get("Без видео · только звук и текст"),
        "desktop"=>UiText.Get("Весь рабочий стол · все экраны"),
        "window"=>UiText.Get("Окно · {0}",Label.StartsWith("Окно · ")?Label[7..]:Label),
        "screen"=>UiText.Get("Экран {0} · {1} × {2}",Label.Split(' ')[1],Width,Height),
        _=>Label
    };
}
internal static class VideoSources
{
    delegate bool EnumWindow(IntPtr hwnd,IntPtr state);
    delegate bool EnumMonitor(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr state);
    [StructLayout(LayoutKind.Sequential)]internal struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindow cb,IntPtr state);
    [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,EnumMonitor cb,IntPtr state);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")]internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int size);
    public static List<VideoSource> List()
    {
        var list=new List<VideoSource>{new("Без видео · только звук и текст","none"),new("Весь рабочий стол · все экраны","desktop")};
        int n=0;EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr h,IntPtr dc,ref Rect r,IntPtr s)=>{list.Add(new($"Экран {++n} · {r.Right-r.Left} × {r.Bottom-r.Top}","screen",0,r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top));return true;},IntPtr.Zero);
        EnumWindows((h,s)=>{if(!IsWindowVisible(h)||IsIconic(h))return true;var title=new StringBuilder(512);GetWindowText(h,title,title.Capacity);if(title.Length>0)list.Add(new("Окно · "+title,"window",h.ToInt64()));return true;},IntPtr.Zero);
        return list;
    }
}
internal sealed class VideoRecorder
{
    Process? process;bool stopping;
    public static List<string> Arguments(VideoSource source,string path)
    {
        if(source.Kind is not ("window" or "screen" or "desktop"))throw new ArgumentException("Choose a video source.");
        var args=new List<string>{"-hide_banner","-loglevel","error","-n","-f","gdigrab","-framerate","15","-draw_mouse","1"};
        if(source.Kind=="screen")args.AddRange(["-offset_x",source.X.ToString(),"-offset_y",source.Y.ToString(),"-video_size",$"{source.Width}x{source.Height}"]);
        args.AddRange(["-i",source.Kind=="window"?"hwnd="+source.Handle:"desktop","-vf","pad=ceil(iw/2)*2:ceil(ih/2)*2","-c:v","libx264","-preset","ultrafast","-crf","24","-pix_fmt","yuv420p",path]);return args;
    }
    public async Task Start(string ffmpeg,VideoSource source,string path,Action<string> failed,CancellationToken ct)
    {
        if(!File.Exists(ffmpeg))throw new FileNotFoundException("FFmpeg not found. Choose its executable in Settings.");
        if(source.Kind=="window"&&(!VideoSources.IsWindow((IntPtr)source.Handle)||VideoSources.IsIconic((IntPtr)source.Handle)))throw new InvalidOperationException("Selected window is closed or minimized. Refresh video sources.");
        var psi=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true};
        foreach(var arg in Arguments(source,path))psi.ArgumentList.Add(arg);
        process=Process.Start(psi)??throw new InvalidOperationException("Video recorder could not start.");
        process.ErrorDataReceived+=(_,_)=>{};process.BeginErrorReadLine();
        process.EnableRaisingEvents=true;process.Exited+=(_,_)=>{if(!stopping)failed("Video recording stopped unexpectedly. The session was stopped; existing files are retained.");};
        await Task.Delay(800,ct);if(process.HasExited)throw new InvalidOperationException("Video source could not be recorded. Restore the window or select a screen.");
    }
    public async Task<bool> Stop()
    {
        stopping=true;var p=Interlocked.Exchange(ref process,null);if(p is null)return true;
        try{if(!p.HasExited){await p.StandardInput.WriteLineAsync("q");await p.StandardInput.FlushAsync();await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}return p.ExitCode==0;}
        catch{try{if(!p.HasExited)p.Kill();}catch{}return false;}finally{p.Dispose();}
    }
}
internal sealed class SessionRecording
{
    public string Folder{get;}
    readonly DateTimeOffset started=DateTimeOffset.UtcNow;
    readonly Dictionary<string,double> offsets=[];
    public SessionRecording(Brief brief,string language,string? root=null)
    {
        Folder=Path.Combine(root??Path.GetFullPath(Path.Combine(Store.Root,"..","..","sessions")),DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"brief.json"),JsonSerializer.Serialize(brief,Store.Json));
        File.WriteAllText(Path.Combine(Folder,"session.json"),JsonSerializer.Serialize(new{started,language,state="recording",audio="separate original device streams; see audio-timing.jsonl for start offsets",video="local only; not sent to AI"},Store.Json));
    }
    public void Turn(Utterance t){File.AppendAllText(Path.Combine(Folder,"transcript.jsonl"),JsonSerializer.Serialize(t)+Environment.NewLine);File.AppendAllText(Path.Combine(Folder,"conversation.md"),$"\n**{t.Speaker} · {t.Seconds:F1}s**\n\n{t.Text}\n");}
    public void Advice(Advice a,string? generated=null)=>File.AppendAllText(Path.Combine(Folder,"advice.jsonl"),JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,move=a.Move,say=generated??a.Say,generated=generated is not null,source=generated is null?a.Source:"OpenAI checked by Jev"})+Environment.NewLine);
    public void Voice(string speaker,VoiceReading reading)=>File.AppendAllText(Path.Combine(Folder,"voice-observations.jsonl"),JsonSerializer.Serialize(new{speaker,reading,source="local acoustic measurements; experimental thresholds; not emotion recognition"})+Environment.NewLine);
    public void AudioStart(string speaker){var offset=(DateTimeOffset.UtcNow-started).TotalSeconds;offsets[speaker]=offset;File.AppendAllText(Path.Combine(Folder,"audio-timing.jsonl"),JsonSerializer.Serialize(new{speaker,startOffsetSeconds=offset})+Environment.NewLine);}
    public void Finish(string state)=>File.WriteAllText(Path.Combine(Folder,"session.json"),JsonSerializer.Serialize(new{started,ended=DateTimeOffset.UtcNow,state},Store.Json));
    public async Task<bool> Mux(string ffmpeg)
    {
        var screen=Path.Combine(Folder,"screen.mkv");var mic=Path.Combine(Folder,"microphone.wav");var remote=Path.Combine(Folder,"computer.wav");
        if(!File.Exists(screen)||!File.Exists(mic)||!File.Exists(remote))return true;
        var psi=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        var args=new[]{"-hide_banner","-loglevel","error","-n","-i",screen,"-itsoffset",offsets.GetValueOrDefault("rep").ToString("F3",CultureInfo.InvariantCulture),"-i",mic,"-itsoffset",offsets.GetValueOrDefault("client").ToString("F3",CultureInfo.InvariantCulture),"-i",remote,"-filter_complex","[1:a]aresample=async=1:first_pts=0[a];[2:a]aresample=async=1:first_pts=0[b];[a][b]amix=inputs=2:duration=longest:normalize=1[mix]","-map","0:v","-map","[mix]","-map","1:a","-map","2:a","-c:v","copy","-c:a","aac","-b:a","128k","-metadata:s:a:0","title=Call mix","-metadata:s:a:1","title=Artur microphone","-metadata:s:a:2","title=Computer audio","-disposition:a:0","default",Path.Combine(Folder,"call.mkv")};
        foreach(var arg in args)psi.ArgumentList.Add(arg);
        using var p=Process.Start(psi);if(p is null)return false;p.ErrorDataReceived+=(_,_)=>{};p.BeginErrorReadLine();
        try{await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));return p.ExitCode==0;}catch{try{p.Kill();}catch{}return false;}
    }
}
internal sealed class TimelineWaveWriter : IDisposable
{
    readonly WaveFileWriter writer;readonly WaveFormat format;readonly byte[] silence=new byte[8192];
    public TimelineWaveWriter(string path,WaveFormat format){this.format=format;writer=new(path,format);}
    public void Append(byte[] buffer,int count,double startSeconds){Pad(startSeconds);writer.Write(buffer,0,count);}
    public void Pad(double seconds){long target=(long)(Math.Max(0,seconds)*format.AverageBytesPerSecond);target-=target%format.BlockAlign;while(target-writer.Length>=format.BlockAlign){var n=(int)Math.Min(silence.Length-silence.Length%format.BlockAlign,target-writer.Length);writer.Write(silence,0,n);}}
    public void Dispose()=>writer.Dispose();
}

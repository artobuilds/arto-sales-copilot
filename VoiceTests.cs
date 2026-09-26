using System.IO;
using System.Text.Json;
using NAudio.Wave;

namespace ArtoSalesCopilot;

internal static class VoiceTests
{
    internal static VoiceFeatures Sample(double pitch=140,double level=-20,string quality="ok")=>new(){Duration=4,SpeechSeconds=4,PitchHz=pitch,LevelDb=level,VoicedFraction=.8,Pauses=[.3],Quality=quality};
    public static void Baselines(List<string> log)
    {
        static void Assert(bool ok){if(!ok)throw new Exception("Assertion failed");}
        void Test(string name,Action action){try{action();log.Add("PASS "+name);}catch(Exception e){log.Add("FAIL "+name+": "+e.Message);}}
        static VoiceBaseline Ready(double hz){var b=new VoiceBaseline();for(int i=0;i<3;i++)b.Add(Sample(hz),i*4,1);return b;}
        Test("Voice reference is per person and needs 12 seconds plus 3 chunks",()=>{
            var a=Ready(100);var b=Ready(240);Assert(a.Ready&&b.Ready);
            Assert(a.Add(Sample(100),16,1).Pitch=="usual"&&b.Add(Sample(240),16,1).Pitch=="usual");
            var shortVoice=new VoiceBaseline();shortVoice.Add(Sample() with{Duration=1,SpeechSeconds=1},0,1);Assert(!shortVoice.Ready);
        });
        Test("One pitch/level excursion is withheld; repeated changes are reported",()=>{
            var b=Ready(140);var first=b.Add(Sample(220,-10),12,1);var second=b.Add(Sample(220,-10),16,1);
            Assert(first.Pitch=="checking"&&first.Level=="checking"&&second.Pitch=="higher"&&second.Level=="louder");
            Assert(b.Add(Sample(),20,1).Pitch=="usual");
        });
        Test("Bad audio does not calibrate or keep an active change",()=>{
            var b=new VoiceBaseline();for(int i=0;i<4;i++)b.Add(Sample(140,-20,"clipped"),i*4,1);Assert(!b.Ready);
            b=Ready(140);b.Add(Sample(220),12,1);Assert(b.Add(Sample(220,-20,"noisy"),16,1).Pitch=="unavailable");
            Assert(b.Add(Sample(220),20,1).Pitch=="checking");
        });
        Test("Unreliable pitch does not suppress valid level; pitch learns later",()=>{
            var b=new VoiceBaseline();var noPitch=Sample() with{PitchHz=null};for(int i=0;i<3;i++)b.Add(noPitch,i*4,1);
            var r=b.Add(noPitch,12,1);Assert(r.Level=="usual"&&r.Pitch=="insufficient");
            for(int i=0;i<3;i++)b.Add(Sample(240),16+i*4,1);
            Assert(b.Add(Sample(240),28,1).Pitch=="usual");
        });
        Test("New speaker reset discards old baseline and pending trends",()=>{
            var b=Ready(100);b.Add(Sample(220),12,1);b.Reset();Assert(!b.Ready);
            for(int i=0;i<3;i++)b.Add(Sample(220),i*4,2);Assert(b.Add(Sample(220),16,2).Pitch=="usual");
        });
        Test("Voice pace is approximate text-derived and needs repeated change",()=>{
            var b=Ready(140);var text=string.Join(' ',Enumerable.Repeat("word",8));for(int i=0;i<3;i++)b.AddText(text,4);
            Assert(b.AddText(text,4)=="usual");Assert(b.AddText(text+" "+text,4)=="checking");Assert(b.AddText(text+" "+text,4)=="faster");
        });
        Test("Voice measurements reject invalid numeric values",()=>{
            Assert(!(Sample() with{PitchHz=double.NaN}).Valid);Assert(!(Sample() with{Pauses=[double.PositiveInfinity]}).Valid);
            Assert(!(Sample() with{SpeechSeconds=10}).Valid);
        });
        Test("Voice output translations cover EN RU UK without emotion inference",()=>{
            foreach(var l in new[]{"en","ru","uk"})foreach(var key in new[]{"higher","lower","louder","quieter","faster","slower","longer","shorter","usual","checking","collecting","insufficient"})Assert(!string.IsNullOrEmpty(VoiceText.Term(key,l)));
        });
        Test("Voice profile generation rejects pending pre-reset and mixed chunks",()=>{
            using var w=new LiveVoice((_,_,_,_)=>{},_=>{});Assert(w.StampProfile("client")==1);
            Assert(w.ResetClient()==2&&w.Profile("rep")==1);Assert(w.StampProfile("client")==0&&w.StampProfile("client")==2);
        });
        Test("Saved voice observations include profile and timestamp, not a voice identity",()=>{
            var s=new SessionRecording(new Brief{Title="Voice fixture"},"en",Path.GetFullPath("test-output/voice-sessions"));
            var b=Ready(140);s.Voice("client",b.Add(Sample(),12,2));s.Finish("test");
            using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(s.Folder,"voice-observations.jsonl")));
            Assert(d.RootElement.GetProperty("reading").GetProperty("Profile").GetInt32()==2);
        });
    }
    public static async Task<bool> Integration()
    {
        Directory.CreateDirectory("test-output");var result=new TaskCompletionSource<VoiceFeatures>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var oldRoot=Store.Root;Store.Root=Path.GetFullPath(Path.Combine("test-output/voice-worker-storage",Guid.NewGuid().ToString("N")));
        using var w=new LiveVoice((_,f,_,_)=>result.TrySetResult(f),reason=>result.TrySetException(new Exception(reason)));
        try {
            // Only the previously generated English fixture, never microphone/client recordings.
            var path=Path.GetFullPath("test-output/voice-fixture-8s.wav");
            using(var r=new WaveFileReader("test-output/demo-english.wav"))using(var writer=new WaveFileWriter(path,r.WaveFormat)){
                var bytes=new byte[Math.Min((int)r.Length,r.WaveFormat.AverageBytesPerSecond*8)];var n=r.Read(bytes,0,bytes.Length);writer.Write(bytes,0,n);
            }
            await w.Start(new Settings().PythonPath,ct.Token);w.Enqueue(new(path,"client",0,8,1));
            var measured=await result.Task.WaitAsync(ct.Token);w.Dispose();await w.Completion;
            if(!measured.Valid||measured.Quality!="ok"||measured.SpeechSeconds<1)throw new Exception("Synthetic speech was not measurable: "+measured.Quality);
            using(var cancelled=new LiveVoice((_,_,_,_)=>{},_=>{})){
                await cancelled.Start(new Settings().PythonPath,ct.Token);
                for(int i=0;i<4;i++)cancelled.Enqueue(new(path,"client",i*8,8,1));
                cancelled.Dispose();await cancelled.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            }
            if(Directory.EnumerateFiles(Store.Root,"*.wav",SearchOption.AllDirectories).Any())throw new Exception("Temporary voice audio remained after Stop");
            File.WriteAllText("test-output/voice-worker-check.json",JsonSerializer.Serialize(new{pass=true,measured,cancelledQueueCleaned=true,scope="local IPC and synthetic English speech only; not a real client acceptance"},Store.Json));return true;
        }catch(Exception ex){File.WriteAllText("test-output/voice-worker-check.json",JsonSerializer.Serialize(new{pass=false,error=ex.Message},Store.Json));return false;}
        finally{Store.Root=oldRoot;}
    }
}

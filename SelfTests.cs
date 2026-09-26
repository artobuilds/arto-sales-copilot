using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace ArtoSalesCopilot;

static class SelfTests
{
    public static async Task<string> Run()
    {
        var log=new List<string>();
        void Test(string name,Action check){try{check();log.Add("PASS "+name);}catch(Exception e){log.Add("FAIL "+name+": "+e.Message);}}
        static void Assert(bool ok,string why="Assertion failed"){if(!ok)throw new Exception(why);}
        Test("Brief validation rejects excessive custom checks",()=>{try{new Brief{CustomChecks=Enumerable.Repeat("Q?",13).ToArray()}.Validate();throw new Exception("Not rejected");}catch(InvalidDataException){}});
        Test("All moves have English, Russian and Ukrainian wording",()=>Assert(Coach.Moves.All(m=>new[]{m.En,m.Ru,m.Uk}.All(x=>!string.IsNullOrWhiteSpace(x)))&&Coach.Moves.Select(m=>m.Id).Distinct().Count()==Coach.Moves.Length));
        Test("All demo states resolve to real moves in three languages",()=>{foreach(var l in new[]{"en","ru","uk"})for(int i=0;i<Demo.Turns.Length;i++){Assert(Demo.Advice(i,l).Source.Contains("SCRIPTED"));Assert(!string.IsNullOrWhiteSpace(Demo.Turn(i,l).Text));}});
        Test("Bounded context retains latest turn",()=>{var t=Enumerable.Range(0,200).Select(i=>new Utterance("client",new string('a',1000)+i,i)).ToArray();var w=Coach.Window(t);Assert(w.Length<100&&w[^1].Text.EndsWith("199")&&w.Sum(x=>x.Text.Length)<=30000);});
        Test("TypeSafe request wire schema and custom checks",()=>{var b=new Brief{Profile="Fictional seller profile",CustomChecks=["Has the client confirmed user roles?"]};using var j=JsonDocument.Parse(JsonSerializer.Serialize(Coach.BuildRequest(b,[Demo.Turn(0,"en")],"en"),Store.Json));Assert(j.RootElement.GetProperty("model").GetString()==Coach.Model);Assert(j.RootElement.GetProperty("questions").GetProperty("custom_0").GetProperty("type").GetString()=="noul");Assert(j.RootElement.GetProperty("state").GetProperty("seller_profile").GetString()==b.Profile);});
        Test("Float audio meter",()=>{var values=new[]{0f,-.5f,.25f};var data=new byte[12];Buffer.BlockCopy(values,0,data,0,12);Assert(Math.Abs(CaptureChannel.Peak(data,12,WaveFormat.CreateIeeeFloatWaveFormat(48000,1))-.5f)<.001);});
        Test("PCM16 audio meter",()=>Assert(CaptureChannel.Peak(BitConverter.GetBytes((short)-16384),2,new WaveFormat(16000,16,1))==.5f));
        Test("Archived audio preserves silence gaps and final WAV duration",()=>{Directory.CreateDirectory("test-output");var path="test-output/timeline.wav";using(var w=new TimelineWaveWriter(path,new WaveFormat(16000,16,1))){w.Append(new byte[3200],3200,.2);w.Append(new byte[3200],3200,.7);w.Pad(1);}using var r=new WaveFileReader(path);Assert(Math.Abs(r.TotalTime.TotalSeconds-1)<.001);});
        Test("Video targets explicit HWND or screen region without shell interpolation",()=>{var a=VideoRecorder.Arguments(new("Fixture","window",123),"file name.mkv");Assert(a.Contains("hwnd=123")&&a[^1]=="file name.mkv"&&!a.Contains("desktop"));var b=VideoRecorder.Arguments(new("Left screen","screen",0,-1920,0,1920,1080),"out.mkv");Assert(b.Contains("-1920")&&b.Contains("1920x1080"));});
        Test("Session persists brief transcript and advice without keys",()=>{var s=new SessionRecording(new Brief{Title="Test"},"en",Path.GetFullPath("test-output/session-test"));s.Turn(new("client","A fictional problem",1));s.Advice(Demo.Advice(0,"en"));s.Finish("test");Assert(File.ReadAllText(Path.Combine(s.Folder,"conversation.md")).Contains("A fictional problem"));Assert(!Directory.GetFiles(s.Folder).Any(p=>p.EndsWith(".key")));});
        Test("DPAPI round trip and no plaintext key at rest",()=>{var old=Store.Root;try{Store.Root=Path.GetFullPath("test-output/key-test");Store.SetKey("typesafe","test-only-not-a-real-key");Assert(Store.GetKey("typesafe")=="test-only-not-a-real-key");Assert(!File.ReadAllText(Path.Combine(Store.Root,"typesafe.key")).Contains("test-only"));Store.SetKey("typesafe","");Assert(Store.GetKey("typesafe")=="");}finally{Store.Root=old;}});
        Test("Brief local save and reload",()=>{var old=Store.Root;try{Store.Root=Path.GetFullPath("test-output/settings-test");Store.Save(new Settings{Language="uk",Brief=new Brief{Title="Тест"}});Assert(Store.Load().Language=="uk"&&Store.Load().Brief.Title=="Тест");}finally{Store.Root=old;}});
        Test("Key-file import reads only the named variable",()=>{Directory.CreateDirectory("test-output");var path="test-output/fake-key.txt";File.WriteAllText(path,"# example\nUNRELATED=not-the-key\nTYPESAFE_API_KEY=fake-key-only-for-tests\n");Assert(MainWindow.ReadTypeSafeFile(path)=="fake-key-only-for-tests");File.WriteAllText(path,"TYPESAFE_API_KEY=\n");try{MainWindow.ReadTypeSafeFile(path);throw new Exception("Empty key accepted");}catch(InvalidDataException){}});
        var response=Response(.9,"discover");
        Test("OpenAI request uses Sol reasoning, bounded context and no stored response",()=>{using var d=JsonDocument.Parse(JsonSerializer.Serialize(Coach.BuildPhrasingRequest(new(),[Demo.Turn(0,"en")],Demo.Advice(0,"en"),"en","gpt-6-sol","low")));var r=d.RootElement;Assert(r.GetProperty("model").GetString()=="gpt-6-sol"&&r.GetProperty("reasoning").GetProperty("effort").GetString()=="low"&&!r.GetProperty("store").GetBoolean());Assert(r.GetProperty("max_output_tokens").GetInt32()==1600);});
        Test("Responses parsing skips reasoning and rejects incomplete/refused output",()=>{using var d=JsonDocument.Parse("{\"status\":\"completed\",\"output\":[{\"type\":\"reasoning\"},{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"What is your priority?\"}]}]}");Assert(Coach.ReadOpenAiText(d.RootElement)=="What is your priority?");using var inc=JsonDocument.Parse("{\"status\":\"incomplete\",\"output\":[]}");Assert(Coach.ReadOpenAiText(inc.RootElement)==null);using var refuse=JsonDocument.Parse("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\"}]}]}");Assert(Coach.ReadOpenAiText(refuse.RootElement)==null);});
        Test("OpenAI key parser rejects ambiguous credentials",()=>{var key="sk-"+new string('a',40);Assert(KeyImport.ParseOpenAi("OPENAI_API_KEY="+key)==key);try{KeyImport.ParseOpenAi(key+"\n"+"sk-"+new string('b',40));throw new Exception("Ambiguous keys accepted");}catch(InvalidDataException){}});
        Test("Public defaults contain no developer profile or machine-specific paths",()=>{var s=new Settings();Assert(s.Brief.Profile.Contains("not supplied")&&!s.Brief.Profile.Contains("Artur"));Assert(s.PythonPath==Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..",".venv","Scripts","python.exe")));Assert(s.ModelCache==Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..",".models"))&&s.FfmpegPath=="");});
        Test("Selected-file key import is isolated and rejects duplicate variables",()=>{var old=Store.Root;try{Store.Root=Path.GetFullPath("test-output/selected-key-import");Directory.CreateDirectory(Store.Root);var p=Path.Combine(Store.Root,"fixture.txt");File.WriteAllText(p,"TYPESAFE_API_KEY=fake-selected-file-key");KeyImport.Import("typesafe",p);Assert(Store.GetKey("typesafe")=="fake-selected-file-key");File.WriteAllText(p,"TYPESAFE_API_KEY=fake-selected-file-key\nTYPESAFE_API_KEY=another-fake-test-key");try{KeyImport.Import("typesafe",p);throw new Exception("Duplicate variables accepted");}catch(InvalidDataException){}Assert(Store.GetKey("typesafe")=="fake-selected-file-key");}finally{Store.Root=old;}});
        Test("Real response contract parses 12 signals",()=>{using var d=JsonDocument.Parse(response);var a=Coach.Parse(d.RootElement,new(),"en");Assert(a.Signals.Count==12&&a.Move=="discover"&&a.InputTokens==123);});
        Test("Low confidence explicitly waits",()=>{using var d=JsonDocument.Parse(Response(.2,"close"));Assert(Coach.Parse(d.RootElement,new(),"en").Move=="wait");});
        Test("Unknown moves rejected",()=>{try{using var d=JsonDocument.Parse(Response(.9,"do_unsafe_thing"));Coach.Parse(d.RootElement,new(),"en");throw new Exception("Not rejected");}catch(InvalidDataException){}});
        try {
            var handler=new MockHandler(response);using var coach=new Coach(handler);var a=await coach.Analyze(new(),[Demo.Turn(0,"en")],"en","fake-test-key",CancellationToken.None);
            Assert(handler.Url=="https://api.typesafe.ai/v1/systemone"&&handler.Auth=="Bearer fake-test-key"&&a.Move=="discover");log.Add("PASS HTTP transport uses fixed HTTPS endpoint and authorization header");
        }catch(Exception ex){log.Add("FAIL HTTP transport: "+ex.Message);}
        try{using var coach=new Coach(new MockHandler("{}",HttpStatusCode.Unauthorized));await coach.Analyze(new(),[],"en","secret-never-print",CancellationToken.None);log.Add("FAIL HTTP401 accepted");}
        catch(Exception ex){log.Add(ex.Message.Contains("401")&&!ex.Message.Contains("secret-never-print")?"PASS HTTP401 surfaced without exposing key":"FAIL HTTP401 handling");}
        try{using var ct=new CancellationTokenSource();ct.Cancel();using var c=new Coach(new CancelHandler());await c.Analyze(new(),[],"en","fake-key",ct.Token);log.Add("FAIL Cancelled request completed");}catch(OperationCanceledException){log.Add("PASS Stop token cancels provider requests");}catch(Exception e){log.Add("FAIL Cancellation: "+e.GetType().Name);}
        try{
            var mock=new PhraseHandler();using var coach=new Coach(mock);var phrase=await coach.Phrase(new(),[Demo.Turn(0,"en")],Demo.Advice(0,"en"),"en","fake-openai","fake-jev",CancellationToken.None);
            Assert(phrase?.Accepted==true&&mock.Calls==2);log.Add("PASS OpenAI generation followed by Jev factual gate");
            var rejected=new PhraseHandler(false);using var coach2=new Coach(rejected);var bad=await coach2.Phrase(new(),[],Demo.Advice(0,"en"),"en","fake-openai","fake-jev",CancellationToken.None);Assert(bad?.Text==null&&bad?.Accepted==false);log.Add("PASS Rejected wording never reaches the coaching card");
        }catch(Exception ex){log.Add("FAIL OpenAI pipeline: "+ex.Message);}
        VoiceTests.Baselines(log);
        return string.Join(Environment.NewLine,log)+Environment.NewLine;
    }
    static string Response(double confidence,string move)
    {
        var answers=Coach.Topics.ToDictionary(t=>t.Id,t=>(object)new{type="noul",noul=.5});answers["move"]=new{type="choice",choice=move,confidence};answers["stage"]=new{type="choice",choice="discovery",confidence=.9};
        return JsonSerializer.Serialize(new{model=Coach.Model,answers,usage=new{input_tokens=123}});
    }
    sealed class MockHandler(string response,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        public string? Url,Auth;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Url=request.RequestUri!.AbsoluteUri;Auth=request.Headers.Authorization?.ToString();return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(response,Encoding.UTF8,"application/json")});}
    }
    sealed class CancelHandler:HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){await Task.Delay(5000,ct);return new(HttpStatusCode.OK);}
    }
    sealed class PhraseHandler(bool grounded=true):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct){
            Calls++;var first=Calls==1;
            if(req.RequestUri!.AbsoluteUri!=(first?"https://api.openai.com/v1/responses":"https://api.typesafe.ai/v1/systemone"))throw new Exception("Wrong provider endpoint");
            if(req.Headers.Authorization?.ToString()!=(first?"Bearer fake-openai":"Bearer fake-jev"))throw new Exception("Wrong provider credentials");
            var body=first?"{\"status\":\"completed\",\"output\":[{\"type\":\"reasoning\"},{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"What would you like to improve first?\"}]}],\"usage\":{\"input_tokens\":100,\"output_tokens\":20}}":JsonSerializer.Serialize(new{answers=new{grounded=new{noul=grounded?.99:.1},relevant=new{noul=.99}}});
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
}

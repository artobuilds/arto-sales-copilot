using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ArtoSalesCopilot;

public partial class App : Application
{
    Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==4&&e.Args[0]=="--provider-test"&&e.Args[1]=="--confirm-paid-fictional"){
            Store.Root=Path.GetFullPath("app/local-data");
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{Shutdown(await ProviderTests.Run(int.Parse(e.Args[2]),int.Parse(e.Args[3]))?0:1);}catch{Directory.CreateDirectory("test-output");File.WriteAllText("test-output/provider-failure.txt","Provider test could not start. Check encrypted keys and fixture range.");Shutdown(1);}return;
        }
        if(e.Args.SequenceEqual(new[]{"--openai-test","--confirm-paid-fictional"})){
            Store.Root=Path.GetFullPath("app/local-data");ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{Shutdown(await ProviderTests.OpenAiOnly()?0:1);}catch{Shutdown(1);}return;
        }
        if(e.Args.SequenceEqual(new[]{"--recording-test"})){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Shutdown(await RecordingTests.Run()?0:1);return;
        }
        if(e.Args.SequenceEqual(new[]{"--voice-test"})){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Shutdown(await VoiceTests.Integration()?0:1);return;
        }
        if(e.Args.Contains("--self-test")) {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var result=await SelfTests.Run();Directory.CreateDirectory("test-output");File.WriteAllText("test-output/self-test.txt",result);Shutdown(result.Contains("FAIL")?1:0);return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--speech-test") {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Directory.CreateDirectory("test-output");
            try {using var worker=new SpeechWorker();using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(120));var start=DateTime.UtcNow;await worker.Start(Store.Load(),ct.Token);var warm=DateTime.UtcNow;var text=await worker.Transcribe(Path.GetFullPath(e.Args[1]),e.Args.Length>2?e.Args[2]:"en","",ct.Token);File.WriteAllText("test-output/speech-test.txt",$"Model load: {(warm-start).TotalSeconds:F2}s\nTranscription: {(DateTime.UtcNow-warm).TotalSeconds:F2}s\nText: {text}");Shutdown(string.IsNullOrWhiteSpace(text)?1:0);}catch(Exception ex){File.WriteAllText("test-output/speech-test.txt","FAIL: "+ex.Message);Shutdown(1);}return;
        }
        var review=e.Args.Contains("--ui-review");
        var smoke=e.Args.Contains("--ui-smoke")||review;
        if(smoke)Store.Root=Path.Combine(Path.GetTempPath(),"ArtoUiFixture-"+Guid.NewGuid().ToString("N"));
        if(!smoke){instance=new Mutex(true,"Local\\ArtoSalesCopilotPreview",out var first);if(!first){MessageBox.Show("Arto Sales Copilot is already open. Use its existing window.","Arto Sales Copilot");Shutdown();return;}}
        var window=new MainWindow(smoke);MainWindow=window;window.Show();
        if(review){window.PrepareUiReview();return;}
        if(smoke) {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try {await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await window.Smoke(Path.GetFullPath("test-output"));window.Close();Shutdown(0);}
            catch(Exception ex) {Directory.CreateDirectory("test-output");File.WriteAllText("test-output/ui-failure.txt",ex.ToString());window.Close();Shutdown(1);}
        }
    }
}

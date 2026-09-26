using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace ArtoSalesCopilot;

internal static class RecordingTests
{
    public static async Task<bool> Run()
    {
        Directory.CreateDirectory("test-output");
        var window=new Window{Title="Arto recording fixture",Width=720,Height=420,Background=Brushes.DarkGreen,Content=new TextBlock{Text="ARTO RECORDING TEST\nSynthetic content only\nNo microphone or meeting captured",Foreground=Brushes.White,FontSize=30,Margin=new Thickness(30)}};
        var recorder=new VideoRecorder();SessionRecording? saved=null;
        try{
            window.Show();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var hwnd=new WindowInteropHelper(window).Handle;
            var sources=VideoSources.List();if(!sources.Any(s=>s.Kind=="window"&&s.Handle==hwnd.ToInt64())||!sources.Any(s=>s.Kind=="screen"))throw new Exception("Source enumeration failed.");
            saved=new(new Brief{Title="Synthetic recording acceptance"},"en",Path.GetFullPath("test-output/recording-fixtures"));
            string? failure=null;using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await recorder.Start(new Settings().FfmpegPath,new("Fixture","window",hwnd.ToInt64()),Path.Combine(saved.Folder,"screen.mkv"),m=>failure=m,ct.Token);
            await Task.Delay(2500,ct.Token);
            if(!await recorder.Stop()||failure is not null)throw new Exception("Window recording failed.");
            foreach(var name in new[]{"microphone.wav","computer.wav"}){
                using var writer=new TimelineWaveWriter(Path.Combine(saved.Folder,name),new WaveFormat(16000,16,1));
                var samples=new byte[3200];for(int i=0;i<1600;i++){var value=(short)(Math.Sin(2*Math.PI*(name.StartsWith("microphone")?400:600)*i/16000)*1000);BitConverter.GetBytes(value).CopyTo(samples,i*2);}
                writer.Append(samples,samples.Length,.2);writer.Append(samples,samples.Length,2);writer.Pad(3.5);
            }
            saved.Turn(new("client","Fictional recording test",0));saved.Advice(Demo.Advice(0,"en"));
            if(!await saved.Mux(new Settings().FfmpegPath))throw new Exception("Recording mux failed.");
            saved.Finish("synthetic-test-complete");
            File.WriteAllText("test-output/recording-check.txt","PASS: window enumeration + own fixture window recording + synthetic dual audio + mux + transcript persistence.\nFolder: "+saved.Folder);return true;
        }catch(Exception ex){await recorder.Stop();saved?.Finish("synthetic-test-failed");File.WriteAllText("test-output/recording-check.txt","FAIL: "+ex.Message);return false;}
        finally{window.Close();}
    }
}

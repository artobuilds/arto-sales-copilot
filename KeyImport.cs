using System.IO;
using System.Text.RegularExpressions;

namespace ArtoSalesCopilot;

internal static class KeyImport
{
    // Read only the file explicitly selected by the user. Never log file contents.
    public static void Import(string provider, string path)
    {
        if(!File.Exists(path)||new FileInfo(path).Length>16000)throw new InvalidDataException("Key file is missing or unexpectedly large.");
        string key;
        if(provider=="typesafe")key=MainWindow.ReadTypeSafeFile(path);
        else if(provider=="openai")
        {
            key=ParseOpenAi(File.ReadAllText(path));
        }
        else throw new ArgumentException("Unsupported import provider.");
        Store.SetKey(provider,key);
    }
    internal static string ParseOpenAi(string text)
    {
        var matches=Regex.Matches(text,@"(?<![A-Za-z0-9_-])sk-[A-Za-z0-9_-]{20,}").Select(m=>m.Value).Distinct().ToArray();
        if(matches.Length!=1)throw new InvalidDataException("Expected exactly one OpenAI API key in the authorized file.");
        return matches[0];
    }
}

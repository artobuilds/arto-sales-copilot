using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtoSalesCopilot;

public sealed class Brief
{
    public string Title { get; set; } = "New client conversation";
    public string Profile { get; set; } = "Seller profile not supplied. Add your own name, role and verified experience before using live coaching. Do not assume credentials, years or results.";
    public string Offer { get; set; } = "Offer not supplied. Describe your service and confirmed commitments. Prices, delivery dates and guarantees are not agreed; do not invent them.";
    public string Client { get; set; } = "Client background not supplied. Ask rather than assume.";
    public string Goal { get; set; } = "Understand the client's pain and commercial fit; establish value, decision process, budget and timing; agree a concrete next step.";
    public string Constraints { get; set; } = "Use only confirmed profile and offer facts. Never invent prices, outcomes, credentials, discounts or delivery promises. Do not push a technical solution before understanding the problem.";
    public string[] CustomChecks { get; set; } = [];
    public static Brief Load(string path)
    {
        if (new FileInfo(path).Length > 128_000) throw new InvalidDataException("Brief must be under 128 KB.");
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Deserialize<Brief>(File.ReadAllText(path), Store.Json) is { } b ? b.Validate() : throw new InvalidDataException("Empty briefing.");
        return new Brief { Client = File.ReadAllText(path), Title = Path.GetFileNameWithoutExtension(path) }.Validate();
    }
    public Brief Validate()
    {
        if (new[] { Title, Profile, Offer, Client, Goal, Constraints }.Any(s => s is null || s.Length > 24_000)) throw new InvalidDataException("Invalid briefing field (max 24,000 characters each).");
        if (CustomChecks is null || CustomChecks.Length > 12 || CustomChecks.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 350)) throw new InvalidDataException("Use up to 12 custom questions, max 350 characters each.");
        return this;
    }
}
public sealed class Settings
{
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }
    public string InterfaceLanguage { get; set; } = "ru";
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "en";
    public string PythonPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".venv", "Scripts", "python.exe"));
    public string ModelCache { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".models"));
    public string Microphone { get; set; } = "";
    public string Output { get; set; } = "";
    public bool UsePhrasing { get; set; }
    public string PhrasingModel { get; set; } = "gpt-6-sol";
    public string ReasoningEffort { get; set; } = "low";
    public bool SaveSession { get; set; } = true;
    public bool AnalyzeVoice { get; set; } = true;
    public string FfmpegPath { get; set; } = "";
    public Brief Brief { get; set; } = new();
}
public sealed record Utterance(string Speaker, string Text, double Seconds);
public sealed record Signal(string Id, string Label, double Value);
public sealed record Advice(string Move, string Title, string Say, string Stage, double Confidence, List<Signal> Signals, string Source, int InputTokens = 0);
public static class Store
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Root { get; set; } = Path.Combine(AppContext.BaseDirectory, "local-data");
    public static Settings Load() => File.Exists(Path.Combine(Root, "settings.json")) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Root, "settings.json")), Json) ?? new() : new();
    public static void Save(Settings s) { s.Brief.Validate(); WriteAtomic("settings.json", JsonSerializer.Serialize(s, Json)); }
    public static void SaveAppearance(string language, string theme)
    {
        // Persist only these preferences; do not save unfinished briefing edits or touch API keys.
        var path=Path.Combine(Root,"settings.json");
        var document=File.Exists(path)?System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) as System.Text.Json.Nodes.JsonObject:new System.Text.Json.Nodes.JsonObject();
        if(document is null)throw new InvalidDataException("Invalid settings document.");
        document["interfaceLanguage"]=UiText.NormalizeLanguage(language);
        document["theme"]=theme=="light"?"light":"dark";
        WriteAtomic("settings.json",document.ToJsonString(Json));
    }
    public static void SaveMiniPosition(double left,double top)
    {
        if(!double.IsFinite(left)||!double.IsFinite(top))throw new ArgumentOutOfRangeException(nameof(left));
        var path=Path.Combine(Root,"settings.json");
        var document=File.Exists(path)?System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) as System.Text.Json.Nodes.JsonObject:new System.Text.Json.Nodes.JsonObject();
        if(document is null)throw new InvalidDataException("Invalid settings document.");
        document["miniLeft"]=left;document["miniTop"]=top;
        WriteAtomic("settings.json",document.ToJsonString(Json));
    }
    static void WriteAtomic(string name, string text) { Directory.CreateDirectory(Root); var p = Path.Combine(Root, name); File.WriteAllText(p + ".tmp", text, Encoding.UTF8); File.Move(p + ".tmp", p, true); }
    public static void SetKey(string provider, string key)
    {
        CheckProvider(provider);
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser);
        WriteAtomic(provider + ".key", Convert.ToBase64String(data));
    }
    public static string GetKey(string provider)
    {
        CheckProvider(provider);
        var p = Path.Combine(Root, provider + ".key");
        if (!File.Exists(p)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(File.ReadAllText(p)), null, DataProtectionScope.CurrentUser));
    }
    static void CheckProvider(string p) { if (p is not ("typesafe" or "anthropic" or "openai")) throw new ArgumentException("Unknown provider."); }
}

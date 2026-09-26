using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ArtoSalesCopilot;

internal static class ProviderTests
{
    static readonly (string Name,string Language,string Text,string[] Moves)[] Cases=[
        ("introduction","en","Before we start, tell me about yourself and how you can help our business.",["introduce"]),
        ("why-you","en","Why should we choose you instead of a cheaper automation freelancer?",["introduce","value","objection"]),
        ("price","en","How much will you charge to fix our lead intake? Can you give me a fixed price today?",["price","clarify","scope"]),
        ("expensive","en","That sounds too expensive. We might just keep doing this manually.",["objection"]),
        ("think","ru","Мне нужно подумать. Я пока не понимаю, окупится ли это для нас.",["objection","value"]),
        ("technical","uk","Чи гарантуєте ви, що інтеграція з нашою закритою ERP працюватиме вже завтра? Документацію API ми ще не надали.",["clarify","scope","objection","timing"]),
        ("next-step","en","This sounds useful. I can send a sample lead and screenshots of our process. What should we do next?",["next","close","scope"])
    ];
    public static async Task<bool> Run(int start,int count)
    {
        if(start<0||count<1||start+count>Cases.Length)throw new ArgumentException("Invalid fixture range.");
        var jev=Store.GetKey("typesafe");var openai=Store.GetKey("openai");
        if(jev.Length==0||openai.Length==0)throw new InvalidOperationException("Both provider keys are required.");
        // Entirely fictional fixture; never load the user's saved client brief.
        var brief=new Brief{Title="Fictional provider acceptance",Profile="Artur is an AI automation specialist working with n8n, Make, Airtable and API integrations. Example portfolio: a lead routing workflow and a document generation workflow. No production metrics or experience years are verified in this fixture.",Offer="Workflow audit and scoped automation implementation. No pricing, dates, discounts or guarantees agreed.",Client="Fictional company: lead requests are manually copied from email to a spreadsheet. No private client data.",Constraints="Never invent a price, ROI, deadline, customer name or technical compatibility. Ask for unknown facts. One useful sales reply, not an essay."};
        Directory.CreateDirectory("test-output");using var coach=new Coach();bool all=true;
        foreach(var c in Cases.Skip(start).Take(count))
        {
            using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(90));var sw=Stopwatch.StartNew();
            var turns=new[]{new Utterance("client",c.Text,0)};
            try{
                var advice=await coach.Analyze(brief,turns,c.Language,jev,ct.Token);var analysis=sw.Elapsed.TotalSeconds;
                var wording=await coach.Phrase(brief,turns,advice,c.Language,openai,jev,ct.Token);
                bool pass=c.Moves.Contains(advice.Move)&&wording?.Accepted==true;all&=pass;
                var result=new{fixture=c.Name,language=c.Language,input=c.Text,pass,move=advice.Move,expectedMoves=c.Moves,advice.Confidence,jevSeconds=analysis,totalSeconds=sw.Elapsed.TotalSeconds,wording};
                File.WriteAllText($"test-output/provider-{c.Name}.json",JsonSerializer.Serialize(result,Store.Json));
            }catch(Exception ex){all=false;File.WriteAllText($"test-output/provider-{c.Name}.json",JsonSerializer.Serialize(new{fixture=c.Name,pass=false,error=ex is OperationCanceledException?"Provider timeout/cancelled":ex.Message},Store.Json));break;}
        }
        return all;
    }
    public static async Task<bool> OpenAiOnly()
    {
        var key=Store.GetKey("openai");if(key.Length==0)throw new InvalidOperationException("OpenAI key missing.");
        using var coach=new Coach();var results=new List<object>();bool all=true;
        foreach(var c in Cases.Where(c=>c.Name is "price" or "think" or "technical")){
            using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(40));
            try{var advice=new Advice(c.Moves[0],"Fixture","", "discovery",1,[],"SCRIPTED INTENT FOR ISOLATED TEST");
                var result=await coach.GenerateForTest(new Brief{Client="Fictional business. No client data, agreed price or deadline."},[new("client",c.Text,0)],advice,c.Language,key,ct.Token);
                all&=result.Text is not null;results.Add(new{fixture=c.Name,language=c.Language,verifiedByJev=false,result});
            }catch(Exception ex){all=false;results.Add(new{fixture=c.Name,error=ex is OperationCanceledException?"Provider timeout":ex.Message});break;}
        }
        File.WriteAllText("test-output/openai-isolated.json",JsonSerializer.Serialize(results,Store.Json));return all;
    }
}

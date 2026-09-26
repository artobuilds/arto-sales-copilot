namespace ArtoSalesCopilot;

public static class Demo
{
    // Scripted offline walkthrough. These results are never presented as a live model evaluation.
    public static readonly (string Speaker, string En, string Ru, string Uk, string Move, string Stage)[] Turns = [
        ("client", "Hi Artur. Before we start, could you tell me a little about yourself?", "Артур, привет. Расскажите немного о себе, прежде чем начнём.", "Артуре, привіт. Розкажіть трохи про себе, перш ніж почнемо.", "introduce", "opening"),
        ("rep", "I'm Artur. I work on AI automation and integrations. What brought you to this conversation?", "Я Артур, занимаюсь AI-автоматизацией и интеграциями. Что привело вас к этому разговору?", "Я Артур, займаюся AI-автоматизацією та інтеграціями. Що привело вас до цієї розмови?", "discover", "discovery"),
        ("client", "Our sales team is spending too much time moving information between tools, and leads get missed.", "Наша команда продаж тратит слишком много времени на перенос информации между сервисами, и заявки теряются.", "Наша команда продаж витрачає забагато часу на перенесення інформації між сервісами, і заявки губляться.", "impact", "discovery"),
        ("client", "Probably ten hours a week. More importantly, customers sometimes wait a day for a response.", "Наверное, десять часов в неделю. Но важнее то, что клиенты иногда ждут ответа целый день.", "Напевно, десять годин на тиждень. Але важливіше те, що клієнти іноді чекають на відповідь цілий день.", "value", "value"),
        ("client", "We want faster responses, but I don't want to pay for another tool nobody will use.", "Нам нужны быстрые ответы, но я не хочу платить за ещё один инструмент, которым никто не будет пользоваться.", "Нам потрібні швидкі відповіді, але я не хочу платити за ще один інструмент, яким ніхто не користуватиметься.", "objection", "objection"),
        ("rep", "That makes sense. What would the team need to see for this to feel useful and easy to adopt?", "Это понятно. Что нужно вашей команде, чтобы решение оказалось полезным и простым в освоении?", "Це зрозуміло. Що потрібно вашій команді, щоб рішення було корисним і простим в освоєнні?", "discover", "discovery"),
        ("client", "It should fit our existing process. Maybe a client portal as well, but we haven't defined who sees what.", "Оно должно вписаться в наш процесс. Возможно, ещё личный кабинет клиента, но мы не определили, кто что видит.", "Воно має вписатися в наш процес. Можливо, ще особистий кабінет клієнта, але ми не визначили, хто що бачить.", "scope", "scoping"),
        ("client", "I can approve a small pilot. Let's work out the scope first and then discuss the investment.", "Небольшой пилот я могу согласовать. Давайте сначала определим объём, а потом обсудим бюджет.", "Невеликий пілот я можу погодити. Давайте спочатку визначимо обсяг, а потім обговоримо бюджет.", "next", "closing")
    ];
    public static Utterance Turn(int index,string language) { var t=Turns[index]; return new(t.Speaker,language switch { "ru"=>t.Ru,"uk"=>t.Uk,_=>t.En },index*8); }
    public static Advice Advice(int index,string language)
    {
        var d=Turns[index]; var m=Coach.Moves.Single(m=>m.Id==d.Move);
        var signals=Coach.Topics.Select(t=>new Signal(t.Id,language switch {"ru"=>t.Ru,"uk"=>t.Uk,_=>t.En}, t.Id switch { "pain" when index>=2=>.95,"impact" when index>=3=>.90,"value" when index>=4=>.9,"objection" when index is 4 or 5=>.91,"authority" when index>=7=>.92,"buying" when index>=7=>.85,_=>.1 })).ToList();
        return new(m.Id,Coach.Title(m.Id,language),m.Line(language),d.Stage,.92,signals,"SCRIPTED DEMO · no AI / no API cost");
    }
}

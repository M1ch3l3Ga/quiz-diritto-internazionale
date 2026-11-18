using System.Net.Http.Json;
using QuizApp.Models;

namespace QuizApp.Services;

/// <summary>Banca dati delle domande, composizione delle sessioni e calcolo delle statistiche.</summary>
public sealed class QuizService(HttpClient http, AppConfig config)
{
    /// <summary>Quota massima della sessione riservata alle domande sbagliate in passato.</summary>
    private const double ReviewShare = 0.4;

    private List<Question>? _questions;

    /// <summary>Domande per sessione.</summary>
    public int Count => config.QuestionsPerSession;

    public async Task<IReadOnlyList<Question>> GetQuestionsAsync()
    {
        _questions ??= await http.GetFromJsonAsync<List<Question>>("data/questions.json") ?? new();
        return _questions;
    }

    public async Task<IReadOnlyList<string>> GetTopicsAsync() =>
        (await GetQuestionsAsync()).Select(q => q.Topic).Distinct().ToList();

    /// <summary>
    /// Sceglie le domande della sessione:
    /// 1) fino al 40% tra quelle la cui ultima risposta era sbagliata (ripasso degli errori);
    /// 2) poi quelle mai viste;
    /// 3) infine le altre, partendo da quelle viste meno di recente.
    /// Le opzioni di ogni domanda vengono rimescolate.
    /// </summary>
    public async Task<List<SessionQuestion>> BuildSessionAsync(IReadOnlyList<AnswerRecord> history, string? topic = null)
    {
        var all = (await GetQuestionsAsync()).Where(q => topic is null || q.Topic == topic).ToList();
        var count = Math.Min(config.QuestionsPerSession, all.Count);

        var last = history
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.AnsweredAt).Last());

        var wrong = Shuffle(all.Where(q => last.TryGetValue(q.Id, out var a) && !a.Correct));
        var unseen = Shuffle(all.Where(q => !last.ContainsKey(q.Id)));
        var seenOk = Shuffle(all.Where(q => last.TryGetValue(q.Id, out var a) && a.Correct))
            .OrderBy(q => last[q.Id].AnsweredAt.Date) // prima quelle viste da più tempo
            .ToList();

        var picked = new List<Question>();
        picked.AddRange(wrong.Take((int)Math.Round(count * ReviewShare)));
        foreach (var pool in new[] { unseen, wrong, seenOk })
            picked.AddRange(pool.Where(q => !picked.Contains(q)).Take(count - picked.Count));

        return Shuffle(picked).Select(ToSessionQuestion).ToList();
    }

    public static SessionQuestion ToSessionQuestion(Question q)
    {
        var order = Shuffle(Enumerable.Range(0, q.Options.Count));
        return new SessionQuestion
        {
            Source = q,
            Options = order.Select(i => q.Options[i]).ToList(),
            CorrectIndex = order.IndexOf(q.Answer),
        };
    }

    public static List<TopicStat> TopicStats(IEnumerable<AnswerRecord> answers, IEnumerable<string> topics)
    {
        var byTopic = answers.GroupBy(a => a.Topic).ToDictionary(g => g.Key, g => g.ToList());
        return topics.Select(t => byTopic.TryGetValue(t, out var l)
                ? new TopicStat { Topic = t, Answered = l.Count, Correct = l.Count(a => a.Correct) }
                : new TopicStat { Topic = t })
            .ToList();
    }

    /// <summary>Domande la cui ultima risposta è sbagliata.</summary>
    public static int PendingErrors(IEnumerable<AnswerRecord> answers) =>
        answers.GroupBy(a => a.QuestionId).Count(g => !g.OrderBy(a => a.AnsweredAt).Last().Correct);

    private static List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

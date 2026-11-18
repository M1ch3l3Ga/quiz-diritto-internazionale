using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QuizApp.Models;

namespace QuizApp.Services;

public sealed class SupabaseHttp
{
    public HttpClient Client { get; } = new();
}

/// <summary>Salvataggio e lettura delle statistiche dell'utente corrente.</summary>
public interface IStatsStore
{
    Task SaveSessionAsync(SessionRecord session, IReadOnlyList<AnswerRecord> answers);
    Task<List<SessionRecord>> GetSessionsAsync();
    Task<List<AnswerRecord>> GetAnswersAsync();
}

/// <summary>Statistiche nel database Supabase (tabelle quiz_sessions e quiz_answers, protette da RLS).</summary>
public sealed class SupabaseStatsStore(AppConfig config, AuthService auth, SupabaseHttp http) : IStatsStore
{
    private const int PageSize = 1000;

    public async Task SaveSessionAsync(SessionRecord session, IReadOnlyList<AnswerRecord> answers)
    {
        await PostAsync("quiz_sessions", new
        {
            id = session.Id,
            started_at = session.StartedAt,
            finished_at = session.FinishedAt,
            total = session.Total,
            correct = session.Correct,
            duration_seconds = session.DurationSeconds,
        });
        await PostAsync("quiz_answers", answers.Select(a => new
        {
            session_id = a.SessionId,
            question_id = a.QuestionId,
            topic = a.Topic,
            correct = a.Correct,
            answered_at = a.AnsweredAt,
        }).ToList());
    }

    public Task<List<SessionRecord>> GetSessionsAsync() =>
        GetAllAsync<SessionRecord>("quiz_sessions?select=id,started_at,finished_at,total,correct,duration_seconds&order=started_at.asc");

    public Task<List<AnswerRecord>> GetAnswersAsync() =>
        GetAllAsync<AnswerRecord>("quiz_answers?select=session_id,question_id,topic,correct,answered_at&order=answered_at.asc");

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{config.SupabaseUrl.TrimEnd('/')}/rest/v1/{path}");
        req.Headers.Add("apikey", config.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await auth.GetAccessTokenAsync());
        return req;
    }

    private async Task PostAsync(string table, object body)
    {
        using var req = await NewRequestAsync(HttpMethod.Post, table);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = JsonContent.Create(body);
        using var resp = await http.Client.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Salvataggio non riuscito ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync()}");
    }

    private async Task<List<T>> GetAllAsync<T>(string path)
    {
        var all = new List<T>();
        for (var from = 0; ; from += PageSize)
        {
            using var req = await NewRequestAsync(HttpMethod.Get, path);
            req.Headers.Add("Range-Unit", "items");
            req.Headers.Add("Range", $"{from}-{from + PageSize - 1}");
            using var resp = await http.Client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Lettura non riuscita ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync()}");
            var page = await resp.Content.ReadFromJsonAsync<List<T>>() ?? new();
            all.AddRange(page);
            if (page.Count < PageSize) return all;
        }
    }
}

/// <summary>Statistiche nel localStorage del browser (modalità locale, senza Supabase).</summary>
public sealed class LocalStatsStore(AuthService auth, BrowserStorage storage) : IStatsStore
{
    private string SessionsKey => $"quiz.local.{auth.CurrentUser?.Id}.sessions";
    private string AnswersKey => $"quiz.local.{auth.CurrentUser?.Id}.answers";

    public async Task SaveSessionAsync(SessionRecord session, IReadOnlyList<AnswerRecord> answers)
    {
        var sessions = await GetSessionsAsync();
        sessions.Add(session);
        await storage.SetAsync(SessionsKey, sessions);
        var all = await GetAnswersAsync();
        all.AddRange(answers);
        await storage.SetAsync(AnswersKey, all);
    }

    public async Task<List<SessionRecord>> GetSessionsAsync() =>
        await storage.GetAsync<List<SessionRecord>>(SessionsKey) ?? new();

    public async Task<List<AnswerRecord>> GetAnswersAsync() =>
        await storage.GetAsync<List<AnswerRecord>>(AnswersKey) ?? new();
}

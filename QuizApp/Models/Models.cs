using System.Text.Json.Serialization;

namespace QuizApp.Models;

/// <summary>Domanda della banca dati (wwwroot/data/questions.json).</summary>
public sealed class Question
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";
    [JsonPropertyName("question")] public string Text { get; set; } = "";
    [JsonPropertyName("options")] public List<string> Options { get; set; } = new();
    [JsonPropertyName("answer")] public int Answer { get; set; }
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
}

/// <summary>Domanda proposta in una sessione, con le opzioni rimescolate.</summary>
public sealed class SessionQuestion
{
    public required Question Source { get; init; }
    /// <summary>Opzioni nell'ordine mostrato all'utente.</summary>
    public required List<string> Options { get; init; }
    /// <summary>Indice della risposta corretta nell'ordine mostrato.</summary>
    public required int CorrectIndex { get; init; }
    public int? ChosenIndex { get; set; }
    public bool IsAnswered => ChosenIndex.HasValue;
    public bool IsCorrect => ChosenIndex == CorrectIndex;
}

/// <summary>Riga della tabella quiz_sessions.</summary>
public sealed class SessionRecord
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("started_at")] public DateTimeOffset StartedAt { get; set; }
    [JsonPropertyName("finished_at")] public DateTimeOffset FinishedAt { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("correct")] public int Correct { get; set; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }

    [JsonIgnore] public double Percent => Total == 0 ? 0 : 100.0 * Correct / Total;
}

/// <summary>Riga della tabella quiz_answers.</summary>
public sealed class AnswerRecord
{
    [JsonPropertyName("session_id")] public Guid SessionId { get; set; }
    [JsonPropertyName("question_id")] public string QuestionId { get; set; } = "";
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";
    [JsonPropertyName("correct")] public bool Correct { get; set; }
    [JsonPropertyName("answered_at")] public DateTimeOffset AnsweredAt { get; set; }
}

/// <summary>Utente autenticato.</summary>
public sealed class AppUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class TopicStat
{
    public string Topic { get; init; } = "";
    public int Answered { get; init; }
    public int Correct { get; init; }
    public double Percent => Answered == 0 ? 0 : 100.0 * Correct / Answered;
}

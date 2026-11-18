using QuizApp.Models;

namespace QuizApp.Services;

/// <summary>Sessione di domande in corso (in memoria) e ultimo risultato salvato.</summary>
public sealed class SessionState
{
    public Guid Id { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public List<SessionQuestion> Questions { get; private set; } = new();
    public int CurrentIndex { get; set; }
    public string? Topic { get; private set; }

    public SessionRecord? LastResult { get; set; }
    public List<SessionQuestion> LastQuestions { get; set; } = new();

    public bool IsActive => Questions.Count > 0;

    public void Start(List<SessionQuestion> questions, string? topic)
    {
        Id = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        Questions = questions;
        CurrentIndex = 0;
        Topic = topic;
    }

    public void Clear()
    {
        Questions = new();
        CurrentIndex = 0;
    }
}

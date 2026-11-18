namespace QuizApp.Services;

/// <summary>
/// Configurazione letta da wwwroot/appsettings.json.
/// Se Url o AnonKey sono vuoti l'app lavora in "modalità locale":
/// utenti e statistiche restano nel browser (utile per provarla senza Supabase).
/// </summary>
public sealed class AppConfig
{
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseAnonKey { get; set; } = "";
    public int QuestionsPerSession { get; set; } = 20;

    public bool IsLocalMode => string.IsNullOrWhiteSpace(SupabaseUrl) || string.IsNullOrWhiteSpace(SupabaseAnonKey);
}

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuizApp.Models;

namespace QuizApp.Services;

/// <summary>
/// Autenticazione con email e password tramite Supabase Auth (API REST).
/// In modalità locale gli utenti sono salvati nel browser.
/// </summary>
public sealed class AuthService(AppConfig config, BrowserStorage storage, SupabaseHttp supabaseHttp)
{
    private const string SessionKey = "quiz.session";
    private const string LocalUsersKey = "quiz.local.users";

    public AppUser? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event Action? Changed;

    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        CurrentUser = await storage.GetAsync<AppUser>(SessionKey);
        if (CurrentUser is not null && !config.IsLocalMode && CurrentUser.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            if (!await TryRefreshAsync()) await ClearAsync();
        }
        Changed?.Invoke();
    }

    /// <summary>Restituisce un token valido, rinnovandolo se sta per scadere.</summary>
    public async Task<string> GetAccessTokenAsync()
    {
        if (CurrentUser is null) throw new AuthException("Sessione scaduta: accedi di nuovo.");
        if (CurrentUser.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1) && !await TryRefreshAsync())
        {
            await ClearAsync();
            throw new AuthException("Sessione scaduta: accedi di nuovo.");
        }
        return CurrentUser!.AccessToken;
    }

    public async Task<string?> SignUpAsync(string name, string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        name = name.Trim();
        if (config.IsLocalMode)
        {
            var users = await storage.GetAsync<Dictionary<string, LocalUser>>(LocalUsersKey) ?? new();
            if (users.ContainsKey(email)) throw new AuthException("Esiste già un utente con questa email.");
            var id = Guid.NewGuid().ToString();
            users[email] = new LocalUser(id, name, Hash(email, password));
            await storage.SetAsync(LocalUsersKey, users);
            await SetUserAsync(new AppUser { Id = id, Email = email, DisplayName = name, ExpiresAt = DateTimeOffset.MaxValue });
            return null;
        }

        var body = new { email, password, data = new { display_name = name } };
        var json = await PostAuthAsync("signup", body);
        if (json.TryGetProperty("access_token", out _))
        {
            await SetUserFromTokenAsync(json);
            return null;
        }
        // Conferma email attiva su Supabase: la sessione arriva solo dopo il clic sul link.
        return "Registrazione eseguita. Ti abbiamo inviato un'email: apri il link di conferma e poi accedi.";
    }

    public async Task SignInAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        if (config.IsLocalMode)
        {
            var users = await storage.GetAsync<Dictionary<string, LocalUser>>(LocalUsersKey) ?? new();
            if (!users.TryGetValue(email, out var u) || u.PasswordHash != Hash(email, password))
                throw new AuthException("Email o password non corrette.");
            await SetUserAsync(new AppUser { Id = u.Id, Email = email, DisplayName = u.Name, ExpiresAt = DateTimeOffset.MaxValue });
            return;
        }
        var json = await PostAuthAsync("token?grant_type=password", new { email, password });
        await SetUserFromTokenAsync(json);
    }

    public async Task SendPasswordResetAsync(string email, string redirectTo)
    {
        if (config.IsLocalMode) throw new AuthException("Il recupero password non è disponibile in modalità locale.");
        await PostAuthAsync("recover", new { email = email.Trim().ToLowerInvariant(), redirect_to = redirectTo });
    }

    public async Task SignOutAsync()
    {
        if (!config.IsLocalMode && CurrentUser is not null)
        {
            try
            {
                using var req = NewRequest(HttpMethod.Post, "logout");
                req.Headers.Authorization = new("Bearer", CurrentUser.AccessToken);
                await Http.SendAsync(req);
            }
            catch (HttpRequestException) { /* logout locale comunque */ }
        }
        await ClearAsync();
    }

    // ---------------------------------------------------------------

    private HttpClient Http => supabaseHttp.Client;

    private async Task<bool> TryRefreshAsync()
    {
        if (CurrentUser is null || string.IsNullOrEmpty(CurrentUser.RefreshToken)) return false;
        try
        {
            var json = await PostAuthAsync("token?grant_type=refresh_token", new { refresh_token = CurrentUser.RefreshToken });
            await SetUserFromTokenAsync(json);
            return true;
        }
        catch (Exception ex) when (ex is AuthException or HttpRequestException)
        {
            return false;
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{config.SupabaseUrl.TrimEnd('/')}/auth/v1/{path}");
        req.Headers.Add("apikey", config.SupabaseAnonKey);
        return req;
    }

    private async Task<JsonElement> PostAuthAsync(string path, object body)
    {
        using var req = NewRequest(HttpMethod.Post, path);
        req.Content = JsonContent.Create(body);
        HttpResponseMessage resp;
        try { resp = await Http.SendAsync(req); }
        catch (HttpRequestException) { throw new AuthException("Impossibile contattare il server. Controlla la connessione."); }

        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new AuthException(TranslateError(text));
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task SetUserFromTokenAsync(JsonElement json)
    {
        var user = json.GetProperty("user");
        var name = "";
        if (user.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("display_name", out var dn))
            name = dn.GetString() ?? "";
        var email = user.GetProperty("email").GetString() ?? "";
        await SetUserAsync(new AppUser
        {
            Id = user.GetProperty("id").GetString() ?? "",
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name,
            AccessToken = json.GetProperty("access_token").GetString() ?? "",
            RefreshToken = json.GetProperty("refresh_token").GetString() ?? "",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()),
        });
    }

    private async Task SetUserAsync(AppUser user)
    {
        CurrentUser = user;
        await storage.SetAsync(SessionKey, user);
        Changed?.Invoke();
    }

    private async Task ClearAsync()
    {
        CurrentUser = null;
        await storage.RemoveAsync(SessionKey);
        Changed?.Invoke();
    }

    private static string Hash(string email, string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email}|{password}|quiz")));

    private static string TranslateError(string body)
    {
        string raw = body;
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            foreach (var p in new[] { "msg", "error_description", "message", "error" })
                if (root.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String) { raw = v.GetString()!; break; }
        }
        catch (JsonException) { }

        var l = raw.ToLowerInvariant();
        if (l.Contains("invalid login credentials")) return "Email o password non corrette.";
        if (l.Contains("email not confirmed")) return "Email non ancora confermata: apri il link che ti abbiamo inviato.";
        if (l.Contains("already registered") || l.Contains("already been registered")) return "Esiste già un utente con questa email.";
        if (l.Contains("password should be at least")) return "La password deve avere almeno 6 caratteri.";
        if (l.Contains("rate limit") || l.Contains("too many")) return "Troppi tentativi: riprova tra qualche minuto.";
        if (l.Contains("invalid") && l.Contains("email")) return "Indirizzo email non valido.";
        return "Operazione non riuscita: " + raw;
    }

    private sealed record LocalUser(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("hash")] string PasswordHash);
}

public sealed class AuthException(string message) : Exception(message);

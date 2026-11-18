using System.Text.Json;
using Microsoft.JSInterop;

namespace QuizApp.Services;

/// <summary>Accesso al localStorage del browser.</summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", key);
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value)); }
        catch (JSException) { /* storage non disponibile (es. navigazione privata) */ }
    }

    public async Task RemoveAsync(string key)
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", key); }
        catch (JSException) { }
    }
}

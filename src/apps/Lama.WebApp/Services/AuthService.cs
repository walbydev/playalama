using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

public sealed record CurrentUser(string PlayerId, string Username, string? Email);

/// <summary>
/// Gère l'état d'authentification Web via JWT stocké en localStorage.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class AuthService(IJSRuntime js, LamaApiClient api)
{
    private CurrentUser? _currentUser;
    private bool _initialized;

    public CurrentUser? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser is not null;

    /// <summary>Initialise depuis localStorage — à appeler dans OnAfterRenderAsync(firstRender:true).</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var stored = await js.InvokeAsync<StoredSession?>("playalamaAuth.loadSession");
            if (stored is not null && !string.IsNullOrWhiteSpace(stored.Token))
                _currentUser = new CurrentUser(stored.PlayerId, stored.Username, stored.Email);
            _initialized = true; // seulement après succès JS (pas pendant le prerendering)
        }
        catch
        {
            // Prerendering : JS indisponible → on ne marque pas comme initialisé pour réessayer
            _currentUser = null;
        }
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        try
        {
            var result = await api.AccountLoginAsync(username, password);
            await PersistSessionAsync(result);
            _currentUser = new CurrentUser(result.PlayerId, result.PlayerName, result.Email);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password, string? email)
    {
        try
        {
            var result = await api.RegisterAsync(username, password, email);
            await PersistSessionAsync(result);
            _currentUser = new CurrentUser(result.PlayerId, result.PlayerName, result.Email);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DevLoginAsync(string playerName)
    {
        try
        {
            var result = await api.DevLoginAsync(playerName);
            await PersistSessionAsync(result);
            _currentUser = new CurrentUser(result.PlayerId, result.PlayerName, result.Email);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await js.InvokeVoidAsync("playalamaAuth.clearSession");
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<StoredSession?>("playalamaAuth.loadSession");
            return stored?.Token;
        }
        catch { return null; }
    }

    private async Task PersistSessionAsync(WebAuthResponse result)
    {
        await js.InvokeVoidAsync("playalamaAuth.saveSession", new
        {
            token = result.Token,
            playerId = result.PlayerId,
            username = result.PlayerName,
            email = result.Email
        });
    }

    private sealed record StoredSession(string Token, string PlayerId, string Username, string? Email);
}

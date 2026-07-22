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
    private bool _isAdmin;
    private bool _initialized;
    private WebPlayerRating? _currentRating;

    public CurrentUser? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser is not null;
    public bool IsInitialized => _initialized;
    public bool IsAdmin => _isAdmin;

    /// <summary>Rating et niveau du joueur connecté (null si non chargé ou non connecté).</summary>
    public WebPlayerRating? CurrentRating => _currentRating;

    /// <summary>Notifie les composants abonnés d'un changement d'état d'authentification.</summary>
    public event Action? OnAuthStateChanged;

    /// <summary>Initialise depuis localStorage — à appeler dans OnAfterRenderAsync(firstRender:true).</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var stored = await js.InvokeAsync<StoredSession?>("playalamaAuth.loadSession");
            if (stored is not null && !string.IsNullOrWhiteSpace(stored.Token))
            {
                if (stored.ExpiresAt.HasValue && stored.ExpiresAt.Value <= DateTime.UtcNow)
                {
                    await js.InvokeVoidAsync("playalamaAuth.clearSession");
                }
                else
                {
                    _currentUser = new CurrentUser(stored.PlayerId, stored.Username, stored.Email);
                    await DetectAdminAsync(stored.Token);
                    await LoadRatingAsync(stored.Token);
                }
            }
            _initialized = true; // seulement après succès JS (pas pendant le prerendering)
        }
        catch
        {
            // Prerendering : JS indisponible → on ne marque pas comme initialisé pour réessayer
            _currentUser = null;
            _isAdmin = false;
        }
        OnAuthStateChanged?.Invoke();
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        try
        {
            var result = await api.AccountLoginAsync(username, password);
            await PersistSessionAsync(result);
            _currentUser = new CurrentUser(result.PlayerId, result.PlayerName, result.Email);
            _isAdmin = result.IsAdmin;
            await LoadRatingAsync(result.Token);
            _initialized = true;
            OnAuthStateChanged?.Invoke();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password, string? email, string? countryCode = null)
    {
        try
        {
            var result = await api.RegisterAsync(username, password, email, countryCode);
            await PersistSessionAsync(result);
            _currentUser = new CurrentUser(result.PlayerId, result.PlayerName, result.Email);
            _isAdmin = result.IsAdmin;
            await LoadRatingAsync(result.Token);
            _initialized = true;
            OnAuthStateChanged?.Invoke();
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
        _currentRating = null;
        _isAdmin = false;
        await js.InvokeVoidAsync("playalamaAuth.clearSession");
        OnAuthStateChanged?.Invoke();
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<StoredSession?>("playalamaAuth.loadSession");
            if (stored is null || string.IsNullOrWhiteSpace(stored.Token))
                return null;
            if (stored.ExpiresAt.HasValue && stored.ExpiresAt.Value <= DateTime.UtcNow)
                return null;
            return stored.Token;
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
            email = result.Email,
            expiresAt = result.ExpiresAt
        });
    }

    private async Task DetectAdminAsync(string token)
    {
        try
        {
            var (_, isAdmin) = await api.GetAuthStatusAsync(token);
            _isAdmin = isAdmin;
        }
        catch
        {
            _isAdmin = false;
        }
    }

    private async Task LoadRatingAsync(string token)
    {
        try
        {
            _currentRating = await api.GetMyRatingAsync(token);
        }
        catch
        {
            _currentRating = null;
        }
    }

    private sealed record StoredSession(string Token, string PlayerId, string Username, string? Email, DateTime? ExpiresAt);
}

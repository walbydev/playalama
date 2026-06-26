using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

public sealed class StatusViewModel(LamaApiClient api, AuthService auth) : IAsyncDisposable
{
    private Timer? _timer;

    public ServerStatusDto? Snapshot        { get; private set; }
    public bool              IsLoading      { get; private set; }
    public bool              IsTerminating  { get; private set; }
    public string?           ErrorMessage   { get; private set; }
    public string?           SuccessMessage { get; private set; }
    public DateTimeOffset?   LastRefreshed  { get; private set; }
    public bool              IsAuthenticated { get; private set; }

    public event Action? StateChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var token = await auth.GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            IsAuthenticated = false;
            ErrorMessage    = null;
            NotifyStateChanged();
            return;
        }

        IsLoading    = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            Snapshot = await api.GetStatusAsync(token, cancellationToken);
            if (Snapshot is null)
            {
                IsAuthenticated = false;
                ErrorMessage    = "Accès refusé. Votre compte n'a pas les droits admin, ou votre session a expiré.";
            }
            else
            {
                IsAuthenticated = true;
                LastRefreshed   = DateTimeOffset.Now;
                ErrorMessage    = null;
            }
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            ErrorMessage    = $"Erreur de connexion : {ex.Message}";
            Snapshot        = null;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task TerminateAllAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
            return;

        IsTerminating  = true;
        SuccessMessage = null;
        ErrorMessage   = null;
        NotifyStateChanged();

        try
        {
            var token  = await auth.GetTokenAsync();
            var count  = await api.TerminateAllGamesAsync(token, cancellationToken);
            SuccessMessage = count == 0
                ? "Aucune partie active à terminer."
                : $"{count} partie{(count > 1 ? "s" : "")} terminée{(count > 1 ? "s" : "")}.";

            await LoadAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            IsAuthenticated = false;
            ErrorMessage    = "Accès refusé.";
            Snapshot        = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Échec : {ex.Message}";
        }
        finally
        {
            IsTerminating = false;
            NotifyStateChanged();
        }
    }

    public void StartAutoRefresh(int intervalSeconds = 30)
    {
        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
            if (IsAuthenticated)
                await LoadAsync();
        }, null, TimeSpan.FromSeconds(intervalSeconds), TimeSpan.FromSeconds(intervalSeconds));
    }

    public void StopAutoRefresh()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null)
            await _timer.DisposeAsync();
    }
}

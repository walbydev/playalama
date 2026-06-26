using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

public sealed class StatusViewModel(LamaApiClient api, IConfiguration configuration) : IAsyncDisposable
{
    private Timer? _timer;

    public ServerStatusDto? Snapshot      { get; private set; }
    public bool              IsLoading    { get; private set; }
    public string?           ErrorMessage { get; private set; }
    public DateTimeOffset?   LastRefreshed { get; private set; }

    /// <summary>Secret fourni via env LAMA_ADMIN_SECRET (ou saisi manuellement dans la page).</summary>
    public string AdminSecret { get; set; } =
        configuration["LAMA_ADMIN_SECRET"]
        ?? Environment.GetEnvironmentVariable("LAMA_ADMIN_SECRET")
        ?? string.Empty;

    public bool IsSecretMissing => string.IsNullOrWhiteSpace(AdminSecret);

    public event Action? StateChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsSecretMissing)
        {
            ErrorMessage = "Secret admin requis.";
            return;
        }

        IsLoading    = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            Snapshot       = await api.GetStatusAsync(AdminSecret, cancellationToken);
            LastRefreshed  = DateTimeOffset.Now;
            ErrorMessage   = Snapshot is null ? "Accès refusé (secret invalide ou endpoint introuvable)." : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de connexion : {ex.Message}";
            Snapshot     = null;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void StartAutoRefresh(int intervalSeconds = 30)
    {
        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
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

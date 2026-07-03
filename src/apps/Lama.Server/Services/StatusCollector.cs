using System.Diagnostics;
using Lama.Server.Data;
using Lama.Server.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Services;

public interface IStatusCollector
{
    Task<ServerStatusSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}

public sealed class StatusCollector(
    GameHubState hubState,
    LamaDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment) : IStatusCollector
{
    public async Task<ServerStatusSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var process   = Process.GetCurrentProcess();
        var startTime = process.StartTime.ToUniversalTime();
        var uptime    = DateTimeOffset.UtcNow - startTime;

        var serverSection = new ServerSection(
            Uptime:       FormatUptime(uptime),
            UptimeSeconds: (long)uptime.TotalSeconds,
            MemoryMb:     Math.Round(process.WorkingSet64 / 1_048_576.0, 1),
            ThreadCount:  process.Threads.Count,
            Environment:  environment.EnvironmentName,
            Version:      GetVersion()
        );

        var gamesSection = await CollectGamesAsync(cancellationToken);
        var (playersSection, historySection, dbSection) = await CollectDatabaseAsync(cancellationToken);
        var aiSection = await CollectAiServerAsync(cancellationToken);

        return new ServerStatusSnapshot(
            CollectedAt: DateTimeOffset.UtcNow,
            Server:      serverSection,
            Games:       gamesSection,
            Players:     playersSection,
            History:     historySection,
            Database:    dbSection,
            AiServer:    aiSection
        );
    }

    // ── In-memory game metrics ────────────────────────────────────────────────

    private Task<GamesSection> CollectGamesAsync(CancellationToken _)
    {
        var games = hubState.ListGames();
        var activeCount = games.Count;
        var activePlayers = hubState.ActivePlayerCount;

        return Task.FromResult(new GamesSection(
            ActiveCount:       activeCount,
            ActivePlayers:     activePlayers
        ));
    }

    // ── Database metrics ─────────────────────────────────────────────────────

    private async Task<(PlayersSection, HistorySection, DatabaseSection)> CollectDatabaseAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return (
                    new PlayersSection(0, 0),
                    new HistorySection(0, 0),
                    new DatabaseSection("unreachable", db.Database.ProviderName ?? "unknown", false)
                );
            }

            var today = DateTimeOffset.UtcNow.Date;

            var totalPlayers       = await db.Players.CountAsync(cancellationToken);
            var playersToday       = await db.Players.CountAsync(p => p.CreatedAt >= today, cancellationToken);
            var totalSessions      = await db.SessionGames.CountAsync(cancellationToken);
            var sessionsToday      = await db.SessionGames.CountAsync(g => g.CreatedAt >= today, cancellationToken);
            var totalCompleted     = await db.CompletedGames.CountAsync(cancellationToken);
            var completedToday     = await db.CompletedGames.CountAsync(g => g.EndedAt >= today, cancellationToken);
            var pendingMigrations  = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();

            return (
                new PlayersSection(totalPlayers, playersToday),
                new HistorySection(totalCompleted, completedToday, totalSessions, sessionsToday),
                new DatabaseSection("ok", db.Database.ProviderName ?? "unknown", pendingMigrations)
            );
        }
        catch (Exception)
        {
            return (
                new PlayersSection(0, 0),
                new HistorySection(0, 0),
                new DatabaseSection("error", "unknown", false)
            );
        }
    }

    // ── AIServer ping ─────────────────────────────────────────────────────────

    private async Task<AiServerSection> CollectAiServerAsync(CancellationToken cancellationToken)
    {
        var aiServerUrl = configuration["LAMA_AI_SERVER_URL"]
                       ?? Environment.GetEnvironmentVariable("LAMA_AI_SERVER_URL");

        if (string.IsNullOrWhiteSpace(aiServerUrl))
            return new AiServerSection("not_configured", null, null, null);

        try
        {
            var http = httpClientFactory.CreateClient("status-aiserver");
            var sw   = Stopwatch.StartNew();

            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await http.GetAsync($"{aiServerUrl.TrimEnd('/')}/health", cts.Token);
            sw.Stop();

            var status = response.IsSuccessStatusCode ? "ok" : "degraded";
            return new AiServerSection(status, aiServerUrl, null, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            var errorKind = ex is OperationCanceledException or TaskCanceledException ? "timeout" : "unreachable";
            return new AiServerSection(errorKind, aiServerUrl, null, null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatUptime(TimeSpan t) =>
        t.TotalDays >= 1
            ? $"{(int)t.TotalDays}j {t.Hours:D2}h {t.Minutes:D2}m"
            : $"{t.Hours:D2}h {t.Minutes:D2}m {t.Seconds:D2}s";

    private static string GetVersion() => BuildInfoConstants.Version;
}

// ── Snapshot DTOs ─────────────────────────────────────────────────────────────

public sealed record ServerStatusSnapshot(
    DateTimeOffset CollectedAt,
    ServerSection Server,
    GamesSection Games,
    PlayersSection Players,
    HistorySection History,
    DatabaseSection Database,
    AiServerSection AiServer
);

public sealed record ServerSection(
    string Uptime,
    long UptimeSeconds,
    double MemoryMb,
    int ThreadCount,
    string Environment,
    string Version
);

public sealed record GamesSection(
    int ActiveCount,
    int ActivePlayers
);

public sealed record PlayersSection(
    int TotalRegistered,
    int RegisteredToday
);

public sealed record HistorySection(
    int TotalCompleted,
    int CompletedToday,
    int TotalSessions = 0,
    int SessionsToday = 0
);

public sealed record DatabaseSection(
    string Status,
    string Provider,
    bool MigrationPending
);

public sealed record AiServerSection(
    string Status,
    string? Url,
    string? Language,
    int? ResponseTimeMs
);

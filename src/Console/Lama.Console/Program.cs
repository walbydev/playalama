using Lama.Console.Commands.Dict;
using Lama.Console.Commands.Game;
using Lama.Console.Commands.Middleware;
using Lama.Console.Commands.Play;
using Lama.Console.Commands.Player;
using Lama.Console.Commands.Show;
using Lama.Console.Commands.Tournament;
using Lama.Console.Modes;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Infrastructure.Auth;
using Lama.Infrastructure.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SystemCmds = Lama.Console.Commands.System;

// ─── Configuration de Serilog ───────────────────────────────────────────────
// Configuré avant le host pour capturer les erreurs de démarrage.
// Tous les logs vont sur stderr — stdout est réservé à la sortie formatée
// (--output json/csv) pour permettre les pipes et la redirection.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft",         LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        standardErrorFromLevel: LogEventLevel.Verbose,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "lama", "logs", "lama-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Démarrage de LAMA");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((_, services) =>
        {
            // ─── Infrastructure ──────────────────────────────────────────────
            // Tous les chemins sont résolus cross-platform.
            // LAMA_SESSION_DIR surcharge le répertoire (tests, CI).
            services.AddSingleton<ISessionService,  SessionService>();
            services.AddSingleton<IAccountService,  AccountService>();
            services.AddSingleton<IAuthService,     AuthService>();

            // ─── Services Contracts ──────────────────────────────────────────
            services.AddSingleton<IAccessControlService, AccessControlService>();

            // ─── Providers de langue ─────────────────────────────────────────
            // TODO: ajouter la référence Lama.Languages.fr dans le .csproj et
            //       décommenter quand disponible.
            // services.AddSingleton<IGameLanguageProvider, FrenchLanguageProvider>();

            // ─── Moteur de jeu ───────────────────────────────────────────────
            // TODO: décommenter quand Lama.Domain sera implémenté.
            // services.AddSingleton<IGameEngine, GameEngine>();

            // ─── Middlewares ─────────────────────────────────────────────────
            services.AddSingleton<AccessControlMiddleware>();
            // TODO: décommenter quand implémentés :
            // services.AddSingleton<AccessibilityMiddleware>();
            // services.AddSingleton<ErrorHandlingMiddleware>();
            // services.AddSingleton<LoggingMiddleware>();

            // ─── Services console ────────────────────────────────────────────
            services.AddSingleton<ICommandDispatcher,       CommandDispatcher>();
            services.AddSingleton<IApplicationModeResolver, ApplicationModeResolver>();

            // ─── Modes d'exécution ───────────────────────────────────────────
            services.AddTransient<CommandLineMode>(provider =>
                new CommandLineMode(
                    args,
                    provider.GetRequiredService<ICommandDispatcher>(),
                    provider.GetRequiredService<AccessControlMiddleware>(),
                    provider.GetRequiredService<ISessionService>(),
                    provider.GetRequiredService<ILogger<CommandLineMode>>()));
            services.AddTransient<InteractiveMode>();

            // ─── Commandes — Authentification ────────────────────────────────
            // login et logout n'ont pas de groupe CLI — commandId direct
            services.AddSingleton<ICommand, SystemCmds.SystemLoginCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemLogoutCommand>();

            // ─── Commandes — Game ────────────────────────────────────────────
            services.AddSingleton<GameCommand>();
            services.AddSingleton<ICommand, GameCreateCommand>();
            services.AddSingleton<ICommand, GameJoinCommand>();
            services.AddSingleton<ICommand, GameListCommand>();
            services.AddSingleton<ICommand, GameShowCommand>();
            services.AddSingleton<ICommand, GamePauseCommand>();
            services.AddSingleton<ICommand, GameSaveCommand>();
            services.AddSingleton<ICommand, GameEndCommand>();

            // ─── Commandes — Play ────────────────────────────────────────────
            services.AddSingleton<PlayCommand>();
            services.AddSingleton<ICommand, PlayMoveCommand>();
            services.AddSingleton<ICommand, PlayPassCommand>();
            services.AddSingleton<ICommand, PlaySwapCommand>();
            services.AddSingleton<ICommand, PlayChallengeCommand>();
            services.AddSingleton<ICommand, PlayCheckCommand>();

            // ─── Commandes — Show ────────────────────────────────────────────
            services.AddSingleton<ShowCommand>();
            services.AddSingleton<ICommand, ShowBoardCommand>();
            services.AddSingleton<ICommand, ShowRackCommand>();
            services.AddSingleton<ICommand, ShowScoresCommand>();
            services.AddSingleton<ICommand, ShowHistoryCommand>();

            // ─── Commandes — Dict ────────────────────────────────────────────
            // NOTE: DictCheckCommand, DictSearchCommand, DictAnagramCommand
            //       dépendent de IGameLanguageProvider.
            //       Décommenter quand Lama.Languages.fr sera référencé.
            services.AddSingleton<DictCommand>();
            // services.AddSingleton<ICommand, DictCheckCommand>();
            // services.AddSingleton<ICommand, DictSearchCommand>();
            // services.AddSingleton<ICommand, DictAnagramCommand>();

            // ─── Commandes — Player ──────────────────────────────────────────
            services.AddSingleton<PlayerCommand>();
            services.AddSingleton<ICommand, PlayerCreateCommand>();

            // ─── Commandes — Tournament ──────────────────────────────────────
            services.AddSingleton<TournamentCommand>();
            services.AddSingleton<ICommand, TournamentCreateCommand>();

            // ─── Commandes — System ──────────────────────────────────────────
            services.AddSingleton<SystemCmds.SystemCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemStatusCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemRestartCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemSetupCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountCreateCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountListCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountRevokeCommand>();

            // ─── Renderers (stubs) ───────────────────────────────────────────
            // TODO: décommenter quand implémentés :
            // services.AddSingleton<Rendering.BoardRenderer>();
            // services.AddSingleton<Rendering.RackRenderer>();
            // services.AddSingleton<Rendering.ScoreRenderer>();
            // services.AddSingleton<Rendering.ThemeManager>();
        })
        .Build();

    // ─── Résolution du mode et exécution ────────────────────────────────────
    var resolver = host.Services.GetRequiredService<IApplicationModeResolver>();
    var mode     = resolver.Resolve(args);

    Log.Debug("Mode résolu : {ModeType}", mode.GetType().Name);

    // Gestion propre de Ctrl+C / SIGTERM
    using var cts = new CancellationTokenSource();
    global::System.Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var exitCode = await mode.RunAsync(cts.Token);

    Log.Information("LAMA terminé avec le code {ExitCode}", exitCode);
    await Log.CloseAndFlushAsync();

    return exitCode;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Erreur fatale au démarrage de LAMA");
    await Log.CloseAndFlushAsync();
    return ExitCodes.GeneralError;
}

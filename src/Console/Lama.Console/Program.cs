using Lama.Console.Commands.Dict;
using Lama.Console.Commands.Game;
using Lama.Console.Commands.Middleware;
using Lama.Console.Commands.Play;
using Lama.Console.Commands.Player;
using Lama.Console.Commands.Rating;
using Lama.Console.Commands.Show;
using Lama.Console.Commands.Tournament;
using Lama.Console.Modes;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Lama.Infrastructure.Auth;
using Lama.Infrastructure.Persistence;
using Lama.Infrastructure.Profile;
using Lama.Infrastructure.Rating;
using Lama.Infrastructure.Session;
using Lama.Languages.fr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SystemCmds = Lama.Console.Commands.System;

// ─── Configuration de Serilog ───────────────────────────────────────────────
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
            services.AddSingleton<ISessionService,  SessionService>();
            services.AddSingleton<IAccountService,  AccountService>();
            services.AddSingleton<IAuthService,     AuthService>();
            services.AddSingleton<IGameRepository,  JsonGameRepository>();
            services.AddSingleton<RuntimeModeService>();
            services.AddSingleton<OnlineGameGateway>(provider =>
            {
                var runtime = provider.GetRequiredService<RuntimeModeService>();
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(runtime.ServerBaseUrl)
                };

                return new OnlineGameGateway(
                    httpClient,
                    runtime,
                    provider.GetRequiredService<ILogger<OnlineGameGateway>>());
            });
            services.AddSingleton<IPlayerProfileService, JsonPlayerProfileService>();
            services.AddSingleton<PlayerRatingRepository>();
            services.AddSingleton<GameResultRepository>();
            services.AddSingleton<IPlayerRatingService, PlayerRatingService>();

            // ─── Services Contracts ──────────────────────────────────────────
            services.AddSingleton<IAccessControlService, AccessControlService>();

            // ─── Providers de langue ─────────────────────────────────────────
            // FrenchLanguageProvider — maintenant disponible via Lama.Languages.fr
            services.AddSingleton<IGameLanguageProvider>(_ =>
            {
                var basePath = Path.Combine(AppContext.BaseDirectory,
                    "assets", "languages", "fr");
                return new FrenchLanguageProvider(basePath);
            });

            // ─── Use Cases Lama.Core ─────────────────────────────────────────
            // CreateGameUseCase est Singleton : il stocke les parties en mémoire + JSON.
            services.AddSingleton<CreateGameUseCase>(provider =>
            {
                var langProvider = provider.GetRequiredService<IGameLanguageProvider>();
                var repository   = provider.GetRequiredService<IGameRepository>();
                return new CreateGameUseCase(langProvider, repository);
            });
            services.AddSingleton<JoinGameUseCase>();
            services.AddSingleton<PlayMoveUseCase>();
            services.AddSingleton<PassTurnUseCase>();
            services.AddSingleton<SwapLettersUseCase>();
            services.AddSingleton<ChallengeWordUseCase>();
            services.AddSingleton<EndGameUseCase>();

            // ─── Middlewares ─────────────────────────────────────────────────
            services.AddSingleton<AccessControlMiddleware>();

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

            // ─── Commandes — Rating ──────────────────────────────────────────
            services.AddSingleton<RatingCommand>();
            services.AddSingleton<ICommand, RatingShowCommand>();
            services.AddSingleton<ICommand, RatingLeaderboardCommand>();
            services.AddSingleton<ICommand, RatingStatsCommand>();

            // ─── Commandes — Dict (maintenant disponibles) ───────────────────
            services.AddSingleton<DictCommand>();
            services.AddSingleton<ICommand, DictCheckCommand>();
            services.AddSingleton<ICommand, DictSearchCommand>();
            services.AddSingleton<ICommand, DictAnagramCommand>();

            // ─── Commandes — Player ──────────────────────────────────────────
            services.AddSingleton<PlayerCommand>();
            services.AddSingleton<ICommand, PlayerCreateCommand>();
            services.AddSingleton<ICommand, PlayerListCommand>();
            services.AddSingleton<ICommand, PlayerShowCommand>();
            services.AddSingleton<ICommand, PlayerUpdateCommand>();

            // ─── Commandes — Tournament ──────────────────────────────────────
            services.AddSingleton<TournamentCommand>();
            services.AddSingleton<ICommand, TournamentCreateCommand>();

            // ─── Commandes — System ──────────────────────────────────────────────
            services.AddSingleton<SystemCmds.SystemCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemStatusCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemRestartCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemSetupCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemCleanCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountCreateCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountListCommand>();
            services.AddSingleton<ICommand, SystemCmds.SystemAccountRevokeCommand>();
        })
        .Build();

    // ─── Résolution du mode et exécution ────────────────────────────────────
    var resolver = host.Services.GetRequiredService<IApplicationModeResolver>();
    var mode     = resolver.Resolve(args);

    Log.Debug("Mode résolu : {ModeType}", mode.GetType().Name);

    var cts = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    var exitCode = await mode.RunAsync(cts.Token);

    Console.CancelKeyPress -= cancelHandler;

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

using Microsoft.Extensions.DependencyInjection;

namespace Lama.Console.Modes;

/// <summary>
/// Résout le mode d'exécution de l'application en fonction des arguments de la ligne de commande.
/// </summary>
public sealed class ApplicationModeResolver : IApplicationModeResolver
{
    private static readonly HashSet<string> InteractiveTriggers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "interactive",
            "shell",
            "ui"
        };

    private readonly IServiceProvider _services;

    /// <summary>
    /// Initialise le resolver avec le conteneur de services.
    /// </summary>
    public ApplicationModeResolver(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public IConsoleMode Resolve(string[] args)
    {
        // Aucun argument ou argument déclenchant le mode interactif
        if (args.Length == 0 || (args.Length == 1 && InteractiveTriggers.Contains(args[0])))
        {
            return _services.GetRequiredService<InteractiveMode>();
        }

        return _services.GetRequiredService<CommandLineMode>();
    }
}

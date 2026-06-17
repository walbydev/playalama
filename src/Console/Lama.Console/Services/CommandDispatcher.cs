using Microsoft.Extensions.Logging;

namespace Lama.Console.Services;

/// <summary>
/// Dispatche une commande vers son handler en recherchant dans la collection
/// de <see cref="ICommand"/> enregistrés par injection de dépendances.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IEnumerable<ICommand> _commands;
    private readonly ILogger<CommandDispatcher> _logger;

    /// <summary>
    /// Initialise le dispatcher avec les commandes disponibles.
    /// </summary>
    public CommandDispatcher(IEnumerable<ICommand> commands, ILogger<CommandDispatcher> logger)
    {
        _commands = commands;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> DispatchAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var command = _commands.FirstOrDefault(c =>
            c.CommandId.Equals(context.CommandId, StringComparison.OrdinalIgnoreCase));

        if (command is null)
        {
            _logger.LogWarning("Commande inconnue : {CommandId}", context.CommandId);
            await System.Console.Error.WriteLineAsync(
                $"Commande inconnue : {context.CommandId}");
            await System.Console.Error.WriteLineAsync(
                "Utilisez --help pour afficher les commandes disponibles.");
            return ExitCodes.InvalidArgument;
        }

        _logger.LogDebug("Exécution de la commande {CommandId}", context.CommandId);
        return await command.ExecuteAsync(context, cancellationToken);
    }
}

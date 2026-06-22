using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system server clear</c> — supprime l'URL serveur persistée et repasse en mode local.
/// </summary>
public sealed class SystemServerClearCommand : ICommand
{
    public string CommandId => "system.server.clear";

    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var deleted = RuntimeServerConfigStore.ClearServerUrl();

        if (deleted)
            global::System.Console.WriteLine("URL serveur persistée effacée. Mode local actif par défaut.");
        else
            global::System.Console.WriteLine("Aucune URL serveur persistée à effacer. Mode local actif par défaut.");

        return Task.FromResult(ExitCodes.Success);
    }
}


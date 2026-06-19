using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system server show</c> — affiche la cible runtime (local ou URL serveur).
/// </summary>
public sealed class SystemServerShowCommand : ICommand
{
    public string CommandId => "system.server.show";

    private readonly RuntimeModeService _runtimeMode;

    public SystemServerShowCommand(RuntimeModeService runtimeMode)
    {
        _runtimeMode = runtimeMode;
    }

    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (_runtimeMode.IsOnline)
            global::System.Console.WriteLine($"online ({_runtimeMode.ServerBaseUrl})");
        else
            global::System.Console.WriteLine("local");

        return Task.FromResult(ExitCodes.Success);
    }
}


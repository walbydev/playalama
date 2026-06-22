namespace Lama.Console.Services;

/// <summary>
/// Helpers d'écriture sur stdout et stderr.
/// Utilise <c>global::System.Console</c> pour éviter les ambiguïtés
/// avec le namespace <c>Lama.Console</c>.
/// Les erreurs sont toujours écrites sur stderr pour ne pas polluer
/// la sortie standard (pipes, --output json).
/// </summary>
public static class ConsoleOutput
{
    /// <summary>Écrit un message sur stdout.</summary>
    public static void WriteLine(string message) =>
        global::System.Console.WriteLine(message);

    /// <summary>Écrit un message d'erreur sur stderr.</summary>
    public static void WriteError(string message) =>
        global::System.Console.Error.WriteLine(message);

    /// <summary>Écrit un message de stub "non implémenté" sur stderr.</summary>
    public static void WriteNotImplemented(string commandId) =>
        global::System.Console.Error.WriteLine(
            $"[{commandId}] Non implémenté — couche applicative (Lama.Core) absente.");
}

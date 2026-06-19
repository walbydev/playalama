namespace Lama.Console.Services;

/// <summary>
/// Extrait les options runtime globales de la CLI sans les transmettre au parseur de commandes.
/// </summary>
public static class RuntimeCliOptionsParser
{
    public static RuntimeCliOptions Parse(string[] args)
    {
        var filteredArgs = new List<string>(args.Length);
        string? serverUrl = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsServerOption(arg))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                {
                    return new RuntimeCliOptions(
                        FilteredArgs: args,
                        ServerUrl: null,
                        ErrorMessage: "Option --server-url/--server-ip invalide : une URL est requise.");
                }

                serverUrl = args[++i];
                continue;
            }

            filteredArgs.Add(arg);
        }

        return new RuntimeCliOptions(filteredArgs.ToArray(), serverUrl, null);
    }

    private static bool IsServerOption(string arg) =>
        arg.Equals("--server-url", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("--server-ip", StringComparison.OrdinalIgnoreCase);
}

public sealed record RuntimeCliOptions(
    string[] FilteredArgs,
    string? ServerUrl,
    string? ErrorMessage);


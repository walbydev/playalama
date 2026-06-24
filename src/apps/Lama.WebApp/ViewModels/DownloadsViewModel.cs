namespace Lama.WebApp.ViewModels;

public sealed class DownloadsViewModel
{
    public IReadOnlyList<DownloadPlatform> Platforms { get; } =
    [
        new("🐧 Linux",   "x64 / ARM64",
        [
            new("linux-x64",   "curl -fsSL https://get.playalama.io/linux | bash",         "/downloads/lama-linux-x64.zip"),
            new("linux-arm64", "curl -fsSL https://get.playalama.io/linux-arm64 | bash",   "/downloads/lama-linux-arm64.zip"),
        ]),
        new("🪟 Windows", "x64",
        [
            new("win-x64",   "winget install Playalama.CLI", "/downloads/lama-win-x64.zip"),
        ]),
        new("🍎 macOS",   "Intel / Apple Silicon",
        [
            new("osx-x64",   "brew install playalama",       "/downloads/lama-osx-x64.zip"),
            new("osx-arm64", "brew install playalama",       "/downloads/lama-osx-arm64.zip"),
        ]),
    ];

    public IReadOnlyList<CliCommandExample> MainCommands { get; } =
    [
        new("Créer une partie",          "lama game create Alice"),
        new("Rejoindre une partie",      "lama game join <game-id> Alice"),
        new("Afficher le plateau",       "lama game show"),
        new("Poser un mot",              "lama play move H8 BONJOUR H"),
        new("Passer son tour",           "lama play pass"),
        new("Échanger des lettres",      "lama play swap ABCDE"),
        new("Jouer en mode en ligne",    "LAMA_RUNTIME_MODE=online lama game create Alice"),
        new("Voir l'aide complète",      "lama --help"),
    ];

    public HashSet<string> CopiedCommands { get; } = [];

    public void MarkCopied(string key)   => CopiedCommands.Add(key);
    public void ClearCopied(string key)  => CopiedCommands.Remove(key);
    public bool IsCopied(string key)     => CopiedCommands.Contains(key);
}

public sealed record DownloadPlatform(string Os, string Arch, IReadOnlyList<DownloadItem> Items);
public sealed record DownloadItem(string Label, string InstallCmd, string ZipUrl);
public sealed record CliCommandExample(string Description, string Command);

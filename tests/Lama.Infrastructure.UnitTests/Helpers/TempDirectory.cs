namespace Lama.Infrastructure.UnitTests.Helpers;

/// <summary>
/// Crée un répertoire temporaire isolé pour les tests et le supprime automatiquement.
/// Utilise IDisposable pour un nettoyage garanti dans les blocs using.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Chemin du répertoire temporaire créé.</summary>
    public string Path { get; }

    /// <summary>
    /// Crée un répertoire temporaire unique sous Path.GetTempPath()/LamaTests/.
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "LamaTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    /// <summary>Supprime le répertoire temporaire et tout son contenu.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

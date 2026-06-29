namespace Lama.Contracts;

/// <summary>
/// Fournit des IGameLanguageProvider par code langue, avec support multi-langues.
/// </summary>
public interface ILanguageProviderRegistry
{
    IReadOnlyList<string> SupportedLanguages { get; }
    bool IsSupported(string code);
    IGameLanguageProvider GetProvider(IReadOnlyList<string> codes);
    IGameLanguageProvider GetProvider(string code);
}

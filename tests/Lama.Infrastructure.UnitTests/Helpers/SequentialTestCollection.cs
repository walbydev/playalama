namespace Lama.Infrastructure.UnitTests.Helpers;

/// <summary>
/// Collection xUnit qui force l'exécution séquentielle des tests.
/// Utilisée pour les tests qui modifient des variables d'environnement partagées
/// (ex: LAMA_SESSION_DIR) afin d'éviter les race conditions.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SequentialTestCollection : ICollectionFixture<object>
{
    public const string Name = "Sequential";
}

namespace Lama.Domain.Validation;

/// <summary>
/// Résultat de la validation d'un coup.
/// </summary>
/// <param name="IsValid">True si le coup est valide.</param>
/// <param name="ErrorMessage">Message d'erreur si invalide, null ou vide si valide.</param>
public record MoveValidationResult(bool IsValid, string? ErrorMessage = null)
{
    /// <summary>Crée un résultat valide.</summary>
    public static MoveValidationResult Valid() => new(true);

    /// <summary>Crée un résultat invalide avec un message d'erreur.</summary>
    public static MoveValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

namespace Lama.Console.Services;

/// <summary>
/// Codes de sortie standardisés de l'application LAMA.
/// Les erreurs sont écrites sur stderr pour ne pas polluer stdout (--output json).
/// </summary>
public static class ExitCodes
{
    /// <summary>Succès.</summary>
    public const int Success = 0;

    /// <summary>Erreur générale non qualifiée.</summary>
    public const int GeneralError = 1;

    /// <summary>Argument invalide ou commande mal formée.</summary>
    public const int InvalidArgument = 2;

    /// <summary>Partie introuvable.</summary>
    public const int GameNotFound = 3;

    /// <summary>Mot hors dictionnaire.</summary>
    public const int WordNotInDictionary = 5;

    /// <summary>Placement impossible (règles de jeu).</summary>
    public const int InvalidPlacement = 6;

    /// <summary>Pas votre tour.</summary>
    public const int NotYourTurn = 8;

    /// <summary>Timeout dépassé.</summary>
    public const int Timeout = 10;

    /// <summary>Droits insuffisants (access denied).</summary>
    public const int AccessDenied = 11;
}

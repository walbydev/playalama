namespace Lama.WebApp.Services;

/// <summary>
/// Service d'annonces pour les lecteurs d'écran.
/// Utilise aria-live regions pour annoncer les changements importants.
/// </summary>
public sealed class ScreenReaderAnnouncer
{
    private string _currentAnnouncement = string.Empty;
    private string _politeness = "polite"; // ou "assertive" pour les annonces urgentes

    public event Action? OnAnnouncementChanged;

    /// <summary>
    /// Annonce un message aux lecteurs d'écran (mode poli).
    /// </summary>
    public void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        _currentAnnouncement = message;
        _politeness = "polite";
        OnAnnouncementChanged?.Invoke();
    }

    /// <summary>
    /// Annonce un message urgent aux lecteurs d'écran (mode assertif).
    /// </summary>
    public void AnnounceAssertive(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        _currentAnnouncement = message;
        _politeness = "assertive";
        OnAnnouncementChanged?.Invoke();
    }

    /// <summary>
    /// Annonce le tour d'un joueur.
    /// </summary>
    public void AnnounceTurn(string playerName, int turnNumber)
    {
        Announce($"Tour {turnNumber} : c'est au tour de {playerName}");
    }

    /// <summary>
    /// Annonce un score.
    /// </summary>
    public void AnnounceScore(string playerName, int score, int total)
    {
        Announce($"{playerName} marque {score} points. Score total : {total}");
    }

    /// <summary>
    /// Annonce la fin d'une partie.
    /// </summary>
    public void AnnounceGameEnd(string winnerName, int winningScore)
    {
        AnnounceAssertive($"Fin de la partie ! Vainqueur : {winnerName} avec {winningScore} points");
    }

    /// <summary>
    /// Annonce un coup invalide.
    /// </summary>
    public void AnnounceInvalidMove(string reason)
    {
        AnnounceAssertive($"Coup invalide : {reason}");
    }

    /// <summary>
    /// Annonce un mot valide.
    /// </summary>
    public void AnnounceValidWord(string word, int score)
    {
        Announce($"Mot valide : {word} pour {score} points");
    }

    public string CurrentAnnouncement => _currentAnnouncement;
    public string Politeness => _politeness;
}

# Intégration du Système de Rating - Guide Rapide

## 📌 Étapes d'Implémentation

### 1. Ajouter au DI Container

```csharp
// Dans votre configuration DIBuilder
services.AddScoped<PlayerRatingRepository>();
services.AddScoped<GameResultRepository>();
services.AddScoped<IPlayerRatingService, PlayerRatingService>();
```

### 2. Connecter à EndGame UseCase

```csharp
// Dans EndGameUseCase.cs
public class EndGameUseCase
{
    private readonly IPlayerRatingService _ratingService;

    public EndGameUseCase(IPlayerRatingService ratingService)
    {
        _ratingService = ratingService;
    }

    public async Task<EndGameResponse> ExecuteAsync(string gameId, ...)
    {
        // ... logique existante ...

        // Préparer les résultats de classement
        var rankings = CalculateRankings(finalState.Players); // 1er, 2e, etc.

        var gameResults = new List<GameResult>();
        foreach (var (player, rank) in rankings)
        {
            gameResults.Add(new GameResult(
                GameId: gameId,
                PlayerId: player.Id, // ou stocker PlayerId dans Player
                PlayerName: player.Name,
                Rank: rank,
                IsAbandoned: false,
                Score: player.Score,
                OpponentIds: rankings
                    .Select(r => r.player.Id)
                    .Where(id => id != player.Id)
                    .ToList(),
                OpponentRatings: // Charger les ratings avant la partie
                    rankings
                    .Select(r => GetPlayerRatingBefore(r.player.Id))
                    .Where(id => id != player.Id)
                    .Select(id => _previousRatings[id])
                    .ToList(),
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: (int)(endTime - startTime).TotalSeconds
            ));
        }

        // Mettre à jour les ratings
        await _ratingService.UpdateRatingsAsync(gameResults);

        return new EndGameResponse(finalState, winner, scores);
    }
}
```

### 3. Ajouter une Commande d'Affichage (Optionnel)

```bash
# Commande proposée
lama rating show <playerId>
```

---

## 🔄 Flux Complet d'une Partie

```
1. Démarrer partie
   └─ Charger ratings initiaux (cache)

2. Jouer les coups
   └─ (Pas d'impact sur Elo)

3. Fin de partie
   ├─ Calculer les rankings
   ├─ Créer GameResults
   ├─ Appeler _ratingService.UpdateRatingsAsync()
   │  ├─ Calculer ΔElo pour chaque joueur (EloCalculator)
   │  ├─ Déterminer nouveaux niveaux (LevelDeterminer)
   │  ├─ Sauvegarder ratings (PlayerRatingRepository)
   │  └─ Sauvegarder résultats (GameResultRepository)
   └─ Afficher nouveaux ratings

4. Données persistées
   ├─ ~/.config/lama/ratings/player-ratings.json
   └─ ~/.config/lama/ratings/game-results/
```

---

## 📊 Exemple CompletAsync

```csharp
// Créer les GameResults
var gameResults = new[]
{
    new GameResult(
        GameId: "game-123",
        PlayerId: "alice",
        PlayerName: "Alice",
        Rank: 1, // Gagnante
        IsAbandoned: false,
        Score: 256,
        OpponentIds: new[] { "bob" },
        OpponentRatings: new[] { 1200.0 }, // Rating avant la partie
        PlayedAt: DateTimeOffset.UtcNow,
        DurationSeconds: 1850
    ),
    new GameResult(
        GameId: "game-123",
        PlayerId: "bob",
        PlayerName: "Bob",
        Rank: 2, // Perdant
        IsAbandoned: false,
        Score: 198,
        OpponentIds: new[] { "alice" },
        OpponentRatings: new[] { 1220.0 }, // Rating d'Alice avant la partie
        PlayedAt: DateTimeOffset.UtcNow,
        DurationSeconds: 1850
    )
};

// Mettre à jour
await _playerRatingService.UpdateRatingsAsync(gameResults);

// Afficher les résultats
foreach (var result in gameResults)
{
    var newRating = await _playerRatingService.GetRatingAsync(result.PlayerId);
    Console.WriteLine($"{newRating.PlayerName} {newRating.LevelName} ({newRating.EloRating:F0}) - Series: {newRating.CurrentStreak}");
}

// Output exemple:
// Alice 🎪 Lama Acrobate (1240) - Series: +1
// Bob 🌱 Jeune Lama (1180) - Series: -1
```

---

## 🧪 Valider l'Intégration

```bash
# Tous les tests doivent passer
dotnet test --filter "Rating"

# Ou complet
dotnet test --no-build -c Debug
```

---

## 🎯 Checklist d'Intégration

- [ ] Ajouter `IPlayerRatingService` au DI Container
- [ ] Injecter dans EndGameUseCase
- [ ] Charger ratings avant la partie (cache)
- [ ] Créer GameResults à la fin
- [ ] Appeler `UpdateRatingsAsync()`
- [ ] Afficher nouveaux ratings (optionnel mais motivant 🦙)
- [ ] Tester avec 2-3 parties complètes
- [ ] Vérifier fichiers JSON créés

---

## 📦 Dépendances Ajoutées

Aucune dépendance externe ! Utilise uniquement :
- `System.Text.Json` (standard)
- `Microsoft.Extensions.Logging` (déjà utilisé)
- `Xunit` + `Moq` (tests uniquement)



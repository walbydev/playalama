# Système de Rating et Classement Lama 🦙

## Vue d'ensemble

Le système de rating implémente un **classement mondial** basé sur l'**Elo adapté** au jeu Lama, avec des **niveaux thématiques** et des **statistiques de suivi**.

## Architecture

### Modèles (Lama.Contracts)

#### `PlayerRating`
```csharp
PlayerId
├─ EloRating (1200-2500) → Score Elo principal
├─ Level (1-6) → Une des 6 divisions Lama
├─ LevelName → Ex: "🦙 Jeune Lama"
├─ WinsCount, LossesCount, AbandonedCount
├─ CurrentStreak → Série actuelle (+ victoires, - défaites)
├─ HighestStreak → Meilleure série
├─ HighScore, AverageScore
└─ LastGameAt, UpdatedAt
```

#### `GameResult`
Persisté après chaque partie :
```csharp
GameId
├─ PlayerId, PlayerName
├─ Rank (1 = gagnant, 2 = 2e, ...)
├─ IsAbandoned
├─ Score
├─ OpponentIds, OpponentRatings
└─ PlayedAt (horodatage UTC)
```

#### `PlayerStatistics`
```csharp
Wins, Losses, Abandoned, HighScore, AverageScore
├─ WinRate (%)
└─ Disponible par période : All, Last7Days, Last30Days, Last365Days
```

---

## 🎮 Niveaux Lama

| Elo        | Level | Nom              | Emoji | Description                      |
|------------|-------|------------------|-------|----------------------------------|
| 1000-1299  | 1     | 🌱 Jeune Lama    | 🌱    | Novice plein d'énergie           |
| 1300-1499  | 2     | 🎪 Lama Acrobate | 🎪    | S'adapte et maîtrise l'équilibre |
| 1500-1699  | 3     | 🎋 Lama Maître   | 🎋    | Maîtrise les fondamentaux        |
| 1700-1899  | 4     | 👑 Lama Seigneur | 👑    | Dominateur                       |
| 1900-2099  | 5     | ✨ Lama Mythique | ✨    | Légende locale                   |
| 2100+      | 6     | 🔥 Lama Éternel  | 🔥    | Hors des charts                  |

---

## 📊 Calcul Elo

### Formule standard avec adaptation multi-joueurs

```
ΔElo = K × (ScoreRéel - ScoreAttendu)
```

- **K-factor** = 40 (dynamique) ou 20 si Elo ≥ 2400 (elite)
- **ScoreattEndu** = moyenne contre tous les adversaires Elo
  ```
  E = 1 / (1 + 10^((opponentElo - monElo) / 400))
  ```
- **ScoreRéel** = (NbJoueurs - Rang) / (NbJoueurs - 1)
  - 1er de 2 → 1.0 (gagne 20-30 pts vs égal)
  - 2e de 2 → 0.0 (perd 20-30 pts vs égal)
  - 1er de 4 → 1.0
  - 2e de 4 → 0.67
  - 4e de 4 → 0.0

### Exemples

| Situation                        | Elo Initial | ΔElo  | Elo Final |
|----------------------------------|-------------|-------|-----------|
| Gagnant vs égal 1v1              | 1200        | +20   | 1220      |
| Perdant vs égal 1v1              | 1200        | -20   | 1180      |
| Gagnant vs plus faible (-200)    | 1200        | +5    | 1205      |
| Gagnant vs plus fort (+200)      | 1200        | +35   | 1235      |

---

## 🔧 Implémentation

### Couches

#### 1. **Lama.Domain.Rating**
- `EloCalculator` : Calculs Elo
- `LevelDeterminer` : Mappage Elo → Niveau + Emoji

#### 2. **Lama.Infrastructure.Rating**
- `PlayerRatingRepository` : Persistance JSON (`ratings/player-ratings.json`)
- `GameResultRepository` : Historique (`ratings/game-results/{playerId}-{gameId}.json`)
- `PlayerRatingService` : Orchestration (implémente `IPlayerRatingService`)

#### 3. **Lama.Contracts**
- `IPlayerRatingService` : Interface publique

---

## 📝 Utilisation

### Dans un UseCase de fin de partie

```csharp
// 1. Créer les GameResult depuis PlayMove
var gameResults = new List<GameResult>();
foreach (var (player, rank) in rankedPlayers)
{
    gameResults.Add(new GameResult(
        GameId: gameId,
        PlayerId: player.Id,
        PlayerName: player.Name,
        Rank: rank,
        IsAbandoned: false,
        Score: player.Score,
        OpponentIds: opponents.Select(o => o.Id).ToList(),
        OpponentRatings: opponents.Select(o => playerRatingService.GetRatingAsync(o.Id).Result.EloRating).ToList(),
        PlayedAt: DateTimeOffset.UtcNow,
        DurationSeconds: (int)(DateTimeOffset.UtcNow - gameStartTime).TotalSeconds
    ));
}

// 2. Mettre à jour les ratings
await _playerRatingService.UpdateRatingsAsync(gameResults);

// 3. Afficher les nouveaux ratings
foreach (var result in gameResults)
{
    var rating = await _playerRatingService.GetRatingAsync(result.PlayerId);
    Console.WriteLine($"{rating.PlayerName}: {rating.LevelName} - Elo {rating.EloRating:F0}");
}
```

### Requêtes

```csharp
// Obtenir le rating d'un joueur
var rating = await _ratingService.GetRatingAsync("player-id");

// Classement mondial
var leaderboard = await _ratingService.GetLeaderboardAsync(100); // Top 100

// Joueurs d'un niveau
var platinumPlayers = await _ratingService.GetPlayersByLevelAsync(4); // Lama Seigneur

// Statistiques
var stats = await _ratingService.GetStatisticsAsync("player-id");
Console.WriteLine($"Taux de victoire (7j): {stats.Last7Days.WinRate:F1}%");
```

---

## 💾 Persistance

### Fichiers

```
~/.config/lama/ratings/
├─ player-ratings.json            # Tous les ratings actuels (1 fichier)
└─ game-results/
   ├─ player-1-game-1.json
   ├─ player-1-game-2.json
   └─ player-2-game-1.json
```

### Structure player-ratings.json
```json
[
  {
    "playerId": "player-1",
    "eloRating": 1250,
    "level": 2,
    "levelName": "🎪 Lama Acrobate",
    "winsCount": 15,
    "lossesCount": 8,
    "abandonedCount": 1,
    "currentStreak": 3,
    "highestStreak": 7,
    "highScore": 642,
    "averageScore": 380.5,
    "lastGameAt": "2026-06-18T15:30:00Z",
    "updatedAt": "2026-06-18T15:30:00Z"
  },
  ...
]
```

---

## 🧪 Tests

```bash
# Tests Elo
dotnet test tests/Lama.Domain.UnitTests --filter "EloCalculator"

# Tests niveaux
dotnet test tests/Lama.Domain.UnitTests --filter "LevelDeterminer"

# Tests service complet
dotnet test tests/Lama.Infrastructure.UnitTests --filter "PlayerRatingService"

# Tous les tests Rating
dotnet test --filter "Rating"
```

---

## 🚀 Intégration Prochaine

### Commandes CLI proposées
```bash
lama rating show <playerId>           # Afficher le rating d'un joueur
lama rating leaderboard [--top N]     # Top N mondial
lama rating level <levelNum>          # Joueurs d'un niveau
lama rating stats <playerId> [--7d]   # Stats avec optionnel par période
```

### Mode Interactif
- Afficher le nouveau rating après chaque partie
- Avertissement si niveau changé
- Indication de progression vers prochain niveau

---

## 📌 Notes

- **Nouvel joueur** : commence à 1200 Elo (🌱 Jeune Lama)
- **Abandon** : no Elo (zéro), remet la série à 0
- **Conservation des données** : historique complet préservé pour audits
- **Recalcul** : possible en relisant `game-results/` → utile post-bug



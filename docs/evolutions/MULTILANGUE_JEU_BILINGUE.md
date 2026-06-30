# Évolution : Jeu bilingue / trilingue avec classements par langue

**Date** : 2026-06-19
**Statut** : Proposition

---

## Résumé

Permettre à deux ou plusieurs joueurs de s'affronter en utilisant des langues différentes au sein
de la **même partie** (ex : Alice joue en français, Bob joue en anglais). Chaque joueur dispose
d'un dictionnaire et d'un barème de lettres propre à sa langue, mais partage le même plateau.
Les classements Elo sont ensuite enrichis d'une dimension **langue** pour créer des files de
classement par catégorie linguistique.

---

## Motivation

- Ouvrir le jeu à des communautés multilingues (ex : FR/EN, FR/ES, FR/DE).
- Rendre les parties compétitives plus équitables (chaque joueur exploite sa langue natale).
- Enrichir l'aspect stratégique : les lettres présentes sur le plateau sont validées selon la
  langue *du joueur qui les pose*, ouvrant des dynamiques de « croissement inter-langues ».
- Distinguer les classements mondiaux par langue (« meilleur joueur FR », « meilleur joueur EN »)
  tout en conservant un classement global.

---

## Comportement attendu

### Création de partie

```
lama game create Alice --lang fr --player-lang fr:Alice,en:Bob
```

ou via JSON API (online) :

```json
{
  "hostName": "Alice",
  "playerLanguages": { "Alice": "fr", "Bob": "en" },
  "sharedBoard": true
}
```

### Règles de validation d'un coup

1. Le mot posé est validé dans **le dictionnaire de la langue du joueur**.
2. Le score de chaque lettre posée est calculé avec **le barème de la langue du joueur**.
3. Les lettres déjà présentes sur le plateau gardent leur valeur originale (langue de pose).
4. Si un mot **traverse** une lettre posée par un autre joueur en langue différente, la lettre
   partagée compte **zéro point** pour les deux (lettre de jonction neutre).

### Affichage plateau

- Les cases avec des lettres posées en langue différente peuvent être coloriées différemment
  (optionnel en v1 ; en v2 via un attribut `LanguageCode` sur chaque tuile).

---

## Impact sur l'architecture

### `Lama.Contracts`

**`IGameLanguageProvider`** — inchangé.

**Nouveau : `ILanguageRegistry`**

```csharp
/// Registre des providers disponibles. Injecté en Singleton.
public interface ILanguageRegistry
{
    IGameLanguageProvider Get(string languageCode);
    IReadOnlyList<string> AvailableLanguages { get; }
}
```

**`PlayerRating`** — ajouter un champ langue :

```csharp
public record PlayerRating(
    // ...champs existants...
    string Language = "fr"   // langue principale du joueur
);
```

**`GameResult`** — le champ `Language` existe déjà (`string Language = "fr"`).
Faire évoluer vers une liste :

```csharp
IReadOnlyDictionary<string, string> PlayerLanguages   // playerId → languageCode
```

**`RankingQueue`** — ajouter des files par langue :

```csharp
public enum RankingQueue
{
    OpenRanked       = 1,
    Tournament       = 2,
    CasualUnranked   = 3,
    GlobalPrestige   = 4,
    // Nouvelles files linguistiques :
    FrenchRanked     = 10,
    EnglishRanked    = 11,
    SpanishRanked    = 12,
    GermanRanked     = 13,
    BilingualRanked  = 20   // file dédiée aux parties multilingues
}
```

---

### `Lama.Domain`

**`GameEngine`**  
Aujourd'hui le moteur reçoit un seul `IGameLanguageProvider`. Il faut passer à un provider
**par joueur** :

```csharp
public void InitializeGame(
    IReadOnlyList<string> playerNames,
    IReadOnlyDictionary<string, IGameLanguageProvider> playerProviders)
```

Règle de validation à adapter :

```csharp
// ValidateMove doit recevoir l'index du joueur actif
// pour utiliser son provider (dictionnaire + barème)
ValidationResult ValidateMove(
    Dictionary<Position, char> letters,
    int currentPlayerIndex);
```

La distribution du **sac** devient mixte en partie bilingue :
- Option A : **sac commun fusionné** (distribution normalisée, sum des deux langues / 2).
- Option B : **sac séparé par joueur** (chaque joueur pioche dans son propre sac). Recommandé en v1.

---

### `Lama.Core`

**`CreateGameUseCase`** — accepter un `playerLanguages` optionnel :

```csharp
public record CreateGameRequest(
    string HostName,
    string Language = "fr",
    GameLevel GameLevel = GameLevel.Standard,
    // Nouveau :
    IReadOnlyDictionary<string, string>? PlayerLanguages = null
);
```

Si `PlayerLanguages` est null → mono-langue (comportement actuel).

---

### `Lama.Infrastructure`

**Nouveau `CompositeLanguageRegistry`** :

```csharp
public sealed class CompositeLanguageRegistry : ILanguageRegistry
{
    private readonly Dictionary<string, IGameLanguageProvider> _providers;

    public CompositeLanguageRegistry(IEnumerable<IGameLanguageProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.GetLocale().Split('-')[0].ToLower());
    }

    public IGameLanguageProvider Get(string languageCode)
    {
        if (_providers.TryGetValue(languageCode, out var p)) return p;
        throw new InvalidOperationException($"Langue non disponible : {languageCode}");
    }

    public IReadOnlyList<string> AvailableLanguages => [.._providers.Keys];
}
```

Enregistrement dans `Program.cs` (Console + Server) :

```csharp
services.AddSingleton<ILanguageRegistry>(provider =>
{
    var providers = new List<IGameLanguageProvider>
    {
        registry.GetProvider("fr"),
        // new EnglishLanguageProvider(Path.Combine(base, "en")),
    };
    return new CompositeLanguageRegistry(providers);
});
```

---

### `Lama.Languages.en` (nouveau projet — Phase 2)

Même structure que `Lama.Languages.fr` :

```
src/libs/Lama.Languages.en/
  EnglishLanguageProvider.cs
  assets/
    scores.json            (barème Scrabble anglais standard)
    tile-distribution.json (distribution Scrabble US)
```

---

### `Lama.Server` (online)

Endpoint `POST /api/v1/games` :

```json
{
  "hostName": "Alice",
  "playerLanguages": { "Alice": "fr", "Bob": "en" },
  "gameLevel": "standard"
}
```

La réponse inclut `availableLanguages` pour que le client affiche les options.

---

## Classement par langue

### Nouveau filtre `language` dans `IPlayerRatingService`

```csharp
Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
    RankingQueue queue = RankingQueue.GlobalPrestige,
    string? language = null,   // "fr", "en", null = toutes langues
    int topCount = 100);
```

### Attribution des points de classement

| Scénario | File mise à jour |
|----------|-----------------|
| Partie mono FR | `FrenchRanked`, `OpenRanked` |
| Partie mono EN | `EnglishRanked`, `OpenRanked` |
| Partie bilingue FR/EN | `BilingualRanked`, `OpenRanked` + `FrenchRanked` pour le joueur FR, `EnglishRanked` pour le joueur EN |
| Tournoi FR | `Tournament`, `FrenchRanked` |

### Affichage CLI

```
lama rating leaderboard --lang fr
lama rating leaderboard --lang en
lama rating leaderboard --queue bilingual
```

---

## Plan de livraison proposé (3 phases)

### Phase 1 — Infrastructure multilingue (prérequis)
- [ ] Créer `ILanguageRegistry` dans `Lama.Contracts`
- [ ] Créer `CompositeLanguageRegistry` dans `Lama.Infrastructure`
- [ ] Enregistrer le registre dans Console et Server
- [ ] Ajouter `language` aux filtres de classement
- [ ] Tester avec la seule langue FR (régression nulle)

### Phase 2 — Langue anglaise + jeu bilingue
- [ ] Créer `Lama.Languages.en` avec dictionnaire SOWPODS open-source
- [ ] Adapter `GameEngine.ValidateMove` pour accepter le provider par joueur
- [ ] Adapter `CreateGameUseCase` (param `PlayerLanguages`)
- [ ] Adapter `GameCreateCommand` et endpoint serveur
- [ ] Ajouter `PlayerLanguages` dans `GameResult`
- [ ] Ajouter les files `FrenchRanked`, `EnglishRanked`, `BilingualRanked`
- [ ] Tests unitaires Engine bilingue
- [ ] Tests E2E online bilingue

### Phase 3 — Troisième langue + UI
- [ ] Ajouter `Lama.Languages.es` ou `Lama.Languages.de`
- [ ] Page download du site statique : liste les builds disponibles avec indicateur des langues supportées
- [ ] Mise à jour `assets/data/releases.json` avec champ `languages: ["fr","en"]`
- [ ] Classements par langue sur `/api/v1/ratings/leaderboard?lang=fr`

---

## Questions ouvertes

| # | Question | Impact |
|---|----------|--------|
| 1 | Quel dictionnaire anglais choisir (SOWPODS, TWL06, SOWPODS-libre) ? | Licence, taille |
| 2 | Sac commun fusionné ou sac séparé par joueur ? | Équilibre jeu |
| 3 | La lettre partagée entre deux joueurs de langues différentes vaut-elle 0 ou la valeur du poseur ? | Règles |
| 4 | L'Elo bilingue est-il comparable à l'Elo mono ? Créer une file séparée ? | Classement |
| 5 | Gérer le cas où un joueur n'a pas de langue assignée (invite, legacy session) ? | Rétro-compat |
| 6 | Interface CLI interactive : demander la langue à chaque joueur à la création ? | UX |

---

## Liens internes

- `docs/architecture/RATING_SYSTEM.md` — système Elo actuel
- `docs/architecture/RATING_INTEGRATION.md` — guide d'intégration
- `src/libs/Lama.Contracts/IGameLanguageProvider.cs`
- `src/libs/Lama.Contracts/PlayerRating.cs`
- `src/libs/Lama.Languages.fr/FrenchLanguageProvider.cs`

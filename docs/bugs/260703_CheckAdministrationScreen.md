# Vérification de l'écran d'administration

## Statut
✅ Corrigé

## Section Tableau de bord

- ✅ Le numéro de version ne correspondait pas à celle affichée dans le bandeau supérieur.
  - **Cause** : `StatusCollector` utilisait `Assembly.GetExecutingAssembly().GetName().Version` (version .NET) au lieu de la version `.build-info`.
  - **Fix** : Création de `BuildInfoConstants.cs` côté serveur (namespace `Lama.Server.Services`), utilisé par `StatusCollector.GetVersion()`. Le script `sync-to-csharp.sh` génère maintenant les deux fichiers (WebApp + Server).
- ✅ Les autres informations correspondent à la réalité (uptime, mémoire, threads, environnement, métriques DB/AIServer).

## Section Utilisateurs

- ✅ L'administrateur est connecté, pourtant aucun utilisateur n'était indiqué dans la liste.
  - **Cause** : `p.Ratings.FirstOrDefault(r => r.Queue == "open")!.EloRating` provoquait un `NullReferenceException` quand le joueur n'avait pas de rating "open". L'exception était catchée → réponse 503 → WebApp recevait `null` → liste vide.
  - **Fix** : Remplacement par `p.Ratings.Where(r => r.Queue == "open").Select(r => (decimal?)r.EloRating).FirstOrDefault() ?? 0m` (et idem pour GamesPlayed/GamesWon).
- ✅ Les résultats sont maintenant connectés à la réalité (LastLoginAt mis à jour au login, Elo/GamesPlayed/GamesWon depuis PlayerRatingEntity, IsConnected depuis GameHubState).

## Section Parties

- ✅ Une partie était en cours, pourtant aucune partie n'était indiquée dans la liste.
  - **Cause** : `s.GameId.ToString("N").ToUpperInvariant()` dans un `.Where()` EF Core n'est pas traduisible en SQL → exception → 503 → liste vide.
  - **Fix** : Materialisation des `SessionGames` d'abord (`.Take(200).ToListAsync()`), puis filtrage en mémoire avec `inMemoryIds.Contains(...)`.
- ✅ L'administrateur peut maintenant supprimer (🗑️) ou fermer (🛑) une partie en cours.
  - Nouvel endpoint `POST /api/v1/admin/games/{gameId}/close` — force la fermeture d'une partie en mémoire (EndGame + IsClosed + SSE `game.ended`).
  - Bouton 🛑 affiché uniquement pour les parties `memory` avec statut `active`/`lobby`.
- ✅ L'identité des joueurs est maintenant indiquée (pseudo + nature IA/Humain).
  - Parties en mémoire : `g.Players.Select(p => new AdminGamePlayerDto(p.PlayerName, p.IsBot))`
  - Parties en DB : jointure avec `SessionPlayersInGame`, détection IA via préfixe `bot-`.
  - Affichage : chips 🤖 pour les bots, 👤 pour les humains.

# Parties et utilisateurs persistents

- **Utilisateurs** : ✅ Persistants. Stockés dans `rating.players` (PostgreSQL via EF Core). Restaurés après redémarrage.
- **Parties terminées/abandonnées** : ✅ Persistantes. Stockées dans `sessions.games` + `sessions.players_in_game` + `history.completed_games`. Restaurées après redémarrage (visibles dans l'onglet Parties avec source "database").
- **Parties en cours** : ⚠️ Non persistantes. Vivent en mémoire dans `GameHubState` (`ConcurrentDictionary<string, OnlineGame>`). Perdues au redémarrage serveur. C'est le comportement attendu (le serveur est autoritaire en mémoire). Les tables `sessions.board_state` et `sessions.turn_log` existent mais ne sont pas écrites actuellement.


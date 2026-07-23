# Bots IA — Règles, Décisions et Fonctionnement

> **Résumé en une phrase** : Les bots Lama sont 5 joueurs IA de niveau croissant qui utilisent le même moteur de suggestions que les joueurs humains, mais appliquent une politique probabiliste (jouer / échanger / passer) calibrée par leur profil pour simuler un comportement humain et réaliste.

---

## 1. Vue d'ensemble

Le système de bots repose sur **trois couches** clairement séparées :

| Couche | Rôle | Fichier(s) principal(aux) |
|--------|------|---------------------------|
| **Identité** | Définit qui est le bot (nom, niveau, paramètres de difficulté) | `BotCatalog.cs`, `BotProfile.cs` |
| **Intelligence** | Calcule les meilleurs coups possibles (le « cerveau ») | `MoveSuggestionEngine.cs`, `SuggestionService.cs` |
| **Comportement** | Décide ce que le bot fait réellement parmi les suggestions | `BotAutoPlayService.cs`, `StartBotLoopIfNeeded` |

**Principe clé** : le moteur de suggestions est **le même** pour les bots et pour les joueurs humains (via `play.suggest`). Ce qui différencie un bon bot d'un mauvais n'est pas l'algorithme de recherche, mais **la façon dont il choisit** parmi les suggestions et **s'il décide de jouer le meilleur coup**.

---

## 2. Les 5 bots

Le catalogue est défini statiquement dans `BotCatalog.cs`. Chaque bot possède un GUID fixe (`00000000-0000-0000-0000-00000000000X`) qui garantit la persistance de son Elo en base de données.

| # | ID interne | Nom affiché | Niveau | Elo initial | Personnalité |
|---|-----------|-------------|--------|-------------|--------------|
| 1 | `bot-karim` | **B'Karim** | 1 | 700 | Débutant : joue court, échange souvent, évite les gros scores |
| 2 | `bot-sophie` | **B'Ingrid** | 2 | 1000 | Novice : encore faible, commence à exploiter quelques coups |
| 3 | `bot-thomas` | **B'Thomas** | 3 | 1300 | Intermédiaire : équilibre entre coups forts et modérés |
| 4 | `bot-leila` | **B'Liv** | 4 | 1600 | Avancé : joue généralement le meilleur coup |
| 5 | `bot-victor` | **B'Victor** | 5 | 1900 | Expert : toujours optimal, ne passe jamais |

La progression est **strictement monotone** : plus le niveau augmente, plus le bot demande de suggestions, passe moins, choisit le meilleur coup plus souvent, échange moins, et n'évite jamais les coups à gros points.

---

## 3. Comment un bot prend une décision (flux détaillé)

Quand c'est le tour d'un bot, le processus suivant s'exécute automatiquement côté serveur :

```
┌──────────────────────────────────────────────────────┐
│  1. La boucle de jeu détecte que c'est au bot        │
│     (StartBotLoopIfNeeded)                            │
│                                                       │
│  2. Délai de réflexion (600 ms hors Blitz,           │
│     8–60 s en Blitz selon le niveau)                  │
│                                                       │
│  3. Le bot demande des suggestions au cerveau IA      │
│     → MoveSuggestionEngine via AIServer               │
│     → BeamWidth suggestions (1 à 50 selon le niveau)  │
│                                                       │
│  4. Sélection du coup (SelectSuggestion)              │
│     → Filtrer / réordonner / choisir                  │
│                                                       │
│  5. Exécution : poser / échanger / passer             │
│     → Appel direct au GameEngine sous verrou          │
│                                                       │
│  6. Émission d'un événement SSE "game.move.played"    │
│     → Identique à un coup humain                      │
└──────────────────────────────────────────────────────┘
```

### 3.1 Étape de sélection (`SelectSuggestion`)

C'est ici que le niveau du bot se manifeste concrètement :

1. **Tri** : toutes les suggestions sont classées par score décroissant, puis par longueur décroissante.

2. **Filtre « gros points »** *(bots faibles uniquement)* : si le profil a un `BigMoveScoreThreshold > 0`, alors avec une probabilité `BigMoveSkipRate`, tous les coups dépassant ce seuil sont écartés — mais **uniquement s'il reste au moins une alternative**. Un bot débutant évite ainsi de « cramer » ses meilleures lettres trop tôt.

3. **Fenêtre de candidats** : on garde les `CandidateWindow` meilleurs coups restants.

4. **Mode « coup faible »** : avec une probabilité `WeakMoveRate`, le bot choisit volontairement un mauvais coup. Il prend les `WeakPoolSize` coups les plus courts et les moins rentables, et en tire un au hasard. Cela simule l'erreur humaine.

5. **Sinon** : le bot prend `candidates[0]`, c'est-à-dire le meilleur coup disponible.

### 3.2 Étape d'exécution

Une fois le coup choisi, l'arbre de décision est le suivant :

```
Aucune suggestion jouable ?
│
├─ OUI → Limites de passes atteintes OU envie d'échanger ?
│        ├─ OUI → Échanger (TrySwap)
│        └─ NON → Passer son tour (PassTurn)
│
└─ NON → Le coup choisi est-il "faible" (score ≤ seuil)
         ET le bot veut-il échanger ?
         ├─ OUI → Échanger (TrySwap)
         └─ NON → Passe intentionnelle ? (probabilité PassRate)
                  ├─ OUI (si limites non atteintes) → Passer
                  └─ NON → Valider et poser le mot
                           ├─ Valide → engine.PlayMove()
                           └─ Invalide → Échanger ou Passer
```

**Garanti** : un bot ne peut **jamais passer deux fois de suite** (`MaxConsecutivePasses = 1`). Après une passe, il est forcé d'échanger ou de tenter un placement.

---

## 4. Le cerveau : moteur de suggestions

### 4.1 Architecture

```
BotAutoPlayService
      │
      ▼
IAISuggestionClient ──── HTTP POST /suggest ────► Lama.AIServer (port 5203)
      │                                               │
      │ (si AIServer indisponible)                    ▼
      ▼                                          SuggestionService
LocalAISuggestionClient                         (SemaphoreSlim, max 3 concurrent)
      │                                               │
      ▼                                               ▼
MoveSuggestionEngine ◄──────────────────────── MoveSuggestionEngine
(même algorithme, même stratégie Balanced)
```

- **AIServer** est un microservice séparé (port 5203) qui isole les calculs CPU-intensifs du serveur de jeu.
- Si l'AIServer est indisponible (réseau, surcharge, non configuré), le serveur bascule automatiquement vers une exécution locale du même moteur (`LocalAISuggestionClient`).
- **Le résultat ne lève jamais d'exception** : en cas d'erreur, la liste est vide et le bot tombe sur la logique « aucune suggestion ».

### 4.2 Algorithme de recherche (`MoveSuggestionEngine`)

L'algorithme parcourt le **dictionnaire en entier** avec un filtrage par ancres :

1. **Premier coup** : tous les mots sont testés à toutes les positions traversant le centre du plateau (ligne/colonne 7).

2. **Coups suivants** :
   - Construction d'un **index d'ancres** : pour chaque lettre déjà présente sur le plateau, on enregistre sa position.
   - Pré-filtrage : tout mot du dictionnaire ne contenant **aucune** des lettres déjà posées est immédiatement écarté.
   - Pour chaque mot restant, on essaie tous les alignements (horizontal/vertical) qui connectent au moins une ancre.

3. **Validation** : chaque placement est validé par `MoveAnalyzer` (mots formés, adjacence, etc.).

4. **Jokers** : assignation automatique des lettres joker (`TryAssignWildcards`).

5. **Scorage** : calcul du score avec bonus de position (mot/lettre double/triple).

6. **Heuristique « Balanced »** : le score est ajusté par `ComputeBalancedScore` qui :
   - **Récompense** l'utilisation des cases bonus (mot/lettre ×2, ×3).
   - **Pénalise** l'exposition de cases bonus adjacentes à l'adversaire.
   - **Pénalise** les croisements perpendiculaires excessifs.

7. **Catégorisation** : les résultats sont divisés en deux catégories :
   - `"score"` : les coups avec le meilleur score.
   - `"length"` : les mots les plus longs (en excluant ceux déjà dans `"score"`).

### 4.3 Limites de calcul

| Paramètre | Valeur | Explication |
|-----------|--------|-------------|
| Timeout bot | **5 s** | Un bot attend au maximum 5 s une suggestion |
| Timeout humain (`play.suggest`) | **15 s** | Un joueur humain a plus de temps |
| Concurrence AIServer | **3** (défaut) | Configurable via `LAMA_AI_MAX_CONCURRENT` (1–20) |
| Surcharge AIServer | **HTTP 503** | Si tous les slots sont occupés, retour immédiat |
| Pool par requête | `topPerCategory × 2 + 4` | Nombre de candidats calculés avant catégorisation |

---

## 5. Paramètres de difficulté détaillés

Chaque profil (`BotProfile`) possède **12 paramètres** qui contrôlent son comportement. Voici la table complète :

| Paramètre | Karim (1) | Ingrid (2) | Thomas (3) | Liv (4) | Victor (5) | Signification |
|-----------|:---------:|:----------:|:----------:|:-------:|:----------:|---------------|
| `BeamWidth` | 1 | 3 | 10 | 25 | 50 | Nombre de suggestions demandées au cerveau |
| `PassRate` | 0,15 | 0,10 | 0,06 | 0,03 | 0,00 | Probabilité de passer volontairement |
| `CandidateWindow` | 8 | 8 | 8 | 6 | 6 | Nombre de meilleurs coups considérés |
| `WeakPoolSize` | 5 | 5 | 4 | 3 | 2 | Taille du sous-ensemble « faible » |
| `WeakMoveRate` | 0,90 | 0,70 | 0,40 | 0,15 | 0,00 | Probabilité de jouer volontairement un mauvais coup |
| `SwapOnNoSuggestionRate` | 0,90 | 0,75 | 0,50 | 0,25 | 0,12 | Probabilité d'échanger quand rien n'est jouable |
| `SwapOnWeakMoveRate` | 0,45 | 0,30 | 0,18 | 0,08 | 0,03 | Probabilité d'échanger au lieu d'un coup faible |
| `WeakMoveScoreThreshold` | 20 | 16 | 12 | 8 | 6 | Score en dessous duquel un coup est jugé « faible » |
| `SwapMaxLetters` | 4 | 3 | 3 | 2 | 2 | Nombre maximum de lettres échangées par tour |
| `BigMoveScoreThreshold` | 22 | 30 | 45 | 0 | 0 | Score au-dessus duquel un coup est « gros points » (0 = désactivé) |
| `BigMoveSkipRate` | 0,85 | 0,60 | 0,25 | 0,00 | 0,00 | Probabilité d'éviter un « gros coup » |
| `MaxConsecutivePasses` | 1 | 1 | 1 | 1 | 1 | Passes consécutives max avant forçage |

### Comment lire cette table

- **Karim (niveau 1)** demande seulement **1 suggestion** (`BeamWidth = 1`), ce qui limite drastiquement ses options. Dans **90 %** des cas il joue un mauvais coup (`WeakMoveRate = 0,90`), et il évite les gros scores dans **85 %** des cas. C'est un adversaire très facile.

- **Victor (niveau 5)** demande **50 suggestions**, ne rate jamais un coup (`WeakMoveRate = 0`), ne passe jamais (`PassRate = 0`) et n'évite jamais les gros scores. Il joue toujours le coup optimal parmi un large éventail.

---

## 6. Stratégie d'échange de lettres

Quand un bot décide d'échanger (`TrySwap`), il ne choisit pas ses lettres au hasard :

### 6.1 Priorité d'échange (`GetSwapPriority`)

Les lettres sont classées par priorité décroissante d'échange :

| Priorité | Lettres | Logique |
|----------|---------|---------|
| **jamais** | `*` (joker) | Un joker est trop précieux pour être échangé |
| **100** (priorité max) | J, Q, K, W, X, Y, Z | Lettres à forte valeur, difficiles à placer |
| **70** | V, F, H, B, G, M, P | Lettres moyennement difficiles |
| **55** | D, C | Valeur modérée |
| **35** | (autres consonnes) | Valeur standard |
| **20** (priorité min) | R, S, T, L, N, E, A, I, O, U | Lettres faciles à placer, à conserver |

Le bot sélectionne les lettres avec la **plus haute priorité** d'échange en premier, en brisant les égalités aléatoirement.

### 6.2 Nombre de lettres échangées

Le bot tire au hasard un nombre entre 1 et `min(SwapMaxLetters, taille du rack, lettres restantes dans la sac)`.

### 6.3 Conditions pour pouvoir échanger

- Le sac ne doit **pas** être vide (`bagCount > 0`).
- Le rack ne doit **pas** être vide.
- `SwapMaxLetters > 0`.
- Si l'échec provoque une `GameException` (règle du jeu), le bot logge en debug et retombe sur « passer ».

---

## 7. Délais de réflexion (simulation humaine)

### 7.1 Mode normal (Casual, Standard, Competitive)

**600 ms** fixe. Les bots jouent quasi-instantanément.

### 7.2 Mode Blitz

En Blitz, chaque joueur a un chronomètre. Pour garantir l'équité, les bots « consomment » du temps de réflexion de façon aléatoire et réaliste :

1. **Facteur de niveau** : `levelFactor = max(0,4 ; 1,0 − (niveau − 1) × 0,15)`
   - Niveau 1 → 1,0 (réfléchit le plus longtemps)
   - Niveau 2 → 0,85
   - Niveau 3 → 0,70
   - Niveau 4 → 0,55
   - Niveau 5 → 0,40 (réfléchit le plus vite)

2. **40 % de chance** → réflexion longue : `21–60 s × levelFactor`
3. **60 % de chance** → coup rapide : `20–30 s × levelFactor`

4. **Plancher absolu : 8 000 ms** (jamais moins de 8 secondes, quel que soit le niveau).

> Un bot expert (niveau 5) en Blitz réfléchit donc entre 8 s et 24 s, tandis qu'un bot débutant (niveau 1) peut prendre jusqu'à 60 s. Cela simule un vrai adversaire humain qui gère son temps.

---

## 8. Contraintes et règles applicables aux bots

### 8.1 Création de partie

| Règle | Détail |
|-------|--------|
| **Mode Tournament interdit** | Les bots sont **refusés** en mode Tournament (HTTP 400). |
| **Humain + bots** | Maximum **3 bots** par partie (le joueur humain occupe le 4e slot au maximum). |
| **Partie 100 % IA** | Nécessite des privilèges **admin** ; entre **2 et 4 bots**. |
| **Nombre total de joueurs** | Toujours borné entre **2 et 4**. |
| **Démarrage automatique** | Si assez de participants (humains + bots) sont présents pour atteindre `maxPlayers`, la partie démarre sans attendre. |

### 8.2 Résolution des bots sélectionnés (`SelectBots`)

Priorité de résolution lors de la création :
1. Si `AiBotIds` est fourni → utilise exactement ces bots (dédupliqués).
2. Sinon si `AiBotCount` est fourni → remplit les slots depuis `BotCatalog.All` dans l'ordre.
3. Sinon si `EnableAi = true` ou `AiBotId` est fourni → ajoute 1 bot.

Le remplissage se fait en parcourant `BotCatalog.All` dans l'ordre (Karim → Ingrid → Thomas → Liv → Victor), en plaçant d'abord les bots explicitement demandés.

### 8.3 Règles de jeu

- Les bots sont soumis aux **mêmes règles** que les joueurs humains : validation par `MoveAnalyzer`, `MoveValidator`, etc.
- Si une suggestion s'avère invalide après re-validation par le moteur, le bot logge un avertissement et tombe sur échanger / passer.
- Un bot **ne peut pas tricher** : il ne peut poser que des mots que le moteur de jeu valide.
- Les bots **n'utilisent pas** `play.suggest` (réservé aux humains) — ils interrogent directement l'`IAISuggestionClient` avec un timeout de 5 s.

### 8.4 Équité SSE

Les coups des bots sont émis via le **même événement SSE** (`game.move.played`) que les coups humains. Les clients (WebApp, CLI) ne peuvent distinguer un bot d'un humain que par le flag `IsBot` et le nom du joueur (`B'Karim`, etc.).

---

## 9. Persistance et évolution de l'Elo

### 9.1 Seeding au démarrage

Au démarrage du serveur (`Program.cs`), pour chaque bot du catalogue :
- Un `PlayerEntity` est inséré en base avec `PlayerId = BotGuid` et `Username = bot.Name` (s'il n'existe pas déjà).
- Un `PlayerRatingEntity` est inséré dans la file `"open"` avec `EloRating = bot.InitialElo` (s'il n'existe pas déjà).

> La base de données peut être absente (en dev) — le seeding échoue silencieusement sans bloquer le démarrage.

### 9.2 Évolution de l'Elo

Les bots participent **pleinement** au système de classement :
- À la fin d'une partie, `GameResult` est créé pour chaque bot avec son `PersistentPlayerId` (= `BotGuid`).
- L'Elo est mis à jour via `PlayerRatingService` avec la même formule que les humains (K=40, K=20 au-dessus de 2400).
- Les bots apparaissent dans les classements (leaderboards) comme n'importe quel joueur.

**Exceptions** : si la partie est `CasualUnranked` ou si des suggestions ont été utilisées par un joueur humain (désactive l'Elo pour cette partie), le classement n'est pas impacté.

---

## 10. API publique du catalogue

Deux endpoints en lecture seule, **sans authentification** :

```
GET /bots          → { bots: [{ botId, name, level, initialElo }, ...] }
GET /bots/{botId}  → { botId, name, level, initialElo }
```

> **Note** : les paramètres de difficulté (`BeamWidth`, `PassRate`, etc.) **ne sont jamais exposés** publiquement. Seuls l'identité et l'Elo initial sont visibles.

La WebApp consomme cette API via `LamaApiClient.GetBotsAsync()` pour alimenter l'interface de sélection de bots à la création de partie.

---

## 11. Synthèse : pourquoi les bots semblent humains

| Mécanisme | Effet |
|-----------|-------|
| `WeakMoveRate` | Les bots faibles font parfois des erreurs volontaires |
| `BigMoveSkipRate` | Les bots faibles ne « crament » pas leurs meilleures lettres |
| `PassRate` | Certains bots passent leur tour occasionnellement |
| Délais Blitz aléatoires | Les bots ne jouent pas instantanément, imitant la réflexion humaine |
| Priorité d'échange intelligente | Les bots ne jettent pas leurs jokers ni leurs voyelles utiles |
| `MaxConsecutivePasses = 1` | Un bot ne reste jamais bloqué à passer indéfiniment |
| Même moteur que les humains | Les mots proposés sont toujours valides et plausibles |

Cette combinaison de **probabilités calibrées** et de **délais réalistes** produit un comportement qui, sans être parfait, donne l'illusion d'affronter des adversaires de compétences variées — du débutant qui galère à l'expert implacable.

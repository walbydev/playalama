# Add Users management section to administration page

## Statut
✅ Implémenté

## Changes
- Create new real administration page that contains :
  - Users management section
  - Games management section
  - Current Status Dashboard section

## Needs
- Show all current connected users
- Show all daily connected users
- Show all users with their last connection date
- Show all inactive users (not connected last 60 days)
- Gives remove user button for each user

## Gameplay change games persistence
- Persist all current and finished games for each user, and show them in the user management page
- The user can show all his games in "Mes parties" page, and can delete them if he wants to

## Implémentation
- **Serveur** : `AdminEndpoints.cs` (nouveau) — endpoints `/api/v1/admin/users` (avec filtres connected/daily/inactive), `/api/v1/admin/games` (mémoire + DB), `DELETE /api/v1/admin/users/{id}`, `DELETE /api/v1/admin/games/{id}`
- **Serveur** : `PlayerEntity` + champ `LastLoginAt` (mis à jour au login)
- **Serveur** : `PlayerEndpoints` — `DELETE /api/v1/players/me/games/{gameId}` (suppression d'une partie de son propre historique)
- **Serveur** : `GameHubState.GetActivePlayerIds()` pour détecter les joueurs connectés
- **WebApp** : `Admin.razor` (nouveau, route `/admin` + `/status`) — 3 onglets : Dashboard, Users, Games
- **WebApp** : `MyGames.razor` — bouton suppression par partie
- **WebApp** : `LamaApiClient` — méthodes `GetAdminUsersAsync`, `DeleteAdminUserAsync`, `GetAdminGamesAsync`, `DeleteAdminGameAsync`, `DeleteMyGameAsync`
- **WebApp** : `Footer.razor` — lien `/admin` (gardé sous guard `Auth.IsAdmin`)
- **WebApp** : Traductions `Admin.*` (fr/en/de) — ~45 clés
 
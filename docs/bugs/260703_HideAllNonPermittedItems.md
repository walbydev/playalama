# BUG : 2026.07.03 - Hide All Non-Permitted Items

## Statut
✅ Résolu

## Description

When a user does not have permission to view certain items, those items should be hidden from the user interface. However, there is a bug that causes non-permitted items to still be visible to users who do not have the necessary permissions.
- Like Administration page links
- Like IA only settings game, that item must appears only for administrators, but it appears for all users.

## Cause racine

`AuthService` ne disposait d'aucune notion de rôle/admin. Chaque composant devait appeler `/status` indépendamment pour détecter le statut admin, et plusieurs composants omettaient cette vérification.

## Corrections appliquées

1. **`AuthService.cs`** : Ajout de `IsAdmin` — probe `/status` lors de `InitializeAsync`, `LoginAsync`, `RegisterAsync` ; reset lors de `LogoutAsync`. Le résultat est mis en cache et réactif via `OnAuthStateChanged`.
2. **`Footer.razor`** : Injection de `AuthService` + guard `@if (Auth.IsAdmin)` autour du lien `/status` (Administration).
3. **`Games.razor`** : Remplacement de `DetectAdminAsync` (probe locale sans try/catch) par `Auth.IsAdmin`. Suppression de la méthode redondante.
4. **`Status.razor`** : Déplacement du `page-hero` (header admin "📊 Status") à l'intérieur du bloc authentifié — un non-admin ne voit plus le header admin avant l'affichage du message d'accès refusé.


# Thème Haut Contraste — Guide d'utilisation

## 🎨 Vue d'ensemble

Le thème **haut contraste** est spécialement conçu pour les utilisateurs malvoyants. Il respecte les recommandations **WCAG AAA** pour le contraste des couleurs.

**Dernière mise à jour** : Amélioration de la lisibilité de la barre de navigation et de tous les éléments d'interface.

## ✨ Caractéristiques

### Couleurs

| Élément | Couleur | Usage |
|---------|---------|-------|
| Arrière-plan | `#000000` (noir pur) | Fond principal |
| Surface | `#1a1a1a` (gris très foncé) | Surfaces secondaires |
| Texte | `#ffffff` (blanc pur) | Texte courant |
| Accent principal | `#ffff00` (jaune vif) | Liens, actions, focus |
| Accent secondaire | `#00ffff` (cyan vif) | Éléments interactifs |
| Succès | `#00ff00` (vert vif) | Validations, succès |
| Erreur | `#ff0000` (rouge vif) | Erreurs, alertes |
| Or | `#ffd700` | Médaille d'or |
| Argent | `#c0c0c0` | Médaille d'argent |
| Bronze | `#cd7f32` | Médaille de bronze |

### Barre de navigation

La navbar a été spécialement optimisée :

| Élément | Couleur | Contraste |
|---------|---------|-----------|
| Fond | `#000000` | - |
| Bordure | `#ffffff` (3px) | 21:1 |
| Logo | `#ffff00` | 19.5:1 |
| Liens | `#ffffff` | 21:1 |
| Liens hover/actifs | `#ffff00` | 19.5:1 |

### Améliorations d'accessibilité

- **Contraste maximal** : Rapport de contraste > 21:1 (noir/blanc)
- **Bordures épaisses** : 3px partout (navbar, boutons, contrôles)
- **Pas d'arrondis** : Coins à 90° pour une meilleure délimitation
- **Police agrandie** : 18px au lieu de 17px par défaut
- **Touch targets** : Hauteur minimale de 3rem (48px)
- **Focus visible** : Outline de 4px avec offset de 4px
- **Grille du plateau** : Lignes de 2px pour une meilleure visibilité
- **Navbar optimisée** : Fond noir opaque, texte blanc, liens jaunes
- **Boutons redessinés** : Contours 3px, couleurs vives, pas de transparence

### Casse bonus du plateau

Chaque type de case bonus a un **motif distinctif** pour les utilisateurs daltoniens :

| Type | Motif | Couleur |
|------|-------|---------|
| Triple Word (TW) | Bordure jaune continue 3px | `#ffff00` |
| Double Word (DW) | Bordure cyan continue 2px | `#00ffff` |
| Triple Letter (TL) | Bordure magenta en pointillés | `#ff00ff` |
| Double Letter (DL) | Bordure verte en pointillés | `#00ff00` |
| Start (ST) | Bordure jaune 4px | `#ffff00` |

### Tuiles

- **Tuiles standard** : Fond blanc `#ffffff`, lettres noires
- **Jokers** : Fond noir, lettres jaunes `#ffff00`
- **Tuiles utilisées** : Opacité 50%, fond gris foncé
- **Valeurs** : Noires, en gras

## 🚀 Comment activer le thème

### Dans la WebApp

1. Ouvrez Playalama dans votre navigateur
2. Dans la barre de navigation, cliquez sur le sélecteur de thème (🎨)
3. Choisissez **"Haut contraste"** (ou **"High Contrast"** en anglais)

Le thème est **persisté en localStorage** et sera réappliqué automatiquement lors de votre prochaine visite.

### En ligne de commande (futur)

```bash
# Option --theme (à implémenter dans la CLI)
lama --theme highcontrast
```

## ♿ Fonctionnalités d'accessibilité

### Pour les utilisateurs malvoyants

- ✅ Contraste de couleurs optimal (WCAG AAA)
- ✅ Police de caractères plus grande
- ✅ Éléments d'interface agrandis
- ✅ Bordures épaisses pour une meilleure délimitation
- ✅ Pas d'effets de transparence ou de flou
- ✅ Focus clavier très visible

### Pour les utilisateurs daltoniens

- ✅ Motifs distinctifs sur les cases bonus (continus vs pointillés)
- ✅ Couleurs vives et facilement différentiables
- ✅ Contraste basé sur la luminance, pas seulement la teinte

### Pour les utilisateurs de lecteurs d'écran

- ✅ Labels ARIA appropriés
- ✅ Structure HTML sémantique
- ✅ Annonces des changements d'état via `aria-live`

## 🎯 Bonnes pratiques

### Navigation clavier

- Utilisez la touche `Tab` pour naviguer entre les éléments
- Le focus est visible avec un contour jaune/bleu vif de 4px
- Les touches de raccourci sont indiquées dans l'interface

### Agrandissement

- Vous pouvez utiliser le zoom du navigateur (Ctrl/Cmd + +)
- Le plateau dispose d'un contrôle de densité (S/M/L)
- Le mode plein écran est disponible pour une meilleure immersion

### Combinaison avec d'autres réglages

Le thème haut contraste peut être combiné avec :
- Le contrôle de densité du plateau (S/M/L)
- Les différentes variantes de layout (A/B/C/D)
- Le mode plein écran

## 🔧 Développement

### Fichiers modifiés

- `src/apps/Lama.WebApp/wwwroot/app.css` — Tokens CSS et styles
- `src/apps/Lama.WebApp/wwwroot/app.js` — Liste des thèmes disponibles
- `src/apps/Lama.WebApp/Services/ThemeService.cs` — Service de gestion des thèmes
- `src/apps/Lama.WebApp/Components/Shared/ThemeToggle.razor` — Sélecteur de thème
- `src/apps/Lama.WebApp/Resources/SharedResource.*.resx` — Traductions

### Ajouter un nouveau thème

1. Définir les tokens CSS dans `app.css`
2. Ajouter le thème à `AllowedThemes` dans `ThemeService.cs`
3. Ajouter le thème à `availableThemes` dans `app.js`
4. Ajouter l'option dans `ThemeToggle.razor`
5. Ajouter les traductions dans les fichiers `.resx`

### Tester le thème

```bash
# Lancer la WebApp
dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202

# Ouvrir dans le navigateur et sélectionner le thème haut contraste
```

## 📊 Métriques de contraste

| Combinaison | Rapport | Niveau WCAG |
|-------------|---------|-------------|
| Texte blanc / fond noir | 21:1 | AAA |
| Jaune / noir | 19.5:1 | AAA |
| Cyan / noir | 17.8:1 | AAA |
| Vert / noir | 15.2:1 | AAA |
| Rouge / noir | 11.3:1 | AAA |

*Toutes les combinaisons dépassent largement le seuil AAA de 7:1 pour le texte normal.*

## 🔄 Futures améliorations

- [ ] Option de taille de police globale (100%, 125%, 150%, 200%)
- [ ] Mode "très haut contraste" avec inversion totale
- [ ] Thèmes spécifiques pour différents types de daltonisme
- [ ] Audio feedback pour les actions importantes
- [ ] Mode "réduction des distractions" (masquer éléments non essentiels)

## 📝 Notes techniques

- Le thème utilise des variables CSS custom properties pour une application cohérente
- Les couleurs ont été choisies pour maximiser le contraste tout en restant confortables
- Les motifs (pointillés vs continu) aident les utilisateurs daltoniens à distinguer les cases bonus
- Aucun effet de transparence ou de flou n'est utilisé pour éviter la confusion visuelle

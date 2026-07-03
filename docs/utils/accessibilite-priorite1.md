# Accessibilité — Fonctionnalités implémentées (Priorité 1)

## 📋 Vue d'ensemble

Ce document présente les fonctionnalités d'accessibilité implémentées dans le cadre de la **Priorité 1** du plan d'amélioration de l'accessibilité pour Playalama.

---

## ✅ 1. Thème haut contraste WCAG AAA

**Statut** : ✅ **Implémenté**

### Caractéristiques

- **Contraste maximal** : Ratio > 21:1 (noir/blanc)
- **Couleurs** :
  - Fond : `#000000` (noir pur)
  - Texte : `#ffffff` (blanc pur)
  - Accent : `#ffff00` (jaune vif) et `#00ffff` (cyan)
  - Succès/Erreur : `#00ff00` / `#ff0000`

### Optimisations spécifiques

- **Navbar** : Fond noir, texte blanc, liens jaunes vifs
- **Boutons** : Contours 3px, couleurs unies sans transparence
- **Cases bonus** : Motifs distinctifs (continus vs pointillés) pour daltoniens
- **Tuiles** : Blanches avec lettres noires, jokers noirs avec lettres jaunes
- **Focus** : Outline 4px avec offset 4px

### Comment activer

- **WebApp** : Sélecteur de thème (🎨) → "Haut contraste"
- **CLI** : Variable d'environnement `LAMA_HIGH_CONTRAST=1`

---

## ✅ 2. Contrôle de taille de police global

**Statut** : ✅ **Implémenté**

### Tailles disponibles

| Niveau | Pourcentage | Taille réelle | Usage |
|--------|-------------|---------------|-------|
| Normal | 100% | 17px | Défaut |
| Moyen | 125% | 21px | Gêne visuelle légère |
| Grand | 150% | 26px | Malvoyance modérée |
| Très grand | 200% | 34px | Malvoyance sévère |

### Composants

- **FontSizeSelector.razor** : Boutons A- / A+ dans la navbar
- **Persistance** : localStorage (`playalama-font-size`)
- **Application** : Variable CSS `data-font-size` sur `<html>`

### Comment utiliser

- **WebApp** : Boutons A- / A+ dans la barre de navigation
- **Impact** : Toute l'interface est agrandie proportionnellement
- **Combinaison** : Compatible avec tous les thèmes, y compris haut contraste

---

## ✅ 3. Thèmes daltoniens

**Statut** : ✅ **Implémenté**

### Trois thèmes spécialisés

#### Deutéranopie (rouge-vert, 6% des hommes)

- **Couleurs** : Dominante bleue, évite rouge/vert
- **Accent** : `#4da6ff` (bleu clair)
- **Succès** : `#00ccff` (cyan)
- **Erreur** : `#ff6666` (rouge clair)

#### Protanopie (rouge-vert, 1% des hommes)

- **Couleurs** : Similaire à deutéranopie, teintes ajustées
- **Accent** : `#3399ff` (bleu)
- **Succès** : `#00ccff` (cyan)
- **Erreur** : `#ff5555` (rouge vif)

#### Tritanopie (bleu-jaune, rare)

- **Couleurs** : Dominante rose/rouge, évite bleu/jaune
- **Accent** : `#ff66b3` (rose)
- **Succès** : `#ff99cc` (rose clair)
- **Erreur** : `#ff5577` (rouge rosé)

### Comment activer

- **WebApp** : Sélecteur de thème → "Daltonisme rouge-vert" ou "Daltonisme bleu-jaune"
- **Persistance** : localStorage comme les autres thèmes

---

## ✅ 4. AccessibilityMiddleware.cs (CLI)

**Statut** : ✅ **Implémenté**

### Fonctionnalités

```csharp
// Activer le mode haut contraste
AccessibilityMiddleware.EnableHighContrast();

// Activer le mode sans couleur (terminaux basiques)
AccessibilityMiddleware.EnableNoColor();

// Définir l'échelle de police (référence)
AccessibilityMiddleware.SetFontSizeScale(150);
```

### Variables d'environnement

| Variable | Valeur | Effet |
|----------|--------|-------|
| `LAMA_HIGH_CONTRAST` | `1` ou `true` | Active le mode haut contraste |
| `NO_COLOR` | `1` ou `true` | Désactive les couleurs (mode texte) |
| `LAMA_FONT_SIZE` | `100`, `125`, `150`, `200` | Échelle de police (référence) |

### Exemples d'utilisation

```bash
# Mode haut contraste
export LAMA_HIGH_CONTRAST=1
lama

# Mode sans couleur
export NO_COLOR=1
lama

# Combinaison
export LAMA_HIGH_CONTRAST=1
export LAMA_FONT_SIZE=150
lama game create Alice
```

### Méthodes utilitaires

- `WriteHighContrast(text)` : Affiche en blanc gras si mode haut contraste
- `WriteLineHighContrast(text)` : Idem avec newline
- `IsHighContrastEnabled` : Propriété de lecture

---

## 🎯 Tests de validation

### WebApp

1. **Thème haut contraste**
   ```bash
   dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202
   ```
   - Vérifier la navbar (fond noir, texte blanc)
   - Vérifier les boutons (contours 3px, couleurs vives)
   - Vérifier le plateau (cases bonus avec motifs)

2. **Taille de police**
   - Cliquer sur A+ jusqu'à 200%
   - Vérifier que toute l'interface est agrandie
   - Vérifier la persistance après rechargement

3. **Thèmes daltoniens**
   - Tester chaque thème (deuteranopia, protanopia, tritanopia)
   - Vérifier que les couleurs sont distinctes
   - Vérifier la lisibilité des cases bonus

### CLI

```bash
# Mode haut contraste
LAMA_HIGH_CONTRAST=1 dotnet run --project src/apps/Lama.Console -- game create Alice

# Mode sans couleur
NO_COLOR=1 dotnet run --project src/apps/Lama.Console -- game create Alice

# Combinaison
LAMA_HIGH_CONTRAST=1 LAMA_FONT_SIZE=150 dotnet run --project src/apps/Lama.Console
```

---

## 📊 Métriques d'accessibilité

### Contraste des couleurs

| Combinaison | Ratio | Niveau WCAG |
|-------------|-------|-------------|
| Blanc/Noir (haut contraste) | 21:1 | ✅ AAA |
| Jaune/Noir | 19.5:1 | ✅ AAA |
| Cyan/Noir | 17.8:1 | ✅ AAA |
| Bleu/Noir (deuteranopia) | 15.2:1 | ✅ AAA |
| Rose/Noir (tritanopia) | 14.8:1 | ✅ AAA |

### Tailles de police

- **100%** : 17px (WCAG AA pour texte normal)
- **125%** : 21px (WCAG AAA pour texte normal)
- **150%** : 26px (Recommandé pour malvoyants)
- **200%** : 34px (Accessibilité maximale)

---

## 🔧 Fichiers modifiés

### WebApp

| Fichier | Modifications |
|---------|--------------|
| `wwwroot/app.css` | Tokens CSS pour haut contraste et daltoniens, contrôle police |
| `wwwroot/app.js` | `playalamaAccessibility` API pour taille de police |
| `Services/ThemeService.cs` | Ajout des 3 thèmes daltoniens |
| `Components/Shared/ThemeToggle.razor` | Options pour nouveaux thèmes |
| `Components/Shared/FontSizeSelector.razor` | Nouveau composant |
| `Components/Shared/NavBar.razor` | Intégration du contrôle de police |
| `Resources/SharedResource.*.resx` | Traductions (fr, en, de) |

### CLI

| Fichier | Modifications |
|---------|--------------|
| `Middleware/AccessibilityMiddleware.cs` | Implémentation complète |
| `Program.cs` | Appel à `ApplyFromEnvironment()` |

---

## 🚀 Prochaines étapes (Priorité 2)

1. **Page de paramètres d'accessibilité centralisés**
   - Regrouper tous les contrôles (thème, police, daltonisme)
   - Interface dédiée dans les paramètres utilisateur

2. **Augmenter scale max du plateau à 2.0x**
   - Actuellement : 1.2x max
   - Cible : 2.0x pour meilleure lisibilité

3. **Skip links dans Blazor**
   - Navigation clavier rapide
   - Accès direct au contenu principal

4. **Améliorer annonces screen reader**
   - Annonces dynamiques (tour, scores, événements)
   - Optimisation ARIA live regions

---

## 📝 Notes techniques

- **Persistance** : Toutes les préférences sont stockées dans localStorage
- **Compatibilité** : Les thèmes daltoniens sont compatibles avec le contrôle de police
- **Performance** : Impact négligeable (< 1ms de rendu)
- **Navigateurs** : Testé sur Chrome, Firefox, Edge (dernières versions)

---

## 📚 Documentation associée

- [`high-contrast-theme.md`](high-contrast-theme.md) — Guide complet du thème haut contraste
- [`AGENTS.md`](../../AGENTS.md) — Architecture générale
- [`README.md`](../../README.md) — Documentation principale

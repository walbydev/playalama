# Règles des Croisements

## Introduction

Comme au Scrabble classique, LAMA permet de poser des mots qui **croisent** des mots existants.
Un croisement se produit quand un mot nouveau partage une ou plusieurs lettres avec des mots déjà placés sur le plateau.

## Règles des croisements

### 1. La lettre doit correspondre
Quand vous posez un mot qui occupe une case déjà occupée, la lettre doit être **exactement la même**.

**Exemple valide :**
```
Plateau avant :
  H G F E D C B A
8 . . . . . . . .
7 . . . . . . . .
6 . . . . . . . .
5 . . . . . . . .
4 . . . . . . . .
3 . . . . . . . .
2 . . . . . . . .
1 L A M A . . . .
  (LAMA positif horizontal en A1-D1)

Tentative : poser MAISON verticalement en A2-A7
  - A2 = M (correspond au M de LAMA ✓)
  - A3 = A (nouvelle lettre ✓)
  - A4 = I (nouvelle lettre ✓)
  - A5 = S (nouvelle lettre ✓)
  - A6 = O (nouvelle lettre ✓)
  - A7 = N (nouvelle lettre ✓)

Résultat : VALIDE ✓
```

### 2. Impossible de poser une lettre incompatible
Si la lettre existante ne correspond pas, le placement est rejeté.

**Exemple invalide :**
```
Tentative : poser POISON verticalement en A2-A7
  - A2 = P (incompatible avec M de LAMA ✗)

Résultat : REJETÉ
Message d'erreur : "À la case A2, la lettre 'M' existe déjà. 
Vous tentez de placer 'P'. Pour un croisement valide, 
les lettres doivent être identiques."
```

### 3. Au moins une lettre doit être nouvelle
Un coup ne peut pas être constitué uniquement de croisements.
Il faut placer **au minimum une lettre nouvelle** (même si les autres lettres croisent des lettres existantes).

## Comment spécifier un croisement

### Via la commande CLI

Utilisez simplement la commande `play move` avec le mot complet :

```bash
# Poser MAISON verticalement en A2, sachant que A2 va croiser avec une lettre existante
lama play move A2 MAISON V
```

Le système valide automatiquement que chaque position :
- Soit est vide (nouvelle lettre)
- Soit contient la même lettre (croisement valide)

### En cas d'erreur

Si une lettre ne correspond pas, vous verrez :
```
[play move] Coup invalide : À la case C5, la lettre 'T' existe déjà. 
Vous tentez de placer 'P'. Pour un croisement valide, les lettres doivent être identiques.
```

## Pièges courants

### Piège 1 : Confondre position cible et lettre
```bash
# FAUX - on place MAISON à partir de A1, mais cela occupe A1-A5 (vertical)
lama play move A1 MAISON V

# CORRECT - on place MAISON à partir de A2, qui croise M de LAMA en A1
lama play move A2 MAISON V
```

### Piège 2 : Oublier que tous les croisements doivent être valides
Si vous placez un mot long qui croise plusieurs mots existants, **chaque croisement** doit avoir une lettre correspondante.

### Piège 3 : Tenter de poser uniquement des croisements
```bash
# INVALIDE - aucune lettre nouvelle
# (même si A2=M croise correctement avec LAMA)
lama play move A2 M V
```

## Scoring avec croisements

Le score tient compte :
- Des **nouvelles lettres** posées
- Des **multiplicateurs** sur les nouvelles cases
- De **tous les mots formés** (mot principal + croisements secondaires)

Voir [`docs/scoring-rules.md`](scoring-rules.md) pour les détails.

## Stratégie et conseil

1. **Contrôlez le plateau** : en posant des lettres clés (S, E, R, etc.), vous rendez possible des croisements futurs
2. **Utilisez les jokers intelligemment** : pour forcer des croisements à des positions précises
3. **Vérifiez les mots formés** : les croisements forment souvent des mots secondaires — ils doivent tous être valides !
4. **Consultez le dictionnaire** : avant de jouer, utilisez `lama dict check MOT` pour vérifier

## Exemples de jeu complet

```bash
# Tour 1 : Poser LAMA horizontal en H8 (premier mot, doit passer par H8)
lama play move H8 LAMA H
# Score : calcul des points LAMA

# Tour 2 (adversaire) : Poser TALON vertical en H8 qui croise le A
# A (H8) correspond déjà au A de LAMA
lama play move H8 TALON V
# Score : points TALON + points de croisement

# Tour 3 : Poser MAINS horizontal en E8 qui croise le A de LAMA (en H8)
lama play move E8 MAINS H
# E8=M, F8=A, G8=I, H8=N (croisement avec A de LAMA), I8=S
# Score : points MAINS + points de croisement
```

## Références

- [`README.md`](../README.md) — règles générales
- [`scoring-rules.md`](scoring-rules.md) — détails du scoring


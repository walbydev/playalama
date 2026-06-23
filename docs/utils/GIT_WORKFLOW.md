# Git Workflow — Feature Branch

Guide de référence pour créer une branche de feature et la fusionner dans `master`.

---

## 1. Créer une branche feature

```bash
# Se positionner sur master à jour
git checkout master
git pull origin master

# Créer et basculer sur la nouvelle branche
git checkout -b feature/nom-de-la-feature
```

---

## 2. Travailler sur la feature

```bash
# Vérifier l'état du dépôt
git status

# Stager les modifications
git add .                        # Tous les fichiers modifiés
git add src/chemin/vers/fichier  # Ou fichier par fichier

# Committer
git commit -m "feat: description courte de la feature

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"

# Pousser la branche sur le remote
git push -u origin feature/nom-de-la-feature
```

> **Tip :** Pousser régulièrement pour sauvegarder et permettre les code reviews.

---

## 3. Rester synchronisé avec master (optionnel mais recommandé)

Si master a évolué pendant le développement :

```bash
git fetch origin
git rebase origin/master
# ou
git merge origin/master
```

---

## 4. Fusionner la feature dans master

### Option A — Merge classique (conserve l'historique des commits)

```bash
git checkout master
git pull origin master
git merge feature/nom-de-la-feature
git push origin master
```

### Option B — Merge avec squash (un seul commit propre dans master)

```bash
git checkout master
git pull origin master
git merge --squash feature/nom-de-la-feature
git commit -m "feat: description de la feature fusionnée"
git push origin master
```

### Option C — Rebase puis fast-forward (historique linéaire)

```bash
git checkout feature/nom-de-la-feature
git rebase origin/master

git checkout master
git merge --ff-only feature/nom-de-la-feature
git push origin master
```

---

## 5. Nettoyer après fusion

```bash
# Supprimer la branche locale
git branch -d feature/nom-de-la-feature

# Supprimer la branche remote
git push origin --delete feature/nom-de-la-feature
```

---

## Résumé rapide

| Étape | Commande |
|-------|----------|
| Créer la branche | `git checkout -b feature/xxx` |
| Committer | `git add . && git commit -m "feat: ..."` |
| Pousser | `git push -u origin feature/xxx` |
| Fusionner | `git checkout master && git merge feature/xxx` |
| Nettoyer | `git branch -d feature/xxx` |

---

## Conventions de nommage

| Préfixe | Usage |
|---------|-------|
| `feature/` | Nouvelle fonctionnalité |
| `fix/` | Correction de bug |
| `chore/` | Maintenance, refactoring |
| `docs/` | Documentation uniquement |
| `test/` | Ajout ou correction de tests |

# 🎯 ACTIVATION RAPIDE de l'alias `lama` dans Rider

## TL;DR : Activation immédiate (30 secondes)

### Dans le terminal Rider :
```bash
source ~/.bashrc
alias lama
```

Si ça n'affiche rien, faites :
```bash
source /home/philippe/RiderProjects/Games/Lama/.lamarc
```

Voilà ! L'alias est maintenant actif. Testez :
```bash
lama --help
```

---

## ⚙️ Pour que ce soit PERMANENT (une seule fois)

Ouvrez votre `~/.bashrc` avec votre éditeur :
```bash
nano ~/.bashrc
```

Collez à la fin :
```bash
# LAMA Configuration
source /home/philippe/RiderProjects/Games/Lama/.lamarc
```

Sauvegardez (Ctrl+O, Enter, Ctrl+X).

**Fait !** À chaque nouveau terminal Rider, l'alias sera automatiquement chargé.

---

## 🧪 Tests après activation

```bash
# Vérifier l'alias
type lama

# Affichage de l'aide
lama --help

# Lancer le mode interactif
lama interactive

# Créer une partie test
lama game create TestPlayer
```

---

## ⚠️ Cas particulier : Dans Rider avec shell autre que bash

Si Rider utilise un autre shell (zsh, fish, etc.), adaptez :

**Pour zsh** : Ajouter la source dans `~/.zshrc` au lieu de `~/.bashrc`
**Pour fish** : Ajouter la source dans `~/.config/fish/config.fish`

---

## 🛠️ Autres alias utiles après activation

```bash
lama-build          # Build complet
lama-test           # Tests unitaires
lama-test-cli       # Tests CLI avec verbosité
lama-clean          # Nettoyer les builds
```

Tous définis dans `/home/philippe/RiderProjects/Games/Lama/.lamarc`

---

**Besoin d'aide ?** Voir `docs/ALIAS_LAMA_CONFIG.md` pour le guide complet.


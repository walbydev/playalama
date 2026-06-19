# Configuration de l'alias `lama` pour Rider

Ce guide explique comment rendre l'alias `lama` permanent et actif dans Rider pour le débogage et les tests.

## 📋 Aperçu

L'alias `lama` remplace l'appel long :
```bash
dotnet run --project /home/philippe/RiderProjects/Games/Lama/src/Console/Lama.Console/Lama.Console.csproj --
```

Par une commande simple :
```bash
lama game create Alice
```

## 🚀 Option 1 : Activation permanente dans ~/.bashrc (Recommandé)

C'est la solution la plus simple et la plus robuste.

### Étape 1 : Ajouter la source du fichier `.lamarc` dans votre `~/.bashrc`

Ouvrez votre `~/.bashrc` :
```bash
nano ~/.bashrc
```

Ajoutez les lignes suivantes à la fin du fichier :
```bash
# Configuration LAMA (alias permanent)
if [ -f "/home/philippe/RiderProjects/Games/Lama/.lamarc" ]; then
    source /home/philippe/RiderProjects/Games/Lama/.lamarc
fi
```

Sauvegardez et fermez (Ctrl+O, Entrée, Ctrl+X pour nano).

### Étape 2 : Recharger votre bash

```bash
source ~/.bashrc
```

ou simplement ouvrir un nouveau terminal.

### Étape 3 : Vérifier que c'est activé

```bash
alias lama
# Sortie attendue : alias lama='dotnet run --project ... --'
```

## 🎯 Option 2 : Activation manuelle dans Rider (Pour cette session)

Si vous voulez l'alias uniquement pour cette session de debug Rider :

### Dans le terminal intégré de Rider :

1. Ouvrez le terminal de Rider (Alt+Backtick ou View → Tool Windows → Terminal)
2. Tapez :
```bash
source /home/philippe/RiderProjects/Games/Lama/.lamarc
```

L'alias sera actif **uniquement dans ce terminal** pour cette session.

## 🔧 Option 3 : Utiliser directement le script setup-alias.sh

Si vous préférez exécuter le script manuellement :

```bash
source /home/philippe/RiderProjects/Games/Lama/tools/scripts/setup-alias.sh
```

## 📝 Configuration Rider pour l'autocomplétion

Pour que Rider reconnaisse l'alias `lama` dans le terminal :

1. **Aller à** : Rider Preferences → Tools → Terminal
2. **Vérifier** que le shell par défaut est `/bin/bash`
3. **Ajouter** une configuration de shell startup (si disponible) :
   ```
   source ~/.bashrc
   ```

## ✅ Utilisation après activation

Une fois l'alias activé :

```bash
# Mode interactif (défaut)
lama

# Créer une partie
lama game create Alice

# Poser un mot
lama play move H8 MAISON H

# Afficher le plateau
lama show board

# Tests CLI
lama-test-cli

# Build complet
lama-build
```

## 🐛 Dépannage

### L'alias n'est pas reconnu dans Rider

**Solution** : Dans le terminal Rider, tapez manuellement :
```bash
source ~/.bashrc  # ou source /home/philippe/RiderProjects/Games/Lama/.lamarc
```

### Le script ne s'exécute pas avec "permission denied"

**Solution** : Rendez le script exécutable :
```bash
chmod +x /home/philippe/RiderProjects/Games/Lama/tools/scripts/setup-alias.sh
```

### Erreur "command not found: dotnet"

**Vérification** : 
- .NET SDK est bien installé : `dotnet --version`
- Si absent, installez-le depuis [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

## 📚 Alias supplémentaires

Le fichier `.lamarc` inclut plusieurs alias utiles :

| Alias | Commande |
|-------|----------|
| `lama` | Lancer le jeu |
| `lama-build` | Construire le projet complet |
| `lama-test` | Exécuter tous les tests |
| `lama-test-cli` | Exécuter les tests CLI avec détails |
| `lama-clean` | Nettoyer tous les fichiers compilés |

## 💡 Astuce pour Rider : Configuration du "Run Configuration"

Vous pouvez aussi configurer Rider pour lancer le projet avec un raccourci clavier :

1. **Edit Run Configurations** (Rider → Menu Run → Edit Configurations)
2. **Créer une nouvelle configuration** :
   - **Type** : .NET Project
   - **Project** : Select Lama.Console
   - **Program arguments** : (vides ou `interactive`)

Ensuite, vous pouvez lancer avec Shift+F10 ou un raccourci personnalisé.

---

**Note** : Cette configuration est locale à votre machine. Pour que d'autres développeurs en profitent, cette documentation doit être partagée (fichier README ou wiki du projet).


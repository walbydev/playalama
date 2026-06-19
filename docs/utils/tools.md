# Outils utilisés pour le développement de ce projet

## Dictionnaire de mots

Le projet utilise un dictionnaire de mots pour vérifier l'orthographe des mots utilisés dans les textes. Le dictionnaire est basé sur le projet aspell (ou ispell).


- Installation de aspell sur Fedora :
```bash
sudo dnf install aspell aspell-fr
```

- Installation de aspell sur Debian/Ubuntu :
```bash
sudo apt-get install aspell aspell-fr
```
- Commande pour extraire tous les mots de la langue française en excluant les mots composés :
```bash
aspell -l fr dump master | aspell -l fr expand \
  | grep -v "'" \
  | grep -v '-' \
  | grep -v ' ' \
  | grep -P '^[a-zA-Z\p{L}]+$' \
  | sort -u \
  > dictionnaire.txt
```

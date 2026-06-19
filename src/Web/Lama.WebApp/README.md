# Lama.WebApp (lot 1 - 1.1.0)

WebApp Blazor (serveur) pour la V1: vitrine + zone de jeu + gestion de profil simple.

## Perimetre lot 1

- Vitrine: accueil, regles, telechargements.
- Jeu online: lister/creer/rejoindre une partie, jouer un tour, afficher le plateau.
- Gestion simple: profil local navigateur (username, email optionnel, mot de passe).
- Theme clair/sombre et densite confortable/compacte.

## Execution locale

```bash
dotnet run --project src/Web/Lama.WebApp/Lama.WebApp.csproj
```

Par defaut, la WebApp appelle `http://127.0.0.1:5000` pour l'API.
Vous pouvez surcharger avec `LAMA_SERVER_URL`.


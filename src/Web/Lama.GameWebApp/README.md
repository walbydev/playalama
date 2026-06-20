# Lama.GameWebApp

WebApp Blazor (serveur) dediee au jeu live: lobby, parties, et profil joueur.

## Perimetre

- Jeu online: lister/creer/rejoindre une partie, jouer un tour, afficher le plateau.
- Gestion simple: profil local navigateur (username, email optionnel, mot de passe).
- Theme clair/sombre et densite confortable/compacte.

## Execution locale

```bash
dotnet run --project src/Web/Lama.GameWebApp/Lama.GameWebApp.csproj
```

Par defaut, la WebApp appelle `http://127.0.0.1:5000` pour l'API.
Vous pouvez surcharger avec `LAMA_SERVER_URL`.


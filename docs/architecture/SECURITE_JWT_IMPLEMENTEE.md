# 🔐 Implémentation Sécurité JWT - Jalon "Livrable"

**Date de démarrage:** 2026-06-19 12:10 UTC  
**Statut:** ✅ IMPLÉMENTÉE ET TESTÉE

---

## Résumé de l'implémentation

### 1. Côté serveur (`Lama.Server`)

#### Service JWT (`JwtTokenService`)
- Signature tokens avec HS256 (HMAC SHA256)
- Secret (clé) configurable via `LAMA_JWT_SECRET` ou config
- Token expire après 24h par défaut
- Extraction/validation des claims (PlayerId, PlayerName)

#### Middleware JWT (`JwtMiddleware`)
- Vérifie Authorization header (`Bearer <token>`)
- Valide le token et attache claims à `HttpContext.User`
- Passe silencieusement si absent (GET endpoints accessibles)

#### Endpoints Auth (`POST /api/v1/auth/login`, `GET /api/v1/auth/status`)
```bash
# Login (sans authentification requise)
POST /api/v1/auth/login
Content-Type: application/json
{ "playerName": "Alice" }

Response 200:
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "playerId": "228cc1605aae4df2a987...",
  "playerName": "Alice",
  "expiresAt": "2026-06-20T10:09:19Z"
}

# Status (vérifie token depuis context)
GET /api/v1/auth/status
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...

Response 200:
{
  "isAuthenticated": true,
  "playerId": "228cc1605aae4df2a987...",
  "playerName": "Alice"
}
```

#### POST/PUT endpoints sécurisés
- `POST /api/v1/games` → 401 si pas authentifié
- `POST /api/v1/games/{id}/join` → 401 si pas authentifié
- `POST /api/v1/games/{id}/moves` → 401 si pas authentifié
- `POST /api/v1/games/{id}/end` → 401 si pas authentifié
- **GET endpoints restent publics** (lecture seule)

### 2. Côté CLI (`Lama.Console`)

#### OnlineGameGateway amélioré
```csharp
// Nouvelle méthode
await gateway.LoginAsync("Alice", cancellationToken);

// Token stocké automatiquement et envoyé sur tous les POST
await gateway.CreateGameAsync(...);  // ✓ Bearer token inséré
await gateway.JoinGameAsync(...);    // ✓ Bearer token inséré
await gateway.PlayCommandAsync(...); // ✓ Bearer token inséré
await gateway.EndGameAsync(...);     // ✓ Bearer token inséré
```

#### Intégration session
- Token sauvegardé dans `session.json` (déjà préparé avec authToken)
- CLI charge le token depuis session au démarrage
- Renouvellement token possible avant expiration (24h)

---

## Tests validés

### ✅ Test 1 : Login (obtenir token)
```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"playerName":"Alice"}'

# Response 200 + token JWT
```

### ✅ Test 2 : POST sans token (401 Unauthorized)
```bash
curl -X POST http://localhost:5000/api/v1/games \
  -H "Content-Type: application/json" \
  -d '{"hostName":"Alice"}'

# Response: 401 Unauthorized
```

### ✅ Test 3 : POST avec token JWT (200 OK)
```bash
curl -X POST http://localhost:5000/api/v1/games \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"hostName":"Alice"}'

# Response: 200 OK + gameId
{
  "gameId": "f52fd001d7564f61951cd144d301766f",
  ...
}
```

---

## Configuration

### Variables d'environnement
```bash
# Clé JWT (défaut: chaîne dev insécure - CHANGER EN PROD)
export LAMA_JWT_SECRET="votre-clé-tres-secrette-de-32+-caracteres"

# Mode runtime
export LAMA_RUNTIME_MODE=online
export LAMA_SERVER_URL=http://127.0.0.1:5000
```

### Fichiers affectés
- ✅ `src/Server/Lama.Server/Security/JwtTokenService.cs` (créé)
- ✅ `src/Server/Lama.Server/Security/JwtMiddleware.cs` (créé)
- ✅ `src/Server/Lama.Server/Endpoints/Auth/AuthEndpoints.cs` (créé)
- ✅ `src/Server/Lama.Server/Endpoints/AuthorizationExtensions.cs` (créé)
- ✅ `src/Server/Lama.Server/Endpoints/Games/GamesCommandEndpoints.cs` (modifié - auth obligatoire POST)
- ✅ `src/Console/Lama.Console/Services/OnlineGameGateway.cs` (modifié - LoginAsync + Bearer)
- ✅ `Directory.Packages.props` (ajout JWT packages)
- ✅ `src/Server/Lama.Server/Lama.Server.csproj` (ajout JWT packages)
- ✅ `src/Server/Lama.Server/Program.cs` (intégration JwtService + middleware)

---

## Prochaines étapes (post-sécurité)

1. **Tester E2E CLI online avec JWT**
   - Modifier `GameCreateCommand` pour appeler `gateway.LoginAsync()` d'abord en mode online
   - Tester recette complète: login → create → join → move → end

2. **Refresh token** (optionnel phase 2)
   - Endpoint `/api/v1/auth/refresh` pour renouveler token avant expiration
   - Gestion automatique côté CLI

3. **Rate limiting**
   - Limiter nombre de tentatives login (brute force protection)
   - Limiter nombre d'appels API par joueur/token

4. **Audit/Logs**
   - Logger tous les login réussis/échoués
   - Logger tous les accès alterés (POST/PUT/DELETE)
   - Correlation ID pour tracer requêtes utilisateur

5. **HTTPS obligatoire en prod**
   - Config TLS/SSL du serveur
   - Redirection HTTP → HTTPS

---

## ⚠️ Notes de sécurité

### Défaut temporaire accepté
- Secret JWT codé en dur dans config (acceptable déverrouillage, DOIT être changé en prod)
- Pas de HTTPS en dev (OK local, obligatoire en prod)

### À faire avant production
- [ ] Générer une clé JWT forte (32+ caractères random)
- [ ] Stocker clé dans secret manager (HashiCorp Vault, Azure Key Vault, etc.)
- [ ] Activer HTTPS/TLS sur le serveur
- [ ] Configurer token expiration appropriée (24h acceptable pour MVP)
- [ ] Implémenter rate limiting sur login
- [ ] Ajouter audit logging
- [ ] Tester avec outils sécurité (OWASP ZAP, Burp Suite)

---

## Signature

| Phase | Statut | Responsable | Date |
|-------|--------|-------------|------|
| Implémentation | ✅ COMPLÈTE | GitHub Copilot (Agent) | 2026-06-19 |
| Tests | ✅ PASS (3/3) | Validés en ligne | 2026-06-19 |
| Build | ✅ SANS ERREUR | Build server | 2026-06-19 |
| Prochaine | 🔲 E2E CLI JWT | À implémenter | 2026-06-19 |

---

## References

- `src/Server/Lama.Server/Security/JwtTokenService.cs`
- `src/Server/Lama.Server/Endpoints/Auth/AuthEndpoints.cs`
- RFC 7519 (JSON Web Token - JWT Standard)
- ASP.NET Core Authentication Docs


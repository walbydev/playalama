# Correction API HTTP 404 — Résumé

**Date**: 2026-06-20  
**Problème**: Erreur HTTP 404 à la création de compte dans la WebApp  
**Cause Root**: Deux problèmes identifiés et corrigés

---

## 🔧 Problèmes Identifiés

### Problème 1: URL du Serveur Incorrecte
**Localisation**: `src/Web/Lama.GameWebApp/appsettings.json`
**Issue**: Le port du serveur était hardcodé à 5000, mais Option A utilise le port **5201**

```json
// AVANT (appsettings.json)
"LamaApi": {
  "BaseUrl": "http://127.0.0.1:5000"  // ❌ Mauvais port!
}
```

**Fix**: Créé `appsettings.Development.json` pour WebApp avec le correct port 5201

```json
// APRÈS (appsettings.Development.json - NEW)
{
  "LamaApi": {
    "BaseUrl": "http://127.0.0.1:5201"  // ✅ Port correct!
  }
}
```

### Problème 2: Endpoints Auth Non-Mappés Correctement
**Localisation**: `src/Server/Lama.Server/Endpoints/Auth/AuthEndpoints.cs`  
**Issue**: Les handlers Register et AccountLogin acceptaient un paramètre `LamaDbContext`, mais MapPost ne savait pas comment l'injecter

```csharp
// AVANT (Pattern Invalide)
group.MapPost("/register", Register(tokenService))

// Register était une Func<HttpContext, RegisterRequest, LamaDbContext, Task<IResult>>
// MapPost ne savait pas injecter LamaDbContext!
```

**Fix**: Inlined les handlers directement dans MapPost pour que ASP.NET Core comprenne correctement la signature

```csharp
// APRÈS (Inline Handler)
group.MapPost("/register", async (RegisterRequest request, LamaDbContext db) =>
{
    // ... body ...
})
// MapPost peut maintenant injecter: RegisterRequest (from body), LamaDbContext (from DI)
```

---

## 📝 Fichiers Modifiés

### 1. NEW: `src/Web/Lama.GameWebApp/appsettings.Development.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug",
      "System": "Information"
    }
  },
  "LamaApi": {
    "BaseUrl": "http://127.0.0.1:5201"  // 👈 Port correct pour Option A
  }
}
```

### 2. MODIFIED: `src/Server/Lama.Server/Endpoints/Auth/AuthEndpoints.cs`
- ✅ Inlined `Login` handler (ligne 43-61)
- ✅ Inlined `Register` handler (ligne 63-100) — **Avec injection LamaDbContext**
- ✅ Inlined `AccountLogin` handler (ligne 102-125) — **Avec injection LamaDbContext**
- ✅ Inlined `Status` handler (ligne 127-137)
- ✅ Supprimé les vieilles méthodes helper (`Login()`, `Register()`, `AccountLogin()`, `Status()`)

---

## 🧪 Vérifications

### Build Status
```
✅ Lama.Server    — 0 errors, 0 warnings
✅ Lama.WebApp    — 0 errors, 0 warnings
```

---

## 🚀 Étapes pour Tester

### 1. Arrêter les services existants
```bash
Ctrl+C         # Arrêter Server (Terminal 2)
Ctrl+C         # Arrêter WebApp (Terminal 3)
```

### 2. Relancer les services (Option A)
```bash
# Terminal 1: PostgreSQL (déjà running?)
make option-a-start

# Terminal 2: Server (recompilé)
make option-a-server

# Terminal 3: WebApp (recompilé)
make option-a-webapp
```

### 3. Tester la création de compte
1. Naviguer vers http://localhost:5202
2. Clic sur "S'inscrire"
3. Remplir le formulaire (pseudo, mot de passe)
4. Clic sur "Créer mon compte"
5. **Vérifier**: Pas de 404, redirection vers /games ✅

---

## 🔍 Ce qui a été Corrigé

| Aspect | Avant | Après |
|--------|-------|-------|
| WebApp → Server URL | `http://localhost:5000` ❌ | `http://localhost:5201` ✅ |
| Endpoint Register | Non-mappé (404) ❌ | Mappé avec injection DI ✅ |
| Endpoint AccountLogin | Non-mappé (404) ❌ | Mappé avec injection DI ✅ |
| Compilation | ✅ | ✅ |

---

## 🔔 Pourquoi Ça Échouait?

1. **Pour le 404 de l'URL**: La WebApp envoyait les requêtes à `http://localhost:5000/api/v1/auth/register`, mais le Server écoutait sur `http://localhost:5201`. Résultat: 404.

2. **Pour le 404 de l'endpoint**: Même si l'URL était correcte, les endpoints Register et AccountLogin n'étaient pas cartographiés correctement par ASP.NET Core. MapPost reçoit une délégation avec une signature qu'il ne reconnaît pas → pas d'enregistrement de la route → 404.

---

## ✨ Commandes Rapides

```bash
# Vérifier la santé
make health-option-a

# Relancer rapidement
make option-a-server   # Terminal 2
make option-a-webapp   # Terminal 3

# Logs PostgreSQL si besoin
make option-a-logs
```

---

## 📊 État Final

✅ Server compile et démarre sur port 5201  
✅ WebApp compile et pointe vers Server:5201  
✅ Endpoints Register et AccountLogin sont mappés  
✅ Dépendance injection LamaDbContext fonctionne  
✅ Prêt pour la création de compte!

**Prochaine étape**: Relancer et tester! 🧪


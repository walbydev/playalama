# Option A Configuration Summary

**Date**: 2026-06-20  
**Status**: ✅ Ready for Development  
**Location**: Project Root

## Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                  Lama v1.1.0 - Option A                  │
│            (Debug Natif + PostgreSQL Docker)             │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Your Workstation (localhost)                           │
│  ┌────────────────────────────────────────────────┐    │
│  │                                                │    │
│  │  ┌─────────┐  ┌─────────┐  ┌──────────────┐  │    │
│  │  │   DB    │  │ Server  │  │   WebApp     │  │    │
│  │  │ Docker  │  │ Native  │  │   Native     │  │    │
│  │  │   :5200 │  │ :5201   │  │    :5202     │  │    │
│  │  └────┬────┘  └────┬────┘  └──────┬───────┘  │    │
│  │       │             │              │          │    │
│  │  PostgreSQL  Lama.Server    Lama.WebApp      │    │
│  │   (alpine)      (.NET)         (.NET)         │    │
│  │                                  │           │    │
│  │  Hot reload  ✓                   │ Hot reload ✓  │    │
│  │  Persistent data ✓               │           │    │
│  │  Debug breakpoints ✓             │           │    │
│  │                                  ▼           │    │
│  │                             Browser :5202   │    │
│  │                                              │    │
│  └──────────────────────────────────────────────┘    │
│                                                       │
└───────────────────────────────────────────────────────┘
```

## Port Mapping

| Service | Port | Type | Notes |
|---------|------|------|-------|
| PostgreSQL | 5200 | Docker Container | `localhost:5200` |
| Lama.Server | 5201 | Native .NET | Debug breakpoints enabled |
| Lama.WebApp | 5202 | Native .NET | Hot-reload on save |

## Files Modified/Created

### Configuration Files
- `docker-compose.local-debug-option-a.yml` (NEW)
- `src/Server/Lama.Server/Properties/launchSettings.json` (MODIFIED: 5055 → 5201)
- `src/Web/Lama.WebApp/Properties/launchSettings.json` (MODIFIED: 5100 → 5202, URL updated)
- `src/Server/Lama.Server/appsettings.Development.json` (MODIFIED: port 5432 → 5200)
- `src/Server/Lama.Server/Data/LamaDbContextFactory.cs` (MODIFIED: port 5432 → 5200)

### Scripts
- `tools/scripts/start-local-debug-option-a.sh` (NEW) — Bundles startup with info
- `tools/scripts/test-option-a.sh` (NEW) — Validates entire stack

### Documentation
- `docs/utils/OPTION_A_QUICKSTART.md` (NEW) — 2-minute quick-start
- `docs/utils/OPTION_A_LOCAL_DEBUG.md` (NEW) — Detailed guide
- `docs/utils/INDEX.md` (NEW/UPDATED) — Utility docs index

### Makefile
- `Makefile` (UPDATED) — Added 6 new `option-a-*` targets

## Quick Start

```bash
# 1. Start PostgreSQL Docker
make option-a-start

# 2. Terminal 1: Launch Server on 5201
make option-a-server

# 3. Terminal 2: Launch WebApp on 5202
make option-a-webapp

# 4. Browser: http://localhost:5202
```

## Verify Setup

```bash
# Check health
make health-option-a

# Expected output:
# ✓ PostgreSQL (5200) OK
# ✗ Server (5201) KO          ← Normal if not running
# ✗ WebApp (5202) KO          ← Normal if not running
```

## Environment Variables (Defaults)

The following are baked into launchSettings.json:

```
# Server
ASPNETCORE_ENVIRONMENT=Development

# WebApp
ASPNETCORE_ENVIRONMENT=Development
LAMA_SERVER_URL=http://127.0.0.1:5201

# PostgreSQL (Docker env vars)
POSTGRES_USER=lama_dev
POSTGRES_PASSWORD=dev_password_change_me
POSTGRES_DB=lama_dev
```

## Key Features Enabled

✅ Auto-database migration (first Server startup)  
✅ SQL logging in Development mode  
✅ Hot-reload on code changes (Ctrl+Shift+S in Rider)  
✅ Persistent PostgreSQL volumes  
✅ Full debug breakpoint support  
✅ CORS enabled for cross-origin requests  

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Port 5200 already in use | `lsof -ti:5200 \| xargs kill -9` |
| "Could not connect to PostgreSQL" | Wait 5–10s for Docker startup |
| "Connection refused" on 5201/5202 | Services not started yet (normal) |
| DB schema mismatch | Auto-migration runs on first Server boot |

## Next Steps

1. **Start the stack** (see Quick Start above)
2. **Navigate to** http://localhost:5202
3. **Create an account** (Register form available)
4. **Set a breakpoint** in `Lama.Server/Endpoints/AuthEndpoints.cs` and play
5. **Modify CSS** in `Lama.WebApp/wwwroot/css/app.css` and see live reload

## Migration to Other Options

To switch to **Option B** (full Docker with hot-reload):
- See `docs/architecture/DOCKER_DEPLOYMENT.md`
- Use `docker-compose.local.yml` instead

To deploy to **Production**:
- See `docs/architecture/HTTPS_DEPLOYMENT.md`
- Use `docker-compose.prod.yml`

---

**Created**: 2026-06-20  
**Updated**: 2026-06-20  
**Status**: Ready for use  
**Maintainer**: Your team


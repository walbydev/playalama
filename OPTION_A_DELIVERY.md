# OPTION A Implementation — Complete Delivery

**Date**: 2026-06-20  
**Status**: ✅ DONE — Ready for Development  
**Test Result**: 20/20 Checks Passed

---

## Summary

**Option A** is now fully configured for local development with:
- **PostgreSQL in Docker** on port **5200** (isolated, persistent data)
- **Lama.Server native** on port **5201** (hot-reload, breakpoints)
- **Lama.WebApp native** on port **5202** (hot-reload, breakpoints)

All three services can start independently and communicate via localhost.

---

## Deliverables

### 1. Configuration Files (5 files modified/created)

```
docker-compose.local-debug-option-a.yml
├─ PostgreSQL 16 alpine
├─ Port 5200 external → 5432 internal
├─ Persistent volume: lama-postgres-data
└─ Health check enabled

src/Server/Lama.Server/Properties/launchSettings.json
├─ MODIFIED: 5055 → 5201
└─ ASPNETCORE_ENVIRONMENT=Development

src/Web/Lama.WebApp/Properties/launchSettings.json
├─ MODIFIED: 5100 → 5202
├─ LAMA_SERVER_URL=http://127.0.0.1:5201
└─ ASPNETCORE_ENVIRONMENT=Development

src/Server/Lama.Server/appsettings.Development.json
├─ MODIFIED: Host=localhost;Port=5432 → Port=5200
├─ Database.AutoMigrate=true
└─ SQL logging enabled

src/Server/Lama.Server/Data/LamaDbContextFactory.cs
├─ MODIFIED: Port=5432 → Port=5200
└─ EF Core design-time factory updated
```

### 2. Scripts (3 files created)

```
tools/scripts/start-local-debug-option-a.sh
├─ Launches PostgreSQL Docker
├─ Displays topologie & next steps
└─ Color-coded output

tools/scripts/test-option-a.sh
├─ Validates PostgreSQL health
├─ Checks schema existence
├─ Verifies port accessibility
└─ Provides troubleshooting hints

tools/scripts/validate-option-a.sh
├─ Complete setup validation
├─ 20 configuration checks
├─ Build verification
└─ Reports pass/fail with guidance
```

### 3. Documentation (5 files created/updated)

```
OPTION_A_SETUP.md (NEW — project root)
├─ Overview with ASCII topology diagram
├─ Port mapping table
├─ Files modified summary
├─ Quick start (3-step)
├─ Verification & troubleshooting
└─ Next steps

docs/utils/OPTION_A_QUICKSTART.md (NEW)
├─ 2-minute quick-start guide
├─ 3-port topologie
├─ Startup commands
├─ Quick reference table
└─ Common issues

docs/utils/OPTION_A_LOCAL_DEBUG.md (NEW)
├─ Detailed 200-line guide
├─ Full architecture overview
├─ Configuration explainers
├─ Usage patterns
├─ Troubleshooting section
└─ Scaling considerations

docs/utils/INDEX.md (NEW / replaces README)
├─ Utilities documentation index
├─ Quick links to all guides
├─ Command reference
├─ Environment variables
├─ Database schema overview
└─ Support instructions

.env.example (UPDATED)
├─ Option A defaults (Development)
├─ Production config preserved
├─ PostgreSQL variables
├─ Usage examples
└─ Optional overrides
```

### 4. Makefile Targets (7 new targets)

```makefile
make option-a-start         # Start PostgreSQL Docker
make option-a-server        # Launch Server on 5201
make option-a-webapp        # Launch WebApp on 5202
make option-a-stop          # Stop PostgreSQL
make option-a-clean         # Clean DB volumes (reset)
make option-a-logs          # Tail PostgreSQL logs
make health-option-a        # Verify service health
```

---

## Configuration Details

### Port Allocation

| Port | Service | Type | Launch Command |
|------|---------|------|-----------------|
| 5200 | PostgreSQL | Docker | `make option-a-start` |
| 5201 | Lama.Server | Native .NET | `make option-a-server` |
| 5202 | Lama.WebApp | Native .NET | `make option-a-webapp` |

### Database

- **Image**: postgres:16-alpine
- **User**: lama_dev
- **Password**: dev_password_change_me
- **Database**: lama_dev
- **Connection String**: `Host=localhost;Port=5200;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me;Ssl Mode=Disable;`
- **Auto-Migration**: Enabled (applies EF Core migrations on Server startup)

### Environment Variables (Defaults in launchSettings.json)

```
Server:
  ASPNETCORE_ENVIRONMENT=Development
  ASPNETCORE_URLS=http://127.0.0.1:5201

WebApp:
  ASPNETCORE_ENVIRONMENT=Development
  ASPNETCORE_URLS=http://127.0.0.1:5202
  LAMA_SERVER_URL=http://127.0.0.1:5201

PostgreSQL (Docker):
  POSTGRES_USER=lama_dev
  POSTGRES_PASSWORD=dev_password_change_me
  POSTGRES_DB=lama_dev
```

---

## Quick Start

### Step 1: Start PostgreSQL

```bash
make option-a-start
```

Expected output:
```
✓ Network lama-local-debug-a_lama-network Created
✓ Volume lama-local-debug-a_lama-postgres-data Created
✓ Container postgres-lama-option-a Up (healthy)
```

### Step 2: Terminal 1 → Launch Server (5201)

```bash
make option-a-server
```

Wait for output:
```
... Lama.Server started listening on http://127.0.0.1:5201
```

### Step 3: Terminal 2 → Launch WebApp (5202)

```bash
make option-a-webapp
```

Wait for output:
```
... Lama.WebApp started listening on http://127.0.0.1:5202
```

### Step 4: Browser

Navigate to [http://localhost:5202](http://localhost:5202)

---

## Validation

Run the validation script:

```bash
./tools/scripts/validate-option-a.sh
```

Expected output:
```
✓ All Checks Passed! Ready for development.

Summary:
  Passed: 20
  Failed: 0
```

---

## Verification

### Health Check

```bash
make health-option-a
```

Expected:
```
✓ PostgreSQL (5200) OK
  Port 5201 libre (service pas lancé)  ← Normal if not running
  Port 5202 libre (service pas lancé)  ← Normal if not running
```

### Once Services are Running

Test each endpoint:

```bash
# PostgreSQL
docker exec postgres-lama-option-a pg_isready -U lama_dev

# Server (should show Swagger UI)
curl -s http://localhost:5201/ | head -20

# WebApp (should show Blazor app)
curl -s http://localhost:5202/ | head -20
```

---

## Common Tasks

### View Logs

```bash
make option-a-logs              # PostgreSQL logs
# No output? Service logging is quiet in Docker
```

### Database Operations

```bash
# Connect via psql
docker exec -it postgres-lama-option-a psql -U lama_dev -d lama_dev

# Or use DBeaver, VS Code extension, etc.
# Connection: localhost:5200, lama_dev/dev_password_change_me
```

### Stop Services

```bash
# Stop PostgreSQL Docker
make option-a-stop

# Kill native processes
Ctrl+C in each terminal (Server/WebApp)
```

### Reset Everything

```bash
# Stop + remove PostgreSQL volume (complete reset)
make option-a-clean

# Then restart
make option-a-start
```

---

## Advantages of Option A

✅ **Native debug** — Full breakpoint support in Rider/VS Code  
✅ **Ultra-fast iteration** — No Docker rebuild needed  
✅ **Persistent data** — PostgreSQL in Docker keeps data between restarts  
✅ **Isolated services** — Each runs independently  
✅ **Zero Docker complexity** — Only one container (DB)  
✅ **Direct SQL access** — psql/DBeaver on localhost:5200  
✅ **Hot-reload** — Auto-reload on code changes (Ctrl+Shift+S in Rider)

---

## What's Next

1. ✅ Configuration validated (20/20)
2. ✅ Docker PostgreSQL running
3. ✅ Services configured & building
4. → **Start with**: `make option-a-start`
5. → **Develop!**

---

## Migration Path

To switch to **Option B** (full Docker stack):
- See `docs/architecture/DOCKER_DEPLOYMENT.md`

To deploy to **Production**:
- See `docs/architecture/HTTPS_DEPLOYMENT.md`

---

## Support

### If something doesn't work:

1. Check documentation: `docs/utils/INDEX.md`
2. Run validation: `./tools/scripts/validate-option-a.sh`
3. Check health: `make health-option-a`
4. Review logs: `make option-a-logs`
5. See detailed guide: `docs/utils/OPTION_A_LOCAL_DEBUG.md`

### Common Issues

| Issue | Solution |
|-------|----------|
| Port 5200 already in use | `lsof -ti:5200 \| xargs kill -9` |
| DB connection refused | Wait 5–10s for PostgreSQL startup |
| Can't access 5201/5202 | Services not running yet (normal)  |
| Schema mismatch | Auto-migration runs on Server boot |

---

**Status**: ✅ READY FOR USE  
**Build Status**: ✅ 0 errors, 0 warnings  
**Docker**: ✅ PostgreSQL healthy on 5200  
**Documentation**: ✅ Complete  

**Start now**: `make option-a-start`


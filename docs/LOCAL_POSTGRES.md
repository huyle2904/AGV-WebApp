# Local PostgreSQL Setup

This project now supports PostgreSQL through `NewAGV.Api`.

## Default local connection

`Host=localhost;Port=5432;Database=agv;Username=agv;Password=agv`

## One-time local setup

1. Install PostgreSQL on Windows.
2. Ensure `psql` is available in `PATH`.
3. Run:

```powershell
./scripts/Initialize-LocalPostgres.ps1
```

If your local postgres admin account differs, pass explicit values, for example:

```powershell
./scripts/Initialize-LocalPostgres.ps1 -AdminUser postgres -AdminPassword your-password
```

## Verify API connectivity

After the database is running, start the API and call:

`GET /api/integration/database`

A healthy response should show `connected = true`.

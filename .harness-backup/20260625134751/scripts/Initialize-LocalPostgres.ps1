param(
    [string]$DbHost = "localhost",
    [int]$Port = 5432,
    [string]$AdminDatabase = "postgres",
    [string]$AdminUser = "postgres",
    [string]$AdminPassword = "postgres",
    [string]$AppDatabase = "agv",
    [string]$AppUser = "agv",
    [string]$AppPassword = "agv"
)

$ErrorActionPreference = "Stop"

$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql)
{
    throw "psql was not found in PATH. Install PostgreSQL locally and add the bin folder to PATH, then run this script again."
}

$env:PGPASSWORD = $AdminPassword

$sql = @"
DO
`$do`
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$AppUser') THEN
        EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', '$AppUser', '$AppPassword');
    END IF;
END
`$do`;

SELECT 'CREATE DATABASE $AppDatabase OWNER $AppUser'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$AppDatabase')
\gexec
"@

& $psql.Source -h $DbHost -p $Port -U $AdminUser -d $AdminDatabase -v ON_ERROR_STOP=1 -c $sql

Write-Host "Database '$AppDatabase' and role '$AppUser' are ready."
Write-Host "Connection string: Host=$DbHost;Port=$Port;Database=$AppDatabase;Username=$AppUser;Password=$AppPassword"

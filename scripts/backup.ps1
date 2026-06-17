# --- 0. Encoding Fix ---
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

# --- 1. Path Setup ---
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Get-Item "$ScriptDir\..").FullName
$BackupDir = "$ProjectRoot\backups"

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}

# --- 2. Load .env Configuration ---
$EnvFile = "$ProjectRoot\.env"
$ConnectionString = $null

if (Test-Path $EnvFile) {
    $ConnLine = Get-Content -Encoding UTF8 $EnvFile | Where-Object { $_ -match "^DB_CONNECTION_STRING\s*=" }
    if ($ConnLine) {
        $ConnectionString = ($ConnLine -split '=', 2)[1].Trim("`"' ")
    }
} else {
    Write-Error "Error: .env file not found!"
    exit 1
}

if (-not $ConnectionString) {
    Write-Error "Error: DB_CONNECTION_STRING is missing in .env!"
    exit 1
}

# --- 2b. Простейший и надежный вырез параметров ---
# Твоя строка: Host=...;Database=...;Username=...;Password=...
$DbHost = ($ConnectionString -split "Host=")[1].Split(";")[0].Trim()
$DbName = ($ConnectionString -split "Database=")[1].Split(";")[0].Trim()
$DbUser = ($ConnectionString -split "Username=")[1].Split(";")[0].Trim()
$DbPass = ($ConnectionString -split "Password=")[1].Split(";")[0].Trim()
$DbPort = "5432"

# Включаем принудительный SSL для утилит Postgres (чтобы Neon не ругался)
$env:PGSSLMODE = "require"
$env:PGPASSWORD = $DbPass

Write-Host "--- DETECTED CONFIG ---" -ForegroundColor Yellow
Write-Host "Host: [$DbHost]" -ForegroundColor Yellow
Write-Host "User: [$DbUser]" -ForegroundColor Yellow
Write-Host "DB:   [$DbName]" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow

# --- 3. Backup Filename Setup ---
$Timestamp = Get-Date -Format "yyyy_MM_dd_HHmmss"
$BackupFile = "$BackupDir\db_backup_$Timestamp.sql"

# --- 4. Locate pg_dump.exe ---
# Твой жестко заданный рабочий путь к 18-й версии
$PgDumpPath = "D:\PostgreSQL\18\bin\pg_dump.exe"

if (-not (Test-Path $PgDumpPath)) {
    if (Get-Command "pg_dump.exe" -ErrorAction SilentlyContinue) {
        $PgDumpPath = "pg_dump.exe"
    } else {
        Write-Error "Error: pg_dump.exe not found at D:\PostgreSQL\18\bin\pg_dump.exe!"
        exit 1
    }
}

# --- 5. Run Backup ---
# --- 5. Run Backup ---
Write-Host "Connecting to Neon Cloud..." -ForegroundColor Cyan
Write-Host "Executing pg_dump.exe..." -ForegroundColor Gray

$ErrorLog = "$ScriptDir\pg_error.log"

# Запускаем через явные флаги, как любит pg_dump
& $PgDumpPath -h $DbHost -p $DbPort -U $DbUser -F p -b -v -f $BackupFile $DbName 2>$ErrorLog

# --- 6. Check Results ---
if ($LASTEXITCODE -eq 0) {
    Write-Host "Backup successfully created: $BackupFile" -ForegroundColor Green
    if (Test-Path $ErrorLog) { Remove-Item $ErrorLog }
} else {
    Write-Error "Backup failed!"
    
    if (Test-Path $ErrorLog) {
        Write-Host "`n=== PG_DUMP ERROR LOG ===" -ForegroundColor Red
        Get-Content $ErrorLog
        Remove-Item $ErrorLog
    }
    
    if (Test-Path $BackupFile) { Remove-Item $BackupFile }
}

# Чистим за собой переменные среды
$env:PGPASSWORD = $null
$env:PGSSLMODE = $null
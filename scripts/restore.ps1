# --- 0. Encoding Fix ---
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

# --- 1. Path Setup ---
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Get-Item "$ScriptDir\..").FullName
$BackupDir = "$ProjectRoot\backups"

# Ищем самый свежий файл бэкапа в папке
$LatestBackup = Get-ChildItem -Path $BackupDir -Filter "db_backup_*.sql" | 
                Sort-Object LastWriteTime -Descending | 
                Select-Object -First 1

if (-not $LatestBackup) {
    Write-Error "Error: No backup files found in $BackupDir!"
    exit 1
}

$BackupFile = $LatestBackup.FullName

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

# --- 2b. Железобетонный вырез параметров ---
$DbHost = ($ConnectionString -split "Host=")[1].Split(";")[0].Trim()
$DbName = ($ConnectionString -split "Database=")[1].Split(";")[0].Trim()
$DbUser = ($ConnectionString -split "Username=")[1].Split(";")[0].Trim()
$DbPass = ($ConnectionString -split "Password=")[1].Split(";")[0].Trim()
$DbPort = "5432"

# Включаем SSL и пароль для psql
$env:PGSSLMODE = "require"
$env:PGPASSWORD = $DbPass

Write-Host "--- DETECTED CONFIG FOR RESTORE ---" -ForegroundColor Yellow
Write-Host "Host:   [$DbHost]" -ForegroundColor Yellow
Write-Host "User:   [$DbUser]" -ForegroundColor Yellow
Write-Host "DB:     [$DbName]" -ForegroundColor Yellow
Write-Host "Target: [$($LatestBackup.Name)]" -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Yellow

# --- 3. Locate psql.exe ---
$PsqlPath = "D:\PostgreSQL\18\bin\psql.exe"
if (-not (Test-Path $PsqlPath)) {
    $PsqlPath = "psql.exe"
}

# --- 3b. Автоматическая очистка данных перед восстановлением ---
Write-Host "Cleaning existing data in tables..." -ForegroundColor Yellow

# Эта команда очистит данные во всех таблицах public схемы, не трогая саму структуру
$TruncateCmd = "DO 'BEGIN EXECUTE (SELECT ''TRUNCATE TABLE '' || string_agg(quote_ident(schemaname) || ''.'' || quote_ident(tablename), '', '') || '' RESTART IDENTITY CASCADE;'' FROM pg_tables WHERE schemaname = ''public''); END';"

# Выполняем очистку перед накатом файла
& $PsqlPath -h $DbHost -p $DbPort -U $DbUser -d $DbName -c $TruncateCmd *> $null

# --- 4. Run Restore ---
Write-Host "Connecting to Neon Cloud for restore..." -ForegroundColor Cyan
Write-Host "Executing psql.exe..." -ForegroundColor Gray

$ErrorLog = "$ScriptDir\pg_restore_error.log"

# Запускаем восстановление базы
& $PsqlPath -h $DbHost -p $DbPort -U $DbUser -d $DbName -f $BackupFile 2>$ErrorLog

# --- 5. Check Results ---
if ($LASTEXITCODE -eq 0) {
    Write-Host "Database successfully restored from: $BackupFile" -ForegroundColor Green
    if (Test-Path $ErrorLog) { Remove-Item $ErrorLog }
} else {
    Write-Error "Restore failed!"
    if (Test-Path $ErrorLog) {
        Write-Host "`n=== PSQL ERROR LOG ===" -ForegroundColor Red
        Get-Content $ErrorLog
        Remove-Item $ErrorLog
    }
}

# Чистим за собой
$env:PGPASSWORD = $null
$env:PGSSLMODE = $null
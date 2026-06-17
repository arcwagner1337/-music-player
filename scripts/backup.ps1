
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'


$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Get-Item "$ScriptDir\..").FullName
$BackupDir = "$ProjectRoot\backups"

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}


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


$DbHost = ($ConnectionString -split "Host=")[1].Split(";")[0].Trim()
$DbName = ($ConnectionString -split "Database=")[1].Split(";")[0].Trim()
$DbUser = ($ConnectionString -split "Username=")[1].Split(";")[0].Trim()
$DbPass = ($ConnectionString -split "Password=")[1].Split(";")[0].Trim()
$DbPort = "5432"


$env:PGSSLMODE = "require"
$env:PGPASSWORD = $DbPass

Write-Host "--- DETECTED CONFIG ---" -ForegroundColor Yellow
Write-Host "Host: [$DbHost]" -ForegroundColor Yellow
Write-Host "User: [$DbUser]" -ForegroundColor Yellow
Write-Host "DB:   [$DbName]" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow


$Timestamp = Get-Date -Format "yyyy_MM_dd_HHmmss"
$BackupFile = "$BackupDir\db_backup_$Timestamp.sql"


$PgDumpPath = "D:\PostgreSQL\18\bin\pg_dump.exe"

if (-not (Test-Path $PgDumpPath)) {
    if (Get-Command "pg_dump.exe" -ErrorAction SilentlyContinue) {
        $PgDumpPath = "pg_dump.exe"
    } else {
        Write-Error "Error: pg_dump.exe not found at D:\PostgreSQL\18\bin\pg_dump.exe!"
        exit 1
    }
}


Write-Host "Connecting to Neon Cloud..." -ForegroundColor Cyan
Write-Host "Executing pg_dump.exe..." -ForegroundColor Gray

$ErrorLog = "$ScriptDir\pg_error.log"


& $PgDumpPath -h $DbHost -p $DbPort -U $DbUser -F p -b -v -f $BackupFile $DbName 2>$ErrorLog


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


$env:PGPASSWORD = $null
$env:PGSSLMODE = $null
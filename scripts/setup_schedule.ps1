
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $IsAdmin) {
    Write-Host "Запуск от имени Администратора..." -ForegroundColor Yellow
    
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}


$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackupScript = "$ScriptDir\backup.ps1"

if (-not (Test-Path $BackupScript)) {
    Write-Error "Ошибка: Скрипт бэкапа не найден по пути $BackupScript"
    Read-Host "Нажми Enter для выхода..."
    exit 1
}


$TaskName = "NeonCloud_DB_Backup_Test"
$Description = "Тестовый бэкап базы данных Neon Cloud"

$Action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$BackupScript`""


$TestTime = [DateTime]::Parse("12:35")
$Trigger = New-ScheduledTaskTrigger -Daily -At $TestTime

$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries


Write-Host "Регистрация задачи в Планировщике Windows..." -ForegroundColor Cyan


if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Description $Description | Out-Null

Write-Host "Успешно! Задача '$TaskName' создана." -ForegroundColor Green
Write-Host "Бэкап будет запускаться каждый день в 03:00 ночи." -ForegroundColor Green


Start-Sleep -Seconds 3
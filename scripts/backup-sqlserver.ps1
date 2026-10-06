<#
.SYNOPSIS
  Kargoyeri Studio SQL Server tam yedek alir, 14 gunden eski yedekleri siler.

.DESCRIPTION
  Windows Task Scheduler ile her gece 02:00 calistirilmasi onerilir:
    pwsh -NoProfile -File "C:\Kargoyeri\scripts\backup-sqlserver.ps1"

.PARAMETER ServerInstance
  SQL Server instance (default: ".")

.PARAMETER Database
  Yedeklenecek DB adi (default: "KargoyeriStudio")

.PARAMETER BackupDir
  Yedeklerin yazilacagi klasor (default: "D:\Backups\Kargoyeri")

.PARAMETER RetentionDays
  Bu gunden eski yedekler silinir (default: 14)
#>
[CmdletBinding()]
param(
    [string] $ServerInstance = ".",
    [string] $Database       = "KargoyeriStudio",
    [string] $BackupDir      = "D:\Backups\Kargoyeri",
    [int]    $RetentionDays  = 14
)

$ErrorActionPreference = "Stop"

$ts   = Get-Date -Format "yyyyMMdd-HHmmss"
$file = Join-Path $BackupDir "$Database-$ts.bak"

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

Write-Host "[$(Get-Date -Format o)] BACKUP -> $file"
$query = "BACKUP DATABASE [$Database] TO DISK = N'$file' WITH COMPRESSION, CHECKSUM, INIT, NAME = N'$Database-Full-$ts';"
sqlcmd -S $ServerInstance -E -b -Q $query

if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd basarisiz oldu (exit=$LASTEXITCODE)"
}

# Eski yedekleri sil
$cutoff = (Get-Date).AddDays(-$RetentionDays)
$old    = Get-ChildItem $BackupDir -Filter "$Database-*.bak" -ErrorAction SilentlyContinue |
          Where-Object { $_.LastWriteTime -lt $cutoff }
if ($old) {
    Write-Host "[$(Get-Date -Format o)] $($old.Count) eski yedek siliniyor..."
    $old | Remove-Item -Force
}

Write-Host "[$(Get-Date -Format o)] BACKUP TAMAM."

<#
.SYNOPSIS
  Storage:BasePath altindaki JSON depolama klasoru ve Data Protection anahtarlarini ZIP olarak yedekler.

.DESCRIPTION
  JSON storage modunda canli isletim icin gunluk olarak calistirilmasi onerilir.
  SQL Server modunda dahi DataProtection klasoru yedeklenmelidir.

.PARAMETER StoragePath
  Studio'nun JSON dosyalarini sakladigi klasor (Storage:BasePath)

.PARAMETER ContentRoot
  Studio.Web'in ContentRoot'u (data-protection-keys icin)

.PARAMETER BackupDir
  Yedeklerin yazilacagi klasor

.PARAMETER RetentionDays
  Bu gunden eski yedekler silinir (default: 30)
#>
[CmdletBinding()]
param(
    [string] $StoragePath   = "C:\Kargoyeri\Storage",
    [string] $ContentRoot   = "C:\inetpub\Kargoyeri",
    [string] $BackupDir     = "D:\Backups\Kargoyeri",
    [int]    $RetentionDays = 30
)

$ErrorActionPreference = "Stop"
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

if (Test-Path $StoragePath) {
    $jsonZip = Join-Path $BackupDir "json-$ts.zip"
    Write-Host "[$(Get-Date -Format o)] JSON -> $jsonZip"
    Compress-Archive -Path "$StoragePath\*" -DestinationPath $jsonZip -CompressionLevel Optimal
}

$dpkSrc = Join-Path $ContentRoot "data-protection-keys"
if (Test-Path $dpkSrc) {
    $dpkZip = Join-Path $BackupDir "dpk-$ts.zip"
    Write-Host "[$(Get-Date -Format o)] DPK -> $dpkZip"
    Compress-Archive -Path "$dpkSrc\*" -DestinationPath $dpkZip -CompressionLevel Optimal
}

# Retention
$cutoff = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem $BackupDir -Filter "json-*.zip" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt $cutoff } | Remove-Item -Force
Get-ChildItem $BackupDir -Filter "dpk-*.zip" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt $cutoff } | Remove-Item -Force

Write-Host "[$(Get-Date -Format o)] BACKUP TAMAM."

<#
.SYNOPSIS
  SQL Server .bak dosyasindan KargoyeriStudio veritabanini geri yukler.

.WARNING
  Mevcut DB'yi UZER YAZAR. Calistirmadan once Studio.Web servisini durdurun.

.EXAMPLE
  .\restore-sqlserver.ps1 -BackupFile "D:\Backups\Kargoyeri\KargoyeriStudio-20260101-020000.bak"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string] $BackupFile,
    [string] $ServerInstance = ".",
    [string] $Database       = "KargoyeriStudio"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    throw "Yedek dosyasi bulunamadi: $BackupFile"
}

Write-Host "[$(Get-Date -Format o)] RESTORE basliyor: $BackupFile -> [$Database]"

$sql = @"
USE master;
IF DB_ID('$Database') IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
RESTORE DATABASE [$Database]
   FROM DISK = N'$BackupFile'
   WITH REPLACE, RECOVERY;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

sqlcmd -S $ServerInstance -E -b -Q $sql
if ($LASTEXITCODE -ne 0) {
    throw "RESTORE basarisiz oldu (exit=$LASTEXITCODE)"
}

Write-Host "[$(Get-Date -Format o)] RESTORE tamam. Studio.Web servisini baslatabilirsiniz."

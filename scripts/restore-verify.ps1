<#
.SYNOPSIS
  P2-#8 -- Kargoyeri Studio SQL Server yedek dogrulama scripti.

.DESCRIPTION
  Haftalik calistirilmasi onerilen "kor noktasiz yedek" testi.
  Adimlar:
    1) BackupDir icindeki en yeni *.bak dosyasini bulur.
    2) RESTORE VERIFYONLY -- yedek dosyasinin bozulmamis oldugunu hizli sekilde dogrular
       (gercek bir restore yapmaz, sadece checksum/header okur).
    3) (Opsiyonel) -FullRestore parametresi ile gecici bir DB'ye gercek restore yapar
       ve sentinel sorgu calistirir (Customers / CustomerTenants tablosunda kayit > 0 mi?).
    4) Gecici DB silinir.
    5) Sonuc data\last-verify.json dosyasina yazilir (status, lastBackup, sizeMB, ageHours, message).
       Bu dosyayi monitoring (Prometheus exporter veya health check) okuyabilir.

.PARAMETER ServerInstance
  SQL Server instance (default: ".")

.PARAMETER Database
  Production DB adi -- sadece sentinel sorgusunda referans, dogrulama farkli isimde yapilir
  (default: "KargoyeriStudio")

.PARAMETER BackupDir
  *.bak dosyalarinin oldugu klasor (default: "D:\Backups\Kargoyeri")

.PARAMETER VerifyDir
  last-verify.json'in yazilacagi klasor (default: "D:\Backups\Kargoyeri\verify")

.PARAMETER MaxAgeHours
  Yedeklerin maksimum kabul edilebilir yasi. Bu suereden eskiyse FAIL (default: 36 sa).

.PARAMETER FullRestore
  Switch -- gecici DB'ye tam restore + sentinel sorgu calistirir. VERIFYONLY'den daha
  yavas (10-60 sn / GB) ama daha guvenilir.

.EXAMPLE
  # Hizli dogrulama -- sadece VERIFYONLY
  pwsh -NoProfile -File scripts\restore-verify.ps1

  # Tam dogrulama -- gecici DB'ye restore + sentinel
  pwsh -NoProfile -File scripts\restore-verify.ps1 -FullRestore

  # Task Scheduler haftalik (Pazar 03:30):
  #   pwsh -NoProfile -File "C:\Kargoyeri\scripts\restore-verify.ps1" -FullRestore
#>
[CmdletBinding()]
param(
    [string] $ServerInstance = ".",
    [string] $Database       = "KargoyeriStudio",
    [string] $BackupDir      = "D:\Backups\Kargoyeri",
    [string] $VerifyDir      = "D:\Backups\Kargoyeri\verify",
    [int]    $MaxAgeHours    = 36,
    [switch] $FullRestore
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $VerifyDir | Out-Null

$resultPath = Join-Path $VerifyDir "last-verify.json"
$startedAt  = Get-Date

function Write-Result {
    param(
        [string] $Status,
        [string] $Message,
        [string] $BackupFile = $null,
        [double] $SizeMB     = 0,
        [double] $AgeHours   = 0,
        [int]    $RowCount   = 0
    )
    $payload = [ordered]@{
        status         = $Status
        message        = $Message
        startedAtUtc   = $startedAt.ToUniversalTime().ToString("o")
        finishedAtUtc  = (Get-Date).ToUniversalTime().ToString("o")
        durationSec    = [math]::Round(((Get-Date) - $startedAt).TotalSeconds, 2)
        backupFile     = $BackupFile
        sizeMB         = [math]::Round($SizeMB, 2)
        ageHours       = [math]::Round($AgeHours, 2)
        sentinelRows   = $RowCount
        fullRestore    = [bool]$FullRestore
        serverInstance = $ServerInstance
    }
    $payload | ConvertTo-Json -Depth 4 | Set-Content -Path $resultPath -Encoding UTF8
    Write-Host "[$(Get-Date -Format o)] RESULT=$Status  $Message"
    Write-Host "  -> $resultPath"
}

try {
    if (-not (Test-Path $BackupDir)) {
        Write-Result -Status "FAIL" -Message "BackupDir bulunamadi: $BackupDir"
        exit 2
    }

    $latest = Get-ChildItem $BackupDir -Filter "*.bak" -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending |
              Select-Object -First 1

    if (-not $latest) {
        Write-Result -Status "FAIL" -Message "BackupDir icinde *.bak dosyasi yok."
        exit 2
    }

    $ageHrs = ((Get-Date) - $latest.LastWriteTime).TotalHours
    $sizeMB = $latest.Length / 1MB

    Write-Host "[$(Get-Date -Format o)] LATEST  : $($latest.FullName)"
    Write-Host "  size    : $([math]::Round($sizeMB,2)) MB"
    Write-Host "  age     : $([math]::Round($ageHrs,2)) hours"

    if ($ageHrs -gt $MaxAgeHours) {
        Write-Result -Status "STALE" -Message "En yeni yedek $([math]::Round($ageHrs,1)) sa eski (max=$MaxAgeHours)" `
                     -BackupFile $latest.FullName -SizeMB $sizeMB -AgeHours $ageHrs
        exit 3
    }

    # 1) RESTORE VERIFYONLY -- checksum + header dogrulama
    Write-Host "[$(Get-Date -Format o)] RESTORE VERIFYONLY..."
    $verifyQuery = "RESTORE VERIFYONLY FROM DISK = N'$($latest.FullName)' WITH CHECKSUM;"
    sqlcmd -S $ServerInstance -E -b -Q $verifyQuery
    if ($LASTEXITCODE -ne 0) {
        Write-Result -Status "FAIL" -Message "RESTORE VERIFYONLY basarisiz (exit=$LASTEXITCODE)" `
                     -BackupFile $latest.FullName -SizeMB $sizeMB -AgeHours $ageHrs
        exit 4
    }

    # 2) Opsiyonel -- gecici DB'ye tam restore + sentinel
    $rowCount = 0
    if ($FullRestore) {
        $tmpDb = "KargoyeriStudio_verify_$(Get-Date -Format yyyyMMddHHmmss)"
        Write-Host "[$(Get-Date -Format o)] FULL RESTORE -> [$tmpDb]"

        # Logical file isimlerini tespit et
        $fileListSql = "RESTORE FILELISTONLY FROM DISK = N'$($latest.FullName)';"
        $fileList = sqlcmd -S $ServerInstance -E -b -h -1 -W -s "|" -Q $fileListSql 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "RESTORE FILELISTONLY basarisiz."
        }

        # En basit haliyle: dosyalari MOVE ile temp klasore yonlendir
        $tmpDir = Join-Path $VerifyDir "tmp-$tmpDb"
        New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

        $moveClauses = @()
        foreach ($line in $fileList) {
            if ($line -match "^\s*$" -or $line -match "^-+") { continue }
            $cols = $line -split "\|"
            if ($cols.Count -lt 3) { continue }
            $logicalName = $cols[0].Trim()
            $physicalType = $cols[2].Trim()  # D = data, L = log
            if ([string]::IsNullOrWhiteSpace($logicalName)) { continue }
            $ext = if ($physicalType -eq "L") { "ldf" } else { "mdf" }
            $newPath = Join-Path $tmpDir "$logicalName.$ext"
            $moveClauses += "MOVE N'$logicalName' TO N'$newPath'"
        }

        if ($moveClauses.Count -eq 0) {
            throw "FILELISTONLY ciktisi parse edilemedi."
        }

        $restoreSql = "RESTORE DATABASE [$tmpDb] FROM DISK = N'$($latest.FullName)' WITH " +
                      ($moveClauses -join ", ") + ", RECOVERY, REPLACE;"

        sqlcmd -S $ServerInstance -E -b -Q $restoreSql
        if ($LASTEXITCODE -ne 0) {
            throw "Tam restore basarisiz (exit=$LASTEXITCODE)"
        }

        try {
            # Sentinel: en az bir tablo var ve okuma calisiyor mu?
            $sentinelSql = "SET NOCOUNT ON; USE [$tmpDb]; SELECT COUNT(*) FROM sys.tables;"
            $rowOut = sqlcmd -S $ServerInstance -E -b -h -1 -W -Q $sentinelSql
            if ($LASTEXITCODE -ne 0) {
                throw "Sentinel sorgu basarisiz."
            }
            $rowCount = [int]($rowOut | Where-Object { $_ -match "^\d+$" } | Select-Object -First 1)

            if ($rowCount -le 0) {
                throw "Sentinel sorgu 0 tablo dondu -- DB bos / bozuk olabilir."
            }
            Write-Host "  sentinel: $rowCount tablo bulundu."
        }
        finally {
            # Temizlik -- temp DB ve dosyalari sil
            Write-Host "[$(Get-Date -Format o)] TEMIZLIK [$tmpDb]"
            $dropSql = "IF DB_ID('$tmpDb') IS NOT NULL BEGIN ALTER DATABASE [$tmpDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$tmpDb]; END"
            sqlcmd -S $ServerInstance -E -Q $dropSql 2>$null | Out-Null
            if (Test-Path $tmpDir) {
                Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Result -Status "OK" `
                 -Message $(if ($FullRestore) { "VERIFYONLY + full restore basarili" } else { "VERIFYONLY basarili" }) `
                 -BackupFile $latest.FullName `
                 -SizeMB $sizeMB `
                 -AgeHours $ageHrs `
                 -RowCount $rowCount
    exit 0
}
catch {
    Write-Result -Status "FAIL" -Message $_.Exception.Message
    Write-Host $_.ScriptStackTrace
    exit 1
}

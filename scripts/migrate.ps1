# =============================================================================
# Kargoyeri.Studio — EF Core Migration Helper
# =============================================================================
# Kullanim:
#   .\scripts\migrate.ps1 -Action list                    # mevcut migration'lari listele
#   .\scripts\migrate.ps1 -Action add -Name AddInvoices   # yeni migration olustur
#   .\scripts\migrate.ps1 -Action update                  # DB'yi en sona getir
#   .\scripts\migrate.ps1 -Action update -Target 20260424123910_InitialCreate
#   .\scripts\migrate.ps1 -Action script -From InitialCreate -To AddInvoices
#   .\scripts\migrate.ps1 -Action remove                  # son migration'i geri al
#   .\scripts\migrate.ps1 -Action drop                    # DB'yi sifirla (DEV-ONLY!)
#
# Onkosul: dotnet-ef global tool yuklu olmali:
#   dotnet tool install --global dotnet-ef --version 8.0.*
# =============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("list","add","update","script","remove","drop","status")]
    [string]$Action,

    [string]$Name,
    [string]$Target,
    [string]$From,
    [string]$To,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Repo kokunden calistirildigini varsayar
$repoRoot = Split-Path -Parent $PSScriptRoot
$infraProj = Join-Path $repoRoot "..\Projeler\KargoEntegre--Full-Otomatik-master\Kargoyeri\src\Kargoyeri.Infrastructure\Kargoyeri.Infrastructure.csproj"
$startupProj = Join-Path $repoRoot "src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj"
$dbContext = "KargoyeriDbContext"

if (-not (Test-Path $infraProj)) {
    Write-Error "Infrastructure projesi bulunamadi: $infraProj"
}
if (-not (Test-Path $startupProj)) {
    Write-Error "Studio.Web projesi bulunamadi: $startupProj"
}

function Invoke-Ef([string[]]$args) {
    $full = @("ef") + $args + @("--project", $infraProj, "--startup-project", $startupProj, "--context", $dbContext)
    Write-Host ">>> dotnet $($full -join ' ')" -ForegroundColor Cyan
    & dotnet @full
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet ef komutu basarisiz oldu (exit $LASTEXITCODE)." }
}

switch ($Action) {
    "list" {
        Invoke-Ef @("migrations","list")
    }
    "status" {
        Write-Host "== Migrations =="; Invoke-Ef @("migrations","list")
        Write-Host "== Pending (--no-build): hizlica kontrol icin =="
        Invoke-Ef @("migrations","has-pending-model-changes")
    }
    "add" {
        if (-not $Name) { Write-Error "Add icin -Name zorunlu (orn: -Name AddBillingTier)" }
        $output = Join-Path (Split-Path -Parent $infraProj) "Persistence\SqlServer\Migrations"
        Invoke-Ef @("migrations","add",$Name,"--output-dir","Persistence\SqlServer\Migrations")
        Write-Host "OK: Migration olusturuldu. Diff'i incele, sonra: .\scripts\migrate.ps1 -Action update" -ForegroundColor Green
    }
    "update" {
        $args = @("database","update")
        if ($Target) { $args += $Target }
        Invoke-Ef $args
    }
    "script" {
        if (-not $From -or -not $To) { Write-Error "Script icin -From ve -To zorunlu." }
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $out = Join-Path $repoRoot "scripts\migration-$From-to-$To-$stamp.sql"
        Invoke-Ef @("migrations","script",$From,$To,"--output",$out,"--idempotent")
        Write-Host "OK: SQL uretildi -> $out" -ForegroundColor Green
    }
    "remove" {
        if (-not $Force) {
            $r = Read-Host "Son migration kaldirilacak. Devam? (yes/no)"
            if ($r -ne "yes") { return }
        }
        Invoke-Ef @("migrations","remove")
    }
    "drop" {
        Write-Warning "Bu islem TUM VERIYI siler. Sadece DEV ortaminda kullan!"
        if (-not $Force) {
            $r = Read-Host "Database drop edilecek. Onayla (yes/no)"
            if ($r -ne "yes") { return }
        }
        Invoke-Ef @("database","drop","--force")
    }
}

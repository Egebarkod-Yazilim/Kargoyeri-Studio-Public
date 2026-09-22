# P5-#10 i18n audit script
#
# .cshtml dosyalarinda hardcoded Turkce karakterli stringleri tarar ve
# resx'e tasinmasi gereken adaylari listeler.
#
# Usage:
#   pwsh scripts/i18n-audit.ps1                # tum cshtml'ler
#   pwsh scripts/i18n-audit.ps1 -Path Views/Billing  # sadece klasor
#   pwsh scripts/i18n-audit.ps1 -Top 20         # en cok ihlal yapan 20 dosya

param(
    [string]$Path = "src/Kargoyeri.Studio.Core/Views",
    [int]$Top = 0,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

# Turkce karakterli stringler veya yaygin Turkce kelimeler
$tokens = @(
    'şğüçöıİĞÜÇÖŞ',  # Turkce diakritik
    '\bKaydet\b','\bIptal\b','\bSil\b','\bDuzenle\b','\bOlustur\b',
    '\bAra\b','\bYenile\b','\bGonder\b','\bAlici\b','\bGonderici\b',
    '\bDurum\b','\bTarih\b','\bToplam\b','\bSaglayici\b'
)

$pattern = $tokens -join '|'

$files = Get-ChildItem -Recurse -Path $Path -Filter *.cshtml -File
$findings = @()

foreach ($f in $files) {
    $lines = Get-Content $f.FullName
    $inFunctions = $false
    $inCodeBlock = $false  # @{ ... } veya @code/functions
    $braceDepth = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        # @functions / @code blok takibi
        if (-not $inFunctions -and $line -match '^\s*@(functions|code)\s*\{') {
            $inFunctions = $true
            $braceDepth = 1
            continue
        }
        if ($inFunctions) {
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            if ($braceDepth -le 0) { $inFunctions = $false }
            continue
        }

        # Razor directive ve tek satirlik C# bloklarini atla
        if ($line -match '^\s*@(model|using|inject|inherits|addTagHelper|removeTagHelper|namespace|section|\*|\{|\})') { continue }
        # Saf C# satirlari (var x = ..., string y = ...): @ ile baslamadan icinde Turkce yoksa pas
        if ($line -match '^\s*(var|string|int|bool|record|static|return|if\s|else|switch|case|new\s)\s' -and $line -notmatch '<\w') { continue }

        if ($line -match $pattern) {
            $findings += [pscustomobject]@{
                File = $f.FullName
                Line = $i + 1
                Text = $line.Trim()
            }
        }
    }
}

if ($Json) {
    $findings | ConvertTo-Json -Depth 3
    return
}

# Ozet
$byFile = $findings | Group-Object File | Sort-Object Count -Descending
$total = $findings.Count
Write-Host ""
Write-Host "i18n audit raporu" -ForegroundColor Cyan
Write-Host "================="
Write-Host "Toplam ihlal: $total"
Write-Host "Etkilenen dosya: $($byFile.Count)"
Write-Host ""

$display = if ($Top -gt 0) { $byFile | Select-Object -First $Top } else { $byFile }
foreach ($g in $display) {
    $rel = $g.Name.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, '')
    Write-Host ("{0,5}  {1}" -f $g.Count, $rel) -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Detay icin: pwsh scripts/i18n-audit.ps1 -Json | ConvertFrom-Json | ?{ `$_.File -like '*Billing*' }" -ForegroundColor DarkGray

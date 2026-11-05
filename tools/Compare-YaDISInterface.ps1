# Compare les exports produits par yaDIS2 et yaDIS3 avec la même bibliothèque.
# Exemple :
# ./tools/Compare-YaDISInterface.ps1 -Yadis2 'C:\exports\yaDIS2\interface' -Yadis3 'C:\exports\yaDIS3\interface'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Yadis2,

    [Parameter(Mandatory = $true)]
    [string]$Yadis3,

    [switch]$ComparePixels
)

$ErrorActionPreference = 'Stop'
$roots = @(
    (Resolve-Path -LiteralPath $Yadis2).Path.TrimEnd('\', '/'),
    (Resolve-Path -LiteralPath $Yadis3).Path.TrimEnd('\', '/')
)

function Get-Manifest {
    param([string]$Root)

    $items = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $relative = $file.FullName.Substring($Root.Length).TrimStart('\', '/').Replace('\', '/')
        $items[$relative] = $file.FullName
    }
    return $items
}

function Get-ComparableText {
    param([string]$Path)

    $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    if ($extension -eq '.xml') {
        $document = [xml](Get-Content -LiteralPath $Path -Raw)
        $document.PreserveWhitespace = $false
        return $document.OuterXml
    }

    if ([IO.Path]::GetFileName($Path) -eq 'dune_folder.txt') {
        $content = Get-Content -LiteralPath $Path -Raw
        # Le format yaDIS2 utilise aussi des séparateurs "|" pour ses directives.
        return ($content -replace '\r\n?', "`n").Trim()
    }

    return $null
}

# Comparaison des pixels décodés JPG/PNG sur Windows.
# Les AAI demeurent comparés octet par octet.
function Get-PixelSignature {
    param([string]$Path)

    $original = $null
    $normalized = $null
    $graphics = $null
    $sha = $null
    $bits = $null
    try {
        $original = [System.Drawing.Bitmap]::new($Path)
        $normalized = [System.Drawing.Bitmap]::new(
            $original.Width,
            $original.Height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        $graphics = [System.Drawing.Graphics]::FromImage($normalized)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImageUnscaled($original, 0, 0)
        $rectangle = [System.Drawing.Rectangle]::new(0, 0, $normalized.Width, $normalized.Height)
        $bits = $normalized.LockBits(
            $rectangle,
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        $bytes = [byte[]]::new([Math]::Abs($bits.Stride) * $bits.Height)
        [System.Runtime.InteropServices.Marshal]::Copy(
            $bits.Scan0, $bytes, 0, $bytes.Length
        )
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '')
        return "$($original.Width)x$($original.Height):$hash"
    }
    finally {
        if ($null -ne $bits) { $normalized.UnlockBits($bits) }
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $graphics) { $graphics.Dispose() }
        if ($null -ne $normalized) { $normalized.Dispose() }
        if ($null -ne $original) { $original.Dispose() }
    }
}

if ($ComparePixels) {
    Add-Type -AssemblyName System.Drawing
}

$left = Get-Manifest -Root $roots[0]
$right = Get-Manifest -Root $roots[1]
$allPaths = @(@($left.Keys) + @($right.Keys) | Sort-Object -Unique)
$rows = foreach ($relative in $allPaths) {
    $a = $left[$relative]
    $b = $right[$relative]
    if (-not $a) {
        $status = 'Seulement yaDIS3'
    }
    elseif (-not $b) {
        $status = 'Seulement yaDIS2'
    }
    else {
        $hashA = (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash
        $hashB = (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash
        if ($hashA -eq $hashB) {
            $status = 'Identique'
        }
        else {
            $textA = Get-ComparableText -Path $a
            $textB = Get-ComparableText -Path $b
            if ($null -ne $textA -and $textA -ceq $textB) {
                $status = 'Texte equivalent, octets differents'
            }
            elseif ($ComparePixels -and [IO.Path]::GetExtension($a).ToLowerInvariant() -in @('.jpg', '.jpeg', '.png')) {
                try {
                    $pixelA = Get-PixelSignature -Path $a
                    $pixelB = Get-PixelSignature -Path $b
                    $status = if ($pixelA -ceq $pixelB) {
                        'Pixels identiques, octets differents'
                    }
                    else {
                        'Different'
                    }
                }
                catch {
                    Write-Warning "Impossible de comparer les pixels de $relative : $_"
                    $status = 'Different'
                }
            }
            else {
                $status = 'Different'
            }
        }
    }

    [pscustomobject]@{
        Fichier = $relative
        Resultat = $status
    }
}

$rows | Format-Table -AutoSize
Write-Host ''
$rows | Group-Object Resultat | Sort-Object Name |
    ForEach-Object { Write-Host "$($_.Name) : $($_.Count)" }

Write-Host ''
Write-Host 'AAI : comparaison binaire uniquement. JPG/PNG : comparaison pixel si -ComparePixels est specifie.'
if (@($rows | Where-Object { $_.Resultat -notin @('Identique', 'Texte equivalent, octets differents', 'Pixels identiques, octets differents') }).Count -gt 0) {
    exit 1
}

# Compare les exports produits par yaDIS2 et yaDIS3 avec la même bibliothèque.
# Exemple :
# ./tools/Compare-YaDISInterface.ps1 -Yadis2 'C:\exports\yaDIS2\interface' -Yadis3 'C:\exports\yaDIS3\interface'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Yadis2,

    [Parameter(Mandatory = $true)]
    [string]$Yadis3
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

$left = Get-Manifest -Root $roots[0]
$right = Get-Manifest -Root $roots[1]
$allPaths = @($left.Keys + $right.Keys | Sort-Object -Unique)
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
Write-Host 'Les differences JPEG/AAI sont binaires : cette comparaison ne prouve pas une equivalence pixel.'
if (@($rows | Where-Object { $_.Resultat -notin @('Identique', 'Texte equivalent, octets differents') }).Count -gt 0) {
    exit 1
}

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',

    [Parameter()]
    [ValidateSet('Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
$root = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$version = (Get-Content -Raw -LiteralPath (Join-Path $root 'VERSION')).Trim()
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts "ServiceDeckManagement-$version-$Runtime"
$staging = Join-Path $artifacts ("staging-" + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $artifacts ("package-" + [Guid]::NewGuid().ToString('N'))
$intermediate = Join-Path $staging 'intermediate'
$lockHashes = @{}
Get-ChildItem -LiteralPath (Join-Path $root 'src'),(Join-Path $root 'tests') `
    -Filter 'packages.lock.json' -File -Recurse |
    ForEach-Object {
        $lockHashes[$_.FullName] =
            (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }

function Assert-UnderArtifacts([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($artifacts).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O destino de publicacao saiu da pasta artifacts.'
    }
}

Assert-UnderArtifacts $output
Assert-UnderArtifacts $staging
Assert-UnderArtifacts $package
if (Test-Path -LiteralPath $output) {
    throw "O destino ja existe: $output"
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
New-Item -ItemType Directory -Path $staging | Out-Null
New-Item -ItemType Directory -Path $package | Out-Null
try {
    $projects = @(
        'ServiceDeckManagement.Host',
        'ServiceDeckManagement.Manager',
        'ServiceDeckManagement.Setup',
        'ServiceDeckManagement.Launcher'
    )
    foreach ($project in $projects) {
        $projectFile = Join-Path $root "src\$project\$project.csproj"
        $projectOutput = Join-Path $staging $project
        & (Join-Path $PSScriptRoot 'dotnet.ps1') publish $projectFile `
            --configuration $Configuration `
            --runtime $Runtime `
            --self-contained true `
            --output $projectOutput `
            "-p:PortablePublishIntermediateRoot=$intermediate"
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao publicar $project."
        }
    }

    $changedLocks = @($lockHashes.GetEnumerator() | Where-Object {
        -not (Test-Path -LiteralPath $_.Key -PathType Leaf) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $_.Key).Hash -ne $_.Value
    })
    if ($changedLocks.Count -gt 0) {
        throw 'A publicacao alterou lock files rastreados.'
    }

    $app = Join-Path $package 'app'
    New-Item -ItemType Directory -Path $app -Force | Out-Null
    foreach ($project in $projects) {
        Copy-Item -Path (Join-Path $staging "$project\*") `
            -Destination $app -Recurse -Force
    }

    Copy-Item -LiteralPath (Join-Path $root '.servicedeck-root') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'VERSION') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'SECURITY.md') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'config') -Destination $package -Recurse

    $required = @(
        'ServiceDeckManagement.Host.exe',
        'ServiceDeckManagement.Manager.exe',
        'ServiceDeckManagement.Setup.exe',
        'ServiceDeckManagement.Launcher.exe'
    )
    foreach ($file in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $app $file) -PathType Leaf)) {
            throw "Binario ausente no pacote: $file"
        }
    }

    $hashes = Get-ChildItem -LiteralPath $app -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($package.Length).TrimStart('\').Replace('\', '/')
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            "$hash  $relative"
        }
    [IO.File]::WriteAllLines((Join-Path $package 'SHA256SUMS'), $hashes, $utf8)
    Move-Item -LiteralPath $package -Destination $output
    Write-Host "Pacote portatil criado em: $output"
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Assert-UnderArtifacts $staging
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    if (Test-Path -LiteralPath $package) {
        Assert-UnderArtifacts $package
        Remove-Item -LiteralPath $package -Recurse -Force
    }
}

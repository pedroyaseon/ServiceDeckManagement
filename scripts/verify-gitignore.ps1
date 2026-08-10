[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$failures = [System.Collections.Generic.List[string]]::new()

$mustBeIgnored = @(
    '.agents/README.md',
    'AGENTS.md',
    '.dotnet/dotnet.exe',
    '.dotnet-home/.sentinel',
    '.packages/example/package.bin',
    'app/ServiceDeckManagement.Manager.exe',
    'apps/Example/Example.exe',
    'artifacts/release.zip',
    'config/application.json',
    'config/security.json',
    'config/services/example.json',
    'data/servicedeckmanagement.db',
    'dashboard/index.html',
    'logs/services/example.log',
    'runtime/state/example.json',
    'src/Example/bin/Debug/example.dll',
    'src/Example/obj/project.assets.json',
    'tests/Example/TestResults/results.trx',
    '.env',
    'certificate.pfx'
)

$mustBeTrackable = @(
    '.servicedeck-root',
    'Directory.Build.props',
    'Directory.Packages.props',
    'NuGet.Config',
    'config/README.md',
    'config/examples/service-definition.example.json',
    'config/schemas/service-definition.v1.schema.json',
    'src/Example/Example.cs',
    'tests/Example/ExampleTests.cs'
)

foreach ($path in $mustBeIgnored) {
    & git -C $root check-ignore --quiet -- $path
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("O caminho deveria estar ignorado: $path")
    }
}

foreach ($path in $mustBeTrackable) {
    & git -C $root check-ignore --quiet -- $path
    if ($LASTEXITCODE -eq 0) {
        $failures.Add("O caminho publico nao pode estar ignorado: $path")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Validacao do .gitignore aprovada.'
exit 0

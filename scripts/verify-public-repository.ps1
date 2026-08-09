[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$failures = [System.Collections.Generic.List[string]]::new()

$ignoredDirectories = @(
    '.git', 'bin', 'obj', 'node_modules', 'runtime', 'artifacts', 'dist'
)
$textExtensions = @(
    '.cs', '.csproj', '.css', '.editorconfig', '.gitattributes', '.gitignore',
    '.json', '.md', '.props', '.ps1', '.targets', '.ts', '.tsx', '.txt',
    '.yaml', '.yml'
)

$files = Get-ChildItem -LiteralPath $root -Recurse -Force -File | Where-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\', '/')
    -not ($ignoredDirectories | Where-Object {
        $relative -eq $_ -or $relative.StartsWith("$_$([IO.Path]::DirectorySeparatorChar)")
    })
}

$forbiddenFilePatterns = @(
    '*.pfx', '*.p12', '*.pem', '*.key', '*.db', '*.sqlite', '*.sqlite3',
    '*.log', '*.local.json', '.env'
)
foreach ($file in $files) {
    foreach ($pattern in $forbiddenFilePatterns) {
        if ($file.Name -like $pattern) {
            $failures.Add("Arquivo proibido para publicação: $($file.FullName)")
        }
    }
}

$utf8 = [Text.UTF8Encoding]::new($false, $true)
$suspiciousText = @(
    [char] 0x00C3,
    [char] 0x00C2,
    (([char] 0x00E2).ToString() + ([char] 0x20AC).ToString()),
    [char] 0xFFFD
)
$secretPatterns = @(
    '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
    '(?i)\b(password|token|api[_-]?key|client[_-]?secret)\s*[:=]\s*["''][^"'']+["'']',
    '\bgh[pousr]_[A-Za-z0-9_]{20,}\b',
    '\bgithub_pat_[A-Za-z0-9_]{20,}\b'
)
$personalPathPatterns = @(
    '(?i)[A-Z]:\\Users\\[^\\\s]+',
    ('(?i)/' + 'home' + '/[^/\s]+'),
    ('(?i)/' + 'Users' + '/[^/\s]+')
)

foreach ($file in $files) {
    if ($textExtensions -notcontains $file.Extension -and
        $file.Name -notin @('AGENTS.md', 'LICENSE', 'README.md', 'VERSION')) {
        continue
    }

    try {
        $content = $utf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $failures.Add("Arquivo textual não é UTF-8 válido: $($file.FullName)")
        continue
    }

    foreach ($marker in $suspiciousText) {
        if ($content.IndexOf($marker, [StringComparison]::Ordinal) -ge 0) {
            $failures.Add("Possível mojibake '$marker': $($file.FullName)")
        }
    }

    foreach ($pattern in $secretPatterns) {
        if ($content -match $pattern) {
            $failures.Add("Possível segredo em: $($file.FullName)")
        }
    }

    foreach ($pattern in $personalPathPatterns) {
        if ($content -match $pattern) {
            $failures.Add("Possível caminho pessoal em: $($file.FullName)")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Verificação pública aprovada para $($files.Count) arquivos."

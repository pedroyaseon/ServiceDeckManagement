[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false, $true)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$failures = [System.Collections.Generic.List[string]]::new()

$textExtensions = @(
    '.cs', '.csproj', '.css', '.editorconfig', '.gitattributes', '.gitignore',
    '.json', '.md', '.props', '.ps1', '.sln', '.targets', '.ts', '.tsx',
    '.txt', '.xml', '.yaml', '.yml'
)

$relativeFiles = @(& git -C $root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) {
    throw 'Nao foi possivel enumerar o conteudo publicavel com o Git.'
}

$files = $relativeFiles |
    Sort-Object -Unique |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) }

$forbiddenFilePatterns = @(
    '*.pfx', '*.p12', '*.pem', '*.key', '*.db', '*.sqlite', '*.sqlite3',
    '*.log', '*.local.json', '.env'
)
foreach ($file in $files) {
    foreach ($pattern in $forbiddenFilePatterns) {
        if ($file.Name -like $pattern) {
            $failures.Add("Arquivo proibido para publicacao: $($file.FullName)")
        }
    }
}

$suspiciousText = @(
    ([char] 0x00C3).ToString(),
    ([char] 0x00C2).ToString(),
    (([char] 0x00E2).ToString() + ([char] 0x20AC).ToString()),
    ([char] 0xFFFD).ToString()
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
        $file.Name -notin @(
            '.servicedeck-root',
            'LICENSE',
            'README.md',
            'VERSION')) {
        continue
    }

    try {
        $content = $utf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $failures.Add("Arquivo textual nao e UTF-8 valido: $($file.FullName)")
        continue
    }

    foreach ($marker in $suspiciousText) {
        if ($content.Contains($marker)) {
            $failures.Add("Possivel mojibake '$marker': $($file.FullName)")
        }
    }

    foreach ($pattern in $secretPatterns) {
        if ($content -match $pattern) {
            $failures.Add("Possivel segredo em: $($file.FullName)")
        }
    }

    foreach ($pattern in $personalPathPatterns) {
        if ($content -match $pattern) {
            $failures.Add("Possivel caminho pessoal em: $($file.FullName)")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Verificacao publica aprovada para $($files.Count) arquivos."
exit 0

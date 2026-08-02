param(
    [Parameter(Mandatory = $true)]
    [string] $PackageRoot,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackageRoot).Path
$requiredFiles = @(
    'package.json',
    'README.md',
    'LICENSE.md',
    'CHANGELOG.md',
    'Editor/Analyzers/UnityBestPractices.Analyzers.dll',
    'Editor/Analyzers/UnityBestPractices.Analyzers.dll.meta'
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath) -PathType Leaf)) {
        throw "UPM package is missing '$relativePath'."
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'com.somedeveloper.unity-best-practices-analyzers') {
    throw "Unexpected UPM package name '$($manifest.name)'."
}

if ($manifest.version -ne $ExpectedVersion) {
    throw "UPM version '$($manifest.version)' does not match '$ExpectedVersion'."
}

$meta = Get-Content -LiteralPath (Join-Path $root 'Editor/Analyzers/UnityBestPractices.Analyzers.dll.meta') -Raw
if ($meta -notmatch '(?m)^-\s+RoslynAnalyzer\s*$') {
    throw 'Analyzer .meta file does not assign the RoslynAnalyzer label.'
}

if ($meta -notmatch '(?m)^\s*validateReferences:\s*0\s*$') {
    throw 'Analyzer .meta file must disable Unity reference validation.'
}

if ($meta -notmatch '(?ms)^\s*- first:\s*\r?\n\s*Standalone: OSXUniversal\s*\r?\n\s*second:\s*\r?\n\s*enabled: 0\s*$') {
    throw 'Analyzer .meta file must explicitly disable macOS player loading.'
}

Write-Host "Validated UPM package '$($manifest.name)' version '$($manifest.version)'."

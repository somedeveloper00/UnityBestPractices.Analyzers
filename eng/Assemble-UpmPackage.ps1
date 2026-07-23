param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $AnalyzerDll,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a valid SemVer package version."
}

$resolvedDll = (Resolve-Path -LiteralPath $AnalyzerDll).Path
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$templateRoot = Join-Path $repositoryRoot 'packaging/upm'
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($resolvedOutputDirectory -eq [System.IO.Path]::GetPathRoot($resolvedOutputDirectory)) {
    throw 'The UPM output directory cannot be a filesystem root.'
}

$packageRoot = Join-Path $resolvedOutputDirectory 'package'

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path (Join-Path $packageRoot 'Editor/Analyzers') -Force | Out-Null

$packageTemplate = Get-Content -LiteralPath (Join-Path $templateRoot 'package.json.template') -Raw
$packageJson = $packageTemplate.Replace('{{VERSION}}', $Version)
Set-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Value $packageJson -NoNewline

Copy-Item -LiteralPath $resolvedDll -Destination (Join-Path $packageRoot 'Editor/Analyzers/UnityBestPractices.Analyzers.dll')
Copy-Item -LiteralPath (Join-Path $templateRoot 'UnityBestPractices.Analyzers.dll.meta') -Destination (Join-Path $packageRoot 'Editor/Analyzers/UnityBestPractices.Analyzers.dll.meta')
Copy-Item -LiteralPath (Join-Path $templateRoot 'README.md') -Destination (Join-Path $packageRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $packageRoot 'CHANGELOG.md')

Write-Host "Assembled UPM package at $packageRoot"

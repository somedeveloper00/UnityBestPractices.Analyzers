param(
    [Parameter(Mandatory = $true)]
    [string] $PackageRoot,

    [Parameter(Mandatory = $true)]
    [string] $FixturesRoot,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion,

    [string] $StagingRoot = 'artifacts/unity-fixtures'
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageRoot).Path
$fixtures = (Resolve-Path -LiteralPath $FixturesRoot).Path
$staging = [System.IO.Path]::GetFullPath($StagingRoot)
$packageName = 'com.somedeveloper.unity-best-practices-analyzers'
$analyzerPath = 'Editor/Analyzers/UnityBestPractices.Analyzers.dll'

& (Join-Path $PSScriptRoot 'Validate-UpmPackage.ps1') -PackageRoot $package -ExpectedVersion $ExpectedVersion

$metadata = Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
if ($null -ne $metadata.dependencies -and $metadata.dependencies.PSObject.Properties.Count -ne 0) {
    throw 'The analyzer-only UPM package must not declare runtime dependencies.'
}

$allowedFiles = @(
    'CHANGELOG.md',
    'LICENSE.md',
    'README.md',
    'package.json',
    $analyzerPath,
    "$analyzerPath.meta"
)
$actualFiles = Get-ChildItem -LiteralPath $package -File -Recurse | ForEach-Object {
    $_.FullName.Substring($package.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
}
$unexpectedFiles = @($actualFiles | Where-Object { $_ -notin $allowedFiles })
if ($unexpectedFiles.Count -ne 0) {
    throw "UPM package contains undeclared runtime payload: $($unexpectedFiles -join ', ')."
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

$fixtureDirectories = @(Get-ChildItem -LiteralPath $fixtures -Directory | Where-Object {
    Test-Path -LiteralPath (Join-Path $_.FullName 'Packages/manifest.json') -PathType Leaf
})
if ($fixtureDirectories.Count -eq 0) {
    throw "No Unity fixtures were found under '$fixtures'."
}

foreach ($fixture in $fixtureDirectories) {
    $destination = Join-Path $staging $fixture.Name
    Copy-Item -LiteralPath $fixture.FullName -Destination $destination -Recurse
    $installedPackage = Join-Path $destination "Packages/$packageName"
    Copy-Item -LiteralPath $package -Destination $installedPackage -Recurse

    $manifestPath = Join-Path $destination 'Packages/manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($null -eq $manifest.dependencies) {
        throw "Fixture '$($fixture.Name)' manifest has no dependencies object."
    }
    $manifest.dependencies | Add-Member -NotePropertyName $packageName -NotePropertyValue "file:$packageName" -Force
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath

    $validatedManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($validatedManifest.dependencies.$packageName -ne "file:$packageName") {
        throw "Fixture '$($fixture.Name)' did not install the local package correctly."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $installedPackage $analyzerPath) -PathType Leaf)) {
        throw "Fixture '$($fixture.Name)' is missing the analyzer at '$analyzerPath'."
    }
    $installedMetadata = Get-Content -LiteralPath (Join-Path $installedPackage 'package.json') -Raw | ConvertFrom-Json
    if ($installedMetadata.name -ne $packageName -or $installedMetadata.version -ne $ExpectedVersion -or $installedMetadata.unity -ne '2021.3') {
        throw "Fixture '$($fixture.Name)' has invalid installed package metadata."
    }
}

Write-Host "Installed and validated package version '$ExpectedVersion' in $($fixtureDirectories.Count) Unity fixtures."

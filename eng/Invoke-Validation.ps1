[CmdletBinding()]
param(
    [ValidateSet('Restore', 'Build', 'RegressionHarness', 'XUnit', 'VersionCoherence', 'Documentation', 'Performance', 'NuGet', 'Upm', 'UnityManifests')]
    [string[]] $Stages = @('Restore', 'Build', 'RegressionHarness', 'XUnit', 'VersionCoherence', 'Documentation', 'Performance', 'NuGet', 'Upm', 'UnityManifests'),

    [switch] $SkipRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Native exit codes are checked explicitly so failures always include our exact command.
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Set-Location $repositoryRoot

$solution = 'UnityBestPractices.sln'
$analyzerProject = 'src/UnityBestPractices.Analyzers/UnityBestPractices.Analyzers.csproj'
$analyzerDll = 'src/UnityBestPractices.Analyzers/bin/Release/netstandard2.0/UnityBestPractices.Analyzers.dll'
$regressionProject = 'tests/UnityBestPractices.Analyzers.Tests/UnityBestPractices.Analyzers.Tests.csproj'
$xunitProject = 'tests/UnityBestPractices.Analyzers.Tests.Xunit/UnityBestPractices.Analyzers.Tests.Xunit.csproj'
$performanceProject = 'tests/UnityBestPractices.Analyzers.PerformanceTests/UnityBestPractices.Analyzers.PerformanceTests.csproj'
$packageDirectory = 'artifacts/packages'
$upmDirectory = 'artifacts/upm'

function Format-Command([string] $File, [string[]] $Arguments) {
    $formatted = $Arguments | ForEach-Object {
        if ($_ -match '[\s"'']') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    }
    return (@($File) + $formatted) -join ' '
}

function Invoke-Native([string] $File, [string[]] $Arguments) {
    $command = Format-Command $File $Arguments
    Write-Host "> $command"
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command exited with code ${LASTEXITCODE}: $command"
    }
}

function Invoke-Stage([string] $Name, [string] $ArtifactPath, [scriptblock] $Action) {
    $absoluteArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath, $repositoryRoot)
    Write-Host "`n=== $Name ==="
    try {
        & $Action
    }
    catch {
        Write-Error "Stage '$Name' failed.`nArtifact path: $absoluteArtifactPath`n$($_.Exception.Message)"
        exit 1
    }
}

$selectedStages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($stage in $Stages) { [void] $selectedStages.Add($stage) }
if ($SkipRestore) { [void] $selectedStages.Remove('Restore') }

if ($selectedStages.Contains('Restore')) {
    Invoke-Stage 'Restore' $solution { Invoke-Native dotnet @('restore', $solution) }
}
if ($selectedStages.Contains('Build')) {
    Invoke-Stage 'Release build' $analyzerDll {
        Invoke-Native dotnet @('build', $solution, '--configuration', 'Release', '--no-restore', '-p:ContinuousIntegrationBuild=true')
    }
}
if ($selectedStages.Contains('RegressionHarness')) {
    Invoke-Stage 'Regression harness' $regressionProject {
        Invoke-Native dotnet @('run', '--project', $regressionProject, '--configuration', 'Release', '--no-build')
    }
}
if ($selectedStages.Contains('XUnit')) {
    Invoke-Stage 'xUnit suite' $xunitProject {
        Invoke-Native dotnet @('test', $xunitProject, '--configuration', 'Release', '--no-build', '--no-restore')
    }
}

$version = $null
function Get-PackageVersion {
    if ($null -eq $script:version) {
        $command = "dotnet msbuild $analyzerProject -getProperty:Version"
        Write-Host "> $command"
        $script:version = (& dotnet msbuild $analyzerProject '-getProperty:Version').Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:version)) {
            throw "Command failed to return a package version: $command"
        }
    }
    return $script:version
}

if ($selectedStages.Contains('VersionCoherence')) {
    Invoke-Stage 'Version coherence' $analyzerDll {
        Invoke-Native pwsh @('-NoProfile', '-File', './eng/Validate-VersionCoherence.ps1', '-AnalyzerDll', $analyzerDll, '-ExpectedVersion', (Get-PackageVersion))
    }
}
if ($selectedStages.Contains('Documentation')) {
    Invoke-Stage 'Documentation generation and drift detection' 'docs/rules' {
        Invoke-Native git @('diff', '--quiet', 'HEAD', '--', 'docs/rules')
        Invoke-Native dotnet @('run', '--project', $regressionProject, '--configuration', 'Release', '--no-build', '--', '--generate-rule-docs', '.')
        Invoke-Native git @('diff', '--exit-code', '--', 'docs/rules')
    }
}
if ($selectedStages.Contains('Performance')) {
    Invoke-Stage 'Performance regression checks' $performanceProject {
        Invoke-Native dotnet @('run', '--project', $performanceProject, '--configuration', 'Release', '--no-build')
    }
}
if ($selectedStages.Contains('NuGet')) {
    Invoke-Stage 'NuGet packing and validation' $packageDirectory {
        $packageVersion = Get-PackageVersion
        Invoke-Native dotnet @('pack', $analyzerProject, '--configuration', 'Release', '--no-build', '--no-restore', '--output', $packageDirectory, '-p:ContinuousIntegrationBuild=true')
        Invoke-Native pwsh @('-NoProfile', '-File', './eng/Validate-NuGetPackage.ps1', '-PackagePath', "$packageDirectory/UnityBestPractices.Analyzers.$packageVersion.nupkg", '-ExpectedVersion', $packageVersion, '-SymbolPackagePath', "$packageDirectory/UnityBestPractices.Analyzers.$packageVersion.snupkg")
    }
}
if ($selectedStages.Contains('Upm')) {
    Invoke-Stage 'UPM assembly and validation' "$upmDirectory/package" {
        $packageVersion = Get-PackageVersion
        Invoke-Native pwsh @('-NoProfile', '-File', './eng/Assemble-UpmPackage.ps1', '-Version', $packageVersion, '-AnalyzerDll', $analyzerDll, '-OutputDirectory', $upmDirectory)
        Invoke-Native pwsh @('-NoProfile', '-File', './eng/Validate-UpmPackage.ps1', '-PackageRoot', "$upmDirectory/package", '-ExpectedVersion', $packageVersion)
    }
}
if ($selectedStages.Contains('UnityManifests')) {
    Invoke-Stage 'Unity manifest validation' 'tests/UnityIntegration/*/Packages/manifest.json' {
        $manifests = @(Get-ChildItem -Path 'tests/UnityIntegration/*/Packages/manifest.json' -File)
        if ($manifests.Count -eq 0) { throw 'No Unity fixture manifests were found.' }
        foreach ($manifest in $manifests) {
            $command = "ConvertFrom-Json $($manifest.FullName)"
            Write-Host "> $command"
            Get-Content -LiteralPath $manifest.FullName -Raw | ConvertFrom-Json | Out-Null
        }
    }
}

Write-Host "`nValidation completed successfully."

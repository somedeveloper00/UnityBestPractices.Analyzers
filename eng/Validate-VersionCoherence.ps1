param(
    [Parameter(Mandatory = $true)]
    [string] $AnalyzerDll,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$resolvedDll = (Resolve-Path -LiteralPath $AnalyzerDll).Path
if ([System.IO.Path]::GetFileName($resolvedDll) -ne 'UnityBestPractices.Analyzers.dll') {
    throw 'The analyzer release DLL must retain its default file name.'
}

$versionCore = ($ExpectedVersion -split '[-+]', 2)[0]
$expectedCoreVersion = [System.Version]::Parse($versionCore)
function Test-VersionCore([System.Version] $actual) {
    return $actual.Major -eq $expectedCoreVersion.Major -and
        $actual.Minor -eq $expectedCoreVersion.Minor -and
        $actual.Build -eq $expectedCoreVersion.Build -and
        ($actual.Revision -le 0)
}

$assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($resolvedDll).Version
if (-not (Test-VersionCore $assemblyVersion)) {
    throw "Assembly version '$assemblyVersion' does not match '$versionCore'."
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedDll)
if (-not (Test-VersionCore ([System.Version]::Parse($versionInfo.FileVersion)))) {
    throw "File version '$($versionInfo.FileVersion)' does not match '$versionCore'."
}

if (-not $versionInfo.ProductVersion.StartsWith($ExpectedVersion, [System.StringComparison]::Ordinal)) {
    throw "Informational version '$($versionInfo.ProductVersion)' does not begin with '$ExpectedVersion'."
}

Write-Host "Validated DLL assembly, file, and informational version coherence for '$ExpectedVersion'."

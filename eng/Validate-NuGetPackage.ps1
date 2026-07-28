param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion,

    [string] $SymbolPackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    $requiredEntries = @(
        'analyzers/dotnet/cs/UnityBestPractices.Analyzers.dll',
        'analyzers/dotnet/cs/UnityBestPractices.Analyzers.pdb',
        'README.md',
        'README.ja.md',
        'README.fa.md',
        'README.ru.md',
        'LICENSE',
        'CHANGELOG.md'
    )

    foreach ($entry in $requiredEntries) {
        if ($entryNames -notcontains $entry) {
            throw "NuGet package is missing '$entry'."
        }
    }

    $nuspecEntry = $archive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
    if ($null -eq $nuspecEntry) {
        throw 'NuGet package does not contain a nuspec.'
    }

    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml] $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ($metadata.id -ne 'UnityBestPractices.Analyzers') {
        throw "Unexpected package ID '$($metadata.id)'."
    }

    if ($metadata.version -ne $ExpectedVersion) {
        throw "NuGet version '$($metadata.version)' does not match '$ExpectedVersion'."
    }

    if ($metadata.license.type -ne 'expression' -or $metadata.license.'#text' -ne 'MIT') {
        throw 'NuGet package must use the MIT license expression.'
    }

    if ($metadata.repository.type -ne 'git' -or
        $metadata.repository.url -ne 'https://github.com/somedeveloper00/UnityBestPractices.Analyzers.git' -or
        [string]::IsNullOrWhiteSpace($metadata.repository.commit)) {
        throw 'NuGet repository metadata is incomplete.'
    }

    if ($metadata.readme -ne 'README.md') {
        throw 'NuGet package readme metadata is missing.'
    }

    if ($metadata.projectUrl -ne 'https://github.com/somedeveloper00/UnityBestPractices.Analyzers' -or
        [string]::IsNullOrWhiteSpace($metadata.tags) -or
        [string]::IsNullOrWhiteSpace($metadata.description)) {
        throw 'NuGet project URL, tags, or description metadata is incomplete.'
    }
}
finally {
    $archive.Dispose()
}

if (-not [string]::IsNullOrWhiteSpace($SymbolPackagePath)) {
    $resolvedSymbols = (Resolve-Path -LiteralPath $SymbolPackagePath).Path
    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($resolvedSymbols)
    try {
        $symbolEntries = @($symbolArchive.Entries | ForEach-Object FullName)
        if ($symbolEntries -notcontains 'analyzers/dotnet/cs/UnityBestPractices.Analyzers.pdb') {
            throw 'Symbol package is missing the analyzer portable PDB.'
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

Write-Host "Validated NuGet package '$resolvedPackage' version '$ExpectedVersion'."

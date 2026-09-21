<#
.SYNOPSIS
Validates a release tag and prints its NuGet version without publishing anything.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Tag)

$ErrorActionPreference = 'Stop'
$pattern = '^v(?<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-(?<prerelease>[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*))?)$'
if ($Tag -cnotmatch $pattern) {
    throw 'Expected vMAJOR.MINOR.PATCH with optional SemVer prerelease identifiers.'
}
$version = $Matches['version']
$prerelease = $Matches['prerelease']
if ($prerelease) {
    foreach ($identifier in $prerelease.Split('.')) {
        if ($identifier -match '^[0-9]+$' -and $identifier.Length -gt 1 -and $identifier.StartsWith('0')) {
            throw 'Numeric prerelease identifiers must not contain leading zeros.'
        }
    }
}
# AssemblyVersion has bounded numeric components even though SemVer itself does not.
foreach ($component in ($version.Split('-')[0]).Split('.')) {
    if ([long]$component -gt 65534) {
        throw 'Version components must fit .NET assembly version fields (0 through 65534).'
    }
}
Write-Output $version

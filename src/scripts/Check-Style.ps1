<#
.SYNOPSIS
Checks whitespace and using-declaration rules not fully enforced by the compiler.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$violations = @()
$files = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
foreach ($file in $files) {
    $lineNumber = 0
    $firstDeclaration = $null
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line -match '^\s*\t' -or $line -match '^\s+(await\s+)?using\s+(?!\()[^;]*=') {
            $violations += "$($file.FullName):$($lineNumber): Use spaces and scoped using blocks."
        }
        if ($null -eq $firstDeclaration -and $line -match '^\s*(?:namespace\s+|(?:(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*(?:class|record|interface|struct|enum|delegate)\s+)') {
            $firstDeclaration = $lineNumber
        }
        elseif ($null -eq $firstDeclaration -and $line -match '^\s*(?:var\s+|await\s+|return\s+|try\s*$|if\s*\(|for\s*\(|foreach\s*\(|while\s*\(|switch\s*\(|using\s*\()') {
            $violations += "$($file.FullName):$($lineNumber): Do not use top-level statements."
        }
    }
}
if ($violations.Count -gt 0) {
    throw ($violations -join [Environment]::NewLine)
}
Write-Output 'Source style checks passed.'

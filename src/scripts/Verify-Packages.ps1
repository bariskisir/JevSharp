<#
.SYNOPSIS
Builds packages, inspects assets, and executes an isolated NuGet consumer without live API calls.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Version)

$ErrorActionPreference = 'Stop'
$Version = & (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -Tag "v$Version"
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solution = Join-Path $repositoryRoot 'src/JevSharp.slnx'
$feed = Join-Path $repositoryRoot 'artifacts/packages'
$consumer = Join-Path $repositoryRoot ('artifacts/package-smoke/' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force -Path $feed, $consumer

function Invoke-DotNet {
    <# .SYNOPSIS Runs dotnet and fails immediately if it returns a nonzero exit code. #>
    param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet build $solution -c Release "-p:JevSharpPackageVersion=$Version"
Invoke-DotNet pack (Join-Path $repositoryRoot 'src/JevSharp/JevSharp.csproj') -c Release "-p:JevSharpPackageVersion=$Version" --no-build --no-restore --output $feed
Add-Type -AssemblyName System.IO.Compression.FileSystem
$id = 'JevSharp'
$archivePath = Join-Path $feed "$id.$Version.nupkg"
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName })
    foreach ($expected in @(
        'NuGetREADME.md',
        'LICENSE',
        'lib/net10.0/JevSharp.dll',
        'lib/net10.0/JevSharp.xml',
        'lib/net10.0/JevSharp.Core.dll',
        'lib/net10.0/JevSharp.Core.xml',
        'lib/net10.0/JevSharp.Abstractions.dll',
        'lib/net10.0/JevSharp.Abstractions.xml',
        'JevSharp.nuspec')) {
        if ($names -notcontains $expected) {
            throw "$archivePath is missing $expected."
        }
    }
    $reader = New-Object System.IO.StreamReader($archive.GetEntry("$id.nuspec").Open())
    try {
        [xml] $manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    $metadata = $manifest.package.metadata
    if ($metadata.id -ne $id -or $metadata.version -ne $Version -or $metadata.authors -ne 'bariskisir' -or $metadata.license.InnerText -ne 'MIT') {
        throw "Unexpected NuGet metadata in $archivePath."
    }
    if ($metadata.repository.url -ne 'https://github.com/bariskisir/JevSharp') {
        throw "Unexpected repository metadata in $archivePath."
    }
}
finally {
    $archive.Dispose()
}
if (-not (Test-Path -LiteralPath (Join-Path $feed "$id.$Version.snupkg"))) {
    throw "Missing symbols for $id."
}
$symbols = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $feed "$id.$Version.snupkg"))
try {
    $symbolNames = @($symbols.Entries | ForEach-Object { $_.FullName })
    foreach ($assembly in @('JevSharp', 'JevSharp.Core', 'JevSharp.Abstractions')) {
        if ($symbolNames -notcontains "lib/net10.0/$assembly.pdb") {
            throw "Missing symbols for $assembly in $id.$Version.snupkg."
        }
    }
}
finally {
    $symbols.Dispose()
}

# Local imports isolate the consumer from repository build/package settings.
Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props') -Value '<Project />' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $consumer 'Directory.Packages.props') -Value '<Project />' -Encoding UTF8
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JevSharp" Version="[__VERSION__]" />
  </ItemGroup>
</Project>
'@
Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Value $project.Replace('__VERSION__', $Version) -Encoding UTF8
$config = @'
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="__FEED__" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="JevSharp*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
'@
Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config') -Value $config.Replace('__FEED__', [System.Security.SecurityElement]::Escape($feed)) -Encoding UTF8
$program = @'
using JevSharp.Abstractions.Clients;
using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;
using JevSharp.Core.Configuration;
using JevSharp.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies the package through a standalone structured application.</summary>
internal static class Program
{
    /// <summary>Runs the offline package-consumer evaluation.</summary>
    private static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddJev(options => options.UseTypeSafe("offline-key"))
            .ConfigurePrimaryHttpMessageHandler(() => new OfflineHandler());
        using (var provider = services.BuildServiceProvider())
        {
            var client = provider.GetRequiredService<IJevClient>();
            var request = new JevRequest("A refund request", new Dictionary<string, JevQuestion>
            {
                ["refund"] = new NoulQuestion("Does the message request a refund?")
            });
            var response = await client.EvaluateAsync(request);
            if (response.GetAnswer<NoulAnswer>("refund").Probability != 0.9)
            {
                throw new InvalidOperationException("Package consumer received an unexpected answer.");
            }

            Console.WriteLine("Package consumer passed: one JevSharp reference provides models, client, and DI.");
        }
    }
}

/// <summary>A local-only HTTP handler for validating packaged assemblies.</summary>
sealed class OfflineHandler : HttpMessageHandler
{
    /// <summary>Returns a valid response without performing network I/O.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""{"model":"jev-1.13.0","usage":{"input_tokens":1,"output_tokens":1},"answers":{"refund":{"type":"noul","noul":0.9}}}""")
        });
    }
}
'@
Set-Content -LiteralPath (Join-Path $consumer 'Program.cs') -Value $program -Encoding UTF8
# A private restore directory prevents stale same-version development packages from masking changes.
Invoke-DotNet restore (Join-Path $consumer 'Consumer.csproj') --configfile (Join-Path $consumer 'NuGet.Config') --packages (Join-Path $consumer 'packages')
Invoke-DotNet run --project (Join-Path $consumer 'Consumer.csproj') -c Release --no-restore
Write-Output "Verified all packages at $feed."

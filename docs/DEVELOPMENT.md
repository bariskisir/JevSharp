# Development, samples, and releases

```shell
dotnet restore src/JevSharp.slnx
dotnet format src/JevSharp.slnx --verify-no-changes --no-restore
dotnet build src/JevSharp.slnx -c Release --no-restore -p:JevSharpPackageVersion=1.0.0
dotnet test src/JevSharp.slnx -c Release --no-build
pwsh -File src/scripts/Verify-Packages.ps1 -Version 1.0.0
```

On Windows PowerShell, use `powershell -File` if PowerShell 7 is unavailable. Offline tests use fake HTTP handlers and clocks. Live tests are skipped unless `JEV_RUN_LIVE_TESTS=1` and the corresponding provider key is present; they may incur provider charges and do not assert exact model probabilities.

The [console sample](../src/Samples/JevSharp.Console/Program.cs) demonstrates a three-question evaluation. The [web sample](../src/Samples/JevSharp.Web/Program.cs) demonstrates dependency injection, cancellation, and a controller endpoint. Replace sample key placeholders before running either project.

See [security reporting](../SECURITY.md), [repository conventions](../AGENTS.md), and [publishing instructions](PUBLISHING.md).

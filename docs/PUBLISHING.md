# Publishing JevSharp

The release workflow publishes one package: `JevSharp`. It contains the public contracts, core implementation, and dependency-injection integration. Author and repository metadata use `bariskisir` and `https://github.com/bariskisir/JevSharp`.

The matching `JevSharp.snupkg` includes portable PDB symbols for all three assemblies: `JevSharp`, `JevSharp.Core`, and `JevSharp.Abstractions`. Package verification checks each symbol entry as well as the assembly and XML documentation assets. Consumers still install only the `JevSharp` package.

## One-time NuGet configuration

Sign in to nuget.org as **bariskisir** and create a Trusted Publishing policy:

| Setting | Value |
| --- | --- |
| Repository owner | `bariskisir` |
| Repository | `JevSharp` |
| Workflow filename | `publish.yml` |
| Environment | Leave blank; the workflow does not use an environment |
| Package scope | `JevSharp` |
| Permissions | Publish new packages and new versions |

The account must own existing package IDs or be allowed to publish new IDs. Account policy configuration cannot be performed by repository files. No long-lived `NUGET_API_KEY` secret is used. The workflow requests a short-lived key through GitHub OIDC immediately before pushing packages.

Enable GitHub Actions on `bariskisir/JevSharp`. The workflow uses `contents: read` and grants `id-token: write` only to the publishing job. Consider repository rules that restrict who can create release tags.

## Release

1. Verify public API compatibility and merge the release commit after CI succeeds.
2. Create and push a version tag pointing to that commit:

```shell
git tag v1.0.0
git push origin v1.0.0
```

Prereleases such as `v1.1.0-preview.1` are supported. Tags must have exactly three numeric version components, optionally followed by valid SemVer prerelease identifiers. Leading zeros in numeric identifiers and build metadata are rejected. The leading `v` is removed for the NuGet version.

The workflow validates the tag, restores, checks style, builds, tests, packs, and installs the packages into an isolated smoke-test consumer. A separate publishing job downloads those verified artifacts, authenticates, and publishes dependencies before the main package. NuGet push also sends each matching `.snupkg` symbol package. Existing versions are skipped on reruns.

Do not move a published tag or replace an existing package version. If a release is wrong, fix it and publish a new version. When a workflow fails after publishing only some packages, rerun it for the same unchanged tag; duplicate packages are skipped.

## Local verification without publishing

```shell
pwsh -File src/scripts/Get-ReleaseVersion.ps1 -Tag v1.0.0
pwsh -File src/scripts/Verify-Packages.ps1 -Version 1.0.0
```

The verification script performs no remote publication and no live Jev requests. Generated packages and the isolated consumer are under `artifacts`. Packages include XML documentation, README, MIT license metadata, portable symbols, and repository metadata. The .NET SDK generates GitHub Source Link information in CI from the checked-out repository; untracked local sources are embedded for debugging.

## Troubleshooting

- **OIDC login fails:** confirm username, repository ownership, workflow filename, policy permissions, and `id-token: write` match. Check whether a newly created policy requires activation.
- **Package ownership fails:** ensure `bariskisir` owns or can create the `JevSharp` ID and that the policy scope includes it.
- **Tests or packaging fail:** no packages are published until the validation job succeeds. Fix the error before tagging another release.
- **Live verification:** ordinary release CI uses offline fixtures. Run explicitly enabled live tests separately when validating provider changes; their requests may incur charges.

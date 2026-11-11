# nupkg-validator

A .NET tool that validates a NuGet package before it ships: every dll is built in Release, carries the expected
version and public key token, and (optionally) the package declares no dependencies. It is also published as a
distroless container (`ghcr.io/nullean/nupkg-validator`) and a GitHub Action (`action.yml`) that runs it.

## Layout

| Path | What |
|---|---|
| `src/nupkg-validator` | the tool (net8.0 and net10.0), a `Nullean.Argh` CLI; `ValidatorCommand.cs` is the `validate` command |
| `build/scripts` | the F# build the `mise` tasks and CI call |
| `build/keys` | the strong-name key the tool's own assembly is signed with |
| `action.yml` | the GitHub Action: runs the `edge` container image |

## Commands

mise installs the SDK pinned in `global.json` (plus the net10.0 runtime the build and the package validation run on).
Run everything through it:

```bash
mise run build    # build the solution
mise run pack     # build, pack, validate the packages, report public API changes
mise run ci       # what CI's build job runs (there are no tests, so this is pack)
mise run check    # workflow security checks (zizmor)
mise run clean    # delete build output
mise run release  # show the release draft; `mise run release publish` releases
```

`mise tasks ls` lists them all. Packages and API reports go to `build/output`. `./build.sh <target>` runs the same
build targets directly; CI calls it step by step, including `publishcontainers`, which has no `mise` task.

## Releasing

release-drafter keeps a draft release up to date from the labels of merged pull requests. **Publishing the draft**
releases: it creates the tag, and `ci.yml` runs on it to build every RID's native-AOT package, push
`ghcr.io/nullean/nupkg-validator` (tagged `latest` and the version) and push the packages to nuget.org.
`mise run release` shows the draft. The [releasing](.claude/skills/releasing/SKILL.md) and
[breaking-changes](.claude/skills/breaking-changes/SKILL.md) skills have the details. Every pull request carries exactly
one release label.

## Packaging

- The tool ships as native-AOT packages, one per RID (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`,
  `osx-arm64`), a root package that points at them, and a framework-dependent `nupkg-validator.any` fallback.
- AOT only compiles for the machine it runs on, so a local `pack` keeps just the root and `any` packages. CI's
  `aot-pack` matrix builds each RID on a matching runner and the build job merges them before validating.
- `dotnet pack` puts the unsigned `obj/` assembly into the `any` package; `generatePackages` swaps in the signed
  `bin/` copy (`fixAnyPackageSigning`). `Paths.ManagedTargetFrameworks` must mirror the csproj's
  `TargetFrameworks`.

## Rules

- The CLI surface (`validate` and its flags) is the contract `action.yml`, the container and other repositories'
  builds rely on. Removing or renaming a flag is a breaking change.
- Inspect dlls through `System.Reflection.Metadata`, as `DllValidator` does, never by loading them: the tool runs
  as a native-AOT binary.

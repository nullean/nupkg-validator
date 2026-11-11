<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png">
    <img src="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-light-544x192.png" width="272" alt="nullean">
  </picture>
</p>

<h1 align="center">nupkg-validator</h1>

<p align="center">Check a NuGet package before you push it: every dll in it is built in Release, carries the version you expect and is strong-named with the key you expect.</p>

<p align="center">
  <a href="https://www.nuget.org/packages/nupkg-validator"><img src="https://img.shields.io/nuget/v/nupkg-validator?style=flat-square" alt="NuGet"></a>
  <a href="https://github.com/nullean/nupkg-validator/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/nullean/nupkg-validator/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
</p>

Validating its own published package, `nupkg-validator.any` 0.11.5:

```console
$ nupkg-validator validate nupkg-validator.any.0.11.5.nupkg -a nupkg-validator -k 96c599bbe3e70f5d
Temp output folder: /var/folders/sy/bhr44x9d3jzfdmc3d3l514900000gn/T/nupkg-validator.any.0.11.5

[dll] tools/net8.0/any/nupkg-validator.dll
[dll] nupkg-validator, Version=0.0.0.0, Culture=neutral, PublicKeyToken=96c599bbe3e70f5d
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.11.5.0
[version] Informational: 0.11.5+310cb5f68f4bdea4526140d7567c0662d9913428
[version] nupkg-validator.dll properly signed with token: 96c599bbe3e70f5d
[dll] tools/net10.0/any/nupkg-validator.dll
[dll] nupkg-validator, Version=0.0.0.0, Culture=neutral, PublicKeyToken=96c599bbe3e70f5d
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.11.5.0
[version] Informational: 0.11.5+310cb5f68f4bdea4526140d7567c0662d9913428
[version] nupkg-validator.dll properly signed with token: 96c599bbe3e70f5d
```

When a check fails it stops there, prints the reason on stderr and exits with `1`:

```console
$ nupkg-validator validate nupkg-validator.any.0.11.5.nupkg -a nupkg-validator -v 0.12.0
...
[version] tools/net8.0/any/nupkg-validator.dll AssemblyFileVersion expected 0.12.0.0, actual: 0.11.5.0
```

## Install

```sh
dotnet tool install --global nupkg-validator
```

On Linux (x64, arm64), Windows (x64, arm64) and macOS (arm64) this installs a self-contained native-AOT executable: no
shared .NET runtime, no JIT warm-up. Everywhere else it falls back to a framework-dependent build, which needs the .NET
runtime installed. Installing needs the .NET 10 SDK or later.

## Quick start

```sh
nupkg-validator validate build/output/MyLib.1.2.3.nupkg -v 1.2.3 -a MyLib -k 96c599bbe3e70f5d
```

It exits with `0` when every check passes. Every dll in the package is checked, unless `-a` narrows it to one assembly
name:

| Flag | Check |
|---|---|
| (always) | every dll is built in Release (the JIT optimizer is not disabled); `-r`, `--skip-release-mode` skips this |
| `-v`, `--expected-version` | AssemblyVersion is fixed to `Major.0.0.0` (`-n`, `--not-major-only` expects the full version instead), AssemblyFileVersion is `Major.Minor.Patch.0`, and AssemblyInformationalVersion is exactly the version given |
| `-k`, `--public-key` | the dll is strong-named with this public key token |
| `--no-dependencies` | the package declares no dependencies (this also prints the nuspec metadata) |

`-a`, `--assembly-name` checks only that dll; `-d`, `--dlls-to-skip` takes a comma-separated list of dlls to leave out.
A package with no matching dll fails, unless `--no-fail-on-missing-dlls` is given. `-t`, `--temp-folder` sets where the
package is extracted (the OS temp folder by default).

The informational version is compared exactly. Builds that append the commit (SourceLink, MinVer) produce
`1.2.3+<sha>`, so pass that full string to `-v`. The `Major.0.0.0` AssemblyVersion follows the
[.NET library versioning guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/versioning#version-numbers).

Always name the `validate` subcommand: a package path as the first argument is read as a command name.
`nupkg-validator validate --help` lists every option.

### GitHub Action

```yaml
- uses: nullean/nupkg-validator@main
  with:
    path: build/output/MyPackage.1.0.0.nupkg
    args: -v 1.0.0 -a MyAssembly -k 96c599bbe3e70f5d
```

The action runs `nupkg-validator validate <path> <args>` in the prebuilt, distroless `ghcr.io/nullean/nupkg-validator:edge`
container, so the workflow needs no .NET SDK. `path` is the package to validate; `args` passes through verbatim. Container
actions run on Linux runners only.

### Container

The same image works anywhere `docker run` does, such as GitLab CI or a machine without the .NET SDK:

```sh
docker run --rm -v "$(pwd)":/workspace ghcr.io/nullean/nupkg-validator:edge validate /workspace/MyPackage.1.0.0.nupkg -v 1.0.0
```

It is native-AOT on a chiseled `runtime-deps` base, with no shell, running as a non-root user. `edge` tracks the latest
build of `main`; `latest` and the version tags (for example `0.11.5`) follow releases.

## Packages

The tool ships as one package per platform. Install the root package; the .NET SDK picks the right one.

| Package | What it is | |
|---|---|---|
| [`nupkg-validator`](src/nupkg-validator/README.md) | the tool; points the SDK at the package for your platform | [![NuGet](https://img.shields.io/nuget/v/nupkg-validator?style=flat-square)](https://www.nuget.org/packages/nupkg-validator) |
| `nupkg-validator.<rid>` | the native-AOT build for `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64` and `osx-arm64` | [![NuGet](https://img.shields.io/nuget/v/nupkg-validator.linux-x64?style=flat-square)](https://www.nuget.org/packages/nupkg-validator.linux-x64) |
| `nupkg-validator.any` | the framework-dependent fallback (net8.0, net10.0) for every other platform | [![NuGet](https://img.shields.io/nuget/v/nupkg-validator.any?style=flat-square)](https://www.nuget.org/packages/nupkg-validator.any) |

## Learn more

- [Releases](https://github.com/nullean/nupkg-validator/releases)
- [Contributing](https://github.com/nullean/.github/blob/main/CONTRIBUTING.md)

MIT licensed. Copyright Nullean and contributors.

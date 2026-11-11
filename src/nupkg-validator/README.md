# nupkg-validator

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

A .NET tool that checks a NuGet package before you push it: every dll in it is built in Release, carries the version
you expect and is strong-named with the key you expect.

[![NuGet](https://img.shields.io/nuget/v/nupkg-validator?style=flat-square)](https://www.nuget.org/packages/nupkg-validator)

## Install

```sh
dotnet tool install --global nupkg-validator
```

On Linux (x64, arm64), Windows (x64, arm64) and macOS (arm64) this installs a native-AOT executable from the
`nupkg-validator.<rid>` package; everywhere else it falls back to the framework-dependent `nupkg-validator.any`. Install
the `nupkg-validator` package itself, not those. Installing needs the .NET 10 SDK or later.

## Quick start

```sh
nupkg-validator validate build/output/MyLib.1.2.3.nupkg -v 1.2.3 -a MyLib -k 96c599bbe3e70f5d
```

It exits with `0` when every check passes, and with `1` and the reason on stderr when one fails:

```console
$ nupkg-validator validate nupkg-validator.any.0.11.5.nupkg -a nupkg-validator -v 0.12.0
...
[version] tools/net8.0/any/nupkg-validator.dll AssemblyFileVersion expected 0.12.0.0, actual: 0.11.5.0
```

| Flag | Check |
|---|---|
| (always) | every dll is built in Release; `-r`, `--skip-release-mode` skips this |
| `-v`, `--expected-version` | AssemblyVersion is fixed to `Major.0.0.0` (`-n` expects the full version instead), AssemblyFileVersion is `Major.Minor.Patch.0`, and AssemblyInformationalVersion is exactly the version given |
| `-k`, `--public-key` | the dll is strong-named with this public key token |
| `--no-dependencies` | the package declares no dependencies |

`-a`, `--assembly-name` checks only that dll; `-d`, `--dlls-to-skip` takes a comma-separated list to leave out. Without
`--no-fail-on-missing-dlls` a package with no matching dll fails. The informational version is compared exactly, so a
build that appends the commit needs the full `1.2.3+<sha>`.

It also runs as a GitHub Action (`uses: nullean/nupkg-validator@main`) and as a container
(`ghcr.io/nullean/nupkg-validator`), both without a .NET SDK.

## Learn more

- Source, GitHub Action and container usage: https://github.com/nullean/nupkg-validator
- Releases: https://github.com/nullean/nupkg-validator/releases

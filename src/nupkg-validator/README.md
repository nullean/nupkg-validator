# nupkg-validator

A .NET tool that checks a NuGet package before you push it: every dll in it is built in Release, carries the version
you expect and is strong-named with the public key token you expect.

```bash
dotnet tool install --global nupkg-validator
nupkg-validator validate build/output/MyLib.1.2.3.nupkg -v 1.2.3 -a MyLib -k 96c599bbe3e70f5d
```

It exits with `0` when every check passes, and with `1` and the reason on stderr when one fails.

## Checks

| Flag | Check |
|---|---|
| (always) | every dll is built in Release (optimizations not disabled); `-r` skips this |
| `-v`, `--expected-version` | AssemblyFileVersion is `M.N.P.0`, AssemblyInformationalVersion is the version itself, AssemblyVersion is fixed to `M.0.0.0` (`-n` expects the full version instead) |
| `-k`, `--public-key` | the dll is strong-named with this public key token |
| `--no-dependencies` | the package declares no dependencies |

`-a`, `--assembly-name` checks only that dll; `-d`, `--dlls-to-skip` takes a comma-separated list to leave out. Without
`--no-fail-on-missing-dlls` a package with no matching dll fails.

On Linux, Windows and macOS (arm64) the tool installs as a native-AOT binary; elsewhere it falls back to a
framework-dependent build. It also runs as a container (`ghcr.io/nullean/nupkg-validator`) and as a GitHub Action: see
https://github.com/nullean/nupkg-validator.

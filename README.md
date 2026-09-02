<p>
<img align="right" src="nuget-icon.png">  

# nupkg-validator
</p>

Inspect and validate the contents of your NuGet packages before you push them out in the world.

Available inspections

- Inspects that all dlls are build in `Release` configuration
- Inspects version numbers of dlls inside the nuget package.
- Inspect that dlls have the right public key token applied

The tool will also emit all metadata in a way that its easy unleash your own bash/powershell/scripting skills
against standard out.

## Installation

Distributed as a .NET tool so install using the following

```
dotnet tool install nupkg-validator
```

On Linux, Windows and macOS/arm64, this resolves to a self-contained native-AOT executable — no
shared .NET runtime required, and no first-run JIT warmup. Everywhere else, it falls back to a
framework-dependent build (requires the .NET runtime the tool targets to already be installed).

## Run

```bat
dotnet nupkg-validator validate <path-to-package>
```

You can omit `dotnet` if you install this as a global tool.

> [!IMPORTANT]
> Starting from `1.0.0`, the package path is passed to an explicit `validate` subcommand rather than
> as a bare first argument (i.e. `nupkg-validator validate <path>` instead of `nupkg-validator <path>`).
> This is a breaking change from earlier `0.x` releases, a side effect of moving off `Argu` (which no
> longer worked once this tool was AOT-compiled, see below) onto [`Nullean.Argh`](https://github.com/nullean/curb).

```bat
Usage: nupkg-validator validate <path> [options]

   Extract a NuGet package and validate the dlls inside it: release-mode, version numbers, strong-name signing, and optionally that the package declares no dependencies.

Arguments:
  <path>  -, --path, Path to the .nupkg file to validate.

Options:
  -a, --assembly-name <string>     Filter for dll(s) with this assembly name. Defaults to every dll in the package.
  -d, --dlls-to-skip <string>      Comma-separated dll file names to skip validation for.
  -v, --expected-version <string>  Assert this version number was set properly on the dlls.
  -n, --not-major-only             Assert AssemblyVersion equals --expected-version exactly, instead of only its Major.0.0.0 component.
  -k, --public-key <string>        Assert this public key token is on the dlls' AssemblyName.
  -t, --temp-folder <string>       Where to extract the package contents. Defaults to the OS temp folder.
  -r, --skip-release-mode          Skip validation that the dlls were built in Release mode.
  --no-fail-on-missing-dlls        Don't fail when no dlls are found (matched by --assembly-name, if given).
  --no-dependencies                Assert the package declares no dependencies.
```

#### Examples:

Print out nuspec and dll metadata information.

By default the tool inspects all dlls for Release mode. There is no toggle to turn this of. Feel free to open an issue
with your usecase if you need this.

```bat
dotnet nupkg-validator validate build/output/nupkg-validator*.nupkg
```

truncated output example:

```
Temp output folder: /tmp/nupkg-validator.0.2.1-canary.0.9

[nuspec] file: /tmp/nupkg-validator.0.2.1-canary.0.9/nupkg-validator.nuspec
[nuspec] namespace: http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd
[metadata] id: nupkg-validator 
[metadata] version: 0.2.1-canary.0.9 
[metadata] title: nupkg-validator: a dotnet tool to validate NuGet packages 
[metadata] authors: nupkg-validator 
[metadata] owners: nupkg-validator 
...
[dll] tools/net10.0/any/nupkg-validator.dll
[dll] nupkg-validator, Version=0.0.0.0, Culture=neutral, PublicKeyToken=96c599bbe3e70f5d
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.2.1.0
[version] Informational: 0.2.1-canary.0.9
```

##### Validate version

```bat
dotnet nupkg-validator validate build/output/nupkg-validator*.nupkg -v 0.2.1-canary.0.9
```

Asserts best practices are being [followed around open source libraries](https://docs.microsoft.com/en-ca/dotnet/standard/library-guidance/versioning#version-numbers)

```
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.2.1.0
[version] Informational: 0.2.1-canary.0.9
```

Noteworthy is that the `AssemblyVersion` is expected to be `Major.0.0.0`, if you don't follow this pattern use `--not-major-only`

```bat
dotnet nupkg-validator validate build/output/nupkg-validator*.nupkg -v 0.2.1-canary.0.9 --not-major-only
```

##### Validate strong name

```bat
dotnet nupkg-validator validate build/output/nupkg-validator*.nupkg -k 96c599bbe3e
```

Asserts `PublicKeyToken=96c599bbe3e` is part of the full assembly name

##### Validate no nuget dependencies

A flag to fail the tool if the nuspec file declares dependencies to other NuGet packages.

```bat
dotnet nupkg-validator validate build/output/nupkg-validator*.nupkg --no-dependencies
```

using System.IO.Compression;
using Nullean.Argh;

namespace NupkgValidator;

internal sealed class ValidatorCommand
{
	/// <summary>
	/// Extract a NuGet package and validate the dlls inside it: release-mode, version numbers, strong-name
	/// signing, and optionally that the package declares no dependencies.
	/// </summary>
	/// <param name="path">-, --path, Path to the .nupkg file to validate.</param>
	/// <param name="assemblyName">-a, --assembly-name, Filter for dll(s) with this assembly name. Defaults to every dll in the package.</param>
	/// <param name="dllsToSkip">-d, --dlls-to-skip, Comma-separated dll file names to skip validation for.</param>
	/// <param name="expectedVersion">-v, --expected-version, Assert this version number was set properly on the dlls.</param>
	/// <param name="notMajorOnly">-n, --not-major-only, Assert AssemblyVersion equals --expected-version exactly, instead of only its Major.0.0.0 component.</param>
	/// <param name="publicKey">-k, --public-key, Assert this public key token is on the dlls' AssemblyName.</param>
	/// <param name="tempFolder">-t, --temp-folder, Where to extract the package contents. Defaults to the OS temp folder.</param>
	/// <param name="skipReleaseMode">-r, --skip-release-mode, Skip validation that the dlls were built in Release mode.</param>
	/// <param name="noFailOnMissingDlls">--no-fail-on-missing-dlls, Don't fail when no dlls are found (matched by --assembly-name, if given).</param>
	/// <param name="noDependencies">--no-dependencies, Assert the package declares no dependencies.</param>
	public int Validate(
		[Argument] string path,
		string? assemblyName = null,
		string? dllsToSkip = null,
		string? expectedVersion = null,
		bool notMajorOnly = false,
		string? publicKey = null,
		string? tempFolder = null,
		bool skipReleaseMode = false,
		bool noFailOnMissingDlls = false,
		bool noDependencies = false)
	{
		try
		{
			Run(path, assemblyName, dllsToSkip, expectedVersion, notMajorOnly, publicKey, tempFolder, skipReleaseMode, noFailOnMissingDlls, noDependencies);
			return 0;
		}
		catch (Exception e)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(e.Message);
			Console.ResetColor();
			return 1;
		}
	}

	private static void Run(
		string path, string? assemblyName, string? dllsToSkipArg, string? expectedVersion, bool notMajorOnly,
		string? publicKey, string? tempFolder, bool skipReleaseMode, bool noFailOnMissingDlls, bool noDependencies)
	{
		var nuGetPackagePath = Path.GetFullPath(path);
		if (!File.Exists(nuGetPackagePath))
			throw new FileNotFoundException($"Package does not exist {nuGetPackagePath}");

		var packageName = Path.GetFileNameWithoutExtension(nuGetPackagePath);
		var tmp = tempFolder ?? Path.GetTempPath();
		var tmpFolder = Directory.CreateDirectory(Path.Combine(tmp, packageName));
		Console.WriteLine($"Temp output folder: {tmpFolder.FullName}");

		try
		{
			ZipFile.ExtractToDirectory(nuGetPackagePath, tmpFolder.FullName, overwriteFiles: true);

			var specFile = tmpFolder.GetFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault()
				?? throw new InvalidOperationException($"No nuspec found in {tmpFolder.FullName}");

			if (noDependencies)
			{
				var spec = NuSpec.Load(specFile);
				var groupCount = spec.Dependencies.Count;
				if (groupCount != 0)
					throw new InvalidOperationException($"Package: {packageName}, has {groupCount} dependencies where none were expected");
			}

			var searchFor = assemblyName is not null ? $"{assemblyName}.dll" : "*.dll";
			var allDlls = tmpFolder.GetFiles(searchFor, SearchOption.AllDirectories);
			if (!noFailOnMissingDlls && allDlls.Length == 0)
				throw new InvalidOperationException($"No dlls found in {tmpFolder.FullName}, looking for {searchFor}");

			var skipDlls = (dllsToSkipArg ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

			DllValidator.Scan(allDlls, tmpFolder, expectedVersion, !notMajorOnly, publicKey, skipDlls, skipReleaseMode);

			if (skipDlls.Length > 0 && allDlls.All(dll => DllValidator.DllFilter(skipDlls, dll)))
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine();
				Console.WriteLine("WARNING filter -d skipped ALL dlls for validation!");
				Console.WriteLine();
				Console.ResetColor();
			}
		}
		finally
		{
			tmpFolder.Delete(true);
		}
	}
}

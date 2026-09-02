using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NupkgValidator;

public static class DllValidator
{
	// DebuggableAttribute.DebuggingModes.DisableOptimizations — not public API to reference directly since
	// this reads the flag out of raw metadata rather than the BCL enum (see IsReleaseMode below).
	private const int DisableOptimizationsFlag = 0x100;

	/// <summary>
	/// Whether <paramref name="dll"/> was built in Release mode, i.e. it either has no assembly-level
	/// <see cref="DebuggableAttribute"/> at all, or has one with optimizations not disabled.
	/// <para>
	/// This reads the attribute straight out of the assembly's metadata tables via
	/// <see cref="System.Reflection.Metadata"/> rather than <see cref="Assembly.LoadFile"/> +
	/// reflection: loading an arbitrary, unrelated assembly at runtime to inspect it is not just
	/// unnecessary here, it is unsupported under NativeAOT outright (<c>PlatformNotSupportedException</c>).
	/// Metadata-only reading works everywhere, including AOT, because it never executes the target
	/// assembly's code or even resolves its dependencies.
	/// </para>
	/// </summary>
	private static bool IsReleaseMode(FileInfo dll)
	{
		using var stream = File.OpenRead(dll.FullName);
		using var peReader = new PEReader(stream);
		if (!peReader.HasMetadata) return true;

		var reader = peReader.GetMetadataReader();
		foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
		{
			var attribute = reader.GetCustomAttribute(handle);
			if (!IsDebuggableAttributeConstructor(reader, attribute.Constructor)) continue;

			var blob = reader.GetBlobBytes(attribute.Value);
			// Custom attribute blob layout (ECMA-335 §II.23.3): a 2-byte prolog, the fixed
			// constructor arguments, then a 2-byte named-argument count. DebuggableAttribute has no
			// writable properties, so it is never invoked with named arguments — the byte count of
			// the fixed arguments alone is enough to tell which of its two constructors was used.
			return blob.Length switch
			{
				6 => blob[3] == 0, // DebuggableAttribute(bool isJITTrackingEnabled, bool isJITOptimizerDisabled)
				8 => (BitConverter.ToInt32(blob, 2) & DisableOptimizationsFlag) == 0, // DebuggableAttribute(DebuggingModes modes)
				_ => true,
			};
		}

		return true;
	}

	private static bool IsDebuggableAttributeConstructor(MetadataReader reader, EntityHandle ctorHandle)
	{
		if (ctorHandle.Kind != HandleKind.MemberReference) return false;

		var memberRef = reader.GetMemberReference((MemberReferenceHandle)ctorHandle);
		if (memberRef.Parent.Kind != HandleKind.TypeReference) return false;

		var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
		return reader.GetString(typeRef.Namespace) == "System.Diagnostics"
			&& reader.GetString(typeRef.Name) == "DebuggableAttribute";
	}

	/// <summary>Parses only the Major.Minor.Patch prefix a semantic version starts with, ignoring any
	/// pre-release/build metadata suffix — all this tool ever compares against the fixed dll versions.</summary>
	private static (int Major, int Minor, int Patch) ParseSemVerCore(string version)
	{
		var core = version.Split('+')[0].Split('-')[0];
		var parts = core.Split('.');
		return (
			int.Parse(parts[0]),
			parts.Length > 1 ? int.Parse(parts[1]) : 0,
			parts.Length > 2 ? int.Parse(parts[2]) : 0);
	}

	public static bool DllFilter(IReadOnlyList<string> skipDlls, FileInfo dll) =>
		skipDlls.Any(skip => skip == dll.Name || skip == Path.GetFileNameWithoutExtension(dll.Name));

	private static void RunValidation(FileInfo dll, string relativePath, string? expectedVersion, bool fixedVersion, string? publicKey, bool skipReleaseMode)
	{
		var namedAssembly = AssemblyName.GetAssemblyName(dll.FullName);
		var dllVersion = FileVersionInfo.GetVersionInfo(dll.FullName);

		if (expectedVersion is not null)
		{
			var a = namedAssembly.Version ?? new Version(0, 0, 0, 0);
			var nonFixedVersion = $"{a.Major}.{a.Minor}.{a.Build}.0";
			var (major, minor, patch) = ParseSemVerCore(expectedVersion);
			var expectedFileVersion = $"{major}.{minor}.{patch}.0";

			if (fixedVersion && (a.Minor > 0 || a.Revision > 0 || a.Build > 0))
				throw new InvalidOperationException($"[version] {relativePath} AssemblyVersion is not fixed to {a.Major}.0.0.0");
			if (!fixedVersion && nonFixedVersion != expectedVersion)
				throw new InvalidOperationException($"[version] {relativePath} AssemblyVersion expected {expectedVersion} actual {nonFixedVersion}");

			if (dllVersion.FileVersion != expectedFileVersion)
				throw new InvalidOperationException($"[version] {relativePath} AssemblyFileVersion expected {expectedFileVersion}, actual: {dllVersion.FileVersion}");

			if (dllVersion.ProductVersion != expectedVersion)
				throw new InvalidOperationException($"[version] {relativePath} AsseblyInformationalVersion: expected: {expectedVersion} actual: {dllVersion.ProductVersion} ");
		}

		if (publicKey is not null)
		{
			var token = $"PublicKeyToken={publicKey}";
			if (!(namedAssembly.FullName ?? "").Contains(token))
				throw new InvalidOperationException($"[version] {dll.Name} is NOT publicly signed with expected token: {publicKey}");
			Console.Write($"[version] {dll.Name} properly signed with token: {publicKey}");
		}

		var releaseCheck = skipReleaseMode || IsReleaseMode(dll);
		if (!releaseCheck)
			throw new InvalidOperationException($"[version] {relativePath} is not build in Release mode. IsJitOptimizerDisabled returned true on assembly");
	}

	public static void Scan(
		IReadOnlyList<FileInfo> dlls, DirectoryInfo tmpFolder, string? expectedVersion, bool fixedVersion,
		string? publicKey, IReadOnlyList<string> skipDlls, bool skipReleaseMode)
	{
		foreach (var dll in dlls)
		{
			var relativePath = Path.GetRelativePath(tmpFolder.FullName, dll.FullName);
			var namedAssembly = AssemblyName.GetAssemblyName(dll.FullName);
			var dllVersion = FileVersionInfo.GetVersionInfo(dll.FullName);

			Console.WriteLine();
			Console.WriteLine($"[dll] {relativePath}");
			Console.WriteLine($"[dll] {namedAssembly.FullName}");
			Console.WriteLine($"[version] Assembly: {namedAssembly.Version}");
			Console.WriteLine($"[version] AssemblyFile: {dllVersion.FileVersion}");
			Console.WriteLine($"[version] Informational: {dllVersion.ProductVersion}");

			if (DllFilter(skipDlls, dll))
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("[dll] skipping validation because dll matches -d filter");
				Console.ResetColor();
			}
			else
				RunValidation(dll, relativePath, expectedVersion, fixedVersion, publicKey, skipReleaseMode);
		}
	}
}

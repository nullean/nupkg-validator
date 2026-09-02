using System.Xml.Linq;

namespace NupkgValidator;

public sealed record NuSpecDependency(string TargetFramework, string Id, string Version, IReadOnlyList<XAttribute> Attributes);

/// <summary>
/// A parsed .nuspec file. Dependencies are read only from &lt;group&gt; elements (the shape every
/// <c>dotnet pack</c>-generated nuspec uses, one group per target framework); a flat, ungrouped
/// &lt;dependencies&gt; list is not something this tool has ever recognized.
/// </summary>
public sealed class NuSpec
{
	public XNamespace Namespace { get; }
	public XDocument Document { get; }
	public IReadOnlyList<XElement> Metadata { get; }

	/// <summary>Dependencies grouped by target framework. The count of this list is the number of
	/// framework groups present, not the total number of individual dependencies across them.</summary>
	public IReadOnlyList<(string TargetFramework, IReadOnlyList<NuSpecDependency> Dependencies)> Dependencies { get; }

	private NuSpec(FileInfo specFile)
	{
		Document = XDocument.Load(specFile.FullName);
		var root = Document.Root ?? throw new InvalidOperationException($"{specFile.FullName} has no root element");
		Namespace = root.GetDefaultNamespace();

		var metadataElement = root.Element(Namespace + "metadata");
		Metadata = metadataElement?.Elements().Where(e => e.Name.LocalName != "dependencies").ToList() ?? [];

		var groups = metadataElement?.Element(Namespace + "dependencies")?.Elements(Namespace + "group") ?? [];
		Dependencies = groups
			.SelectMany(group =>
			{
				var tfm = group.Attribute("targetFramework")?.Value ?? "";
				return group.Elements(Namespace + "dependency").Select(dep => new NuSpecDependency(
					tfm,
					dep.Attribute("id")?.Value ?? "",
					dep.Attribute("version")?.Value ?? "",
					dep.Attributes().Where(a => a.Name.LocalName is not ("id" or "version")).ToList()));
			})
			.GroupBy(d => d.TargetFramework)
			.Select(g => (g.Key, (IReadOnlyList<NuSpecDependency>)g.ToList()))
			.ToList();
	}

	public static NuSpec Load(FileInfo specFile)
	{
		Console.WriteLine();
		Console.WriteLine($"[nuspec] file: {specFile.FullName}");

		var spec = new NuSpec(specFile);

		Console.WriteLine($"[nuspec] namespace: {spec.Namespace}");
		foreach (var e in spec.Metadata)
		{
			var attrs = string.Join(", ", e.Attributes().Select(a => $"@{a.Name.LocalName}={a.Value}"));
			Console.WriteLine($"[metadata] {e.Name.LocalName}: {e.Value} {attrs}");
		}

		if (spec.Metadata.Count == 0)
		{
			var nl = Environment.NewLine;
			throw new InvalidOperationException(
				$"Nuspec file yielded no metadata{nl}{nl}{spec.Document}{nl}{nl}This is most likely an xml namespace issue in this tool, please report an issue!{nl}");
		}

		foreach (var (tfm, deps) in spec.Dependencies)
		{
			Console.WriteLine();
			Console.WriteLine($"[framework] {tfm}");
			foreach (var d in deps)
			{
				var attrs = string.Join(", ", d.Attributes.Select(a => $"@{a.Name.LocalName}={a.Value}"));
				Console.WriteLine($"[dependency] {d.TargetFramework}, Id:{d.Id}, Version:{d.Version} {attrs}");
			}
		}

		return spec;
	}
}

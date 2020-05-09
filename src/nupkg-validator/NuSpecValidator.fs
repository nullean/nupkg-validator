module NuSpecValidator 

open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

type Dependency = {
    TargetFramework: string;
    Id: string;
    Version: string
    Attributes: (string * string) list
}

type NuSpec(specFile: FileInfo) =
    let spec = XDocument.Load(specFile.FullName)
    let (nameSpace, ns) =
        let ns = XmlNamespaceManager(NameTable())
        let attrs = spec.XPathEvaluate(@"//namespace::*[not(. = ../../namespace::*)]") :?> System.Collections.IEnumerable |> Seq.cast<XAttribute>
        // xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
        attrs
        |> Seq.iter (fun a ->
            let name = Regex.Replace(a.Name.LocalName, "(xmlns\:?)", "")
            ns.AddNamespace((match name with | "" -> "x" | n -> n), a.Value))

        //Instantiate an XmlNamespaceManager object. 
        //ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd")
        (ns.GetNamespacesInScope(XmlNamespaceScope.All).["x"], ns)
        
    let elements = spec.Root.XPathSelectElements("//x:metadata/*", ns)
    let metadata =
        elements
        |> Seq.filter(fun e -> e.Name.LocalName <> "dependencies")
        |> Seq.map (fun e -> e)
        |> Seq.toList
    
    let groups =
        spec.Document.Root.XPathSelectElements("//x:metadata/x:dependencies/x:group", ns)
        |> Seq.collect (fun e ->
            let tfm = e.Attribute(XName.Get("targetFramework"))
            let dependencies = e.XPathSelectElements("//x:dependency", ns)
            dependencies
            |> Seq.map (fun d ->
                let id = d.Attribute(XName.Get("id"))
                let version = d.Attribute(XName.Get("version"))
                let attrs =
                    d.Attributes()
                    |> Seq.toList
                    |> List.filter (fun a -> not(["id";"version"] |> List.contains a.Name.LocalName))
                    |> List.map (fun a -> (a.Name.LocalName, a.Value))
                
                { TargetFramework=tfm.Value; Id=id.Value; Version=version.Value; Attributes=attrs}
            )
            |> Seq.toList
        )
        |> Seq.toList
        |> List.groupBy (fun d -> d.TargetFramework)
    
    member this.NuSpecXmlNamespace = nameSpace
    member this.Document = spec
    member this.Metadata = metadata
    member this.Dependencies = groups
    

let Load (specFile:FileInfo) =
    // printfn "Specification file: %s" (spec.ToString())
    printfn ""
    printfn "[nuspec] file: %s" specFile.FullName
    
    let spec = NuSpec(specFile)
    
    printfn "[nuspec] namespace: %s" spec.NuSpecXmlNamespace
    spec.Metadata
    |> List.iter (fun e ->
        let attrsString = e.Attributes() |> Seq.map (fun a -> sprintf "@%s=%s" a.Name.LocalName a.Value) |> String.concat ", "
        printfn "[metadata] %s: %s %s" e.Name.LocalName e.Value attrsString
    )
    
    if spec.Metadata.Length = 0 then
        let failure =
            let nl = Environment.NewLine
            (sprintf "Nuspec file yielded no metadata%s%s" nl nl)
                + (sprintf "%s" (spec.Document.ToString()))
                + (sprintf "%s%sThis is most likely an xml namespace issue in this tool, please report an issue!%s" nl nl nl)
        failwith failure
        
    spec.Dependencies
    |> List.iter (fun (tfm, deps) ->
        printfn "" 
        printfn "[framework] %s" tfm
        deps |> List.iter (fun d ->
             let attrsString = d.Attributes |> List.map (fun (name, value) -> sprintf "@%s=%s" name value) |> String.concat ", "
             printfn "[dependency] %s, Id:%s, Version:%s %s" d.TargetFramework d.Id d.Version attrsString
        )
    )
    spec


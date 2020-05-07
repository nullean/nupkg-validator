module NuSpecValidator 

open System.IO
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
    let ns =
        let ns = XmlNamespaceManager(NameTable())
        ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd")
        ns
        
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
    
    member this.Document = spec
    member this.Metadata = metadata
    member this.Dependencies = groups
    

let Load (specFile:FileInfo) =
    // printfn "Specification file: %s" (spec.ToString())
    printfn "Specification file: %s" specFile.FullName
    printfn ""
    
    let spec = NuSpec(specFile)
    
    spec.Metadata
    |> List.iter (fun e -> printfn "[metadata] %s: %s" e.Name.LocalName e.Value)
    
    
    spec.Dependencies
    |> List.iter (fun (tfm, deps) ->
        printfn "" 
        printfn "[framework] %s" tfm
        deps |> List.iter (fun d ->
             let attrsString = d.Attributes |> List.map (fun (name, value) -> sprintf "%s=%s" name value) |> String.concat ", "
             printfn "[dependency] %s, Id:%s, Version:%s, @: %s" d.TargetFramework d.Id d.Version attrsString
        )
    )
    spec


module Program

open System
open System.IO
open Argu
open Fake.IO


type Arguments =
    | [<First; MainCommand; Mandatory; CliPrefix(CliPrefix.None)>] NuGetPackagePath of string
    | [<AltCommandLine("-v")>]ExpectedVersion of string 
    | [<AltCommandLine("-f")>]FixedVersion of bool
    | [<AltCommandLine("-a")>]AssemblyNameToLookFor of string
    | [<AltCommandLine("-k")>]PublicKey of string
    
    // validations
    | NoDependencies of bool
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | NuGetPackagePath _ -> "Specify the path to the nuget package"
            | ExpectedVersion _ -> "The version we expect to be set for all dlls in the nuget package"
            | FixedVersion _ -> "Make sure AssemblyVersion in dll is rounded down to the nearest major, defaults to true"
            | AssemblyNameToLookFor _ -> "The name of the assembly to look for in the nupkg, defaults to all"
            | PublicKey _ -> "The public key we expect the dlls to be signed with"
            
            | NoDependencies _ -> "Assert the package has NO dependencies"
            

let runValidation (parsed:ParseResults<Arguments>) =
    let nuGetPackagePath = parsed.GetResult NuGetPackagePath |> Path.GetFullPath
    if not(File.Exists nuGetPackagePath) then failwithf "Package does not exist %s" nuGetPackagePath
    
    let assemblyName = System.IO.Path.GetFileNameWithoutExtension nuGetPackagePath
    
    let tmp = Path.GetTempPath ()
    let tmpFolder = Directory.CreateDirectory(Path.Combine (tmp, assemblyName))
    printfn "Temp output folder: %s" tmpFolder.FullName
    
    Zip.unzip tmpFolder.FullName nuGetPackagePath
    
    let specFile =
        match tmpFolder.GetFiles("*.nuspec", SearchOption.TopDirectoryOnly) |> Seq.toList with
        | head::_ -> head
        | _ -> failwithf "No nuspec found in %s" tmpFolder.FullName
        
    let validateNoDependencies = parsed.TryGetResult NoDependencies |> Option.defaultValue false
    let dependencies =
        let spec = NuSpecValidator.Load specFile
        match (spec.Dependencies, validateNoDependencies) with
        | ([], true) -> spec
        | (_, true) -> failwithf "Package: %s, has dependencies where none were expected" assemblyName 
        | (deps, _) -> spec
        
    let dlls =
        let searchFor =
            match parsed.TryGetResult AssemblyNameToLookFor with
            | Some s -> sprintf "%s.dll" s
            | None -> "*.dll"
        match tmpFolder.GetFiles(searchFor, SearchOption.AllDirectories) |> Seq.toList with
        | [] -> failwithf "No dlls found in %s" tmpFolder.FullName
        | head -> head
    
    let version = parsed.GetResult ExpectedVersion 
    let fixedVersion = parsed.TryGetResult FixedVersion |> Option.defaultValue true 
    let publicKey = parsed.TryGetResult PublicKey
    
    DllValidator.Scan dlls tmpFolder version fixedVersion publicKey
    
    

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "nupkg-validator")
    let parsed = 
        try Some <| parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        with e ->
            printfn "%s" e.Message
            None
    match parsed with
    | None -> 2
    | Some p ->
        try runValidation p
        with e ->
            Console.ForegroundColor <- ConsoleColor.Red
            eprintfn "%s" e.Message
            Console.ResetColor()
            1

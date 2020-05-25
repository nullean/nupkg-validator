module Program

open System
open System.IO
open Argu
open Fake.IO


type Arguments =
    | [<First; MainCommand; Mandatory; CliPrefix(CliPrefix.None)>] NuGetPackagePath of string
    | [<AltCommandLine("-a")>]AssemblyNameToLookFor of string
    | [<AltCommandLine("-d")>]DllsToSkip of string
    
    // validations
    | [<AltCommandLine("-v")>]ExpectedVersion of string 
    | [<AltCommandLine("-n")>]NotMajorOnly of bool
    | [<AltCommandLine("-k")>]PublicKey of string
    | [<AltCommandLine("-t")>]TempFolder of string
    | [<AltCommandLine("-r")>]SkipReleaseMode of bool
    | NoDependencies of bool
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | NuGetPackagePath _ -> "Specify the path to the nuget package"
            | TempFolder _ -> "The temp folder location in which to extract the package contents"
            
            | ExpectedVersion _ -> "Assert that this version number was set properly on the dlls"
            | NotMajorOnly _ -> "Assert AssemblyVersion is the --expectedversion, by default we assert its MAJOR.0.0.0"
            | PublicKey _ -> "Assert this public key token makes it way on the AssemblyName for the dlls"
            | NoDependencies _ -> "Assert the package has NO dependencies"
            | SkipReleaseMode _ -> "Skip validation that the dlls are built in release mode"
            
            | AssemblyNameToLookFor _ -> "Filter for dll(s) with this AssemblyName"
            | DllsToSkip _ -> "Filter, comma separated list of strings of dlls file names to skip, defaults to none"

let private runSteps (parsed:ParseResults<Arguments>) (tmpFolder:DirectoryInfo) assemblyName =
    
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
        let dlls = tmpFolder.GetFiles(searchFor, SearchOption.AllDirectories) |> Seq.toList
        match dlls  with
        | [] -> failwithf "No dlls found in %s, looking for %s" tmpFolder.FullName searchFor
        | head -> head
      
        
    let skipDlls =
        let parsed = parsed.TryGetResult DllsToSkip |> Option.defaultValue ""
        parsed.Split(",", StringSplitOptions.RemoveEmptyEntries) |> Seq.toList
    
    let version = parsed.TryGetResult ExpectedVersion 
    let fixedVersion = parsed.TryGetResult NotMajorOnly |> Option.defaultValue true 
    let publicKey = parsed.TryGetResult PublicKey
    let skipReleaseMode = parsed.TryGetResult SkipReleaseMode |> Option.defaultValue false 
    
    DllValidator.Scan dlls tmpFolder version fixedVersion publicKey skipDlls skipReleaseMode
    
    let filteredAll = skipDlls.Length > 0 && dlls |> List.filter (fun dll -> not (DllValidator.DllFilter skipDlls dll)) |> List.length = 0
    if filteredAll then
       Console.ForegroundColor <- ConsoleColor.Blue
       printfn ""
       printfn "WARNING filter -d skipped ALL dlls for validation!"
       printfn ""
       Console.ResetColor()
            

let runValidation (parsed:ParseResults<Arguments>) =
    let nuGetPackagePath = parsed.GetResult NuGetPackagePath |> Path.GetFullPath
    if not(File.Exists nuGetPackagePath) then failwithf "Package does not exist %s" nuGetPackagePath
    
    let assemblyName = System.IO.Path.GetFileNameWithoutExtension nuGetPackagePath
    
    let tmp = parsed.TryGetResult TempFolder |> Option.defaultValue (Path.GetTempPath())
    let tmpFolder = Directory.CreateDirectory(Path.Combine (tmp, assemblyName))
    printfn "Temp output folder: %s" tmpFolder.FullName
    try
        Zip.unzip tmpFolder.FullName nuGetPackagePath
        runSteps parsed tmpFolder assemblyName
    finally
        tmpFolder.Delete(true)

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
        try
            runValidation p
            0
        with e ->
            Console.ForegroundColor <- ConsoleColor.Red
            eprintfn "%s" e.Message
            Console.ResetColor()
            1

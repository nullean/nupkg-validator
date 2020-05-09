module DllValidator

open System
open System
open Fake.Core
open System.Diagnostics
open System.IO
open System.Reflection


let private isReleaseMode (dll:FileInfo) =
    let a = Assembly.LoadFile dll.FullName
    let attribs = a.GetCustomAttributes(typeof<DebuggableAttribute>, false)
    match attribs.Length with
    | 0 -> true
    | _ ->
        let debuggableAttribute = attribs.[0] :?> DebuggableAttribute
        not debuggableAttribute.IsJITOptimizerDisabled

let private runValidation (dll:FileInfo) relativePath expectedVersion fixedVersion publicKey = 
    let namedAssembly = AssemblyName.GetAssemblyName(dll.FullName)
    let dllVersion = FileVersionInfo.GetVersionInfo(dll.FullName)
    match expectedVersion with
    | None -> ignore()
    | Some expectedVersion ->
        
        let a = namedAssembly.Version
        let nonFixedVersion = sprintf "%i.%i.%i.0" a.Major a.Minor a.Build
        let expectedFileVersion = 
            let v = SemVer.parse expectedVersion
            sprintf "%i.%i.%i.0" v.Major v.Minor v.Patch
            
        // validate that AssemblyVersion 
        if (fixedVersion && (a.Minor > 0 || a.Revision > 0 || a.Build > 0)) then
            failwith (sprintf "[version] %s AssemblyVersion is not fixed to %i.0.0.0" relativePath a.Major)
        elif not fixedVersion && (nonFixedVersion <> expectedVersion) then
            failwith (sprintf "[version] %s AssemblyVersion expected %s actual %s" relativePath expectedVersion nonFixedVersion)

        // validate that AssemblyFileVersion is the expected version without prerelease info
        if (dllVersion.FileVersion <> expectedFileVersion) then
            failwith (sprintf "[version] %s AssemblyFileVersion expected %s, actual: %s" relativePath expectedFileVersion dllVersion.FileVersion)
         
        // validate that AssemblyInformationVersion is the full expectedVersionString   
        if (dllVersion.ProductVersion <> expectedVersion) then 
            failwith <| sprintf "[version] %s AsseblyInformationalVersion: expected: %s actual: %s " relativePath expectedVersion dllVersion.ProductVersion
    
    match publicKey with
    | None -> ignore()
    | Some p ->
        let token = sprintf "PublicKeyToken=%s" p
        if not <| namedAssembly.FullName.Contains(token) then
            failwith <| sprintf "[version] %s is not publicly signed with expected token: %s" dll.Name p
            
    match isReleaseMode dll with
    | true -> ignore()
    | false -> failwith <| sprintf "[version] %s is not build in Release mode. IsJitOptimizerDisabled returned true on assembly" relativePath

let DllFilter skipDlls (dll:FileInfo) = skipDlls |> List.exists (fun skip -> skip = dll.Name || skip = Path.GetFileNameWithoutExtension(dll.Name))

let Scan (dlls:FileInfo list) (tmpFolder:DirectoryInfo) expectedVersion fixedVersion publicKey skipDlls =
    dlls
    |> List.iter (fun dll ->
        let relativePath = Path.GetRelativePath(tmpFolder.FullName, dll.FullName);
        
        let namedAssembly = AssemblyName.GetAssemblyName(dll.FullName)
        let assemblyVersion = namedAssembly.Version
        
        let dllVersion = FileVersionInfo.GetVersionInfo(dll.FullName)
        printfn "" 
        printfn "[dll] %s" relativePath
        printfn "[dll] %s" namedAssembly.FullName
        printfn "[version] Assembly: %A" assemblyVersion
        printfn "[version] AssemblyFile: %s"  dllVersion.FileVersion 
        printfn "[version] Informational: %s" dllVersion.ProductVersion
       
        match DllFilter skipDlls dll with
        | true ->
            Console.ForegroundColor <- ConsoleColor.Blue
            printfn "[dll] skipping validation because dll matches -d filter"
            Console.ResetColor()
        | false -> runValidation dll relativePath expectedVersion fixedVersion publicKey
    )
    

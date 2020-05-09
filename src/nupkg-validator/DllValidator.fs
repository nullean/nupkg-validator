module DllValidator

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

let Scan (dlls:FileInfo list) (tmpFolder:DirectoryInfo) version fixedVersion publicKey =
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
        
        let a = assemblyVersion
        // validate that AssemblyVersion only sets major version component
        if (fixedVersion && (a.Minor > 0 || a.Revision > 0 || a.Build > 0)) then
            failwith (sprintf "[version] %s AssemblyVersion is not fixed to %i.0.0.0" relativePath a.Major)
            
        // validate that AssemblyFileVersion is the expected version without prerelease info
        let expectedFileVersion = 
            let v = SemVer.parse dllVersion.FileVersion
            sprintf "%i.%i.%i.0" v.Major v.Minor v.Patch
        if (dllVersion.FileVersion <> expectedFileVersion) then
            failwith (sprintf "[version] %s AssemblyFileVersion expected %s, actual: %s" relativePath expectedFileVersion dllVersion.FileVersion)
         
        // validate that AssemblyInformationVersion is the full expectedVersionString   
        if (dllVersion.ProductVersion <> version) then 
            failwith <| sprintf "[version] %s AsseblyInformationalVersion: expected: %s actual: %s " relativePath version dllVersion.ProductVersion
        
        match publicKey with
        | None -> ignore()
        | Some p ->
            let token = sprintf "PublicKeyToken=%s" p
            if not <| namedAssembly.FullName.Contains(token) then
                failwith <| sprintf "[version] %s is not publicly signed with expected token: %s" dll.Name p
                
        match isReleaseMode dll with
        | true -> ignore()
        | false -> failwith <| sprintf "[version] %s is not build in Release mode. IsJitOptimizerDisabled returned true on assembly" relativePath
    )
    0
    

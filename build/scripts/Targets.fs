module Targets

open Argu
open System
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet

let exec binary args =
    let r = Proc.Exec (binary, args |> List.toArray)
    match r.HasValue with | true -> r.Value | false -> failwithf "invocation of `%s` timed out" binary 

let private bump (arguments:ParseResults<BumpArguments>) =
    printfn "bump version"
    
let private build (arguments:ParseResults<Arguments>) =
    let result = exec "dotnet" ["build"; "-c"; "Release"] 
    
    printfn "build"
    
let private pristineCheck (arguments:ParseResults<Arguments>) =
    let clean = Information.isCleanWorkingCopy "."
    match clean with
    | true  ->
        printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> 
        failwithf "The checkout folder has pending changes, aborting"
    
let private release (arguments:ParseResults<Arguments>) =
    let publish = exec "dotnet" ["pack"; "-c"; "Release"; "-o"; "build/output"]
    
    printfn "release"
    
let private publish (arguments:ParseResults<Arguments>) =
    printfn "publish" 

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let cmd (name:string) dependencies action = Targets.Target(name, dependencies, Action(action))

    cmd BumpArguments.Name [] <| fun _ ->
        match subCommand with | Bump b -> bump b | _ -> failwithf "bump needs bump args"
    cmd Build.Name [] <| fun _ -> build parsed
    
    cmd "pristine-check" [] <| fun _ -> pristineCheck parsed
    cmd Release.Name ["pristine-check"; Build.Name] <| fun _ -> release parsed
    cmd Publish.Name [Release.Name] <| fun _ -> publish parsed


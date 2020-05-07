module Targets

open Argu
open System
open Bullseye
open CommandLine
open ProcNet

let private bump (arguments:ParseResults<BumpArguments>) =
    
    printfn "bump version"
    
let private build (arguments:ParseResults<Arguments>) =
    let result = Proc.Exec ("dotnet", ["build"; "-c"; "Release"] |> List.toArray)
    
    printfn "build"
    
let private release (arguments:ParseResults<Arguments>) =
    
    printfn "release"
    
let private publish (arguments:ParseResults<Arguments>) =
    
    printfn "publish"

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let cmd (name:string) dependencies action = Targets.Target(name, dependencies, Action(action))

    cmd BumpArguments.Name [] <| fun _ ->
        match subCommand with | Bump b -> bump b | _ -> failwithf "bump needs bump args"
    cmd Build.Name [] <| fun _ -> build parsed
    cmd Release.Name [] <| fun _ -> release parsed
    cmd Publish.Name [] <| fun _ -> publish parsed


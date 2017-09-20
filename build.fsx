// include Fake lib
#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.PaketTemplate
open Fake.Paket
open Fake.AssemblyInfoFile
open Fake.AppVeyor
open System.Net
open System

// Properties
let mode = getBuildParamOrDefault "mode" "Release"
let buildDir = "./bin/" + mode.ToLower() + "/"
let protobuf = "https://github.com/google/protobuf/releases/download/v3.4.0/protoc-3.4.0-win32.zip"

// Targets
Target "Clean" (fun _ -> 
        CleanDir buildDir
        DeleteFile "Nakama/Api.pb.cs"
)

Target "Build" (fun _ ->
        "Building " + mode + " configuration" |> trace
        !! "Nakama/**/*.csproj"
            |>
            match mode.ToLower() with
                | "release" -> MSBuild buildDir "Build" [("Configuration","Release"); ("TargetFrameworkVersion","v4.5.1")]
                | _ -> MSBuild buildDir "Build" [("Configuration","Debug"); ("TargetFrameworkVersion","v4.5.1")]
            |> Log "AppBuild-Output: "
)

Target "CreatePaketTemplate" (fun _ ->
    ensureDirectory "./deploytemp"
    PaketTemplate (fun p ->
        { p with
            TemplateType = File
            TemplateFilePath = Some "./Nakama/Nakama.csproj.paket.template"
            Id = Some "Nakama.Standalone"
            Version = GetAttributeValue "AssemblyVersion" "./Nakama/Properties/AssemblyInfo.cs"
            Authors = ["Nakama Team"; "Patrick Yates"]
            Files = [ Include ("../bin/" + mode.ToLower() + "/NakamaUnity.dll", "/lib/") ]
            Dependencies = [ "Google.Protobuf", GreaterOrEqual LOCKEDVERSION
                             "WebSocketSharp", GreaterOrEqual LOCKEDVERSION ]
            Description = ["A Nakama C# client, without Unity"]
        }
    )
)

Target "Pack" (fun _ ->
    Pack (fun p ->
        { p with
            OutputPath = "./artifacts/"
        }
    )
)

Target "UpdateAppveyorVersion" (fun _ ->
    let newVersion = 
        match (GetAttributeValue "AssemblyVersion" "./Nakama/Properties/AssemblyInfo.cs") with
            | Some n -> n + AppVeyorEnvironment.BuildNumber
            | _ -> AppVeyorEnvironment.BuildNumber
    trace newVersion
    UpdateBuildVersion newVersion
)

Target "GetProtoc" (fun _ ->
    let wc = new WebClient()
    ensureDirectory "./temp"
    wc.DownloadFile(protobuf, "./temp/proto.zip")
    Fake.ZipHelper.Unzip "./temp/proto" "./temp/proto.zip"
)

Target "CompileProtobufSchema" (fun _ ->
    let result = Fake.ProcessHelper.execProcess (fun info ->
        info.FileName <- 
            match Fake.EnvironmentHelper.isWindows with
                | true -> "protoc.exe"
                 | false -> "protoc"
        info.Arguments <- "-I. -I./server/server --csharp_out=./Nakama/ --csharp_opt=file_extension=.pb.cs ./server/server/api.proto") (TimeSpan.FromMinutes 10.0)
    match result with
    | false -> failwith "Error compiling protobuf schema"
    | _ -> ()
)

Target "Default" (fun _ ->
    ()
)

// Dependencies
"Clean"
    ==> "Build"
    ==> "Default"
"CompileProtobufSchema"
    ==> "Build"
"Build"
    ==> "CreatePaketTemplate"
"CreatePaketTemplate"
    ==> "Pack"

// start build
RunTargetOrDefault "Default"

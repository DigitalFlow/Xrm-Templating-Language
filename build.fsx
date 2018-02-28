// include Fake libs
#I @"packages\FAKE\tools\"
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.Testing.NUnit3
open System
open System.IO
open System.Text.RegularExpressions
open Fake.Paket
open Fake.Git

//Project config
let projectName = "Xrm.Oss.XTL"
let projectDescription = "A domain specific language for templating inside Dynamics CRM"
let authors = ["Florian Kroenert"]

// Directories
let buildDir  = @".\build\"
let interpreterBuildDir = buildDir + @"interpreter\"
let templatingBuildDir = buildDir + @"templating"
let testDir   = @".\test\"

let deployDir = @".\Publish\"
let interpreterDeployDir = deployDir + @"interpreter\"
let templatingDeployDir = deployDir + @"templating"

let nugetDir = @".\nuget\"
let packagesDir = @".\packages\"

let sha = Git.Information.getCurrentHash()

// version info
let major           = "1"
let minor           = "0"
let patch           = "0"
let mutable build           = buildVersion
let mutable asmVersion      = ""
let mutable asmFileVersion  = ""

// Targets
Target "Clean" (fun _ ->

    CleanDirs [buildDir; testDir; deployDir; nugetDir]
)

Target "BuildVersions" (fun _ ->
    if isLocalBuild then
        build <- "0"

    // Follow SemVer scheme: http://semver.org/
    asmVersion  <- major + "." + minor + "." + patch 
    asmFileVersion      <- major + "." + minor + "." + patch + "+" + sha

    SetBuildNumber asmFileVersion
)

Target "AssemblyInfo" (fun _ ->
    BulkReplaceAssemblyInfoVersions "src" (fun f -> 
                                              {f with
                                                  AssemblyVersion = asmVersion
                                                  AssemblyInformationalVersion = asmVersion
                                                  AssemblyFileVersion = asmFileVersion
                                              })
)

Target "BuildInterpreter" (fun _ ->
    !! @"src\lib\Xrm.Oss.XTL.Interpreter\*.csproj"
        |> MSBuildRelease interpreterBuildDir "Build"
        |> Log "Build-Output: "
)

Target "BuildTemplating" (fun _ ->
    !! @"src\lib\Xrm.Oss.XTL.Templating\*.csproj"
        |> MSBuildRelease templatingBuildDir "Build"
        |> Log "Build-Output: "
)

Target "BuildTest" (fun _ ->
    !! @"src\test\**\*.csproj"
      |> MSBuildDebug testDir "Build"
      |> Log "Build Log: "
)

Target "NUnit" (fun _ ->
    let testFiles = !!(testDir @@ @"\**\*.Tests.dll")
    
    if testFiles.Includes.Length <> 0 then
      testFiles
        |> NUnit3 (fun test ->
             {test with
                   ShadowCopy = false;
                   ToolPath = "packages" @@ "nunit.consolerunner" @@ "tools" @@ "nunit3-console.exe";})
)

Target "Publish" (fun _ ->
    CreateDir interpreterDeployDir
    CreateDir templatingDeployDir
    
    !! (interpreterBuildDir @@ @"*.*")
        |> CopyTo interpreterDeployDir

    !! (templatingDeployDir @@ @"*.*")
        |> CopyTo templatingDeployDir
)

// Dependencies
"Clean"
  ==> "BuildVersions"
  =?> ("AssemblyInfo", not isLocalBuild )
  ==> "BuildInterpreter"
  ==> "BuildTemplating"
  ==> "BuildTest"
  ==> "NUnit"
  ==> "Publish"

// start build
RunTargetOrDefault "Publish"
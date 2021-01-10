Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse Cyclic -ErrorAction Ignore

if (Test-Path Cyclic.zip) {
    Expand-Archive Cyclic.zip -DestinationPath .
} else {
    mkdir Cyclic >$null
    Set-Location Cyclic
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    New-Projects -SolutionName C -Names C
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet pack C
    dotnet add A/A package B
    dotnet add B/B package C
    dotnet add C/C package A
    dotnet build A
    dotnet build B
    dotnet build C
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive Cyclic Cyclic.zip
}
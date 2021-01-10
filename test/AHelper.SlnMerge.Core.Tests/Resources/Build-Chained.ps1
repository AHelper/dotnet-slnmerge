Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse Chained -ErrorAction Ignore

if (Test-Path Chained.zip) {
    Expand-Archive Chained.zip -DestinationPath .
} else {
    mkdir Chained >$null
    Set-Location Chained
    New-Projects -SolutionName A -Names AA, AB
    New-Projects -SolutionName B -Names BA, BB
    New-Projects -SolutionName C -Names CA, CB
    New-Projects -SolutionName D -Names DA, DB
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet pack C
    dotnet pack D
    dotnet add A/AA package BA
    dotnet add A/AB package CB
    dotnet add B/BA reference B/BB
    dotnet add B/BB package CA
    dotnet add C/CA reference C/CB
    dotnet add C/CB package DA
    dotnet add D/DA reference D/DB
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive Chained Chained.zip
}
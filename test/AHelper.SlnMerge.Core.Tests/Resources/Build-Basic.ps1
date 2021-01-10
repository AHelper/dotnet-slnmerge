Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse Basic -ErrorAction Ignore

if (Test-Path Basic.zip) {
    Expand-Archive Basic.zip -DestinationPath .
} else {
    mkdir Basic >$null
    Set-Location Basic
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive Basic Basic.zip
}
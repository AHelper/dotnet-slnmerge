Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse DifferentSlnName -ErrorAction Ignore

if (Test-Path DifferentSlnName.zip) {
    Expand-Archive DifferentSlnName.zip -DestinationPath .
} else {
    mkdir DifferentSlnName >$null
    Set-Location DifferentSlnName
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    mv A/A.sln A/Test.sln
    mv B/B.sln B/Test.sln
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive DifferentSlnName DifferentSlnName.zip
}
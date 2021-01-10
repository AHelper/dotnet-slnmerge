Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$bNuspec = Get-Nuspec -Id 'B.Nuspec' -AssemblyName 'B'
$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse MissingCsproj -ErrorAction Ignore

if (Test-Path MissingCsproj.zip) {
    Expand-Archive MissingCsproj.zip -DestinationPath .
} else {
    mkdir MissingCsproj >$null
    Set-Location MissingCsproj
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B
    dotnet build A
    Remove-Item B/B/B.csproj
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MissingCsproj MissingCsproj.zip
}
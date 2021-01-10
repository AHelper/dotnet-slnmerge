Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$bNuspec = Get-Nuspec -Id 'B.Nuspec' -AssemblyName 'B'
$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse MissingNuspec -ErrorAction Ignore

if (Test-Path MissingNuspec.zip) {
    Expand-Archive MissingNuspec.zip -DestinationPath .
} else {
    mkdir MissingNuspec >$null
    Set-Location MissingNuspec
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Add-Property -CsprojPath B/B/B.csproj -Property "<NuspecFile>B.nuspec</NuspecFile>"
    Set-Content B/B/B.nuspec $bNuspec
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B.Nuspec
    dotnet build A
    Remove-Item B/B/B.nuspec
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MissingNuspec MissingNuspec.zip
}
Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$bNuspec = Get-Nuspec -Id 'B.Nuspec' -AssemblyName 'B'
$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse CsprojNotInSln -ErrorAction Ignore

if (Test-Path CsprojNotInSln.zip) {
    Expand-Archive CsprojNotInSln.zip -DestinationPath .
} else {
    mkdir CsprojNotInSln >$null
    Set-Location CsprojNotInSln
    New-Projects -SolutionName A -Names AA, AB
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet add A/AA reference A/AB
    dotnet sln A remove A/AB
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive CsprojNotInSln CsprojNotInSln.zip
}
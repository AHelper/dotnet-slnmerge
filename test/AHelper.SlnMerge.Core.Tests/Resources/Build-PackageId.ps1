Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse PackageId -ErrorAction Ignore

if (Test-Path PackageId.zip) {
    Expand-Archive PackageId.zip -DestinationPath .
} else {
    mkdir PackageId >$null
    Set-Location PackageId
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Add-Property -CsprojPath B/B/B.csproj -Property "<PackageId>B.PackageId</PackageId>"
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B.PackageId
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive PackageId PackageId.zip
}
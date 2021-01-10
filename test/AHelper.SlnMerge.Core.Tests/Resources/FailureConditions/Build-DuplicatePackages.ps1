Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse DuplicatePackages -ErrorAction Ignore

if (Test-Path DuplicatePackages.zip) {
    Expand-Archive DuplicatePackages.zip -DestinationPath .
} else {
    mkdir DuplicatePackages >$null
    Set-Location DuplicatePackages
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names BA, BB
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B
    dotnet build A
    Add-Property -CsprojPath B/BA/BA.csproj -Property "<PackageId>B.PackageId</PackageId>"
    Add-Property -CsprojPath B/BB/BB.csproj -Property "<PackageId>B.PackageId</PackageId>"
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive DuplicatePackages DuplicatePackages.zip
}
Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$bDirectoryBuildTargets = Get-DirectoryBuildTargets -PackageId 'B.DirectoryBuildTargets'
$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse DirectoryBuildTargets -ErrorAction Ignore

if (Test-Path DirectoryBuildTargets.zip) {
    Expand-Archive DirectoryBuildTargets.zip -DestinationPath .
} else {
    mkdir DirectoryBuildTargets >$null
    Set-Location DirectoryBuildTargets
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Set-Content B/B/Directory.Build.targets $bDirectoryBuildTargets
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B.DirectoryBuildTargets
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive DirectoryBuildTargets DirectoryBuildTargets.zip
}
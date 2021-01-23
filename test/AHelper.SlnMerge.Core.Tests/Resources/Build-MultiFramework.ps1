Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse MultiFramework -ErrorAction Ignore

if (Test-Path MultiFramework.zip) {
    Expand-Archive MultiFramework.zip -DestinationPath .
} else {
    mkdir MultiFramework >$null
    Set-Location MultiFramework
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B -Framework netstandard2.0
    New-Projects -SolutionName C -Names C -Framework net5.0
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    Add-Property -CsprojPath A/A/A.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath A/A/A.csproj -TagName "TargetFramework"
    dotnet pack A
    dotnet pack B
    dotnet pack C
    dotnet add A/A package B -f netstandard2.0
    dotnet add A/A package C -f net5.0
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MultiFramework MultiFramework.zip
}
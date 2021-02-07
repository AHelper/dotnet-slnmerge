Import-Module ../Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse MultiFrameworkUndo -ErrorAction Ignore

if (Test-Path MultiFrameworkUndo.zip) {
    Expand-Archive MultiFrameworkUndo.zip -DestinationPath .
} else {
    mkdir MultiFrameworkUndo >$null
    Set-Location MultiFrameworkUndo
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B -Framework netstandard2.0
    New-Projects -SolutionName C -Names C -Framework net5.0
    New-Projects -SolutionName D -Names D
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    Add-Property -CsprojPath A/A/A.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath A/A/A.csproj -TagName "TargetFramework"
    Add-Property -CsprojPath D/D/D.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath D/D/D.csproj -TagName "TargetFramework"
    dotnet pack A
    dotnet pack B
    dotnet pack C
    dotnet pack D
    dotnet add A/A package B -f netstandard2.0
    dotnet add A/A package C -f net5.0
    dotnet add A/A package D
    dotnet add B/B package D
    dotnet add C/C package D
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MultiFrameworkUndo MultiFrameworkUndo.zip
}
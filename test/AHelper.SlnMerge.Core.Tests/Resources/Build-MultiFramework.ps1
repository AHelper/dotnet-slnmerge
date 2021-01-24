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
    New-Projects -SolutionName B -Names BA, BB -Framework netstandard2.0
    New-Projects -SolutionName C -Names CA, CB -Framework net5.0
    New-Projects -SolutionName D -Names DA, DB
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    Add-Property -CsprojPath A/A/A.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath A/A/A.csproj -TagName "TargetFramework"
    Add-Property -CsprojPath D/DA/DA.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath D/DA/DA.csproj -TagName "TargetFramework"
    Add-Property -CsprojPath D/DB/DB.csproj -Property "<TargetFrameworks>net5.0;netstandard2.0</TargetFrameworks>"
    Remove-Property -CsprojPath D/DB/DB.csproj -TagName "TargetFramework"
    dotnet pack A
    dotnet pack B
    dotnet pack C
    dotnet pack D
    dotnet add A/A package BA -f netstandard2.0
    dotnet add A/A package BB -f netstandard2.0
    dotnet add A/A package CA -f net5.0
    dotnet add A/A package CB -f net5.0
    dotnet add A/A package DA
    dotnet add A/A package DB
    dotnet add B/BA package DA
    dotnet add B/BA package DB
    dotnet add C/CA package DA
    dotnet add C/CA package DB
    dotnet build A
    dotnet build B
    dotnet build C
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MultiFramework MultiFramework.zip
}
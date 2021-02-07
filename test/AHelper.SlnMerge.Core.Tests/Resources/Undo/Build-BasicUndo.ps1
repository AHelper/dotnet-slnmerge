Import-Module ../Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse BasicUndo -ErrorAction Ignore

if (Test-Path BasicUndo.zip) {
    Expand-Archive BasicUndo.zip -DestinationPath .
} else {
    mkdir BasicUndo >$null
    Set-Location BasicUndo
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Add-Property -CsprojPath B/B/B.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B.AssemblyName
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive BasicUndo BasicUndo.zip
}
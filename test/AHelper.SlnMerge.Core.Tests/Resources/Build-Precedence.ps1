Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$bNuspec = Get-Nuspec -Id 'B.Nuspec' -AssemblyName 'B.AssemblyName'
$bDirectoryBuildTargets = Get-DirectoryBuildTargets -PackageId 'B.Directory.Build.targets'
$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse Precedence -ErrorAction Ignore

if (Test-Path Precedence.zip) {
    Expand-Archive Precedence.zip -DestinationPath .
} else {
    mkdir Precedence >$null
    Set-Location Precedence
    New-Projects -SolutionName A -Names AA, AB, AC, AD
    New-Projects -SolutionName B -Names BA, BB, BC, BD
    Add-Property -CsprojPath B/BA/BA.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Add-Property -CsprojPath B/BA/BA.csproj -Property "<PackageId>B.PackageId</PackageId>"
    Add-Property -CsprojPath B/BB/BB.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Add-Property -CsprojPath B/BC/BC.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Add-Property -CsprojPath B/BC/BC.csproj -Property "<NuspecFile>BC.nuspec</NuspecFile>"
    Add-Property -CsprojPath B/BC/BC.csproj -Property "<PackageId>B.PackageId</PackageId>"
    Add-Property -CsprojPath B/BD/BD.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Add-Property -CsprojPath B/BD/BD.csproj -Property "<PackageId>B.PackageId</PackageId>"
    Set-Content B/BD/Directory.Build.targets $bDirectoryBuildTargets
    Set-Content B/BC/BC.nuspec $bNuspec
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/AA package B.AssemblyName
    dotnet add A/AB package B.PackageId
    dotnet add A/AC package B.Nuspec
    dotnet add A/AD package B.Directory.Build.targets
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive Precedence Precedence.zip
}
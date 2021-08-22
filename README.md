# dotnet-slnmerge
[![Nuget](https://img.shields.io/nuget/v/dotnet-slnmerge)](https://www.nuget.org/packages/dotnet-slnmerge/) [![Build](https://img.shields.io/gitlab/pipeline/dotnet-slnmerge/dotnet-slnmerge/master)](https://gitlab.com/dotnet-slnmerge/dotnet-slnmerge/-/pipelines?scope=branches&page=1)

A .NET CLI tool to automatically merge projects in multiple solutions, adding `<ProjectReference>`s when a corresponding `<PackageReference>` is found.

## Usage
Install the tool globally with:
```
dotnet tool install --global dotnet-slnmerge
```
Merge projects from one or more solutions into another:
```
slnmerge src/ ../other/solution.sln
```
Undo the merge operation with:
```
slnmerge -u src/ ../other/solution.sln
```

## Why?
I find myself having to debug changes in different projects across multiple solutions at once that normally use `<PackageReference>`s.  Adding these as `<ProjectReference>`s and adding to the solution simplifies debugging.  For large projects with multiple repositories, this can be a very tedious task to set up by hand.

## Example
Take the 3 solutions in separate repositories checked out locally:
```
/repos/
 |- Server/
 |   \- Server.sln
 |       \- src/Server/Server.csproj
 |            Package References
 |             \- Data
 |- Data/
 |   \- Data.sln
 |       \- src/Data/Data.csproj
 |            Package References
 |             \- Core
 \- Core/
     \- Core.sln
         \- src/Core/Core.csproj
```

Run:
```
PS /repos> slnmerge Server Data Core
```
_Tip: You can specify a solution file or a folder containing one._

The structure afterwards:
```
/repos/
 |- Server/
 |   \- Server.sln
 |       |- src/Server/Server.csproj
 |       |    Package References
 |       |     \- Data
 |       |    Project References
 |       |     \- ../../../Data/src/Data/Data.csproj
 |       |- ../Data/src/Data/Data.csproj
 |       |    Package References
 |       |     \- Core
 |       |    Project References
 |       |     \- ../../../Core/src/Core/Core.csproj
 |       \- ../Core/src/Core/Core.csproj
 |
 |- Data/
 |   \- Data.sln
 |       |- src/Data/Data.csproj
 |       |    Package References
 |       |     \- Core
 |       |    Project References
 |       |     \- ../../../Core/src/Core/Core.csproj
 |       \- src/Core/Core.csproj
 \- Core/
     \- Core.sln
         \- src/Core/Core.csproj
```

The result of slnmerge are projects that have a ProjectReference added for every local project that it finds that corresponds to a PackageReference.  Solutions are also updated to include those newly reference projects.

If you open Server.sln in an IDE, you can modify sources and set breakpoints in Core and debug Server with those changes.

If you wish to remove the references added by this tool without reverting all changes in .csproj and .sln files:
```
PS /repos> slnmerge --undo Server Data
```
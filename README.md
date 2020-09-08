# slnmerge
A .NET CLI tool to automatically merge projects in multiple solutions, adding `<ProjectReference>`s when a corresponding `<PackageReference>` is found.

## Usage
Install the tool globally with:
```
dotnet tool install --global AHelper.SlnMerge
```
Merge projects from one or more solutions into another:
```
slnmerge dest.sln source.sln
```

## Why?
I find myself having to debug changes in different projects across multiple solutions at once that normally use `<PackageReference>`s.  Adding these as `<ProjectReference>`s and adding to the solution simplifies debugging.

Example:

3 projects in separate git repositories: Server, Data, Core.

Server is an executable AspNetCore project, Data and Core are classlibs.

Data and Core publish nugets to a private feed in release mode, no debug symbols available.

Server -> Data -> Core.

In order to debug a change in Core from Server, you must get the new DLLs and PDBs in Server.  The easiest way I have found is to simply directly reference the projects and allow MSBuild to handle copying DLLs and PDBs.
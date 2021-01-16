Feature: Handle errors gracefully

    Scenario: Cyclical reference throws CyclicReferenceException
        Given test project "Cyclic" created with "FailureConditions/Build-Cyclic.ps1"
        When merging solutions with exceptions: A, B, C
        Then it should throw a CyclicReferenceException with projects A -> B -> C -> A
    Scenario: Missing nuspec throws FileReadException(Nuspec) error
        Given test project "MissingNuspec" created with "FailureConditions/Build-MissingNuspec.ps1"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B/B/B.nuspec'
    Scenario: Missing csproj throws FileReadException(Csproj) error
        Given test project "MissingCsproj" created with "FailureConditions/Build-MissingCsproj.ps1"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B/B/B.csproj'
    Scenario: Missing csproj from solution throws FileReadException(ProjectReference) warning
        Given test project "CsprojNotInSln" created with "FailureConditions/Build-CsprojNotInSln.ps1"
        When merging solutions with exceptions: A
        Then it should print warning FileReadException for 'A/AB/AB.csproj'
    Scenario: Missing sln throws FileReadException(Sln) error
        Given test project "MissingSln" created with "FailureConditions/Build-MissingSln.ps1"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B'
    Scenario: Package ID conflict throws AmbiguousProjectException
        Given test project "DuplicatePackages" created with "FailureConditions/Build-DuplicatePackages.ps1"
        When merging solutions with exceptions: A, B
        Then it should throw an AmbiguousProjectException with package id 'B.PackageId'
    Scenario: Multiple solutions in a folder throws AmbiguousSolutionException
        Given test project "AmbiguousSolution" created with "FailureConditions/Build-AmbiguousSolution.ps1"
        When merging solutions with exceptions: A
        Then it should throw an AmbiguousSolutionException with solutions: A/A.sln, A/B.sln
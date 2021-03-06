Feature: Failure conditions

    Scenario: Cyclical reference throws CyclicReferenceException
        Given test project created with "FailureConditions/Cyclic.xml"
        When merging solutions with exceptions: A, B, C
        Then it should throw a CyclicReferenceException with projects A -> B -> C -> A
    Scenario: Missing nuspec throws FileReadException(Nuspec) error
        Given test project created with "FailureConditions/MissingNuspec.xml"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B/B/B.nuspec'
    Scenario: Missing csproj throws FileReadException(Csproj) error
        Given test project created with "FailureConditions/MissingCsproj.xml"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B/B/B.csproj'
    Scenario: Missing csproj from solution throws FileReadException(ProjectReference) warning
        Given test project created with "FailureConditions/CsprojNotInSln.xml"
        When merging solutions with exceptions: A
        Then it should print warning FileReadException for 'A/AB/AB.csproj'
    Scenario: Missing sln throws FileReadException(Sln) error
        Given test project created with "FailureConditions/MissingSln.xml"
        When merging solutions with exceptions: A, B
        Then it should print error FileReadException for 'B'
    Scenario: Package ID conflict throws AmbiguousProjectException
        Given test project created with "FailureConditions/DuplicatePackages.xml"
        When merging solutions with exceptions: A, B
        Then it should throw an AmbiguousProjectException with package id 'B.PackageId'
    Scenario: Multiple solutions in a folder throws AmbiguousSolutionException
        Given test project created with "FailureConditions/AmbiguousSolution.xml"
        When merging solutions with exceptions: A
        Then it should throw an AmbiguousSolutionException with solutions: A/A.sln, A/B.sln
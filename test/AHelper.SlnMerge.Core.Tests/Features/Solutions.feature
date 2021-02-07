Feature: Solution handling

    Scenario: DifferentSlnName
        Given test project created with "Solutions/DifferentSlnName.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: LocalSln
        Given test project created with "Solutions/LocalSln.xml"
        When merging the local solution with: A
        Then project B/B.csproj should reference ../A/A/A.csproj

    Scenario: Solution folder
        Given test project created with "Solutions/SlnFolder.xml"
        When merging solutions: A, B
        Then project A/src/A/A.csproj should reference ../../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj
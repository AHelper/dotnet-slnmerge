Feature: Undo
	If slnmerge was used to merge 2 or more solutions, the tool should be able to remove
	its changes to .csproj and .sln files.

	Scenario: Undo basic merge
		Given test project created with "Undo/BasicUndo.xml"
		When merging solutions: A, B
		And undoing merges in solutions: A, B
		Then project A/A/A.csproj should not reference ../../B/B/B.csproj
		And solution A/A.sln should include A/A.csproj
		And solution A/A.sln should not include ../B/B/B.csproj
		And solution B/B.sln should include B/B.csproj

	Scenario: Undo multi-framework merge
		Given test project created with "Undo/MultiFrameworkUndo.xml"
		When merging solutions: A, B, C, D
		And undoing merges in solutions: A, B, C, D
		Then project A/A/A.csproj should not reference ../../B/B/B.csproj
		And project A/A/A.csproj should not reference ../../C/C/C.csproj
		And project A/A/A.csproj should not reference ../../D/D/D.csproj
		And project B/B/B.csproj should not reference ../../D/D/D.csproj
		And project C/C/C.csproj should not reference ../../D/D/D.csproj
		And solution A/A.sln should include A/A.csproj
		And solution A/A.sln should not include ../B/B/B.csproj, ../C/C/C.csproj, ../D/D/D.csproj
		And solution B/B.sln should include B/B.csproj
		And solution B/B.sln should not include ../D/D/D.csproj
		And solution C/C.sln should include C/C.csproj
		And solution C/C.sln should not include ../D/D/D.csproj

    Scenario: Undo with Windows paths
        Given test project created with "Undo/WindowsPaths.xml"
        When undoing merges in solutions: A, B
		Then project A/A.csproj should not reference ../B/B.csproj
		And solution A/A.sln should not include ../B/B.csproj

    Scenario: Undo multiple merged projects
        Given test project created with "Undo/UndoMultiple.xml"
        When undoing merges in solutions: A, B
        Then project A/src/AA/AA.csproj should not reference ../../../B/src/BA/BA.csproj
        And project A/src/AB/AB.csproj should not reference ../../../B/src/BB/BB.csproj
        And solution A/A.sln should not include ../B/src/BA/BA.csproj, ../B/src/BB/BB.csproj

    Scenario: Undo chained solutions
        Given test project created with "Undo/UndoChain.xml"
        When merging solutions: A, B, C
        And undoing merges in solutions: A, B, C
		Then solution A/A.sln should include A/A.csproj
        And solution A/A.sln should not include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../C/C/C.csproj
		Then solution B/B.sln should include BA/BA.csproj, BB/BB.csproj
        And solution B/B.sln should not include ../C/C/C.csproj

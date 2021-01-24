Feature: Undoing merges to solutions
	If slnmerge was used to merge 2 or more solutions, the tool should be able to remove
	its changes to .csproj and .sln files.

	@ignore
	Scenario: Undo basic merge
		Given test project "BasicUndo" created with "Undo/Build-BasicUndo.ps1"
		When merging solutions: A, B
		And undoing merges in solutions: A, B
		Then project A/A/A.csproj should not reference ../../B/B/B.csproj
		And solution A/A.sln should include A/A.csproj
		And solution A/A.sln should not include ../B/B/B.csproj
		And solution B/B.sln should include B/B.csproj
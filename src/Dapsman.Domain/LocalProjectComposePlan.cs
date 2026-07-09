namespace Dapsman.Domain;

public sealed class LocalProjectComposePlan
{
	public required string ProjectName { get; init; }
	public required string ProjectPath { get; init; }
	public required IReadOnlyList<string> ComposeFiles { get; init; }
	public required IReadOnlyList<string> PrerequisiteScripts { get; init; } = [];
}

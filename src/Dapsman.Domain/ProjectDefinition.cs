namespace Dapsman.Domain;

public sealed class ProjectDefinition
{
	/// <summary>
	/// The project name. It should match the project folder's name
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// The project path in the workstation context
	/// </summary>
	public required string Path { get; init; }
}

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

	/// <summary>
	/// When true, the project is excluded from multi-project workflows.
	/// Explicitly targeting a disabled project with --project is an error.
	/// </summary>
	public bool Disabled { get; init; }
}

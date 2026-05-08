namespace Dapsman.Domain;

public class DapsProject : Resolved
{
	/// <summary>
	/// The project definition from the daps.yaml config file
	/// </summary>
	public required ProjectDefinition Definition { get; init; }

	public required string WorkstationScriptsPath { get; init; }
	public required string WorkstationBackupsPath { get; init; }

	/// <summary>
	/// The path within the toolkit to get to the project folder, as defined in its daps 
	/// docker compose extension (e.g. compose_daps_projectname.dev.yaml) bind mount(s).
	/// </summary>
	public required string? ToolkitPath { get; init; }

	public string? ToolkitScriptsPath => $"{ToolkitPath}/_scripts";
	public string? ToolkitBackupsPath => $"{ToolkitPath}/_backups";

	public IReadOnlyList<string> LocalPrerequisiteScripts { get; init; } = [];
	public IReadOnlyList<string> ScriptsToUploadToRemote { get; init; } = [];
}

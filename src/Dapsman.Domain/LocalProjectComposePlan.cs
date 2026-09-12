namespace Dapsman.Domain;

public sealed class LocalProjectComposePlan
{
	public required string ProjectName { get; init; }
	public required string ProjectPath { get; init; }
	public required IReadOnlyList<string> ComposeFiles { get; init; }
	public required IReadOnlyList<string> PrerequisiteScripts { get; init; } = [];

	/// <summary>
	/// Environment passed to every prerequisite script. Template scripts read the project name from
	/// DAPS_PROJECT rather than having it baked in by the init transform, which keeps a project's
	/// _scripts identical to the template's.
	/// </summary>
	public IReadOnlyDictionary<string, string> PrerequisiteScriptEnvVars { get; init; }
		= new Dictionary<string, string>();

	public required string ComposeUpCommand { get; init; }

	/// <summary>
	/// Optional, only used when project containers are being rebuilt
	/// </summary>
	public string? ComposeDownCommand { get; init; }
}

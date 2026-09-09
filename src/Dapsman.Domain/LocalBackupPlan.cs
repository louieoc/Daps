namespace Dapsman.Domain;

public sealed class LocalBackupPlan
{
	public required string ProjectName { get; init; }
	public required string ToolkitContainerName { get; init; }

	/// <summary>Path to backup-local.toolkit.sh inside the toolkit container.</summary>
	public required string BackupScriptToolkitPath { get; init; }

	/// <summary>Destination directory inside the toolkit container where backup files are written.</summary>
	public required string BackupDestinationToolkitPath { get; init; }

	public string ScriptArguments => $"--env local --to \"{BackupDestinationToolkitPath}\"";

	public IReadOnlyDictionary<string, string> EnvVars => new Dictionary<string, string>
	{
		["DAPS_PROJECT"] = ProjectName,
	};
}

namespace Dapsman.Domain;

public sealed class RemoteBackupPlan
{
	public required string ProjectName { get; init; }
	public required string DapsRootPath { get; init; }
	public required string ToolkitContainerName { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }

	/// <summary>Absolute path to the SSH key inside the toolkit container.
	/// Tilde doesn't expand inside bash variables — must be absolute.</summary>
	public required string SshKeyToolkitPath { get; init; }

	/// <summary>Path to the backup script inside the toolkit container.</summary>
	public required string BackupScriptToolkitPath { get; init; }

	/// <summary>Destination directory inside the toolkit container where backup files are written.</summary>
	public required string BackupDestinationToolkitPath { get; init; }

	public string ScriptArguments => $"--env prod --to \"{BackupDestinationToolkitPath}\"";

	public IReadOnlyDictionary<string, string> EnvVars => new Dictionary<string, string>
	{
		["DAPS_PROJECT"] = ProjectName,
		["DAPS_REMOTE_HOST"] = RemoteHost,
		["DAPS_REMOTE_USER"] = RemoteUser,
		["DAPS_SSH_KEY"] = SshKeyToolkitPath,
	};
}

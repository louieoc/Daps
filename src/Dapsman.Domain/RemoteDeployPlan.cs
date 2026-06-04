namespace Dapsman.Domain;

public sealed class RemoteDeployPlan
{
	public required string DapsRootPath { get; init; }
	public required string ProviderName { get; init; }
	public required string ToolkitContainerName { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }
	public required string SshKeyName { get; init; }
	public required string DockerCommandPrefix { get; init; }
	public required IReadOnlyList<BuildImageCommandPlan> BuildImageCommands { get; init; }
	public required string CaddyfileSourcePath { get; init; }

	/// <summary>
	/// The caddy files that will actually be uploaded, e.g. filtered
	/// </summary>
	public required IReadOnlyList<CaddyUploadPlan> CaddySiteFilesToUpload { get; init; }

	/// <summary>
	/// Caddy files for all configured projects. Anything outside this list and CaddySiteFilesToUpload can be deleted
	/// </summary>
	public required IReadOnlyList<string> ExpectedProdCaddyFileNames { get; init; }

	public required IReadOnlyList<string> DapsComposeFilesToUpload { get; init; }
	public required IReadOnlyList<RemoteProjectDeployPlan> ProjectPlans { get; init; }
	public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class BuildImageCommandPlan
{
	public required string ProjectName { get; init; }
	public required string ToolkitScriptPath { get; init; }
	/// <summary>
	/// True when tar files already exist in the project's image-exports folder.
	/// False means a build is required before deploy; true means --build can be
	/// used to force a rebuild but the existing tar will be used otherwise.
	/// </summary>
	public required bool HasExistingExports { get; init; }
}

public sealed class CaddyUploadPlan
{
	public required string ProjectName { get; init; }
	public required string SourcePath { get; init; }
	public required string DestinationFileName { get; init; }
}

public sealed class RemoteProjectDeployPlan
{
	public required string ProjectName { get; init; }
	public required IReadOnlyList<string> ComposeFilesToUpload { get; init; }
	public required IReadOnlyList<string> ComposeFileNamesForRemoteRun { get; init; }
	public required string ImageExportsSourcePath { get; init; } = string.Empty;
	public required IReadOnlyList<string> ImageExportFilesToUpload { get; init; }
	public required IReadOnlyList<ProjectUploadFilePlan> UploadFiles { get; init; }

	/// <summary>
	/// Scripts from _scripts/ that should be uploaded to the remote server.
	/// Includes *.prod.sh and bare *.sh files; excludes *.dev.sh and *.toolkit.sh.
	/// </summary>
	public IReadOnlyList<string> RemoteScriptFilesToUpload { get; init; } = [];

	/// <summary>
	/// Ordered prerequisite scripts to run on the remote server after uploading.
	/// prerequisites.sh (all environments) runs first, then prerequisites.prod.sh — mirroring compose file layering.
	/// </summary>
	public IEnumerable<string> RemotePrerequisiteScriptNames =>
		new[] { "prerequisites.sh", "prerequisites.prod.sh" }
			.Where(name => RemoteScriptFilesToUpload.Any(f =>
				string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase)));

	/// <summary>
	/// Toolkit-relative path to post-remote-deploy.toolkit.sh, if present in the project's _scripts/ folder.
	/// When set, this script is run inside the toolkit container after containers are up and Caddy is reloaded.
	/// </summary>
	public string? PostDeployScriptPath { get; init; }
}

public sealed class ProjectUploadFilePlan
{
	public required string SourcePath { get; init; }
	public required string RemoteDestinationPath { get; init; }
}

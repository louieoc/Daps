namespace Dapsman.Domain;

public sealed class TeardownPlan
{
	public required string ProjectName { get; init; }
	public required string DapsRootPath { get; init; }
	public required string ToolkitContainerName { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }
	public required string SshKeyName { get; init; }

	/// <summary>e.g. "docker" for root, "sudo docker" for non-root remote users.</summary>
	public required string DockerCommandPrefix { get; init; }

	/// <summary>Filename of the Caddy site file on the remote, e.g. mywpsite.prod.caddy. Null if the project has no prod caddy file.</summary>
	public required string? CaddySiteFileName { get; init; }

	/// <summary>Content to write for the "site removed" Caddy block. Null if no caddy file.</summary>
	public required string? RemovedCaddyContent { get; init; }

	/// <summary>Remote path to the project folder, e.g. /srv/projects/mywpsite.</summary>
	public required string RemoteProjectPath { get; init; }
}

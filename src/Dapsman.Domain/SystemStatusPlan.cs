namespace Dapsman.Domain;

public sealed class SystemStatusPlan
{
	public required string ProviderName { get; init; }
	public required string DapsRootPath { get; init; }
	public required string ToolkitContainerName { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }

	/// <summary>SSH private key file name, resolved against ~/.ssh inside the toolkit.</summary>
	public required string SshKeyName { get; init; }

	/// <summary>e.g. "docker" for root, "sudo docker" for non-root remote users.</summary>
	public required string DockerCommandPrefix { get; init; }

	/// <summary>Where projects live on the remote, scanned for per-project disk usage.</summary>
	public required string RemoteProjectsRoot { get; init; }

	/// <summary>Workstation path to scripts/remote-system-status.sh, staged into the toolkit.</summary>
	public required string StatusScriptHostPath { get; init; }

	/// <summary>True when per-container, per-project, and uptime detail was requested.</summary>
	public required bool Verbose { get; init; }
}

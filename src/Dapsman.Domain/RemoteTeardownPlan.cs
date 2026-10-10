namespace Dapsman.Domain;

public sealed class RemoteTeardownPlan
{
	public required string ProjectName { get; init; }
	public required string DapsRootPath { get; init; }
	public required string ProviderName { get; init; }
	public required string ToolkitContainerName { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }
	public required string SshKeyName { get; init; }

	/// <summary>False when the remote user needs sudo for docker and for removing project files.</summary>
	public required bool IsRoot { get; init; }

	/// <summary>
	/// Filename of the Caddy site file on the remote, e.g. mywpsite.prod.caddy. Derived by
	/// convention from the project name, so it is known even for a project that daps.yaml
	/// no longer lists.
	/// </summary>
	public required string CaddySiteFileName { get; init; }

	/// <summary>
	/// Content to write for the "site removed" Caddy block, and the discriminator for the
	/// caddy step. When set, the remote site file is overwritten with this 410 page. When
	/// null the domain could not be read — the project is not in daps.yaml, or it has no
	/// local prod caddy file — and the remote site file is deleted instead, so the retired
	/// host stops claiming the domain and stops renewing a certificate for it.
	/// </summary>
	public required string? RemovedCaddyContent { get; init; }

	/// <summary>Remote path to the project folder, e.g. /srv/projects/mywpsite.</summary>
	public required string RemoteProjectPath { get; init; }
}

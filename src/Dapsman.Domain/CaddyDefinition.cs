namespace Dapsman.Domain;

public class CaddyDefinition : Resolved
{
	/// <summary>
	/// Full path to the caddyfile on the caddy container
	/// </summary>
	public string ContainerConfigPath => "/etc/caddy/Caddyfile";


	/// <summary>
	/// The name of the Daps caddy container. By default it should be daps-caddy-1
	/// </summary>
	public required string ContainerName { get; init; }

	/// <summary>
	/// False when no caddy container is currently running; ContainerName is then the
	/// conventional name the container will have once it is built.
	/// </summary>
	public bool IsRunning { get; init; }

	/// <summary>
	/// The full file path for the Caddyfile, in workstation context
	/// </summary>
	public required string WorkstationCaddyFilePath { get; init; }

	/// <summary>
	/// The central folder that project site files will be copied to for local deployment, in workstation context
	/// </summary>
	public required string WorkstationSitesPath { get; init; }

	/// <summary>
	/// All defined site files for prod. Any file in prod that's not in this list might be deleted during deploy
	/// </summary>
	public required IReadOnlyList<string> AllWorkstationProdSiteFileNames { get; init; }
}

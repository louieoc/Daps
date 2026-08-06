namespace Dapsman.Domain;

public sealed class DapsConfig
{
	/// <summary>
	/// The prefix for docker container names. This is hard-coded in daps docker compose files but a future
	/// enhancement could allow customizing it via the daps.yaml file and running compose with -p
	/// </summary>
	public string DapsComposeProjectName => "daps";

	/// <summary>
	/// The shared Docker network connecting Caddy to every project container. Compose files declare
	/// it as external, so local build creates it if it does not exist yet.
	/// </summary>
	public string SharedNetworkName => "daps_net";

	public required string DapsRootPath { get; init; }
	public required string FullYamlPath { get; init; }
	public IReadOnlyList<ProjectDefinition> Projects { get; init; } = [];
	public IReadOnlyList<ProviderDefinition> Providers { get; init; } = [];
}

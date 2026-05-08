namespace Dapsman.Domain;

public sealed class DapsConfig
{
	/// <summary>
	/// The prefix for docker container names. This is hard-coded in daps docker compose files but a future
	/// enhancement could allow customizing it via the daps.yaml file and running compose with -p
	/// </summary>
	public string DapsComposeProjectName => "daps";
	public required string DapsRootPath { get; init; }
	public required string FullYamlPath { get; init; }
	public IReadOnlyList<ProjectDefinition> Projects { get; init; } = [];
	public IReadOnlyList<ProviderDefinition> Providers { get; init; } = [];
}

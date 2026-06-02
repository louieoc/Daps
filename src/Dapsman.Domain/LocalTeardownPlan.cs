namespace Dapsman.Domain;

public sealed class LocalTeardownPlan
{
	public required string ProjectName { get; init; }
	public required string ProjectPath { get; init; }

	/// <summary>Absolute workstation paths to compose files — base + dev overlay.</summary>
	public required IReadOnlyList<string> ComposeFiles { get; init; }

	/// <summary>Absolute workstation paths to *.dev.caddy files in dapsRoot/caddy_sites/ to delete.</summary>
	public required IReadOnlyList<string> CaddySiteFilesToDelete { get; init; }

	public required string CaddyContainerName { get; init; }
	public required string CaddyConfigPath { get; init; }
	public required string DapsYamlPath { get; init; }
}

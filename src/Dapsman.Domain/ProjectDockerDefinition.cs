namespace Dapsman.Domain;

public sealed class ProjectDockerDefinition : Resolved
{
	public required string ProjectDockerPath { get; init; }

	public string? ImageExportsFolder { get; set; }

	public IReadOnlyList<string> ImageExports { get; set; } = [];

	/// <summary>
	/// Workstation paths to local (e.g. dev) docker compose files that extend daps (e.g. to bind mount the project on the toolkit and remote VM)
	/// </summary>
	public IReadOnlyList<string> LocalDapsExtensionComposeFiles { get; set; } = [];

	/// <summary>
	/// Workstation paths to local (e.g. dev) docker compose files for the project
	/// </summary>
	public IReadOnlyList<string> LocalProjectComposeFiles { get; set; } = [];

	/// <summary>
	/// Workstation paths to remote/prod docker compose files that extend daps (e.g. to bind mount the project on the toolkit and remote VM)
	/// </summary>
	public IReadOnlyList<string> ProdDapsExtensionComposeFiles { get; set; } = [];

	/// <summary>
	/// Workstation paths to remote/prod docker compose files for the project
	/// </summary>
	public IReadOnlyList<string> ProdProjectComposeFiles { get; set; } = [];
}

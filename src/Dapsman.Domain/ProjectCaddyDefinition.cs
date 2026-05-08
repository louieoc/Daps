namespace Dapsman.Domain;

public class ProjectCaddyDefinition : Resolved
{
	/// <summary>
	/// Workstation path to the project's _caddy_sites folder
	/// </summary>
	public required string ProjectSitesPath { get; init; }

	/// <summary>
	/// Workstation paths to local (e.g. dev) caddy site files for the project
	/// </summary>
	public required IReadOnlyList<string> WorkstationLocalSiteFilePaths { get; init; }

	/// <summary>
	/// Workstation paths to remote/prod caddy site files for the project
	/// </summary>
	public required IReadOnlyList<string> WorkstationProdSiteFilePaths { get; init; }

	/// <summary>
	/// The project's production caddy site filename, in workstation context
	/// </summary>
	public string? WorkstationProdSiteFile { get; set; }

	public required string OfflineCaddyFilename { get; init; }
}
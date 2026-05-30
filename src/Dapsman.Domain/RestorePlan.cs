namespace Dapsman.Domain;

public sealed class RestorePlan
{
	public required string ProjectName { get; init; }
	public required string ToolkitContainerName { get; init; } // todo: do we need this? Only used for printing rn
	public required RestorePoint RestorePoint { get; init; }

	/// <summary>Path to restore-local.toolkit.sh inside the toolkit container.</summary>
	public required string RestoreScriptToolkitPath { get; init; }

	/// <summary>Path to _backups/from_prod/ inside the toolkit container.</summary>
	public required string BackupsToolkitPath { get; init; }

	/// <summary>Explicit prod URL override; null or empty means the script derives it from the caddy file.</summary>
	public required string? ProdUrl { get; init; }

	public IReadOnlyDictionary<string, string> EnvVars => new Dictionary<string, string>
	{
		["DAPS_PROJECT"] = ProjectName,
		["DAPS_RESTORE_ENV"] = RestorePoint.Env,
		["DAPS_RESTORE_TIMESTAMP"] = RestorePoint.Timestamp,
		["DAPS_BACKUPS_PATH"] = BackupsToolkitPath,
		["DAPS_PROD_URL"] = ProdUrl ?? "",
	};
}

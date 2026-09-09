namespace Dapsman.Domain;

/// <summary>
/// Subdirectories of a project's _backups/ folder, one per environment a backup can come from.
/// The directory name is also the source identifier carried on a <see cref="RestorePoint"/>.
/// </summary>
public static class BackupSources
{
	public const string FromProd = "from_prod";
	public const string FromLocal = "from_local";

	/// <summary>Search order for restore point discovery.</summary>
	public static readonly IReadOnlyList<string> All = [FromProd, FromLocal];
}

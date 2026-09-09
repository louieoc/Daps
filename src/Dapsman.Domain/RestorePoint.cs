namespace Dapsman.Domain;

// A record so discovery can renumber Index when merging restore points from several source
// directories into one list.
public sealed record RestorePoint
{
	/// <summary>1-based index in sorted list (most recent = 1).</summary>
	public required int Index { get; init; }

	/// <summary>Environment the backup came from, e.g. "prod".</summary>
	public required string Env { get; init; }

	/// <summary>The _backups/ subdirectory this restore point was discovered in, e.g. "from_prod".</summary>
	public required string SourceDirectory { get; init; }

	/// <summary>Raw timestamp from the backup filename, e.g. "20260420_202738".</summary>
	public required string Timestamp { get; init; }

	public required DateTimeOffset ParsedTimestamp { get; init; }

	/// <summary>True when all expected backup files for this restore point are present.</summary>
	public required bool IsComplete { get; init; }

	public string DisplayLabel => $"{Env}: {ParsedTimestamp:yyyy-MM-dd HH:mm:ss}";

	/// <summary>True when the backup files include production URLs that need to be replaced for the local instance.</summary>
	public bool ReplacesUrls => !string.Equals(Env, "local", StringComparison.OrdinalIgnoreCase);
}

namespace Dapsman.Domain;

public sealed class RestorePoint
{
	/// <summary>1-based index in sorted list (most recent = 1).</summary>
	public required int Index { get; init; }

	/// <summary>Environment the backup came from, e.g. "prod".</summary>
	public required string Env { get; init; }

	/// <summary>Raw timestamp from the backup filename, e.g. "20260420_202738".</summary>
	public required string Timestamp { get; init; }

	public required DateTimeOffset ParsedTimestamp { get; init; }

	/// <summary>True when all expected backup files for this restore point are present.</summary>
	public required bool IsComplete { get; init; }

	public string DisplayLabel => $"{Env}: {ParsedTimestamp:yyyy-MM-dd HH:mm:ss}";
}

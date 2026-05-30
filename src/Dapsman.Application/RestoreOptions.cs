namespace Dapsman.Application;

public sealed class RestoreOptions
{
	/// <summary>1-based index ("1") or raw timestamp ("20260420_202738"). Null defaults to most recent complete restore point.</summary>
	public string? SelectedRestorePoint { get; init; }

	public string? ProdUrl { get; init; }
}

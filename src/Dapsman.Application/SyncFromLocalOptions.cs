namespace Dapsman.Application;

public sealed class SyncFromLocalOptions
{
	public bool DryRun { get; init; }
	public string? ProjectName { get; init; }
	public string? ProviderName { get; init; }
}

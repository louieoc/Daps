namespace Dapsman.Application;

public sealed class UnprovisionOptions
{
	public bool DryRun { get; init; }
	public string? ProviderName { get; init; }
}

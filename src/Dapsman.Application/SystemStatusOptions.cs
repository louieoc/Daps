namespace Dapsman.Application;

public sealed class SystemStatusOptions
{
	public bool DryRun { get; init; }
	public string? ProviderName { get; init; }
	public bool Verbose { get; init; }
}

namespace Dapsman.Application;

public sealed class RemoteDeployOptions
{
	public bool DryRun { get; init; }
	public bool SkipBuildImages { get; init; }
	public string? ProviderName { get; init; }
	public string? SetVarsScriptPath { get; init; }
	public IReadOnlyList<string> ProjectFilters { get; init; } = Array.Empty<string>();
}

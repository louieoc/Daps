namespace Dapsman.Application;

public sealed class RemoteBuildOptions
{
	public bool DryRun { get; init; }
	public string? ProviderName { get; init; }
	public string? SetVarsScriptPath { get; init; }
	public string? CreateScriptPath { get; init; }
}

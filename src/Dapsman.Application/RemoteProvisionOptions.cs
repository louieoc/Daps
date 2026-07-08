namespace Dapsman.Application;

public sealed class RemoteProvisionOptions
{
	public bool DryRun { get; init; }
	public string? ProviderName { get; init; }
	public string? SetVarsScriptPath { get; init; }
	public string? CreateScriptPath { get; init; }
	public bool Upgrade { get; init; }
}

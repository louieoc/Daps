namespace Dapsman.Domain;

public sealed class RemoteBuildPlan
{
    public required string ProviderName { get; init; }
    public required string DefaultKeyName { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required string OpenRcScriptHostPath { get; init; }
    public required string SetVarsScriptHostPath { get; init; }
    public required string CreateScriptHostPath { get; init; }
    public required string OpenRcScriptContainerPath { get; init; }
    public required string SetVarsScriptContainerPath { get; init; }
    public required string CreateScriptContainerPath { get; init; }
    public required string ToolkitCommand { get; init; }
}

namespace Dapsman.Domain;

public sealed class RemoteProvisionPlan
{
    public required string ProviderName { get; init; }
    public required string DefaultKeyName { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required IReadOnlyList<KeyValuePair<string, string>> ProviderDetails { get; init; }
    public required string ToolkitCommand { get; init; }
}

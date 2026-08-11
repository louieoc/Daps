namespace Dapsman.Domain;

public sealed class RemoteProvisionPlan
{
    public required string ProviderName { get; init; }
    public required string DefaultKeyName { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required IReadOnlyList<KeyValuePair<string, string>> ProviderDetails { get; init; }
    public required string ToolkitCommand { get; init; }

    public required string DapsRootPath { get; init; }

    /// <summary>
    /// Host paths of scripts the executor stages before running the plan. Staging
    /// rewrites them with LF endings, since they are copied on to a Linux host where
    /// a CRLF script fails to run.
    /// </summary>
    public required IReadOnlyList<string> ScriptFilesToStage { get; init; }
}

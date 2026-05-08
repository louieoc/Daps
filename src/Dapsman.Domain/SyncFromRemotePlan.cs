namespace Dapsman.Domain;

public sealed class SyncFromRemotePlan
{
    public required string ProjectName { get; init; }
    public required string DapsRootPath { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required string RemoteHost { get; init; }
    public required string RemoteUser { get; init; }

    /// <summary>Path to the SSH key inside the toolkit container, e.g. /root/.ssh/daps-key-ramnode.
    /// Absolute path required — tilde doesn't expand inside bash variables.</summary>
    public required string SshKeyToolkitPath { get; init; }

    /// <summary>Path to the sync script inside the toolkit container.</summary>
    public required string SyncScriptToolkitPath { get; init; }

    public IReadOnlyDictionary<string, string> EnvVars => new Dictionary<string, string>
    {
        ["DAPS_PROJECT"] = ProjectName,
        ["DAPS_REMOTE_HOST"] = RemoteHost,
        ["DAPS_REMOTE_USER"] = RemoteUser,
        ["DAPS_SSH_KEY"] = SshKeyToolkitPath,
    };
}

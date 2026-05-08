namespace Dapsman.Domain;

public sealed class OfflineStatusPlan
{
    public required string ProjectName { get; init; }
    public required string DapsRootPath { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required string RemoteHost { get; init; }
    public required string RemoteUser { get; init; }

    /// <summary>SSH private key file name (used by the staging script SCP/SSH commands).</summary>
    public required string SshKeyName { get; init; }

    /// <summary>Filename of the Caddy site config on the remote, e.g. mywpsite.prod.caddy.</summary>
    public required string CaddySiteFileName { get; init; }

    /// <summary>Content to write to the remote Caddy site config file.</summary>
    public required string CaddySiteContent { get; init; }

    /// <summary>e.g. "docker" for root, "sudo docker" for non-root remote users.</summary>
    public required string DockerCommandPrefix { get; init; }

    /// <summary>True when taking the project offline; false when restoring it online.</summary>
    public required bool IsOffline { get; init; }
}

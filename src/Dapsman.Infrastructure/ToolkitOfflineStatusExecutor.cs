using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitOfflineStatusExecutor : IOfflineStatusExecutor
{
    private readonly IBashRunner _bashRunner;

    public ToolkitOfflineStatusExecutor(IBashRunner bashRunner)
    {
        _bashRunner = bashRunner;
    }

    public void Execute(OfflineStatusPlan plan)
    {
        var staging = CreateStaging(plan);
        try
        {
            var script = BuildScript(plan, staging.CaddySiteToolkitPath).Replace("\r\n", "\n");
            File.WriteAllText(staging.ScriptHostPath, script);
            _bashRunner.RunScript(staging.ScriptToolkitPath, plan.DapsRootPath);
        }
        finally
        {
            TryDeleteStaging(staging.HostPath);
        }
    }

    private static StagingPaths CreateStaging(OfflineStatusPlan plan)
    {
        var id = Guid.NewGuid().ToString("N");
        var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "offline-status", id);
        Directory.CreateDirectory(hostPath);

        File.WriteAllText(Path.Combine(hostPath, plan.CaddySiteFileName), plan.CaddySiteContent.Replace("\r\n", "\n"));

        return new StagingPaths
        {
            HostPath = hostPath,
            CaddySiteToolkitPath = $"/srv/daps/.dapsman/offline-status/{id}/{EscapeBash(plan.CaddySiteFileName)}",
            ScriptHostPath = Path.Combine(hostPath, "set-offline-status.sh"),
            ScriptToolkitPath = $"/srv/daps/.dapsman/offline-status/{id}/set-offline-status.sh",
        };
    }

    private static string BuildScript(OfflineStatusPlan plan, string caddySiteToolkitPath)
    {
        var keyName = EscapeBash(plan.SshKeyName);
        var remote = $"{EscapeBash(plan.RemoteUser)}@{EscapeBash(plan.RemoteHost)}";
        var remoteCaddySitePath = $"/srv/daps/caddy_sites/{EscapeBash(plan.CaddySiteFileName)}";

        return $$"""
            #!/usr/bin/env bash
            set -euo pipefail
            remote="{{remote}}"
            ssh_key="~/.ssh/{{keyName}}"
            ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i "$ssh_key")

            scp "${ssh_opts[@]}" "{{caddySiteToolkitPath}}" "$remote:{{remoteCaddySitePath}}"
            ssh "${ssh_opts[@]}" "$remote" "{{EscapeBash(plan.DockerCommandPrefix)}} exec daps-caddy-1 caddy reload --config /etc/caddy/Caddyfile"
            """;
    }

    private static void TryDeleteStaging(string hostStagingPath)
    {
        try
        {
            if (Directory.Exists(hostStagingPath))
                Directory.Delete(hostStagingPath, recursive: true);
        }
        catch
        {
            // no-op
        }
    }

    private static string EscapeBash(string value) => value.Replace("\\", "/").Replace("\"", "\\\"");

    private sealed class StagingPaths
    {
        public required string HostPath { get; init; }
        public required string CaddySiteToolkitPath { get; init; }
        public required string ScriptHostPath { get; init; }
        public required string ScriptToolkitPath { get; init; }
    }
}

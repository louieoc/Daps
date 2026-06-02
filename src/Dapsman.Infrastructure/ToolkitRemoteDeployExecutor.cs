using System.Text;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitRemoteDeployExecutor : IRemoteDeployExecutor
{
    private readonly IBashRunner _bashRunner;

    public ToolkitRemoteDeployExecutor(IBashRunner bashRunner)
    {
        _bashRunner = bashRunner;
    }

    public void ExecuteBuildImages(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath)
    {
        foreach (var command in commands)
        {
            _bashRunner.RunScript(command.ToolkitScriptPath, dapsRootPath);
        }
    }

    public void Execute(RemoteDeployPlan plan)
    {
        var staging = CreateDeployStaging(plan);
        try
        {
            var script = BuildRemoteDeployScript(plan, staging.ToolkitPath).Replace("\r\n", "\n");
            File.WriteAllText(staging.ScriptHostPath, script);
            _bashRunner.RunScript(staging.ScriptToolkitPath, plan.DapsRootPath);
        }
        finally
        {
            TryDeleteStaging(staging.HostPath);
        }
    }

    private static DeployStaging CreateDeployStaging(RemoteDeployPlan plan)
    {
        var id = Guid.NewGuid().ToString("N");
        var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "deploy", id);
        Directory.CreateDirectory(hostPath);

        var caddyPath = Path.Combine(hostPath, "caddy");
        Directory.CreateDirectory(caddyPath);
        CopyFile(plan.CaddyfileSourcePath, Path.Combine(caddyPath, "Caddyfile"));

        var caddySitesPath = Path.Combine(hostPath, "caddy_sites");
        Directory.CreateDirectory(caddySitesPath);
        foreach (var caddyFile in plan.CaddySiteFilesToUpload)
        {
            CopyFile(caddyFile.SourcePath, Path.Combine(caddySitesPath, caddyFile.DestinationFileName));
        }

        var dockerPath = Path.Combine(hostPath, "docker");
        Directory.CreateDirectory(dockerPath);
        foreach (var dapsCompose in plan.DapsComposeFilesToUpload)
        {
            CopyFile(dapsCompose, Path.Combine(dockerPath, Path.GetFileName(dapsCompose)));
        }

        var projectsRoot = Path.Combine(hostPath, "projects");
        Directory.CreateDirectory(projectsRoot);
        foreach (var projectPlan in plan.ProjectPlans)
        {
            var projectDockerPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_docker");
            Directory.CreateDirectory(projectDockerPath);
            var exportsPath = Path.Combine(projectDockerPath, "image-exports");
            Directory.CreateDirectory(exportsPath);
            var uploadsPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_uploads");
            Directory.CreateDirectory(uploadsPath);

            if (projectPlan.RemoteScriptFilesToUpload.Count > 0)
            {
                var scriptsPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_scripts");
                Directory.CreateDirectory(scriptsPath);
                foreach (var scriptFile in projectPlan.RemoteScriptFilesToUpload)
                {
                    CopyFile(scriptFile, Path.Combine(scriptsPath, Path.GetFileName(scriptFile)));
                }
            }

            foreach (var composeFile in projectPlan.ComposeFilesToUpload)
            {
                CopyFile(composeFile, Path.Combine(projectDockerPath, Path.GetFileName(composeFile)));
            }

            foreach (var exportFile in projectPlan.ImageExportFilesToUpload)
            {
                CopyFile(exportFile, Path.Combine(exportsPath, Path.GetFileName(exportFile)));
            }

            for (var i = 0; i < projectPlan.UploadFiles.Count; i++)
            {
                var upload = projectPlan.UploadFiles[i];
                var stagedName = $"{i:D3}_{Path.GetFileName(upload.SourcePath)}";
                CopyFile(upload.SourcePath, Path.Combine(uploadsPath, stagedName));
            }
        }

        return new DeployStaging
        {
            HostPath = hostPath,
            ToolkitPath = $"/srv/daps/.dapsman/deploy/{id}",
            ScriptHostPath = Path.Combine(hostPath, "deploy.sh"),
            ScriptToolkitPath = $"/srv/daps/.dapsman/deploy/{id}/deploy.sh",
        };
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void TryDeleteStaging(string hostStagingPath)
    {
        try
        {
            if (Directory.Exists(hostStagingPath))
            {
                Directory.Delete(hostStagingPath, recursive: true);
            }
        }
        catch
        {
            // no-op
        }
    }

    private static string BuildRemoteDeployScript(RemoteDeployPlan plan, string stagingToolkitPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();
        sb.AppendLine($"remote=\"{EscapeBash(plan.RemoteUser)}@{EscapeBash(plan.RemoteHost)}\"");
        sb.AppendLine($"ssh_key=\"~/.ssh/{EscapeBash(plan.SshKeyName)}\"");
        sb.AppendLine("ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i \"$ssh_key\")");
        sb.AppendLine();
        sb.AppendLine("ssh \"${ssh_opts[@]}\" \"$remote\" 'mkdir -p /srv/daps/caddy /srv/daps/caddy_sites /srv/daps/docker /srv/projects'");
        sb.AppendLine();
        sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/caddy/Caddyfile\" \"$remote:/srv/daps/caddy/Caddyfile\"");

        sb.AppendLine();
        sb.AppendLine("ssh \"${ssh_opts[@]}\" \"$remote\" 'bash -s' <<'REMOTE_CLEAN'");
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine("mkdir -p /srv/daps/caddy_sites");
        sb.AppendLine("mapfile -t expected < <(cat <<'EOF_EXPECTED'");
        foreach (var fileName in plan.ExpectedProdCaddyFileNames)
        {
            sb.AppendLine(fileName);
        }
        sb.AppendLine("EOF_EXPECTED");
        sb.AppendLine(")");
        sb.AppendLine("for existing in /srv/daps/caddy_sites/*.prod.caddy; do");
        sb.AppendLine("  [ -e \"$existing\" ] || break");
        sb.AppendLine("  base=$(basename \"$existing\")");
        sb.AppendLine("  keep=0");
        sb.AppendLine("  for item in \"${expected[@]}\"; do");
        sb.AppendLine("    if [[ \"$item\" == \"$base\" ]]; then");
        sb.AppendLine("      keep=1");
        sb.AppendLine("      break");
        sb.AppendLine("    fi");
        sb.AppendLine("  done");
        sb.AppendLine("  if [[ \"$keep\" == \"0\" ]]; then");
        sb.AppendLine("    rm -f \"$existing\"");
        sb.AppendLine("  fi");
        sb.AppendLine("done");
        sb.AppendLine("REMOTE_CLEAN");
        sb.AppendLine();

        foreach (var caddyFile in plan.CaddySiteFilesToUpload)
        {
            sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/caddy_sites/{EscapeBash(caddyFile.DestinationFileName)}\" \"$remote:/srv/daps/caddy_sites/{EscapeBash(caddyFile.DestinationFileName)}\"");
        }

        foreach (var dapsFile in plan.DapsComposeFilesToUpload)
        {
            sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/docker/{EscapeBash(Path.GetFileName(dapsFile))}\" \"$remote:/srv/daps/docker/{EscapeBash(Path.GetFileName(dapsFile))}\"");
        }

        foreach (var projectPlan in plan.ProjectPlans)
        {
            sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'mkdir -p /srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker /srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/image-exports'");
            foreach (var composeFile in projectPlan.ComposeFilesToUpload)
            {
                sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/{EscapeBash(Path.GetFileName(composeFile))}\" \"$remote:/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/{EscapeBash(Path.GetFileName(composeFile))}\"");
            }

            foreach (var exportFile in projectPlan.ImageExportFilesToUpload)
            {
                sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/image-exports/{EscapeBash(Path.GetFileName(exportFile))}\" \"$remote:/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/image-exports/{EscapeBash(Path.GetFileName(exportFile))}\"");
            }

            for (var i = 0; i < projectPlan.UploadFiles.Count; i++)
            {
                var upload = projectPlan.UploadFiles[i];
                var stagedName = $"{i:D3}_{Path.GetFileName(upload.SourcePath)}";
                var remoteDestination = EscapeBash(upload.RemoteDestinationPath);
                var remoteTempPath = EscapeBash($"/tmp/dapsman-upload-{projectPlan.ProjectName}-{i:D3}");
                sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'mkdir -p \"$(dirname \"{remoteDestination}\")\"'");
                sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_uploads/{EscapeBash(stagedName)}\" \"$remote:{remoteTempPath}\"");
                sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'if [ -d \"{remoteDestination}\" ]; then rm -rf \"{remoteDestination}\"; fi'");
                sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'rm -f \"{remoteDestination}\" && install -m 600 \"{remoteTempPath}\" \"{remoteDestination}\" && rm -f \"{remoteTempPath}\"'");
                sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'if [ ! -f \"{remoteDestination}\" ]; then echo \"Expected upload destination to be a file: {remoteDestination}\" >&2; exit 1; fi'");
            }

            if (projectPlan.RemoteScriptFilesToUpload.Count > 0)
            {
                var remoteScriptsDir = $"/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts";
                sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'mkdir -p {remoteScriptsDir}'");
                foreach (var scriptFile in projectPlan.RemoteScriptFilesToUpload)
                {
                    var fileName = EscapeBash(Path.GetFileName(scriptFile));
                    sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts/{fileName}\" \"$remote:{remoteScriptsDir}/{fileName}\"");
                    sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'chmod +x {remoteScriptsDir}/{fileName}'");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("ssh \"${ssh_opts[@]}\" \"$remote\" 'bash -s' <<'REMOTE_DOCKER'");
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine($"{plan.DockerCommandPrefix} network inspect daps_net >/dev/null 2>&1 || {plan.DockerCommandPrefix} network create daps_net >/dev/null");
        var dapsRemoteComposeArgs = string.Join(" ", plan.DapsComposeFilesToUpload.Select(f => $"-f /srv/daps/docker/{Path.GetFileName(f)}"));
        sb.AppendLine($"{plan.DockerCommandPrefix} compose {dapsRemoteComposeArgs} up -d");
        foreach (var projectPlan in plan.ProjectPlans)
        {
            foreach (var prereqName in projectPlan.RemotePrerequisiteScriptNames)
            {
                sb.AppendLine($"bash /srv/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts/{EscapeBash(prereqName)}");
            }

            sb.AppendLine($"for image_file in /srv/projects/{projectPlan.ProjectName}/_docker/image-exports/*; do");
            sb.AppendLine("  [ -e \"$image_file\" ] || continue");
            sb.AppendLine($"  {plan.DockerCommandPrefix} load -i \"$image_file\"");
            sb.AppendLine("done");
            var projectComposeArgs = string.Join(" ", projectPlan.ComposeFileNamesForRemoteRun.Select(f => $"-f /srv/projects/{projectPlan.ProjectName}/_docker/{f}"));
            sb.AppendLine($"{plan.DockerCommandPrefix} compose {projectComposeArgs} up -d --force-recreate");
        }
        sb.AppendLine("REMOTE_DOCKER");

        return sb.ToString();
    }

    private static string EscapeBash(string value)
    {
        return value.Replace("\\", "/").Replace("\"", "\\\"");
    }

    private sealed class DeployStaging
    {
        public required string HostPath { get; init; }
        public required string ToolkitPath { get; init; }
        public required string ScriptHostPath { get; init; }
        public required string ScriptToolkitPath { get; init; }
    }
}

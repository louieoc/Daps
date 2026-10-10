using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitRemoteTeardownExecutor : IRemoteTeardownExecutor
{
	private readonly IBashRunner _bashRunner;

	public ToolkitRemoteTeardownExecutor(IBashRunner bashRunner)
	{
		_bashRunner = bashRunner;
	}

	public void Execute(RemoteTeardownPlan plan)
	{
		var staging = CreateStaging(plan);
		try
		{
			var script = BuildScript(plan, staging).Replace("\r\n", "\n");
			File.WriteAllText(staging.ScriptHostPath, script);
			_bashRunner.RunScript(staging.ScriptToolkitPath, plan.DapsRootPath);
		}
		finally
		{
			TryDeleteStaging(staging.HostPath);
		}
	}

	private static StagingPaths CreateStaging(RemoteTeardownPlan plan)
	{
		var id = Guid.NewGuid().ToString("N");
		var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "teardown", id);
		Directory.CreateDirectory(hostPath);

		string? caddySiteToolkitPath = null;
		if (plan.RemovedCaddyContent is not null)
		{
			File.WriteAllText(
				Path.Combine(hostPath, plan.CaddySiteFileName),
				plan.RemovedCaddyContent.Replace("\r\n", "\n"));

			caddySiteToolkitPath = $"/srv/daps/.dapsman/teardown/{id}/{EscapeBash(plan.CaddySiteFileName)}";
		}

		return new StagingPaths
		{
			HostPath = hostPath,
			CaddySiteToolkitPath = caddySiteToolkitPath,
			ScriptHostPath = Path.Combine(hostPath, "teardown.sh"),
			ScriptToolkitPath = $"/srv/daps/.dapsman/teardown/{id}/teardown.sh",
		};
	}

	private static string BuildScript(RemoteTeardownPlan plan, StagingPaths staging)
	{
		var keyName = EscapeBash(plan.SshKeyName);
		var remote = $"{EscapeBash(plan.RemoteUser)}@{EscapeBash(plan.RemoteHost)}";
		var dockerPrefix = plan.IsRoot ? "docker" : "sudo docker";
		var sudo = plan.IsRoot ? "" : "sudo ";
		var projectName = EscapeBash(plan.ProjectName);
		var remoteProjectPath = EscapeBash(plan.RemoteProjectPath);

		var lines = new List<string>
		{
			"#!/usr/bin/env bash",
			"set -euo pipefail",
			$"remote=\"{remote}\"",
			$"ssh_key=\"~/.ssh/{keyName}\"",
			"ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i \"$ssh_key\")",
			"",
		};

		var remoteCaddySitePath = $"/srv/daps/caddy_sites/{EscapeBash(plan.CaddySiteFileName)}";
		if (staging.CaddySiteToolkitPath is not null)
		{
			lines.Add("# Step 1: replace caddy site file with removed page and reload");
			lines.Add($"scp \"${{ssh_opts[@]}}\" \"{staging.CaddySiteToolkitPath}\" \"$remote:{remoteCaddySitePath}\"");
		}
		else
		{
			// No domain to render a removed page with. Deleting stops the retired host claiming
			// the domain and renewing a certificate for it. rm -f is a no-op when it is absent.
			lines.Add("# Step 1: delete caddy site file and reload");
			lines.Add($"ssh \"${{ssh_opts[@]}}\" \"$remote\" \"{sudo}rm -f '{remoteCaddySitePath}'\"");
		}

		// A stopped Caddy reads the changed site file when it next starts, so there is nothing to
		// reload. A running one that fails to reload has a broken config, and that still stops
		// the teardown.
		lines.Add($"ssh \"${{ssh_opts[@]}}\" \"$remote\" \"if {dockerPrefix} ps -q -f name=daps-caddy-1 -f status=running | grep -q .; then {dockerPrefix} exec daps-caddy-1 caddy reload --config /etc/caddy/Caddyfile; else echo 'Caddy is not running; skipped reload.'; fi\"");
		lines.Add("");

		var baseCompose = $"/srv/projects/{projectName}/_docker/compose_{projectName}.yaml";
		var prodCompose = $"/srv/projects/{projectName}/_docker/compose_{projectName}.prod.yaml";

		lines.Add("# Step 2: bring down containers and delete volumes");
		lines.Add($"ssh \"${{ssh_opts[@]}}\" \"$remote\" \"if [[ -f '{prodCompose}' ]]; then {dockerPrefix} compose -f '{baseCompose}' -f '{prodCompose}' down -v || true; else {dockerPrefix} compose -f '{baseCompose}' down -v || true; fi\"");
		lines.Add("");
		lines.Add("# Step 3: delete remote project folder");
		// Containers write into the project folder as their own users (e.g. www-data), so a
		// non-root remote user cannot delete it without sudo.
		lines.Add($"ssh \"${{ssh_opts[@]}}\" \"$remote\" \"{sudo}rm -rf '{remoteProjectPath}'\"");

		return string.Join("\n", lines);
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
		public required string? CaddySiteToolkitPath { get; init; }
		public required string ScriptHostPath { get; init; }
		public required string ScriptToolkitPath { get; init; }
	}
}

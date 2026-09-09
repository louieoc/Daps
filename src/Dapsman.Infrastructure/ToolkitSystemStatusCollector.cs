using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitSystemStatusCollector : ISystemStatusCollector
{
	private readonly IBashRunner _bashRunner;

	public ToolkitSystemStatusCollector(IBashRunner bashRunner)
	{
		_bashRunner = bashRunner;
	}

	public SystemStatus Collect(SystemStatusPlan plan)
	{
		var staging = CreateStaging(plan);
		try
		{
			var script = BuildScript(plan, staging.PayloadToolkitPath).Replace("\r\n", "\n");
			File.WriteAllText(staging.ScriptHostPath, script);

			var output = _bashRunner.CaptureScript(staging.ScriptToolkitPath, plan.DapsRootPath);
			return SystemStatusParser.Parse(output);
		}
		finally
		{
			TryDeleteStaging(staging.HostPath);
		}
	}

	private static StagingPaths CreateStaging(SystemStatusPlan plan)
	{
		var id = Guid.NewGuid().ToString("N");
		var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "system-status", id);
		Directory.CreateDirectory(hostPath);

		var payloadName = Path.GetFileName(plan.StatusScriptHostPath);
		ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(
			plan.StatusScriptHostPath,
			Path.Combine(hostPath, payloadName));

		return new StagingPaths
		{
			HostPath = hostPath,
			PayloadToolkitPath = $"/srv/daps/.dapsman/system-status/{id}/{payloadName}",
			ScriptHostPath = Path.Combine(hostPath, "collect-system-status.sh"),
			ScriptToolkitPath = $"/srv/daps/.dapsman/system-status/{id}/collect-system-status.sh",
		};
	}

	/// <summary>
	/// The payload goes to the remote over stdin rather than as an argument to ssh. The status
	/// script is full of $, quotes and awk, and embedding it would have to survive this file's
	/// interpolation, the `docker exec bash -lc "..."` wrapper, and the remote shell's own
	/// expansion. Redirecting from a file removes the last two of those entirely.
	/// </summary>
	private static string BuildScript(SystemStatusPlan plan, string payloadToolkitPath)
	{
		var remote = $"{EscapeBash(plan.RemoteUser)}@{EscapeBash(plan.RemoteHost)}";
		var verbose = plan.Verbose ? "1" : "0";

		// The remote command is quoted twice on purpose. ssh does not pass an argument vector: it
		// joins everything after the host into one string and hands it to the remote login shell,
		// which splits it again. Passing the arguments unquoted would turn "sudo docker" into two
		// arguments on the far side, shifting every later argument along by one — which silently
		// disables the verbose section rather than failing. The inner single quotes survive the
		// join and put the arguments back together on the remote.
		var remoteArgs = string.Join(" ", new[]
		{
			plan.DockerCommandPrefix,
			verbose,
			plan.RemoteProjectsRoot,
		}.Select(SingleQuote));

		return $$"""
			#!/usr/bin/env bash
			set -euo pipefail
			ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i "$HOME/.ssh/{{EscapeBash(plan.SshKeyName)}}")

			ssh -q "${ssh_opts[@]}" "{{remote}}" "bash -s -- {{remoteArgs}}" < "{{payloadToolkitPath}}"
			""";
	}

	/// <summary>Wraps a value in single quotes for the remote shell, escaping any it contains.</summary>
	private static string SingleQuote(string value) => $"'{value.Replace("'", "'\\''")}'";

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
		public required string PayloadToolkitPath { get; init; }
		public required string ScriptHostPath { get; init; }
		public required string ScriptToolkitPath { get; init; }
	}
}

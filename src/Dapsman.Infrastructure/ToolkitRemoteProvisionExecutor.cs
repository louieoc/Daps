using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// Runs a provision plan from the toolkit container, staging the scripts it needs
/// under .dapsman/provision first. Staging exists so that scripts leaving the
/// workstation for a Linux host are written with LF endings: a CRLF shell script
/// fails immediately on Linux with "set: pipefail: invalid option name", and a
/// Windows checkout can produce CRLF working-tree files.
/// </summary>
public sealed class ToolkitRemoteProvisionExecutor : IRemoteProvisionExecutor
{
	/// <summary>
	/// Path of the staging directory as seen from inside the toolkit container.
	/// Plan builders reference it to point scp at the staged copy of a script.
	/// </summary>
	public const string StagingToolkitPath = "/srv/daps/.dapsman/provision";

	private const string ProvisionScriptName = "provision.sh";

	private readonly IBashRunner _bashRunner;

	public ToolkitRemoteProvisionExecutor(IBashRunner bashRunner)
	{
		_bashRunner = bashRunner;
	}

	public void Execute(RemoteProvisionPlan plan)
	{
		// Unlike deploy, the staging path is fixed rather than per-run: it has to be
		// known when the plan is built so the printed dry-run command matches what
		// actually runs. Provisioning is a single interactive workflow, so there is no
		// concurrent run to collide with.
		var stagingHostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "provision");
		TryDeleteStaging(stagingHostPath);
		Directory.CreateDirectory(stagingHostPath);

		try
		{
			foreach (var scriptPath in plan.ScriptFilesToStage)
			{
				ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(
					scriptPath,
					Path.Combine(stagingHostPath, Path.GetFileName(scriptPath)));
			}

			var provisionScriptHostPath = Path.Combine(stagingHostPath, ProvisionScriptName);
			File.WriteAllText(provisionScriptHostPath, BuildProvisionScript(plan).Replace("\r\n", "\n"));

			_bashRunner.RunScript(
				$"{StagingToolkitPath}/{ProvisionScriptName}",
				plan.DapsRootPath,
				interactive: true);
		}
		finally
		{
			TryDeleteStaging(stagingHostPath);
		}
	}

	private static string BuildProvisionScript(RemoteProvisionPlan plan)
	{
		// The chain is already assembled by the plan builder so that --dry-run can print
		// exactly what will run. Wrapping it in a file rather than passing it to
		// `bash -lc` keeps quoting intact and makes failures point at a line.
		return string.Join("\n", new[]
		{
			"#!/usr/bin/env bash",
			"set -euo pipefail",
			"",
			plan.ToolkitCommand,
			"",
		});
	}

	private static void TryDeleteStaging(string stagingHostPath)
	{
		try
		{
			if (Directory.Exists(stagingHostPath))
			{
				Directory.Delete(stagingHostPath, recursive: true);
			}
		}
		catch
		{
			// no-op
		}
	}
}

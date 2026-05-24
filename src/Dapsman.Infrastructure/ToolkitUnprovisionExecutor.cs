using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitUnprovisionExecutor : IUnprovisionExecutor
{
	private readonly IBashRunner _toolkitBashRunner;

	public ToolkitUnprovisionExecutor(IBashRunner toolkitBashRunner)
	{
		_toolkitBashRunner = toolkitBashRunner;
	}

	public void Execute(UnprovisionPlan plan)
	{
		var staging = CreateStaging(plan);
		try
		{
			var script = BuildScript(plan).Replace("\r\n", "\n");
			File.WriteAllText(staging.ScriptHostPath, script);
			_toolkitBashRunner.RunScript(staging.ScriptToolkitPath, plan.DapsRootPath);
		}
		finally
		{
			TryDeleteStaging(staging.HostPath);
		}

		if (File.Exists(plan.InstanceVarsFilePath))
			File.Delete(plan.InstanceVarsFilePath);
	}

	private static StagingPaths CreateStaging(UnprovisionPlan plan)
	{
		var id = Guid.NewGuid().ToString("N");
		var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "unprovision", id);
		Directory.CreateDirectory(hostPath);

		return new StagingPaths
		{
			HostPath = hostPath,
			ScriptHostPath = Path.Combine(hostPath, "unprovision.sh"),
			ScriptToolkitPath = $"/srv/daps/.dapsman/unprovision/{id}/unprovision.sh",
		};
	}

	private static string BuildScript(UnprovisionPlan plan)
	{
		var openRc = EscapeBash(plan.OpenRcContainerPath);
		var instanceVars = EscapeBash(plan.InstanceVarsContainerPath);
		var defaultInstance = EscapeBash(plan.DefaultInstanceName);
		var defaultKey = EscapeBash(plan.DefaultKeyName);
		var sshKey = EscapeBash(plan.SshKeyToolkitPath);

		return $$"""
			#!/usr/bin/env bash
			set -euo pipefail
			source "{{openRc}}"
			source "{{instanceVars}}"
			instance_name="${OS_INSTANCE_NAME:-{{defaultInstance}}}"
			key_name="${OS_KEY_NAME:-{{defaultKey}}}"

			openstack server delete "$instance_name" || true
			openstack keypair delete "$key_name" || true

			rm -f "{{sshKey}}" "{{sshKey}}.pub"
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
		public required string ScriptHostPath { get; init; }
		public required string ScriptToolkitPath { get; init; }
	}
}

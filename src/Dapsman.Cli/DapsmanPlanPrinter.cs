using Dapsman.Application;
using Dapsman.Domain;

internal static class DapsmanPlanPrinter
{
	public static void PrintInit(InitPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");

		Console.WriteLine();
		Console.WriteLine("Step: copy-template");
		Console.WriteLine($"- template: {plan.TemplatePath}");
		Console.WriteLine($"- destination: {plan.DestinationPath}");
		Console.WriteLine(plan.Overlay
			? "- mode: overlay (add Daps files to existing directory, skip existing files)"
			: "- mode: normal (create new directory)");
		Console.WriteLine($"- register in daps.yaml: {plan.ProjectName}: path: {plan.DapsYamlProjectRelativePath}");
		if (plan.ProdUrl is not null)
			Console.WriteLine($"- set prod url in _caddy_sites/*.prod.caddy and _docker/*.prod.yaml: {plan.ProdUrl}");
		else
			Console.WriteLine("- prod url: not set (edit _caddy_sites/*.prod.caddy and _docker/*.prod.yaml manually)");
		if (plan.DevPortAssignments.Count > 0)
		{
			Console.WriteLine("- dev port assignments:");
			foreach (var a in plan.DevPortAssignments)
			{
				if (a.AssignedPort != a.OriginalPort)
					Console.WriteLine($"  {a.OriginalPort} → {a.AssignedPort} ({a.Reason})");
				else
					Console.WriteLine($"  {a.OriginalPort} (available)");
			}
		}
	}

	// todo: figure out how to only accept a plan object
	public static void PrintLocalBuild(LocalBuildPlan plan, LocalBuildOptions options, CaddyRestartPlan caddyRestartPlan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");

		Console.WriteLine();
		Console.WriteLine("Step: sync-caddy");
		Console.WriteLine($"- runtime path: {plan.CaddySync.RuntimeSitesPath}");
		if (plan.CaddySync.FilesToCopy.Count == 0)
		{
			Console.WriteLine("- no project caddy files found; placeholder will be created");
		}
		else
		{
			foreach (var copy in plan.CaddySync.FilesToCopy)
				Console.WriteLine($"- {copy.ProjectName}: \"{copy.SourcePath}\" -> \"{copy.DestinationPath}\"");
		}

		Console.WriteLine();
		Console.WriteLine("Step: prerequisites");
		var hasPrereqs = plan.ProjectComposePlans.Any(p => p.PrerequisiteScripts?.Count > 0);
		if (!hasPrereqs)
		{
			Console.WriteLine("- no prerequisite scripts found");
		}
		else
		{
			foreach (var projectPlan in plan.ProjectComposePlans)
				foreach (var script in projectPlan.PrerequisiteScripts)
					Console.WriteLine($"- {projectPlan.ProjectName}: bash \"{script}\"");
		}

		Console.WriteLine();
		Console.WriteLine("Step: docker-network");
		Console.WriteLine($"- docker network create {plan.SharedNetworkName} (if missing)");

		Console.WriteLine();
		Console.WriteLine("Step: compose-daps");
		Console.WriteLine($"- {plan.DapsComposeCommand}");

		if (options.Rebuild)
		{
			Console.WriteLine();
			Console.WriteLine("Step: rebuild-teardown");
			foreach (var projectPlan in plan.ProjectComposePlans)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: {projectPlan.ComposeDownCommand}");
			}
		}

		Console.WriteLine();
		Console.WriteLine("Step: compose-project");
		if (plan.ProjectComposePlans.Count == 0)
		{
			Console.WriteLine("- no projects selected/configured; skipped");
		}
		else
		{
			foreach (var projectPlan in plan.ProjectComposePlans)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: {projectPlan.ComposeUpCommand}");
			}
		}

		Console.WriteLine();
		Console.WriteLine("Step: caddy-reload");
		Console.WriteLine($"- {caddyRestartPlan.ReloadCommand}");

		Console.WriteLine();
		Console.WriteLine("Summary:");
		Console.WriteLine($"- projects configured: {(plan.HasProjectsConfigured ? "yes" : "no")}");
		Console.WriteLine($"- project compose runs: {plan.ProjectComposePlans.Count}");

		if (plan.Warnings.Count > 0)
			foreach (var warning in plan.Warnings)
				Console.WriteLine($"- warning: {warning}");
	}

	public static void PrintCaddyRestart(CaddyRestartPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine();
		Console.WriteLine("Step: caddy-reload");
		Console.WriteLine($"- {plan.ReloadCommand}");
	}

	public static void PrintRemoteProvision(RemoteProvisionPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- provider: {plan.ProviderName}");
		Console.WriteLine($"- default key name: {plan.DefaultKeyName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		foreach (var (label, value) in plan.ProviderDetails)
		{
			Console.WriteLine($"- {label}: {value}");
		}

		Console.WriteLine();
		Console.WriteLine("Step: setup-prod-environment");
		Console.WriteLine($"- docker exec -it {plan.ToolkitContainerName} bash -lc <script-chain>");
		Console.WriteLine($"- script-chain: {plan.ToolkitCommand}");
	}

	public static void PrintRemoteDeploy(RemoteDeployPlan plan, RemoteDeployOptions options, CaddyRestartPlan caddyRestartPlan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- provider: {plan.ProviderName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote host: {plan.RemoteHost}");
		Console.WriteLine($"- remote user: {plan.RemoteUser}");
		Console.WriteLine($"- ssh key: {plan.SshKeyName}");

		if (plan.SystemStatusPlan is not null)
		{
			Console.WriteLine();
			Console.WriteLine("Step: probe-platform");
			Console.WriteLine($"- ask {plan.RemoteHost} which docker platform it runs (value determined at run time)");
			Console.WriteLine("- skipped when no image build runs");

			Console.WriteLine();
			Console.WriteLine("Step: build-images");
			if (plan.BuildImageCommands.Count == 0)
			{
				Console.WriteLine("- no project image build scripts found");
			}
			else
			{
				foreach (var command in plan.BuildImageCommands)
				{
					string disposition;
					if (options.BuildImages)
						disposition = "will build (--build)";
					else if (!command.HasExistingExports)
						disposition = "will build (no existing tar)";
					else
						disposition = "skipped (tar exists; use --build to rebuild)";
					Console.WriteLine($"- {command.ProjectName}: {disposition}");
				}
			}
		}
		else
		{
			Console.WriteLine();
			Console.WriteLine("Step: probe-platform SKIPPED");
			Console.WriteLine();
			Console.WriteLine("Step: build-images SKIPPED");
		}

		Console.WriteLine();
		Console.WriteLine("Step: upload-caddy");
		Console.WriteLine($"- source caddyfile: {plan.CaddyfileSourcePath}");
		Console.WriteLine($"- stale cleanup baseline (*.prod.caddy) from all daps.yaml projects: {plan.ExpectedProdCaddyFileNames.Count} file(s)");
		if (plan.CaddySiteFilesToUpload.Count == 0)
		{
			Console.WriteLine("- no selected-project prod caddy files to upload");
		}
		else
		{
			foreach (var copy in plan.CaddySiteFilesToUpload)
				Console.WriteLine($"- {copy.ProjectName}: \"{copy.SourcePath}\" -> /srv/daps/caddy_sites/{copy.DestinationFileName}");
		}

		Console.WriteLine();
		Console.WriteLine("Step: upload-docker");
		foreach (var composeFile in plan.DapsComposeFilesToUpload)
			Console.WriteLine($"- daps: \"{composeFile}\" -> /srv/daps/docker/{Path.GetFileName(composeFile)}");

		foreach (var projectPlan in plan.ProjectPlans)
		{
			foreach (var composeFile in projectPlan.ComposeFilesToUpload)
				Console.WriteLine($"- {projectPlan.ProjectName}: \"{composeFile}\" -> /srv/projects/{projectPlan.ProjectName}/_docker/{Path.GetFileName(composeFile)}");

			if (!string.IsNullOrWhiteSpace(projectPlan.ImageExportsSourcePath) && projectPlan.ImageExportFilesToUpload.Count == 0)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: no image exports found under \"{projectPlan.ImageExportsSourcePath}\"");
			}
			else
			{
				foreach (var exportFile in projectPlan.ImageExportFilesToUpload)
					Console.WriteLine($"- {projectPlan.ProjectName}: \"{exportFile}\" -> /srv/projects/{projectPlan.ProjectName}/_docker/image-exports/{Path.GetFileName(exportFile)}");
			}

			if (projectPlan.UploadFiles.Count == 0)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: no additional upload files");
			}
			else
			{
				foreach (var upload in projectPlan.UploadFiles)
					Console.WriteLine($"- {projectPlan.ProjectName}: \"{upload.SourcePath}\" -> {upload.RemoteDestinationPath}");
			}

			foreach (var script in projectPlan.RemoteScriptFilesToUpload)
				Console.WriteLine($"- {projectPlan.ProjectName}: \"{script}\" -> /srv/projects/{projectPlan.ProjectName}/_scripts/{Path.GetFileName(script)}");
		}

		Console.WriteLine();
		Console.WriteLine("Step: run-docker");
		Console.WriteLine("- ensure daps_net exists");
		Console.WriteLine("- compose up daps (prod/shared files)");
		foreach (var projectPlan in plan.ProjectPlans)
		{
			foreach (var prereqName in projectPlan.RemotePrerequisiteScriptNames)
				Console.WriteLine($"- {projectPlan.ProjectName}: run {prereqName} on remote (DAPS_PROJECT={projectPlan.ProjectName})");
			Console.WriteLine($"- {projectPlan.ProjectName}: docker load each file in /srv/projects/{projectPlan.ProjectName}/_docker/image-exports");
			Console.WriteLine($"- {projectPlan.ProjectName}: verify each loaded image matches the host platform (deploy fails if not)");
			Console.WriteLine($"- {projectPlan.ProjectName}: compose up project (prod/shared files)");
		}

		Console.WriteLine();
		Console.WriteLine("Step: caddy-reload");
		Console.WriteLine($"- {caddyRestartPlan.ReloadCommand}");

		foreach (var projectPlan in plan.ProjectPlans.Where(p => p.PostDeployScriptPath is not null))
		{
			Console.WriteLine();
			Console.WriteLine($"Step: post-deploy-hook ({projectPlan.ProjectName})");
			Console.WriteLine($"- {projectPlan.PostDeployScriptPath}");
		}

		Console.WriteLine();
		Console.WriteLine("Summary:");
		Console.WriteLine($"- project deploy runs: {plan.ProjectPlans.Count}");
		Console.WriteLine($"- expected prod caddy files (all configured projects): {plan.ExpectedProdCaddyFileNames.Count}");
		if (plan.Warnings.Count > 0)
			foreach (var warning in plan.Warnings)
				Console.WriteLine($"- warning: {warning}");
	}

	public static void PrintProdSetStatus(OfflineStatusPlan plan, string action)
	{
		Console.WriteLine($"- action: {action}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- caddy site file: {plan.CaddySiteFileName}");
	}

	public static void PrintLocalBackup(LocalBackupPlan plan)
	{
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine("Step: backup");
		Console.WriteLine($"- script: {plan.BackupScriptToolkitPath}");
		Console.WriteLine($"- destination: {plan.BackupDestinationToolkitPath}");
		Console.WriteLine($"- args: {plan.ScriptArguments}");
		foreach (var (key, value) in plan.EnvVars)
			Console.WriteLine($"  {key}={value}");
	}

	public static void PrintProdBackup(RemoteBackupPlan plan)
	{
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- ssh key: {plan.SshKeyToolkitPath}");
		Console.WriteLine("Step: backup");
		Console.WriteLine($"- script: {plan.BackupScriptToolkitPath}");
		Console.WriteLine($"- destination: {plan.BackupDestinationToolkitPath}");
		Console.WriteLine($"- args: {plan.ScriptArguments}");
		foreach (var (key, value) in plan.EnvVars)
			Console.WriteLine($"  {key}={value}");
	}

	public static void PrintSyncFromRemote(SyncFromRemotePlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- project: {plan.ProjectName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- ssh key: {plan.SshKeyToolkitPath}");
		Console.WriteLine();
		Console.WriteLine("Step: sync-from-prod");
		Console.WriteLine($"- script: {plan.SyncScriptToolkitPath}");
		foreach (var (key, value) in plan.EnvVars)
			Console.WriteLine($"  {key}={value}");
	}

	public static void PrintLocalRestore(RestorePlan plan)
	{
		var rp = plan.RestorePoint;
		Console.WriteLine("Step: validate");
		Console.WriteLine($"- project: {plan.ProjectName}");
		Console.WriteLine($"- restore point: {rp.DisplayLabel} (index {rp.Index})");
		Console.WriteLine($"- sql: _backups/{rp.SourceDirectory}/{plan.ProjectName}_{rp.Env}_{rp.Timestamp}.sql");
		Console.WriteLine($"- wp-content: _backups/{rp.SourceDirectory}/{plan.ProjectName}_{rp.Env}_wp-content_{rp.Timestamp}.tar.gz");
		// A backup taken from the local instance already holds dev URLs, so the script skips the
		// replacement — see restore-local.toolkit.sh.
		if (rp.ReplacesUrls)
			Console.WriteLine($"- prod url: {(string.IsNullOrEmpty(plan.ProdUrl) ? "(derived from caddy file)" : plan.ProdUrl)}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine();
		Console.WriteLine("Step: restore");
		Console.WriteLine("- import SQL into local database");
		Console.WriteLine("- extract wp-content archive");
		if (rp.ReplacesUrls)
			Console.WriteLine("- wp search-replace prod-url -> dev-url");
		Console.WriteLine("- wp cache flush");
		foreach (var (key, value) in plan.EnvVars)
			Console.WriteLine($"  {key}={value}");
	}

	public static void PrintLocalTeardown(LocalTeardownPlan plan)
	{
		Console.WriteLine("Step: stop containers");
		if (plan.ComposeFiles.Count > 0)
			foreach (var f in plan.ComposeFiles)
				Console.WriteLine($"- {f}");
		else
			Console.WriteLine("- no compose files found");
		Console.WriteLine("  docker compose down -v");
		Console.WriteLine();
		Console.WriteLine("Step: delete caddy site files");
		if (plan.CaddySiteFilesToDelete.Count > 0)
			foreach (var f in plan.CaddySiteFilesToDelete)
				Console.WriteLine($"- {f}");
		else
			Console.WriteLine("- no caddy site files");
		Console.WriteLine();
		Console.WriteLine("Step: reload Caddy");
		Console.WriteLine($"- docker exec {plan.CaddyContainerName} caddy reload");
		Console.WriteLine();
		Console.WriteLine("Step: delete project folder");
		Console.WriteLine($"- {plan.ProjectPath}");
		Console.WriteLine();
		Console.WriteLine("Step: deregister from daps.yaml");
		Console.WriteLine($"- remove '{plan.ProjectName}' from {plan.DapsYamlPath}");
	}

	public static void PrintSyncFromLocal(SyncFromLocalPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- project: {plan.ProjectName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- ssh key: {plan.SshKeyToolkitPath}");
		Console.WriteLine();
		Console.WriteLine("Step: sync-from-local");
		Console.WriteLine($"- script: {plan.SyncScriptToolkitPath}");
		foreach (var (key, value) in plan.EnvVars)
			Console.WriteLine($"  {key}={value}");
	}

	public static void PrintProdTeardown(RemoteTeardownPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- project: {plan.ProjectName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- ssh key: {plan.SshKeyName}");
		Console.WriteLine();
		Console.WriteLine("Step: teardown");
		if (plan.CaddySiteFileName is not null)
			Console.WriteLine($"- replace caddy site '{plan.CaddySiteFileName}' with removed page and reload");
		else
			Console.WriteLine("- no prod caddy file found; caddy step skipped");
		Console.WriteLine($"- docker compose down -v for project containers");
		Console.WriteLine($"- delete {plan.RemoteProjectPath}");
	}

	public static void PrintProdUnprovision(UnprovisionPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- openrc: {plan.OpenRcContainerPath}");
		Console.WriteLine($"- instance vars: {plan.InstanceVarsContainerPath}");
		Console.WriteLine($"- default instance name: {plan.DefaultInstanceName}");
		Console.WriteLine($"- default key name: {plan.DefaultKeyName}");
		Console.WriteLine();
		Console.WriteLine("Step: unprovision");
		Console.WriteLine($"- delete OpenStack instance '{plan.DefaultInstanceName}' (or OS_INSTANCE_NAME from vars)");
		Console.WriteLine($"- delete OpenStack keypair '{plan.DefaultKeyName}' (or OS_KEY_NAME from vars)");
		Console.WriteLine($"- delete SSH key files: {plan.SshKeyToolkitPath}");
		Console.WriteLine($"- delete instance vars file: {plan.InstanceVarsFilePath}");
	}

	public static void PrintSystemStatusPlan(SystemStatusPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- provider: {plan.ProviderName}");
		Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
		Console.WriteLine($"- ssh key: {plan.SshKeyName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine();
		Console.WriteLine("Step: read-system-status");
		Console.WriteLine($"- send '{plan.StatusScriptHostPath}' to the remote over ssh and read its output");
		Console.WriteLine($"- docker command: {plan.DockerCommandPrefix}");
		Console.WriteLine($"- detail: {(plan.Verbose ? $"verbose (containers, projects under {plan.RemoteProjectsRoot}, uptime)" : "summary only")}");
		Console.WriteLine("- nothing is written to the remote host");
	}

	public static void PrintSystemStatusReport(SystemStatus status, SystemStatusPlan plan)
	{
		var host = status.Hostname is null ? plan.RemoteHost : $"{status.Hostname}";

		Console.WriteLine($"System: {plan.ProviderName} ({host})");
		Console.WriteLine($"- Architecture: {FormatArchitecture(status)}");
		Console.WriteLine($"- CPU: {FormatCpu(status)}");
		Console.WriteLine($"- RAM: {FormatMemory(status)}");
		Console.WriteLine($"- Disk: {FormatDisk(status)}");
		Console.WriteLine($"- Reboot required: {FormatRebootRequired(status)}");

		if (!plan.Verbose)
			return;

		Console.WriteLine($"- Uptime: {FormatUptime(status.UptimeSeconds)}");

		Console.WriteLine();
		Console.WriteLine("Containers:");
		if (status.Containers.Count == 0)
		{
			Console.WriteLine("- none reported");
		}
		else
		{
			var width = status.Containers.Max(c => c.Name.Length);
			foreach (var container in status.Containers)
			{
				var cpu = container.CpuPercent is null ? "  --  " : $"{container.CpuPercent.Value,5:0.0}%";
				Console.WriteLine($"- {container.Name.PadRight(width)}   CPU {cpu}   RAM {FormatBytes(container.MemoryBytes)}");
			}
		}

		Console.WriteLine();
		Console.WriteLine("Project disk usage:");
		if (status.Projects.Count == 0)
		{
			Console.WriteLine($"- none found under {plan.RemoteProjectsRoot}");
		}
		else
		{
			var width = status.Projects.Max(p => p.Name.Length);
			foreach (var project in status.Projects.OrderByDescending(p => p.Bytes))
				Console.WriteLine($"- {project.Name.PadRight(width)}   {FormatBytes(project.Bytes)}");
		}

		if (status.DockerDisk.Count > 0)
		{
			var total = SumOrNull(status.DockerDisk.Select(d => d.SizeBytes));
			var reclaimable = SumOrNull(status.DockerDisk.Select(d => d.ReclaimableBytes));
			Console.WriteLine($"- Docker images/volumes/build cache: {FormatBytes(total)} ({FormatBytes(reclaimable)} reclaimable)");
		}
	}

	/// <summary>
	/// Shows the kernel architecture, plus the Docker platform when verbose collected it. The two
	/// are printed together rather than separately because the pair is what matters when an image
	/// will not run: the docker platform is the value a build must target.
	/// </summary>
	private static string FormatArchitecture(SystemStatus status)
	{
		if (status.KernelArchitecture is null && status.DockerPlatform is null)
			return "unavailable";

		if (status.DockerPlatform is null)
			return status.KernelArchitecture!;

		if (status.KernelArchitecture is null)
			return $"docker {status.DockerPlatform}";

		return $"{status.KernelArchitecture} (docker {status.DockerPlatform})";
	}

	private static string FormatCpu(SystemStatus status)
	{
		if (status.Load1 is null)
			return "unavailable";

		var cores = status.CpuCores is null ? "unknown cores" : $"{status.CpuCores} core{(status.CpuCores == 1 ? "" : "s")}";
		var load = $"load {status.Load1:0.00} / {status.Load5:0.00} / {status.Load15:0.00}";
		var percent = status.CpuLoadPercent is null ? "" : $" ({status.CpuLoadPercent.Value:0}% of capacity)";

		return $"{cores}, {load}{percent}";
	}

	private static string FormatMemory(SystemStatus status)
	{
		if (status.MemoryUsedBytes is null || status.MemoryTotalBytes is null)
			return "unavailable";

		var used = $"{FormatBytes(status.MemoryUsedBytes)} used of {FormatBytes(status.MemoryTotalBytes)} ({Percent(status.MemoryUsedBytes, status.MemoryTotalBytes)})";

		// A host with no swap configured reports a zero total; saying "0 GB of 0 GB" reads as a
		// fault rather than a normal configuration, so the clause is dropped entirely.
		if (status.SwapTotalBytes is null or 0)
			return used;

		return $"{used}, swap {FormatBytes(status.SwapUsedBytes)} of {FormatBytes(status.SwapTotalBytes)}";
	}

	private static string FormatDisk(SystemStatus status)
	{
		if (status.DiskUsedBytes is null || status.DiskTotalBytes is null)
			return "unavailable";

		return $"{FormatBytes(status.DiskUsedBytes)} used of {FormatBytes(status.DiskTotalBytes)} " +
			   $"({Percent(status.DiskUsedBytes, status.DiskTotalBytes)}) — {FormatBytes(status.DiskFreeBytes)} free";
	}

	/// <summary>
	/// There is no Dapsman command to run here. `prod provision` configures unattended-upgrades
	/// with Automatic-Reboot at 04:00, so a pending reboot normally clears itself overnight, and
	/// the only manual route is the hosting provider's own control panel. If a `prod reboot`
	/// workflow is ever added, this is the only string that needs to change.
	/// </summary>
	private static string FormatRebootRequired(SystemStatus status) => status.RebootRequired switch
	{
		null => "unavailable",
		false => "no",
		true => "yes — the host reboots itself automatically at 04:00 to finish installing" +
				Environment.NewLine +
				"  updates, so this normally clears overnight. If it is still showing after" +
				Environment.NewLine +
				"  that, reboot the VM from your hosting provider's control panel.",
	};

	private static string FormatUptime(double? seconds)
	{
		if (seconds is null)
			return "unavailable";

		var span = TimeSpan.FromSeconds(seconds.Value);
		if (span.TotalDays >= 1)
			return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")}, {span.Hours} hour{(span.Hours == 1 ? "" : "s")}";
		if (span.TotalHours >= 1)
			return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")}, {span.Minutes} minute{(span.Minutes == 1 ? "" : "s")}";

		return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes == 1 ? "" : "s")}";
	}

	private static long? SumOrNull(IEnumerable<long?> values)
	{
		var known = values.Where(v => v is not null).Select(v => v!.Value).ToList();
		return known.Count == 0 ? null : known.Sum();
	}

	private static string Percent(long? part, long? whole) =>
		whole is null or 0 || part is null ? "unavailable" : $"{part.Value * 100.0 / whole.Value:0}%";

	/// <summary>
	/// Decimal units, not binary: a user comparing this against their hosting plan is reading
	/// "40 GB" off a pricing page, which is 40 billion bytes, not 40 GiB.
	/// </summary>
	private static string FormatBytes(long? bytes) => bytes switch
	{
		null => "unavailable",
		>= 1_000_000_000L => $"{bytes.Value / 1_000_000_000.0:0.0} GB",
		>= 1_000_000L => $"{bytes.Value / 1_000_000.0:0} MB",
		>= 1_000L => $"{bytes.Value / 1_000.0:0} kB",
		_ => $"{bytes.Value} B",
	};
}

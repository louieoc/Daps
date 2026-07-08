using Dapsman.Application;
using Dapsman.Domain;
using Dapsman.Infrastructure;

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
		if (plan.InitScriptPath is not null)
		{
			var scriptArgs = plan.Overlay ? $"{plan.ProjectName} --overlay" : plan.ProjectName;
			Console.WriteLine($"- init script: bash \"{plan.InitScriptPath}\" {scriptArgs}");
		}
		else
			Console.WriteLine("- no init-template.toolkit.sh found in template");
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
		Console.WriteLine("Step: compose-daps");
		var dapsCommand = WorkstationDockerComposeExecutor.BuildDockerComposeCommand(plan.DapsComposeFiles, options.BuildImages);
		Console.WriteLine($"- {dapsCommand}");

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
				var projectCommand = WorkstationDockerComposeExecutor.BuildDockerComposeCommand(projectPlan.ComposeFiles, options.BuildImages);
				Console.WriteLine($"- {projectPlan.ProjectName}: {projectCommand}");
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
				Console.WriteLine($"- {projectPlan.ProjectName}: run {prereqName} on remote");
			Console.WriteLine($"- {projectPlan.ProjectName}: docker load each file in /srv/projects/{projectPlan.ProjectName}/_docker/image-exports");
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

	public static void PrintProdBackup(BackupPlan plan)
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
		Console.WriteLine($"- sql: _backups/from_prod/{plan.ProjectName}_{rp.Env}_{rp.Timestamp}.sql");
		Console.WriteLine($"- wp-content: _backups/from_prod/{plan.ProjectName}_{rp.Env}_wp-content_{rp.Timestamp}.tar.gz");
		Console.WriteLine($"- prod url: {(string.IsNullOrEmpty(plan.ProdUrl) ? "(derived from caddy file)" : plan.ProdUrl)}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine();
		Console.WriteLine("Step: restore");
		Console.WriteLine("- import SQL into local database");
		Console.WriteLine("- extract wp-content archive");
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

	public static void PrintProdTeardown(TeardownPlan plan)
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
}

using Dapsman.Application;
using Dapsman.Domain;
using Dapsman.Infrastructure;

internal sealed class DapsmanRunner
{
	private readonly IConfigLoader _configLoader;
	private readonly IContainerManager _workstationContainerManager;
	private readonly IContainerManager _remoteContainerManager;
	private readonly IHostingProviderResolver _hostingResolver;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly ICaddyResolver _workstationCaddyResolver;
	private readonly ICaddyResolver _remoteCaddyResolver;
	private readonly IDockerResolver _dockerResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly ToolkitDefinition _toolkitDefinition;
	private readonly IBashRunner _toolkitBashRunner;
	private readonly IBashRunner _workstationBashRunner;
	private readonly IPrerequisiteChecker _localPrerequisiteChecker;

	private readonly CliArguments _parsed;
	private readonly DapsConfig _config;

	public DapsmanRunner(CliArguments parsed)
	{
		_parsed = parsed;
		_configLoader = new DapsYamlConfigLoader();
		_config = _configLoader.Load(parsed.ConfigPath);

		_hostingResolver = new HostingProviderResolver(_config);
		_workstationContainerManager = new WorkstationContainerManager();
		_remoteContainerManager = new HardcodedRemoteContainerManager(); // this will need to be replaced with something real
		_toolkitResolver = new ToolkitResolver(_config, _workstationContainerManager);
		_toolkitDefinition = _toolkitResolver.Resolve();
		_toolkitBashRunner = new ContainerBashRunner(_toolkitDefinition.ContainerName);
		_workstationBashRunner = new WorkstationBashRunner();
		_workstationCaddyResolver = new CaddyResolver(_config, _workstationContainerManager);
		_remoteCaddyResolver = new CaddyResolver(_config, _remoteContainerManager);
		_dockerResolver = new DockerResolver(_config);
		_projectResolver = new ProjectResolver(_config);
		_localPrerequisiteChecker = new LocalPrerequisiteChecker();
	}

	public async Task<int> RunAsync()
	{
		if (_parsed.IsInit)
		{
			return await RunInit();
		}

		if (_parsed.IsProdSyncFromLocal)
		{
			return await RunSyncFromLocal();
		}

		if (_parsed.IsLocalSyncFromProd)
		{
			return await RunSyncFromRemote();
		}

		if (_parsed.IsLocalRestore && _parsed.ListRestorePoints)
		{
			return await RunLocalListRestorePoints();
		}

		if (_parsed.IsLocalRestore)
		{
			return await RunLocalRestore();
		}

		if (_parsed.IsLocalTeardown)
		{
			return await RunLocalTeardown();
		}

		if (_parsed.IsProdBackup)
		{
			return await RunProdBackup();
		}

		if (_parsed.IsProdOffline)
		{
			return await RunProdSetStatus(offline: true);
		}

		if (_parsed.IsProdOnline)
		{
			return await RunProdSetStatus(offline: false);
		}

		if (_parsed.IsLocalCaddyRestart)
		{
			return await RunCaddyRestart(remote: false);
		}

		if (_parsed.IsProdCaddyRestart)
		{
			return await RunCaddyRestart(remote: true);
		}

		if (_parsed.IsLocalBuild)
		{
			return await RunLocalBuild();
		}

		if (_parsed.IsProdDeploy)
		{
			return await RunRemoteDeploy();
		}

		if (_parsed.IsProdProvision)
		{
			return await RunRemoteProvision();
		}

		if (_parsed.IsProdTeardown)
		{
			return await RunProdTeardown();
		}

		if (_parsed.IsProdUnprovision)
		{
			return await RunProdUnprovision();
		}

		PrintUsage();
		return 1;
	}

	private Task<int> RunLocalBuild()
	{
		var service = new LocalBuildService(
			_localPrerequisiteChecker,
			_configLoader,
			new ConventionComposePlanBuilder(_config, _dockerResolver, _workstationCaddyResolver, _projectResolver),
			new DevCaddySiteSync(),
			new WorkstationDockerComposeExecutor(),
			_workstationBashRunner);

		var options = new LocalBuildOptions
		{
			BuildImages = _parsed.BuildImages,
			DryRun = _parsed.DryRun,
			ProjectFilters = _parsed.ProjectFilters,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		var caddyRestartPlanBuilder = new ConventionLocalCaddyRestartPlanBuilder(_config, _workstationCaddyResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();
		PrintLocalBuildPlan(plan, options, caddyRestartPlan);

		if (!options.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: sync-caddy");
			service.SyncCaddySites(plan);
			Console.WriteLine($"- synced caddy site files to '{plan.CaddySync.RuntimeSitesPath}'");

			Console.WriteLine();
			Console.WriteLine("Step: prerequisites");
			foreach (var projectPlan in plan.ProjectComposePlans)
			{
				if (projectPlan.PrerequisiteScripts?.Count == 0) continue;
				Console.WriteLine($"- {projectPlan.ProjectName}: running prerequisite scripts");
				service.ExecutePrerequisites(projectPlan);
				Console.WriteLine($"- {projectPlan.ProjectName}: done");
			}

			Console.WriteLine();
			Console.WriteLine("Step: compose-daps");
			var dapsCommand = WorkstationDockerComposeExecutor.BuildDockerComposeCommand(plan.DapsComposeFiles, options.BuildImages);
			Console.WriteLine($"- executing: {dapsCommand}");
			service.ExecuteDapsCompose(plan, options);
			Console.WriteLine("- done");

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
					Console.WriteLine($"- {projectPlan.ProjectName}: executing: {projectCommand}");
					service.ExecuteProjectCompose(projectPlan, options);
					Console.WriteLine($"- {projectPlan.ProjectName}: done");
				}
			}

			Console.WriteLine();
			Console.WriteLine("Step: caddy-reload");
			new CaddyRestartService(caddyRestartPlanBuilder, _workstationBashRunner).Execute(caddyRestartPlan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunRemoteProvision()
	{
		var openStackPlanBuilder = new OpenStackRemoteProvisionPlanBuilder(_toolkitResolver, _hostingResolver);

		var service = new RemoteProvisionService(
			_localPrerequisiteChecker,
			_configLoader,
			openStackPlanBuilder,
			_toolkitBashRunner);

		var options = new RemoteProvisionOptions
		{
			DryRun = _parsed.DryRun,
			ProviderName = _parsed.ProviderName,
			SetVarsScriptPath = _parsed.SetVarsScriptPath,
			CreateScriptPath = _parsed.CreateScriptPath,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		PrintRemoteProvisionPlan(plan);

		if (!options.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: run-toolkit-command");
			Console.WriteLine($"- container: {plan.ToolkitContainerName}");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunRemoteDeploy()
	{
		var planBuilder = new ConventionRemoteDeployPlanBuilder(
			_hostingResolver,
			_dockerResolver,
			_toolkitResolver,
			_workstationCaddyResolver,
			_projectResolver);

		var executor = new ToolkitRemoteDeployExecutor(_toolkitBashRunner);

		var service = new RemoteDeployService(
			_localPrerequisiteChecker,
			_configLoader,
			planBuilder,
			executor);

		var options = new RemoteDeployOptions
		{
			DryRun = _parsed.DryRun,
			BuildImages = _parsed.BuildImages,
			ProviderName = _parsed.ProviderName,
			SetVarsScriptPath = _parsed.SetVarsScriptPath,
			ProjectFilters = _parsed.ProjectFilters,
		};

		var caddyRestartPlanBuilder = new ConventionRemoteCaddyRestartPlanBuilder(
			_config, options.ProviderName, _toolkitResolver, _remoteCaddyResolver, _hostingResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		PrintRemoteDeployPlan(plan, options, caddyRestartPlan);

		if (!options.DryRun)
		{
			var commandsToRun = SelectBuildCommands(plan, options);
			if (commandsToRun.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine("Step: build-images");
				service.ExecuteBuildImageScripts(commandsToRun, plan.DapsRootPath);
				Console.WriteLine("- done");
			}

			Console.WriteLine();
			Console.WriteLine("Step: deploy-remote");
			service.Execute(plan);
			Console.WriteLine("- done");

			Console.WriteLine();
			Console.WriteLine("Step: caddy-reload");
			new CaddyRestartService(caddyRestartPlanBuilder, _toolkitBashRunner).Execute(caddyRestartPlan);
			Console.WriteLine("- done");

			foreach (var projectPlan in plan.ProjectPlans.Where(p => p.PostDeployScriptPath is not null))
			{
				Console.WriteLine();
				Console.WriteLine($"Step: post-deploy-hook ({projectPlan.ProjectName})");
				_toolkitBashRunner.RunScript(projectPlan.PostDeployScriptPath!, projectPlan.PostDeployScriptPath!);
				Console.WriteLine("- done");
			}
		}

		return Task.FromResult(0);
	}

	private Task<int> RunInit()
	{
		if (string.IsNullOrEmpty(_parsed.TemplateName))
			throw new ArgumentException("--template <name> is required for init.");
		var projectName = _parsed.ProjectName
			?? (_parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null);
		if (string.IsNullOrEmpty(projectName))
			throw new ArgumentException("--name <project-name> (or --project <name>) is required for init.");

		var service = new InitService(
			new ConventionInitPlanBuilder(_config),
			_workstationBashRunner,
			new DapsYamlEditor());

		var options = new InitOptions
		{
			TemplateName = _parsed.TemplateName,
			ProjectName = projectName,
			DestinationPath = _parsed.DestinationPath,
			DryRun = _parsed.DryRun,
		};

		var plan = service.CreatePlan(options);
		PrintInitPlan(plan, options);

		if (!options.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: copy-template");
			service.Execute(plan);
			Console.WriteLine($"- copied '{plan.TemplatePath}' -> '{plan.DestinationPath}'");
			if (plan.InitScriptPath is not null)
				Console.WriteLine($"- ran init-template.toolkit.sh");
			Console.WriteLine($"- registered '{plan.ProjectName}' in {plan.DapsYamlPath}");
			Console.WriteLine();
			Console.WriteLine($"Done. Project '{plan.ProjectName}' created at: {plan.DestinationPath}");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunCaddyRestart(bool remote)
	{
		ICaddyRestartPlanBuilder planBuilder;
		IBashRunner bashRunner;

		if (remote)
		{
			planBuilder = new ConventionRemoteCaddyRestartPlanBuilder(_config, _parsed.ProviderName, _toolkitResolver, _remoteCaddyResolver, _hostingResolver);
			bashRunner = _toolkitBashRunner;
		}
		else
		{
			planBuilder = new ConventionLocalCaddyRestartPlanBuilder(_config, _workstationCaddyResolver);
			bashRunner = _workstationBashRunner;
		}

		var plan = planBuilder.BuildPlan();
		var service = new CaddyRestartService(planBuilder, bashRunner);

		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine();
		Console.WriteLine("Step: caddy-reload");
		Console.WriteLine($"- {plan.ReloadCommand}");

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: caddy-reload");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunProdSetStatus(bool offline)
	{
		var action = offline ? "offline" : "online";
		var projects = _projectResolver.Resolve(_parsed.ProjectFilters);

		var planBuilder = new ConventionOfflineStatusPlanBuilder(
			offline,
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_workstationCaddyResolver,
			_hostingResolver);

		var executor = new ToolkitOfflineStatusExecutor(_toolkitBashRunner);
		var service = new OfflineStatusService(_configLoader, planBuilder, executor);

		foreach (var project in projects)
		{
			var options = new OfflineStatusOptions
			{
				DryRun = _parsed.DryRun,
				ProjectName = project.Definition.Name,
				ProviderName = _parsed.ProviderName,
			};

			var result = service.CreatePlan(_parsed.ConfigPath, options);

			Console.WriteLine($"Project: {project.Definition.Name}");
			if (!result.IsSupported)
			{
				foreach (var reason in result.Warnings)
					Console.WriteLine($"- skipped: {reason}");
				Console.WriteLine();
				continue;
			}

			var plan = result.Plan!;
			Console.WriteLine($"- action: {action}");
			Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
			Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
			Console.WriteLine($"- caddy site file: {plan.CaddySiteFileName}");

			if (!_parsed.DryRun)
			{
				Console.WriteLine();
				Console.WriteLine($"Step: set-{action}");
				service.Execute(plan);
				Console.WriteLine("- done");
			}

			Console.WriteLine();
		}

		return Task.FromResult(0);
	}

	private Task<int> RunProdBackup()
	{
		var projects = _projectResolver.Resolve(_parsed.ProjectFilters);

		var planBuilder = new ConventionBackupPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new BackupService(_configLoader, planBuilder, _toolkitBashRunner);

		foreach (var project in projects)
		{
			var options = new BackupOptions
			{
				DryRun = _parsed.DryRun,
				ProjectName = project.Definition.Name,
				ProviderName = _parsed.ProviderName,
			};

			var result = service.CreatePlan(_parsed.ConfigPath, options);

			Console.WriteLine($"Project: {project.Definition.Name}");
			if (!result.IsSupported)
			{
				foreach (var reason in result.Warnings)
					Console.WriteLine($"- skipped: {reason}");
				Console.WriteLine();
				continue;
			}

			var plan = result.Plan!;
			Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
			Console.WriteLine($"- remote: {plan.RemoteUser}@{plan.RemoteHost}");
			Console.WriteLine($"- ssh key: {plan.SshKeyToolkitPath}");
			Console.WriteLine("Step: backup");
			Console.WriteLine($"- script: {plan.BackupScriptToolkitPath}");
			Console.WriteLine($"- destination: {plan.BackupDestinationToolkitPath}");
			Console.WriteLine($"- args: {plan.ScriptArguments}");
			foreach (var (key, value) in plan.EnvVars)
				Console.WriteLine($"  {key}={value}");

			if (!_parsed.DryRun)
			{
				Console.WriteLine();
				Console.WriteLine("Step: backup");
				service.Execute(plan);
				Console.WriteLine("- done");
			}

			Console.WriteLine();
		}

		return Task.FromResult(0);
	}

	private Task<int> RunSyncFromRemote()
	{
		var options = new SyncFromRemoteOptions
		{
			DryRun = _parsed.DryRun,
			ProjectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null,
			ProviderName = _parsed.ProviderName,
		};

		var planBuilder = new ConventionLocalSyncFromRemotePlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new SyncFromRemoteService(_configLoader, planBuilder, _toolkitBashRunner);
		var result = service.CreatePlan(_parsed.ConfigPath, options);

		if (!result.IsSupported)
		{
			foreach (var reason in result.Warnings)
				Console.WriteLine($"skipped: {reason}");
			return Task.FromResult(0);
		}

		var plan = result.Plan!;
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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: sync-from-prod");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunLocalListRestorePoints()
	{
		var projectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null;
		var project = _projectResolver.Resolve(projectName);

		var discoverer = new ConventionRestorePointsDiscoverer(_toolkitBashRunner);
		var points = discoverer.Discover(project);

		if (points.Count == 0)
		{
			Console.WriteLine($"No backups found in {Path.Combine(project.WorkstationBackupsPath, "from_prod")}.");
			return Task.FromResult(0);
		}

		foreach (var p in points)
		{
			var incomplete = p.IsComplete ? "" : "  (incomplete)";
			Console.WriteLine($"{p.Index}. {p.DisplayLabel}{incomplete}");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunLocalRestore()
	{
		var projectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null;
		var project = _projectResolver.Resolve(projectName);

		var discoverer = new ConventionRestorePointsDiscoverer(_toolkitBashRunner);
		var planBuilder = new ConventionLocalRestorePlanBuilder(_toolkitResolver, discoverer);
		var service = new RestoreService(planBuilder, new ToolkitLocalRestoreExecutor(_toolkitBashRunner));

		var options = new RestoreOptions
		{
			SelectedRestorePoint = _parsed.SelectedRestorePoint,
			ProdUrl = _parsed.ProdUrl,
		};

		var result = service.CreatePlan(project, options);

		if (!result.IsSupported)
		{
			foreach (var reason in result.Warnings)
				Console.WriteLine($"skipped: {reason}");

			return Task.FromResult(0);
		}

		var plan = result.Plan!;
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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.Write("WARNING: This will overwrite the local database and wp-content. Type 'yes' to confirm: ");
			var confirm = Console.ReadLine()?.Trim();
			if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Restore cancelled.");
				return Task.FromResult(1);
			}

			Console.WriteLine();
			Console.WriteLine("Step: restore");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunLocalTeardown()
	{
		var projectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null;
		if (string.IsNullOrEmpty(projectName))
			throw new ArgumentException("--project <name> is required for local teardown.");

		var planBuilder = new ConventionLocalTeardownPlanBuilder(
			_projectResolver,
			_dockerResolver,
			_workstationCaddyResolver);

		var service = new LocalTeardownService(
			_configLoader,
			planBuilder,
			new WorkstationLocalTeardownExecutor(_workstationBashRunner),
			new DapsYamlEditor());

		var plan = service.CreatePlan(_parsed.ConfigPath, new LocalTeardownOptions { ProjectName = projectName });

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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.Write($"This will delete all containers, volumes, caddy site files, and the project folder for '{plan.ProjectName}', including backups, and remove it from daps.yaml. Type 'yes' to confirm: ");
			var confirm = Console.ReadLine()?.Trim();
			if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Teardown cancelled.");
				return Task.FromResult(1);
			}

			Console.WriteLine();
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunSyncFromLocal()
	{
		var options = new SyncFromLocalOptions
		{
			DryRun = _parsed.DryRun,
			ProjectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null,
			ProviderName = _parsed.ProviderName,
		};

		var planBuilder = new ConventionRemoteSyncFromLocalPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new SyncFromLocalService(_configLoader, planBuilder, _toolkitBashRunner);
		var result = service.CreatePlan(_parsed.ConfigPath, options);

		if (!result.IsSupported)
		{
			foreach (var reason in result.Warnings)
				Console.WriteLine($"skipped: {reason}");
			return Task.FromResult(0);
		}

		var plan = result.Plan!;
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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: sync-from-local");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunProdTeardown()
	{
		var projectName = _parsed.ProjectFilters.Count == 1 ? _parsed.ProjectFilters[0] : null;
		if (string.IsNullOrEmpty(projectName))
			throw new ArgumentException("--project <name> is required for prod teardown.");

		var planBuilder = new ConventionTeardownPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_workstationCaddyResolver,
			_hostingResolver);

		var service = new TeardownService(
			_configLoader,
			planBuilder,
			new ToolkitTeardownExecutor(_toolkitBashRunner));

		var options = new TeardownOptions
		{
			DryRun = _parsed.DryRun,
			ProjectName = projectName,
			ProviderName = _parsed.ProviderName,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.Write($"Are you sure you want to teardown '{plan.ProjectName}'? This will delete all containers, volumes, and remote project files. Type 'yes' to confirm: ");
			var confirm = Console.ReadLine()?.Trim();
			if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Teardown cancelled.");
				return Task.FromResult(1);
			}

			Console.WriteLine();
			Console.WriteLine("Step: teardown");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private Task<int> RunProdUnprovision()
	{
		var planBuilder = new OpenStackUnprovisionPlanBuilder(_toolkitResolver, _hostingResolver);

		var service = new UnprovisionService(
			_configLoader,
			planBuilder,
			new ToolkitUnprovisionExecutor(_toolkitBashRunner));

		var options = new UnprovisionOptions
		{
			DryRun = _parsed.DryRun,
			ProviderName = _parsed.ProviderName,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

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

		if (!_parsed.DryRun)
		{
			Console.WriteLine();
			Console.Write("Are you sure you want to delete the remote OpenStack environment? This cannot be undone. Type 'yes' to confirm: ");
			var confirm1 = Console.ReadLine()?.Trim();
			if (!string.Equals(confirm1, "yes", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Unprovision cancelled.");
				return Task.FromResult(1);
			}

			Console.Write("Are you really, really sure? Type 'yes' again to proceed: ");
			var confirm2 = Console.ReadLine()?.Trim();
			if (!string.Equals(confirm2, "yes", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Unprovision cancelled.");
				return Task.FromResult(1);
			}

			Console.WriteLine();
			Console.WriteLine("Step: unprovision");
			service.Execute(plan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private static void PrintUsage()
	{
		Console.WriteLine("Usage:");
		Console.WriteLine("  dapsman init --template <name> --name <project-name>|--project <name> [--path <destination>] [--dry-run] [--config <path>]");
		Console.WriteLine("  dapsman local build [--build] [--dry-run] [--project <name>...] [--config <path>]");
		Console.WriteLine("  dapsman local caddy restart [--dry-run] [--config <path>]");
		Console.WriteLine("  dapsman prod caddy restart [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod provision [--dry-run] [--provider <name>] [--set-vars-script <path>] [--create-script <path>] [--config <path>]");
		Console.WriteLine("  dapsman prod deploy [--dry-run] [--project <name>...] [--provider <name>] [--build] [--set-vars-script <path>] [--config <path>]");
		Console.WriteLine("  dapsman prod sync-from-local --project <name> [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman local sync-from-prod --project <name> [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman local restore --project <name> [--dry-run] [--list-restore-points] [--restore-point <n|timestamp>] [--prod-url <url>] [--config <path>]");
		Console.WriteLine("  dapsman prod backup [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod offline [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod online [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod teardown --project <name> [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod unprovision [--dry-run] [--provider <name>] [--config <path>]");
	}

	private static void PrintInitPlan(InitPlan plan, InitOptions options)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");

		Console.WriteLine();
		Console.WriteLine("Step: copy-template");
		Console.WriteLine($"- template: {plan.TemplatePath}");
		Console.WriteLine($"- destination: {plan.DestinationPath}");
		if (plan.InitScriptPath is not null)
			Console.WriteLine($"- init script: bash \"{plan.InitScriptPath}\" {plan.ProjectName}");
		else
			Console.WriteLine("- no init-template.toolkit.sh found in template");
		Console.WriteLine($"- register in daps.yaml: {plan.ProjectName}: path: {plan.DapsYamlProjectRelativePath}");
	}

	private static void PrintLocalBuildPlan(LocalBuildPlan plan, LocalBuildOptions options, CaddyRestartPlan caddyRestartPlan)
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
			{
				Console.WriteLine($"- {copy.ProjectName}: \"{copy.SourcePath}\" -> \"{copy.DestinationPath}\"");
			}
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
			{
				foreach (var script in projectPlan.PrerequisiteScripts)
				{
					Console.WriteLine($"- {projectPlan.ProjectName}: bash \"{script}\"");
				}
			}
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
		{
			foreach (var warning in plan.Warnings)
			{
				Console.WriteLine($"- warning: {warning}");
			}
		}
	}

	private static void PrintRemoteProvisionPlan(RemoteProvisionPlan plan)
	{
		Console.WriteLine("Step: validate");
		Console.WriteLine("- prerequisites ok");
		Console.WriteLine($"- provider: {plan.ProviderName}");
		Console.WriteLine($"- default key name: {plan.DefaultKeyName}");
		Console.WriteLine($"- toolkit container: {plan.ToolkitContainerName}");
		Console.WriteLine($"- openrc script: {plan.OpenRcScriptHostPath}");
		Console.WriteLine($"- set-vars script: {plan.SetVarsScriptHostPath}");
		Console.WriteLine($"- create script: {plan.CreateScriptHostPath}");

		Console.WriteLine();
		Console.WriteLine("Step: setup-prod-environment");
		Console.WriteLine($"- docker exec -it {plan.ToolkitContainerName} bash -lc <script-chain>");
		Console.WriteLine($"- script-chain: {plan.ToolkitCommand}");
	}

	private static IReadOnlyList<BuildImageCommandPlan> SelectBuildCommands(RemoteDeployPlan plan, RemoteDeployOptions options)
	{
		return plan.BuildImageCommands
			.Where(c => options.BuildImages || !c.HasExistingExports)
			.ToList();
	}

	private static void PrintRemoteDeployPlan(RemoteDeployPlan plan, RemoteDeployOptions options, CaddyRestartPlan caddyRestartPlan)
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
			{
				Console.WriteLine($"- {copy.ProjectName}: \"{copy.SourcePath}\" -> /srv/daps/caddy_sites/{copy.DestinationFileName}");
			}
		}

		Console.WriteLine();
		Console.WriteLine("Step: upload-docker");
		foreach (var composeFile in plan.DapsComposeFilesToUpload)
		{
			Console.WriteLine($"- daps: \"{composeFile}\" -> /srv/daps/docker/{Path.GetFileName(composeFile)}");
		}

		foreach (var projectPlan in plan.ProjectPlans)
		{
			foreach (var composeFile in projectPlan.ComposeFilesToUpload)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: \"{composeFile}\" -> /srv/projects/{projectPlan.ProjectName}/_docker/{Path.GetFileName(composeFile)}");
			}

			if (!string.IsNullOrWhiteSpace(projectPlan.ImageExportsSourcePath) && projectPlan.ImageExportFilesToUpload.Count == 0)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: no image exports found under \"{projectPlan.ImageExportsSourcePath}\"");
			}
			else
			{
				foreach (var exportFile in projectPlan.ImageExportFilesToUpload)
				{
					Console.WriteLine($"- {projectPlan.ProjectName}: \"{exportFile}\" -> /srv/projects/{projectPlan.ProjectName}/_docker/image-exports/{Path.GetFileName(exportFile)}");
				}
			}

			if (projectPlan.UploadFiles.Count == 0)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: no additional upload files");
			}
			else
			{
				foreach (var upload in projectPlan.UploadFiles)
				{
					Console.WriteLine($"- {projectPlan.ProjectName}: \"{upload.SourcePath}\" -> {upload.RemoteDestinationPath}");
				}
			}

			foreach (var script in projectPlan.RemoteScriptFilesToUpload)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: \"{script}\" -> /srv/projects/{projectPlan.ProjectName}/_scripts/{Path.GetFileName(script)}");
			}
		}

		Console.WriteLine();
		Console.WriteLine("Step: run-docker");
		Console.WriteLine("- ensure daps_net exists");
		Console.WriteLine("- compose up daps (prod/shared files)");
		foreach (var projectPlan in plan.ProjectPlans)
		{
			foreach (var prereqName in projectPlan.RemotePrerequisiteScriptNames)
			{
				Console.WriteLine($"- {projectPlan.ProjectName}: run {prereqName} on remote");
			}
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
		{
			foreach (var warning in plan.Warnings)
			{
				Console.WriteLine($"- warning: {warning}");
			}
		}
	}
}
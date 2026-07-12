using Dapsman.Application;
using Dapsman.Domain;
using Dapsman.Infrastructure;

internal sealed class DapsmanRunner
{
	private readonly IDapsConfigLoader _dapsConfigLoader;
	private readonly IContainerManager _workstationContainerManager;
	private readonly IContainerManager _remoteContainerManager;
	private readonly IHostingProviderResolver _hostingResolver;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly ICaddyResolver _workstationCaddyResolver;
	private readonly ICaddyResolver _remoteCaddyResolver;
	private readonly IDockerResolver _dockerResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly IBashRunner _workstationBashRunner;

	private IBashRunner _toolkitBashRunner = null!;

	private readonly CliArguments _parsed;
	private readonly DapsConfig _config;

	public DapsmanRunner(CliArguments parsed)
	{
		_parsed = parsed;
		_dapsConfigLoader = new DapsYamlConfigLoader();
		_config = _dapsConfigLoader.Load(parsed.ConfigPath);

		_hostingResolver = new HostingProviderResolver(_config);
		_workstationContainerManager = new WorkstationContainerManager();
		_remoteContainerManager = new HardcodedRemoteContainerManager(); // todo: this will need to be replaced with something real
		_toolkitResolver = new ToolkitResolver(_config, _workstationContainerManager);
		_workstationBashRunner = new WorkstationBashRunner();
		_workstationCaddyResolver = new CaddyResolver(_config, _workstationContainerManager);
		_remoteCaddyResolver = new CaddyResolver(_config, _remoteContainerManager);
		_dockerResolver = new DockerResolver(_config);
		_projectResolver = new ProjectResolver(_config);
	}

	public void ResolveDependencies()
	{
		var localPrerequisiteChecker = new LocalPrerequisiteChecker();
		localPrerequisiteChecker.EnsureLocalBuildPrerequisites();

		var toolkitDefinition = _toolkitResolver.Resolve().ContainerName;
		_toolkitBashRunner = new ContainerBashRunner(toolkitDefinition);
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
			_dapsConfigLoader,
			new LocalBuildPlanBuilder(_config, _dockerResolver, _workstationCaddyResolver, _projectResolver, new HostPortManager()),
			new LocalCaddySiteSync(),
			new WorkstationDockerComposeExecutor(),
			_workstationBashRunner);

		var options = new LocalBuildOptions
		{
			BuildImages = _parsed.BuildImages,
			DryRun = _parsed.DryRun,
			ProjectFilters = _parsed.ProjectFilters,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		var caddyRestartPlanBuilder = new LocalCaddyRestartPlanBuilder(_config, _workstationCaddyResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();
		DapsmanPlanPrinter.PrintLocalBuild(plan, options, caddyRestartPlan);

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
			// todo: make this a responsibility of the plan builder
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
		var genericVpsPlanBuilder = new GenericVpsRemoteProvisionPlanBuilder(_toolkitResolver, _hostingResolver);
		var planBuilder = new CompositeRemoteProvisionPlanBuilder(_hostingResolver, openStackPlanBuilder, genericVpsPlanBuilder);

		var service = new RemoteProvisionService(
			_dapsConfigLoader,
			planBuilder,
			_toolkitBashRunner);

		var options = new RemoteProvisionOptions
		{
			DryRun = _parsed.DryRun,
			ProviderName = _parsed.ProviderName,
			SetVarsScriptPath = _parsed.SetVarsScriptPath,
			CreateScriptPath = _parsed.CreateScriptPath,
			Upgrade = _parsed.Upgrade,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		DapsmanPlanPrinter.PrintRemoteProvision(plan);

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
		var planBuilder = new RemoteDeployPlanBuilder(
			_hostingResolver,
			_dockerResolver,
			_toolkitResolver,
			_workstationCaddyResolver,
			_projectResolver);

		var executor = new ToolkitRemoteDeployExecutor(_toolkitBashRunner);

		var service = new RemoteDeployService(
			_dapsConfigLoader,
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

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

		var caddyRestartPlanBuilder = new RemoteCaddyRestartPlanBuilder(
			_config, plan.ProviderName, _toolkitResolver, _remoteCaddyResolver, _hostingResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();
		DapsmanPlanPrinter.PrintRemoteDeploy(plan, options, caddyRestartPlan);

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
				_toolkitBashRunner.RunScript(
					projectPlan.PostDeployScriptPath!,
					plan.DapsRootPath,
					env: projectPlan.PostDeployScriptEnvVars);
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
			new InitPlanBuilder(_config, _projectResolver, _dockerResolver, new HostPortManager()),
			_workstationBashRunner,
			new DapsYamlEditor());

		var options = new InitOptions
		{
			TemplateName = _parsed.TemplateName,
			ProjectName = projectName,
			DestinationPath = _parsed.DestinationPath,
			ProdUrl = _parsed.ProdUrl,
			Overlay = _parsed.Overlay,
			DryRun = _parsed.DryRun,
		};

		var plan = service.CreatePlan(options);
		DapsmanPlanPrinter.PrintInit(plan);

		if (!options.DryRun)
		{
			Console.WriteLine();
			Console.WriteLine("Step: copy-template");
			service.Execute(plan);
			if (plan.Overlay)
				Console.WriteLine($"- overlaid '{plan.TemplatePath}' -> '{plan.DestinationPath}' (skipped existing files)");
			else
				Console.WriteLine($"- copied '{plan.TemplatePath}' -> '{plan.DestinationPath}'");
			if (plan.InitScriptPath is not null)
				Console.WriteLine($"- ran init-template.toolkit.sh");
			Console.WriteLine($"- registered '{plan.ProjectName}' in {plan.DapsYamlPath}");
			if (plan.ProdUrl is not null)
				Console.WriteLine($"- set prod url: {plan.ProdUrl}");
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
			planBuilder = new RemoteCaddyRestartPlanBuilder(_config, _parsed.ProviderName, _toolkitResolver, _remoteCaddyResolver, _hostingResolver);
			bashRunner = _toolkitBashRunner;
		}
		else
		{
			planBuilder = new LocalCaddyRestartPlanBuilder(_config, _workstationCaddyResolver);
			bashRunner = _workstationBashRunner;
		}

		var plan = planBuilder.BuildPlan();
		var service = new CaddyRestartService(planBuilder, bashRunner);

		DapsmanPlanPrinter.PrintCaddyRestart(plan);

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

		var planBuilder = new RemoteOfflineStatusPlanBuilder(
			offline,
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_workstationCaddyResolver,
			_hostingResolver);

		var executor = new ToolkitOfflineStatusExecutor(_toolkitBashRunner);
		var service = new OfflineStatusService(_dapsConfigLoader, planBuilder, executor);

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
			DapsmanPlanPrinter.PrintProdSetStatus(plan, action);

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

		var planBuilder = new RemoteBackupPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new RemoteBackupService(_dapsConfigLoader, planBuilder, _toolkitBashRunner);

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
			DapsmanPlanPrinter.PrintProdBackup(plan);

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

		var planBuilder = new LocalSyncFromRemotePlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new SyncFromRemoteService(_dapsConfigLoader, planBuilder, _toolkitBashRunner);
		var result = service.CreatePlan(_parsed.ConfigPath, options);

		if (!result.IsSupported)
		{
			foreach (var reason in result.Warnings)
				Console.WriteLine($"skipped: {reason}");
			return Task.FromResult(0);
		}

		var plan = result.Plan!;
		DapsmanPlanPrinter.PrintSyncFromRemote(plan);

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

		var discoverer = new RestorePointsDiscoverer(_workstationBashRunner);
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

		var discoverer = new RestorePointsDiscoverer(_workstationBashRunner);
		var planBuilder = new LocalRestorePlanBuilder(_toolkitResolver, discoverer);
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
		DapsmanPlanPrinter.PrintLocalRestore(plan);

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

		var planBuilder = new LocalTeardownPlanBuilder(
			_projectResolver,
			_dockerResolver,
			_workstationCaddyResolver);

		var service = new LocalTeardownService(
			_dapsConfigLoader,
			planBuilder,
			new WorkstationLocalTeardownExecutor(_workstationBashRunner),
			new DapsYamlEditor());

		var plan = service.CreatePlan(_parsed.ConfigPath, new LocalTeardownOptions { ProjectName = projectName });

		DapsmanPlanPrinter.PrintLocalTeardown(plan);

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

		var planBuilder = new RemoteSyncFromLocalPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_hostingResolver);

		var service = new SyncFromLocalService(_dapsConfigLoader, planBuilder, _toolkitBashRunner);
		var result = service.CreatePlan(_parsed.ConfigPath, options);

		if (!result.IsSupported)
		{
			foreach (var reason in result.Warnings)
				Console.WriteLine($"skipped: {reason}");
			return Task.FromResult(0);
		}

		var plan = result.Plan!;
		DapsmanPlanPrinter.PrintSyncFromLocal(plan);

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

		var planBuilder = new RemoteTeardownPlanBuilder(
			_parsed.ProviderName,
			_toolkitResolver,
			_projectResolver,
			_workstationCaddyResolver,
			_hostingResolver);

		var service = new RemoteTeardownService(
			_dapsConfigLoader,
			planBuilder,
			new ToolkitRemoteTeardownExecutor(_toolkitBashRunner));

		var options = new TeardownOptions
		{
			DryRun = _parsed.DryRun,
			ProjectName = projectName,
			ProviderName = _parsed.ProviderName,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

		DapsmanPlanPrinter.PrintProdTeardown(plan);

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
			_dapsConfigLoader,
			planBuilder,
			new ToolkitUnprovisionExecutor(_toolkitBashRunner));

		var options = new UnprovisionOptions
		{
			DryRun = _parsed.DryRun,
			ProviderName = _parsed.ProviderName,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

		DapsmanPlanPrinter.PrintProdUnprovision(plan);

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

	private static IReadOnlyList<BuildImageCommandPlan> SelectBuildCommands(RemoteDeployPlan plan, RemoteDeployOptions options)
	{
		return plan.BuildImageCommands
			.Where(c => options.BuildImages || !c.HasExistingExports)
			.ToList();
	}

	private static void PrintUsage()
	{
		Console.WriteLine("Usage:");
		Console.WriteLine("  dapsman init --template <name> --name <project-name>|--project <name> [--overlay] [--prod-url <domain>] [--path <destination>] [--dry-run] [--config <path>]");
		Console.WriteLine("  dapsman local build [--build] [--dry-run] [--project <name>...] [--config <path>]");
		Console.WriteLine("  dapsman local caddy restart [--dry-run] [--config <path>]");
		Console.WriteLine("  dapsman prod caddy restart [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod provision [--dry-run] [--provider <name>] [--upgrade] [--set-vars-script <path>] [--create-script <path>] [--config <path>]");
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

}
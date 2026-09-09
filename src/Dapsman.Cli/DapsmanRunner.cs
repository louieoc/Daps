using Dapsman.Application;
using Dapsman.Domain;
using Dapsman.Infrastructure;

internal sealed class DapsmanRunner
{
	// lazy-loading dependencies, resolved on first use, not at startup. This supports workflows
	// that may need to run before the availability of certain dependencies, like init, or local
	// build. local build creates the toolkit, so requiring the toolkit to exist before you can
	// run it makes that workflow impossible
	private IDapsConfigLoader? _lazyDapsConfigLoader;
	private IDapsConfigLoader DapsConfigLoader => _lazyDapsConfigLoader ??= new DapsYamlConfigLoader();

	private IContainerManager? _lazyWorkstationContainerManager;
	private IContainerManager WorkstationContainerManager => _lazyWorkstationContainerManager ??= new WorkstationContainerManager();

	private IContainerManager? _lazyRemoteContainerManager;
	private IContainerManager RemoteContainerManager => _lazyRemoteContainerManager ??= new HardcodedRemoteContainerManager(); // todo: this will need to be replaced with something real

	private IHostingProviderResolver? _lazyHostingResolver;
	private IHostingProviderResolver HostingResolver => _lazyHostingResolver ??= new HostingProviderResolver(Config);

	private IToolkitResolver? _lazyToolkitResolver;
	private IToolkitResolver ToolkitResolver => _lazyToolkitResolver ??= new ToolkitResolver(Config, WorkstationContainerManager);

	private ICaddyResolver? _lazyWorkstationCaddyResolver;
	private ICaddyResolver WorkstationCaddyResolver => _lazyWorkstationCaddyResolver ??= new CaddyResolver(Config, WorkstationContainerManager);

	private ICaddyResolver? _lazyRemoteCaddyResolver;
	private ICaddyResolver RemoteCaddyResolver => _lazyRemoteCaddyResolver ??= new CaddyResolver(Config, RemoteContainerManager);

	private IDockerResolver? _lazyDockerResolver;
	private IDockerResolver DockerResolver => _lazyDockerResolver ??= new DockerResolver(Config);

	private IProjectResolver? _lazyProjectResolver;
	private IProjectResolver ProjectResolver => _lazyProjectResolver ??= new ProjectResolver(Config);

	private IBashRunner? _lazyWorkstationBashRunner;
	private IBashRunner WorkstationBashRunner => _lazyWorkstationBashRunner ??= new WorkstationBashRunner();

	private IBashRunner? _lazyToolkitBashRunner;
	private IBashRunner ToolkitBashRunner => _lazyToolkitBashRunner ??= new ContainerBashRunner(RequireToolkitContainerName());

	private DapsConfig? _lazyConfig;
	private DapsConfig Config => _lazyConfig ??= DapsConfigLoader.Load(_parsed.ConfigPath);

	private readonly CliArguments _parsed;

	public DapsmanRunner(CliArguments parsed)
	{
		_parsed = parsed;
	}

	public void ResolveDependencies()
	{
		// --version doesn't need anything to run
		if (_parsed.IsGetVersion)
		{
			return;
		}

		// check prerequisites
		var localPrerequisiteChecker = new LocalPrerequisiteChecker();

		// init only copies a template and runs a bash script; requiring Docker to be running would
		// block a first-time user from creating a project before Daps itself is built.
		if (_parsed.IsInit)
		{
			localPrerequisiteChecker.EnsureInitPrerequisites();
			return;
		}

		localPrerequisiteChecker.EnsureLocalBuildPrerequisites();
	}

	private string RequireToolkitContainerName()
	{
		var toolkit = ToolkitResolver.Resolve();

		// A dry run only prints a plan, so it can name a container that does not exist yet.
		if (!toolkit.IsRunning && !_parsed.DryRun)
		{
			throw new InvalidOperationException(
				$"The Daps toolkit container ('{toolkit.ContainerName}') is not running. Run 'dapsman local build' first.");
		}

		return toolkit.ContainerName;
	}

	public async Task<int> RunAsync()
	{
		if (_parsed.IsGetVersion)
		{
			return await RunGetVersion();
		}

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

		if (_parsed.IsLocalBackup)
		{
			return await RunLocalBackup();
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

		if (_parsed.IsProdSystem)
		{
			return await RunProdSystem();
		}

		PrintUsage();
		return 1;
	}

	private Task<int> RunGetVersion()
	{
		var version = GetType().Assembly.GetName().Version?.ToString() ?? "unknown";

		// the version returned here has 4 parts, only 3 of them are meaningful to us
		var numbers = version.Split('.');
		if (numbers.Length > 3)
		{
			version = $"{numbers[0]}.{numbers[1]}.{numbers[2]}";
		}
		Console.WriteLine($"Dapsman version: {version}");
		return Task.FromResult(0);
	}

	private Task<int> RunLocalBuild()
	{
		var service = new LocalBuildService(
			DapsConfigLoader,
			new LocalBuildPlanBuilder(Config, DockerResolver, WorkstationCaddyResolver, ProjectResolver, new HostPortManager()),
			new LocalCaddySiteSync(),
			new WorkstationDockerExecutor(),
			WorkstationBashRunner);

		if (_parsed.Rebuild && _parsed.ProjectFilters.Count == 0)
		{
			throw new ArgumentException("--project <name> is required for --rebuild, so that volumes are never destroyed for every project at once.");
		}

		var options = new LocalBuildOptions
		{
			BuildImages = _parsed.BuildImages,
			Rebuild = _parsed.Rebuild,
			DryRun = _parsed.DryRun,
			ProjectFilters = _parsed.ProjectFilters,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);
		var caddyRestartPlanBuilder = new LocalCaddyRestartPlanBuilder(Config, WorkstationCaddyResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();
		DapsmanPlanPrinter.PrintLocalBuild(plan, options, caddyRestartPlan);

		if (!options.DryRun)
		{
			if (options.Rebuild && !ConfirmRebuild(plan))
			{
				Console.WriteLine("Rebuild cancelled.");
				return Task.FromResult(1);
			}

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
			Console.WriteLine("Step: docker-network");
			service.EnsureSharedNetwork(plan, Config.DapsRootPath);
			Console.WriteLine($"- '{plan.SharedNetworkName}' exists");

			Console.WriteLine();
			Console.WriteLine("Step: compose-daps");
			Console.WriteLine($"- executing: {plan.DapsComposeCommand}");
			service.ExecuteDapsCompose(plan);
			Console.WriteLine("- done");

			if (options.Rebuild)
			{
				Console.WriteLine();
				Console.WriteLine("Step: rebuild-teardown");
				foreach (var projectPlan in plan.ProjectComposePlans)
				{
					Console.WriteLine($"- {projectPlan.ProjectName}: executing: {projectPlan.ComposeDownCommand}");
					service.ExecuteProjectComposeDown(projectPlan);
					Console.WriteLine($"- {projectPlan.ProjectName}: done");
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
					Console.WriteLine($"- {projectPlan.ProjectName}: executing: {projectPlan.ComposeUpCommand}");
					service.ExecuteProjectCompose(projectPlan);
					Console.WriteLine($"- {projectPlan.ProjectName}: done");
				}
			}

			Console.WriteLine();
			Console.WriteLine("Step: caddy-reload");
			new CaddyRestartService(caddyRestartPlanBuilder, WorkstationBashRunner).Execute(caddyRestartPlan);
			Console.WriteLine("- done");
		}

		return Task.FromResult(0);
	}

	private bool ConfirmRebuild(LocalBuildPlan plan)
	{
		if (_parsed.AssumeYes)
		{
			return true;
		}

		var projectNames = string.Join(", ", plan.ProjectComposePlans.Select(p => p.ProjectName));

		Console.WriteLine();
		Console.Write($"This will DELETE all containers and volumes for '{projectNames}', including the database. Type 'yes' to confirm: ");
		var confirm = Console.ReadLine()?.Trim();

		return string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase);
	}

	private Task<int> RunRemoteProvision()
	{
		var openStackPlanBuilder = new OpenStackRemoteProvisionPlanBuilder(ToolkitResolver, HostingResolver);
		var genericVpsPlanBuilder = new GenericVpsRemoteProvisionPlanBuilder(ToolkitResolver, HostingResolver);
		var planBuilder = new CompositeRemoteProvisionPlanBuilder(HostingResolver, openStackPlanBuilder, genericVpsPlanBuilder);

		var service = new RemoteProvisionService(
			DapsConfigLoader,
			planBuilder,
			new ToolkitRemoteProvisionExecutor(ToolkitBashRunner));

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
			HostingResolver,
			DockerResolver,
			ToolkitResolver,
			WorkstationCaddyResolver,
			ProjectResolver);

		var executor = new ToolkitRemoteDeployExecutor(ToolkitBashRunner);

		var service = new RemoteDeployService(
			DapsConfigLoader,
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
			Config, plan.ProviderName, ToolkitResolver, RemoteCaddyResolver, HostingResolver);
		var caddyRestartPlan = caddyRestartPlanBuilder.BuildPlan();
		DapsmanPlanPrinter.PrintRemoteDeploy(plan, options, caddyRestartPlan);

		if (!options.DryRun)
		{
			if (plan.SystemStatusPlan is not null)
			{
				// Read before building, never cached: it costs one SSH round trip, and a stored
				// value would go stale the moment the provider's host is replaced -- silently, and
				// in the one direction that breaks the deploy.
				//
				// This is the same collector `prod system` uses. Deploy wants one field out of it,
				// but the alternative was a second path doing the same staging, ssh and escaping.
				Console.WriteLine();
				Console.WriteLine("Step: probe-platform");
				var status = new ToolkitSystemStatusCollector(ToolkitBashRunner).Collect(plan.SystemStatusPlan);

				// Collect throws if the host is unreachable. This covers the other case: it
				// answered, but reported no docker platform. Fail closed either way -- falling
				// back to an unpinned build is the silent mismatch this step exists to prevent.
				var targetPlatform = status.DockerPlatform
					?? throw new InvalidOperationException(
						$"Could not read the Docker platform from '{plan.RemoteHost}'. Expected something " +
						"like 'linux/amd64'. Check that prod provision has installed Docker there.");
				Console.WriteLine($"- {plan.RemoteHost} runs {targetPlatform}");

				Console.WriteLine();
				Console.WriteLine("Step: build-images");
				service.ExecuteBuildImageScripts(plan.SelectedBuildImageCommands, plan.DapsRootPath, targetPlatform);
				Console.WriteLine("- done");
			}

			Console.WriteLine();
			Console.WriteLine("Step: deploy-remote");
			service.Execute(plan);
			Console.WriteLine("- done");

			Console.WriteLine();
			Console.WriteLine("Step: caddy-reload");
			new CaddyRestartService(caddyRestartPlanBuilder, ToolkitBashRunner).Execute(caddyRestartPlan);
			Console.WriteLine("- done");

			foreach (var projectPlan in plan.ProjectPlans.Where(p => p.PostDeployScriptPath is not null))
			{
				Console.WriteLine();
				Console.WriteLine($"Step: post-deploy-hook ({projectPlan.ProjectName})");
				ToolkitBashRunner.RunScript(
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
			new InitPlanBuilder(Config, ProjectResolver, DockerResolver, new HostPortManager(), new TemplateConfigLoader()),
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
			planBuilder = new RemoteCaddyRestartPlanBuilder(Config, _parsed.ProviderName, ToolkitResolver, RemoteCaddyResolver, HostingResolver);
			bashRunner = ToolkitBashRunner;
		}
		else
		{
			// Unlike local build, this workflow cannot create the container it reloads.
			var caddy = WorkstationCaddyResolver.Resolve();
			if (!caddy.IsRunning && !_parsed.DryRun)
			{
				throw new InvalidOperationException(
					$"The Daps caddy container ('{caddy.ContainerName}') is not running. Run 'dapsman local build' first.");
			}

			planBuilder = new LocalCaddyRestartPlanBuilder(Config, WorkstationCaddyResolver);
			bashRunner = WorkstationBashRunner;
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
		var projects = ProjectResolver.Resolve(_parsed.ProjectFilters);

		var planBuilder = new RemoteOfflineStatusPlanBuilder(
			offline,
			_parsed.ProviderName,
			ToolkitResolver,
			ProjectResolver,
			WorkstationCaddyResolver,
			HostingResolver);

		var executor = new ToolkitOfflineStatusExecutor(ToolkitBashRunner);
		var service = new OfflineStatusService(DapsConfigLoader, planBuilder, executor);

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

	private Task<int> RunLocalBackup()
	{
		var projects = ProjectResolver.Resolve(_parsed.ProjectFilters);

		var planBuilder = new LocalBackupPlanBuilder(ToolkitResolver);
		var service = new LocalBackupService(planBuilder, ToolkitBashRunner);

		foreach (var project in projects)
		{
			var options = new BackupOptions
			{
				DryRun = _parsed.DryRun,
				ProjectName = project.Definition.Name,
			};

			var result = service.CreatePlan(project, options);

			Console.WriteLine($"Project: {project.Definition.Name}");
			if (!result.IsSupported)
			{
				foreach (var reason in result.Warnings)
					Console.WriteLine($"- skipped: {reason}");
				Console.WriteLine();
				continue;
			}

			var plan = result.Plan!;
			DapsmanPlanPrinter.PrintLocalBackup(plan);

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

	private Task<int> RunProdBackup()
	{
		var projects = ProjectResolver.Resolve(_parsed.ProjectFilters);

		var planBuilder = new RemoteBackupPlanBuilder(
			_parsed.ProviderName,
			ToolkitResolver,
			ProjectResolver,
			HostingResolver);

		var service = new RemoteBackupService(DapsConfigLoader, planBuilder, ToolkitBashRunner);

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
			ToolkitResolver,
			ProjectResolver,
			HostingResolver);

		var service = new SyncFromRemoteService(DapsConfigLoader, planBuilder, ToolkitBashRunner);
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
		var project = ProjectResolver.Resolve(projectName);

		var discoverer = new RestorePointsDiscoverer(WorkstationBashRunner);
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
		var project = ProjectResolver.Resolve(projectName);

		var discoverer = new RestorePointsDiscoverer(WorkstationBashRunner);
		var planBuilder = new LocalRestorePlanBuilder(ToolkitResolver, discoverer);
		var service = new RestoreService(planBuilder, new ToolkitLocalRestoreExecutor(ToolkitBashRunner));

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
			ProjectResolver,
			DockerResolver,
			WorkstationCaddyResolver);

		var service = new LocalTeardownService(
			DapsConfigLoader,
			planBuilder,
			new WorkstationLocalTeardownExecutor(WorkstationBashRunner),
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
			ToolkitResolver,
			ProjectResolver,
			HostingResolver);

		var service = new SyncFromLocalService(DapsConfigLoader, planBuilder, ToolkitBashRunner);
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
			ToolkitResolver,
			ProjectResolver,
			WorkstationCaddyResolver,
			HostingResolver);

		var service = new RemoteTeardownService(
			DapsConfigLoader,
			planBuilder,
			new ToolkitRemoteTeardownExecutor(ToolkitBashRunner));

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
		var planBuilder = new OpenStackUnprovisionPlanBuilder(ToolkitResolver, HostingResolver);

		var service = new UnprovisionService(
			DapsConfigLoader,
			planBuilder,
			new ToolkitUnprovisionExecutor(ToolkitBashRunner));

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

	private Task<int> RunProdSystem()
	{
		var planBuilder = new RemoteSystemStatusPlanBuilder(ToolkitResolver, HostingResolver);

		var service = new SystemStatusService(
			DapsConfigLoader,
			planBuilder,
			new ToolkitSystemStatusCollector(ToolkitBashRunner));

		var options = new SystemStatusOptions
		{
			DryRun = _parsed.DryRun,
			ProviderName = _parsed.ProviderName,
			Verbose = _parsed.Verbose,
		};

		var plan = service.CreatePlan(_parsed.ConfigPath, options);

		DapsmanPlanPrinter.PrintSystemStatusPlan(plan);

		if (!options.DryRun)
		{
			var status = service.Execute(plan);
			Console.WriteLine();
			DapsmanPlanPrinter.PrintSystemStatusReport(status, plan);
		}

		return Task.FromResult(0);
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
		Console.WriteLine("  dapsman local backup [--project <name>...] [--dry-run] [--config <path>]");
		Console.WriteLine("  dapsman prod backup [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod offline [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod online [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod teardown --project <name> [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod unprovision [--dry-run] [--provider <name>] [--config <path>]");
		Console.WriteLine("  dapsman prod system [--verbose] [--dry-run] [--provider <name>] [--config <path>]");
	}

}
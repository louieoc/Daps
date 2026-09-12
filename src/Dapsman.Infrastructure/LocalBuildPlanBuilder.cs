using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class LocalBuildPlanBuilder : ILocalBuildPlanBuilder
{
	private readonly DapsConfig _config;
	private readonly IDockerResolver _dockerResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly IHostPortManager _hostPortManager;

	public LocalBuildPlanBuilder(
		DapsConfig config,
		IDockerResolver dockerResolver,
		ICaddyResolver caddyResolver,
		IProjectResolver projectResolver,
		IHostPortManager hostPortManager)
	{
		_config = config;
		_dockerResolver = dockerResolver;
		_caddyResolver = caddyResolver;
		_projectResolver = projectResolver;
		_hostPortManager = hostPortManager;
	}

	public LocalBuildPlan BuildLocalPlan(LocalBuildOptions options)
	{
		var allProjects = _projectResolver.Resolve([]);
		var selectedProjects = _projectResolver.Resolve(options.ProjectFilters);

		var warnings = new List<string>();

		if (_config.Projects.Count == 0)
		{
			warnings.Add("No projects configured in daps.yaml; continuing with Daps-only local build.");
		}
		else if (selectedProjects.Count == 0)
		{
			warnings.Add("Project filters did not match configured projects; continuing with Daps-only local build.");
		}

		// ensure rebuild is only requested when there are projects to rebuild
		if (options.Rebuild)
		{
			if (options.ProjectFilters.Count == 0)
			{
				throw new InvalidOperationException("A project filter is required for --rebuild, so that volumes are never destroyed for all projects at once.");
			}

			if (selectedProjects.Count == 0)
			{
				throw new InvalidOperationException("Cannot rebuild project containers when no projects are selected.");
			}
		}

		var dockerDefinition = _dockerResolver.Resolve();
		var dapsComposeFiles = ConfigUtils.RequireFiles(dockerDefinition.LocalDapsComposeFilePaths, "All expected Daps compose files must be present.").ToList();

		// Always include all projects' toolkit mount files so Docker Compose doesn't evict
		// mounts for projects that aren't in the current --project filter.
		foreach (var project in allProjects)
		{
			var projectDocker = _dockerResolver.ResolveForProject(project);
			dapsComposeFiles.AddRange(projectDocker.LocalDapsExtensionComposeFiles);
		}

		var caddySyncPlan = BuildCaddySyncPlan(_config, _caddyResolver, selectedProjects, warnings);

		var projectPlans = new List<LocalProjectComposePlan>();
		var allBindings = new List<HostPortBinding>();

		foreach (var project in allProjects)
		{
			var devFiles = _dockerResolver.ResolveForProject(project).LocalDevComposeFiles;
			allBindings.AddRange(_hostPortManager.GetBindings(project.Definition.Name, devFiles));
		}

		foreach (var project in selectedProjects)
		{
			var projectDocker = _dockerResolver.ResolveForProject(project);
			var projectComposeBuilder = new WorkstationDockerComposeBuilder(projectDocker.LocalProjectComposeFiles);
			var upCommand = projectComposeBuilder.BuildDockerComposeUpCommand(options.BuildImages);
			var downCommand = options.Rebuild ? projectComposeBuilder.BuildDockerComposeDownCommand(removeVolumes: true) : null;

			projectPlans.Add(new LocalProjectComposePlan
			{
				ProjectName = project.Definition.Name,
				ProjectPath = project.Definition.Path,
				ComposeFiles = projectDocker.LocalProjectComposeFiles,
				PrerequisiteScripts = project.LocalPrerequisiteScripts,
				PrerequisiteScriptEnvVars = new Dictionary<string, string>
				{
					["DAPS_PROJECT"] = project.Definition.Name
				},
				ComposeUpCommand = upCommand,
				ComposeDownCommand = downCommand
			});
		}

		foreach (var conflict in _hostPortManager.FindConflicts(allBindings))
		{
			var projects = string.Join(", ", conflict.Bindings.Select(b =>
				$"{b.ProjectName} ({Path.GetFileName(b.FilePath)})"));
			warnings.Add($"Port {conflict.Port} is used by multiple projects: {projects}");
		}

		var dapsComposeBuilder = new WorkstationDockerComposeBuilder(dapsComposeFiles);
		var dapsUpCommand = dapsComposeBuilder.BuildDockerComposeUpCommand(options.BuildImages);

		return new LocalBuildPlan
		{
			CaddySync = caddySyncPlan,
			SharedNetworkName = _config.SharedNetworkName,
			DapsComposeFiles = dapsComposeFiles,
			ProjectComposePlans = projectPlans,
			Warnings = warnings,
			HasProjectsConfigured = _config.Projects.Count > 0,
			DapsComposeCommand = dapsUpCommand
		};
	}

	private static LocalCaddySyncPlan BuildCaddySyncPlan(
		DapsConfig config,
		ICaddyResolver caddyResolver,
		IReadOnlyList<DapsProject> projects,
		List<string> warnings)
	{
		var caddyDef = caddyResolver.Resolve();
		var runtimeSitesPath = caddyDef.WorkstationSitesPath;
		var filesToCopy = new List<LocalCaddySiteCopyPlan>();
		var destinationTracker = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var project in projects)
		{
			var projectCaddyDef = caddyResolver.ResolveForProject(project);
			if (!Directory.Exists(projectCaddyDef.ProjectSitesPath))
			{
				warnings.Add($"Project '{project.Definition.Name}' is missing _caddy_sites directory at '{projectCaddyDef.ProjectSitesPath}'.");
				continue;
			}

			foreach (var sourcePath in projectCaddyDef.WorkstationLocalSiteFilePaths)
			{
				var fileName = Path.GetFileName(sourcePath);
				var destinationPath = Path.Combine(runtimeSitesPath, fileName);

				if (!destinationTracker.Add(destinationPath))
				{
					throw new InvalidOperationException(
						$"Duplicate caddy filename '{fileName}' detected while planning sync. Ensure project caddy filenames are globally unique.");
				}

				filesToCopy.Add(new LocalCaddySiteCopyPlan
				{
					ProjectName = project.Definition.Name,
					SourcePath = sourcePath,
					DestinationPath = destinationPath,
				});
			}
		}

		var placeholderPath = Path.Combine(runtimeSitesPath, "000-empty.dev.caddy");

		return new LocalCaddySyncPlan
		{
			RuntimeSitesPath = runtimeSitesPath,
			FilesToCopy = filesToCopy,
			ShouldCreatePlaceholder = filesToCopy.Count == 0,
			PlaceholderFilePath = placeholderPath,
		};
	}
}

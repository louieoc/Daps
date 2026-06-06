using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionComposePlanBuilder : ILocalPlanBuilder
{
	private readonly DapsConfig _config;
	private readonly IDockerResolver _dockerResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IProjectResolver _projectResolver;

	public ConventionComposePlanBuilder(
		DapsConfig config,
		IDockerResolver dockerResolver,
		ICaddyResolver caddyResolver,
		IProjectResolver projectResolver)
	{
		_config = config;
		_dockerResolver = dockerResolver;
		_caddyResolver = caddyResolver;
		_projectResolver = projectResolver;
	}

	public LocalBuildPlan BuildLocalPlan(LocalBuildOptions options)
	{
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

		var dockerDefinition = _dockerResolver.Resolve();
		var dapsComposeFiles = ConfigUtils.RequireFiles(dockerDefinition.LocalDapsComposeFilePaths, "All expected Daps compose files must be present.").ToList();

		var caddySyncPlan = BuildCaddySyncPlan(_config, _caddyResolver, selectedProjects, warnings);

		var projectPlans = new List<ProjectComposePlan>();
		foreach (var project in selectedProjects)
		{
			var projectDocker = _dockerResolver.ResolveForProject(project);
			dapsComposeFiles.AddRange(projectDocker.LocalDapsExtensionComposeFiles);

			projectPlans.Add(new ProjectComposePlan
			{
				ProjectName = project.Definition.Name,
				ProjectPath = project.Definition.Path,
				ComposeFiles = projectDocker.LocalProjectComposeFiles,
				PrerequisiteScripts = project.LocalPrerequisiteScripts
			});
		}

		return new LocalBuildPlan
		{
			CaddySync = caddySyncPlan,
			DapsComposeFiles = dapsComposeFiles,
			ProjectComposePlans = projectPlans,
			Warnings = warnings,
			HasProjectsConfigured = _config.Projects.Count > 0,
		};
	}

	private static CaddySyncPlan BuildCaddySyncPlan(
		DapsConfig config,
		ICaddyResolver caddyResolver,
		IReadOnlyList<DapsProject> projects,
		List<string> warnings)
	{
		var caddyDef = caddyResolver.Resolve();
		var runtimeSitesPath = caddyDef.WorkstationSitesPath;
		var filesToCopy = new List<CaddySiteCopyPlan>();
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

				filesToCopy.Add(new CaddySiteCopyPlan
				{
					ProjectName = project.Definition.Name,
					SourcePath = sourcePath,
					DestinationPath = destinationPath,
				});
			}
		}

		var placeholderPath = Path.Combine(runtimeSitesPath, "000-empty.dev.caddy");

		return new CaddySyncPlan
		{
			RuntimeSitesPath = runtimeSitesPath,
			FilesToCopy = filesToCopy,
			ShouldCreatePlaceholder = filesToCopy.Count == 0,
			PlaceholderFilePath = placeholderPath,
		};
	}
}

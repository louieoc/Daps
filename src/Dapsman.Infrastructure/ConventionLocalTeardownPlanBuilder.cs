using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionLocalTeardownPlanBuilder : ILocalTeardownPlanBuilder
{
	private readonly IProjectResolver _projectResolver;
	private readonly IDockerResolver _dockerResolver;
	private readonly ICaddyResolver _caddyResolver;

	public ConventionLocalTeardownPlanBuilder(
		IProjectResolver projectResolver,
		IDockerResolver dockerResolver,
		ICaddyResolver caddyResolver)
	{
		_projectResolver = projectResolver;
		_dockerResolver = dockerResolver;
		_caddyResolver = caddyResolver;
	}

	public LocalTeardownPlan BuildPlan(DapsConfig config, LocalTeardownOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);
		var dockerDef = _dockerResolver.ResolveForProject(project);
		var caddyDef = _caddyResolver.Resolve();
		var projectCaddyDef = _caddyResolver.ResolveForProject(project);

		var caddySiteFilesToDelete = projectCaddyDef.WorkstationLocalSiteFilePaths
			.Select(p => Path.Combine(caddyDef.WorkstationSitesPath, Path.GetFileName(p)))
			.ToList();

		return new LocalTeardownPlan
		{
			ProjectName = project.Definition.Name,
			ProjectPath = project.Definition.Path,
			ComposeFiles = dockerDef.LocalProjectComposeFiles,
			CaddySiteFilesToDelete = caddySiteFilesToDelete,
			CaddyContainerName = caddyDef.ContainerName,
			CaddyConfigPath = caddyDef.ContainerConfigPath,
		};
	}
}

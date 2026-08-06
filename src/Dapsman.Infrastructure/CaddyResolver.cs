using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public class CaddyResolver : ICaddyResolver
{
	private readonly DapsConfig _config;
	private readonly IContainerManager _manager;

	public CaddyResolver(DapsConfig config, IContainerManager manager)
	{
		_config = config;
		_manager = manager;
	}

	public CaddyDefinition Resolve()
	{
		var warnings = new List<string>();
		var (containerName, isRunning) = ResolveContainerName();
		var caddyfileSourcePath = Path.Combine(_config.DapsRootPath, "caddy", "Caddyfile");
		var siteFileFolder = Path.Combine(_config.DapsRootPath, "caddy_sites");
		var prodSiteFileNames = CollectExpectedProdCaddyFileNames(warnings);

		return new CaddyDefinition
		{
			ContainerName = containerName,
			IsRunning = isRunning,
			WorkstationCaddyFilePath = caddyfileSourcePath,
			WorkstationSitesPath = siteFileFolder,
			AllWorkstationProdSiteFileNames = prodSiteFileNames,
			Warnings = warnings
		};
	}

	public ProjectCaddyDefinition ResolveForProject(DapsProject project)
	{
		var warnings = new List<string>();
		var projectSitesPath = GetProjectSiteCaddyFilePath(project.Definition);
		var prodSiteFilePaths = CollectProjectCaddyFilePaths(project.Definition.Name, projectSitesPath, warnings, "prod");
		var devSiteFilePaths = CollectProjectCaddyFilePaths(project.Definition.Name, projectSitesPath, warnings, "dev");

		var expectedProdCaddySiteFileName = $"{project.Definition.Name}.prod.caddy";
		var prodSiteFile = prodSiteFilePaths.FirstOrDefault(f => Path.GetFileName(f).Equals(expectedProdCaddySiteFileName, StringComparison.OrdinalIgnoreCase));
		var offlineCaddyFile = $"{project.Definition.Name}.offline.caddy";

		return new ProjectCaddyDefinition
		{
			ProjectSitesPath = projectSitesPath,
			WorkstationLocalSiteFilePaths = devSiteFilePaths,
			WorkstationProdSiteFilePaths = prodSiteFilePaths,
			WorkstationProdSiteFile = Path.GetFileName(prodSiteFile),
			OfflineCaddyFilename = offlineCaddyFile
		};
	}

	private static string GetProjectSiteCaddyFilePath(ProjectDefinition projectDefinition)
	{
		return Path.Combine(projectDefinition.Path, "_caddy_sites");
	}

	// Container name follows Docker Compose convention: <compose-project-name>-<service>-<index>
	// DAPS names its compose project "daps" (default), so the Caddy container should be daps-caddy-1.
	// A missing caddy container is not an error here: local build resolves the plan before the
	// container exists and creates it along the way. It resolves to the conventional name with
	// IsRunning false, and the caller decides whether to require a running container.
	private (string ContainerName, bool IsRunning) ResolveContainerName()
	{
		var expected = $"{_config.DapsComposeProjectName}-caddy-1";
		var containers = _manager.GetContainerNames();

		var preferred = containers.FirstOrDefault(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase))
			?? containers.FirstOrDefault(n => n.EndsWith("caddy-1", StringComparison.OrdinalIgnoreCase))
			?? containers.FirstOrDefault(n => n.Contains("caddy", StringComparison.OrdinalIgnoreCase));

		return preferred is null ? (expected, false) : (preferred, true);
	}

	private List<string> CollectExpectedProdCaddyFileNames(List<string> warnings)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var project in _config.Projects)
		{
			var projectSitesPath = GetProjectSiteCaddyFilePath(project);

			if (!Directory.Exists(projectSitesPath))
			{
				continue;
			}

			foreach (var sourcePath in Directory.EnumerateFiles(projectSitesPath, "*.prod.caddy"))
			{
				var fileName = Path.GetFileName(sourcePath);
				if (!result.Add(fileName))
				{
					warnings.Add($"Duplicate prod caddy filename '{fileName}' detected across projects.");
				}
			}
		}

		return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static List<string> CollectProjectCaddyFilePaths(string projectName, string projectSitesPath, List<string> warnings, string environment)
	{
		if (!Directory.Exists(projectSitesPath))
		{
			warnings.Add($"Project '{projectName}' is missing _caddy_sites directory at '{projectSitesPath}'.");
			return [];
		}

		var result = Directory.EnumerateFiles(projectSitesPath, $"*.{environment}.caddy");
		return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
	}
}

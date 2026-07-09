using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class InitPlanBuilder : IInitPlanBuilder
{
	public const string InitTemplateToolkitScript = "init-template.toolkit.sh";

	private readonly DapsConfig _config;
	private readonly IProjectResolver _projectResolver;
	private readonly IDockerResolver _dockerResolver;
	private readonly IHostPortManager _hostPortManager;

	public InitPlanBuilder(
		DapsConfig config,
		IProjectResolver projectResolver,
		IDockerResolver dockerResolver,
		IHostPortManager hostPortManager)
	{
		_config = config;
		_projectResolver = projectResolver;
		_dockerResolver = dockerResolver;
		_hostPortManager = hostPortManager;
	}

	public InitPlan BuildInitPlan(InitOptions options)
	{
		var dapsRoot = _config.DapsRootPath
			?? throw new InvalidOperationException("Cannot determine Daps root from config path.");

		if (_config.Projects.Any(p => string.Equals(p.Name, options.ProjectName, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidOperationException($"Project '{options.ProjectName}' is already registered in daps.yaml. Choose a different name or remove the existing entry first.");

		var templatePath = Path.GetFullPath(Path.Combine(dapsRoot, "templates", options.TemplateName));
		if (!Directory.Exists(templatePath))
			throw new InvalidOperationException($"Template not found: {templatePath}");

		var destinationPath = options.DestinationPath is not null
			? Path.GetFullPath(options.DestinationPath)
			: Path.GetFullPath(Path.Combine(dapsRoot, "..", options.ProjectName));

		if (options.Overlay && !Directory.Exists(destinationPath))
			throw new InvalidOperationException($"Overlay mode requires the destination directory to already exist: {destinationPath}");

		if (!options.Overlay && Directory.Exists(destinationPath))
			throw new InvalidOperationException($"Destination already exists: {destinationPath}. Use --overlay to add Daps files to an existing directory.");

		var initScriptPath = Path.Combine(templatePath, "_scripts", InitTemplateToolkitScript);
		var hasInitScript = File.Exists(initScriptPath);

		var relativePath = Path.GetRelativePath(dapsRoot, destinationPath)
			.Replace(Path.DirectorySeparatorChar, '/');

		// Ensure relative paths use forward slashes and a leading ./ for clarity
		if (!relativePath.StartsWith("..") && !relativePath.StartsWith("./"))
			relativePath = "./" + relativePath;

		var portAssignments = ComputePortAssignments(templatePath, options.TemplateName);

		return new InitPlan
		{
			TemplateName = options.TemplateName,
			TemplatePath = templatePath,
			ProjectName = options.ProjectName,
			DestinationPath = destinationPath,
			DapsYamlPath = _config.FullYamlPath,
			DapsYamlProjectRelativePath = relativePath,
			InitScriptPath = hasInitScript ? initScriptPath : null,
			ProdUrl = NormalizeProdUrl(options.ProdUrl),
			Overlay = options.Overlay,
			DevPortAssignments = portAssignments,
		};
	}

	private IReadOnlyList<PortAssignment> ComputePortAssignments(string templatePath, string templateName)
	{
		var templateDockerDir = Path.Combine(templatePath, "_docker");
		if (!Directory.Exists(templateDockerDir))
			return [];

		var templateDevFiles = Directory.GetFiles(templateDockerDir, "*.dev.yaml");
		if (templateDevFiles.Length == 0)
			return [];

		// Ports already in use by configured projects
		var existingProjects = _projectResolver.Resolve([]);
		var existingBindings = new List<HostPortBinding>();
		foreach (var project in existingProjects)
		{
			var docker = _dockerResolver.ResolveForProject(project);
			existingBindings.AddRange(_hostPortManager.GetBindings(project.Definition.Name, docker.LocalDevComposeFiles));
		}
		var reservedPorts = new HashSet<int>(existingBindings.Select(b => b.HostPort));

		// Ports the template wants
		var templateBindings = _hostPortManager.GetBindings(templateName, templateDevFiles);
		// Deduplicate template ports (same port in multiple files counts once)
		var templatePorts = templateBindings.Select(b => b.HostPort).Distinct().ToList();

		var assignments = new List<PortAssignment>();
		foreach (var port in templatePorts)
		{
			if (!reservedPorts.Contains(port))
			{
				assignments.Add(new PortAssignment(port, port, "available"));
				reservedPorts.Add(port); // Reserve for subsequent ports in same template
			}
			else
			{
				var conflictingProject = existingBindings
					.FirstOrDefault(b => b.HostPort == port)?.ProjectName ?? "another project";
				var assigned = _hostPortManager.FindNextAvailable(port, reservedPorts);
				assignments.Add(new PortAssignment(port, assigned, $"conflict with {conflictingProject}"));
				reservedPorts.Add(assigned);
			}
		}

		return assignments;
	}

	private static string? NormalizeProdUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url)) return null;
		// Strip scheme so the value is a bare hostname suitable for Caddy (e.g. "dapster.org")
		foreach (var scheme in new[] { "https://", "http://" })
			if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
				url = url[scheme.Length..];
		return url.TrimEnd('/');
	}
}

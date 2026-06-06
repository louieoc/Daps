using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionInitPlanBuilder : IInitPlanBuilder
{
	public const string InitTemplateToolkitScript = "init-template.toolkit.sh";

	private readonly DapsConfig _config;

	public ConventionInitPlanBuilder(DapsConfig config)
	{
		_config = config;
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
		};
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

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
			?? throw new InvalidOperationException("Cannot determine DAPS root from config path.");

		var templatePath = Path.GetFullPath(Path.Combine(dapsRoot, "templates", options.TemplateName));
		if (!Directory.Exists(templatePath))
			throw new InvalidOperationException($"Template not found: {templatePath}");

		var destinationPath = options.DestinationPath is not null
			? Path.GetFullPath(options.DestinationPath)
			: Path.GetFullPath(Path.Combine(dapsRoot, "..", options.ProjectName));

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
		};
	}
}

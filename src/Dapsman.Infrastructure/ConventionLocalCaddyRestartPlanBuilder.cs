using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionLocalCaddyRestartPlanBuilder : ICaddyRestartPlanBuilder
{
	private readonly DapsConfig _config;
	private readonly ICaddyResolver _caddyResolver;

	public ConventionLocalCaddyRestartPlanBuilder(DapsConfig config, ICaddyResolver caddyResolver)
	{
		_config = config;
		_caddyResolver = caddyResolver;
	}

	public CaddyRestartPlan BuildPlan()
	{
		var caddyDefinition = _caddyResolver.Resolve();
		var caddyContainer = caddyDefinition.ContainerName;

		return new CaddyRestartPlan
		{
			ReloadCommand = $"docker exec {caddyContainer} caddy reload --config {caddyDefinition.ContainerConfigPath}",
			WorkingDirectory = _config.DapsRootPath,
		};
	}
}

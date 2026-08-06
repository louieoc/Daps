using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class LocalCaddyRestartPlanBuilder : ICaddyRestartPlanBuilder
{
	private readonly DapsConfig _config;
	private readonly ICaddyResolver _caddyResolver;

	public LocalCaddyRestartPlanBuilder(DapsConfig config, ICaddyResolver caddyResolver)
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
			// --force: Caddy skips a reload when the incoming config is byte-identical to the
			// running one. `caddy restart` is the command a user reaches for to make Caddy retry
			// something (e.g. certificate issuance after a DNS change), so it must always reload.
			ReloadCommand = $"docker exec {caddyContainer} caddy reload --force --config {caddyDefinition.ContainerConfigPath}",
			WorkingDirectory = _config.DapsRootPath,
		};
	}
}

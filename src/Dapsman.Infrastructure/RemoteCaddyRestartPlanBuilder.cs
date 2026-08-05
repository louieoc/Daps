using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RemoteCaddyRestartPlanBuilder : ICaddyRestartPlanBuilder
{
	private readonly DapsConfig _config;
	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public RemoteCaddyRestartPlanBuilder(
		DapsConfig config,
		string? providerName,
		IToolkitResolver toolkitResolver,
		ICaddyResolver caddyResolver,
		IHostingProviderResolver hostingResolver)
	{
		_config = config;
		_providerName = providerName;
		_toolkitResolver = toolkitResolver;
		_caddyResolver = caddyResolver;
		_hostingResolver = hostingResolver;
	}

	public CaddyRestartPlan BuildPlan()
	{
		var provider = _hostingResolver.ResolveExplicit(_providerName);
		var toolkitDef = _toolkitResolver.Resolve();
		var caddyDef = _caddyResolver.Resolve();

		var sshKey = $"~/.ssh/{provider.KeyName}";
		var sshOpts = $"-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i {sshKey}";
		// --force: Caddy skips a reload when the incoming config is byte-identical to the running
		// one. The main use for this workflow is making Caddy retry certificate issuance after a
		// DNS change, where the caddy file has not changed at all, so it must always reload.
		var reloadCmd = $"ssh {sshOpts} {provider.RemoteUser}@{provider.RemoteHost} 'docker exec {caddyDef.ContainerName} caddy reload --force --config {caddyDef.ContainerConfigPath}'";

		return new CaddyRestartPlan
		{
			ReloadCommand = reloadCmd,
			WorkingDirectory = _config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
		};
	}
}

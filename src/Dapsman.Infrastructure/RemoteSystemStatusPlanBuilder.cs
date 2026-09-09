using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RemoteSystemStatusPlanBuilder : ISystemStatusPlanBuilder
{
	public const string StatusScript = "remote-system-status.sh";
	public const string RemoteProjectsRoot = "/srv/projects";

	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _providerResolver;

	public RemoteSystemStatusPlanBuilder(
		IToolkitResolver toolkitResolver,
		IHostingProviderResolver providerResolver)
	{
		_toolkitResolver = toolkitResolver;
		_providerResolver = providerResolver;
	}

	public SystemStatusPlan BuildPlan(DapsConfig config, SystemStatusOptions options)
	{
		// ResolveExplicit, not Resolve: this reports on a host, and there is no project to infer a
		// provider from. With several providers configured, silently reading the first one would
		// answer "how is my server doing?" about a different server. Mirrors prod unprovision.
		var provider = _providerResolver.ResolveExplicit(options.ProviderName);
		var toolkitDef = _toolkitResolver.Resolve();
		var dockerPrefix = ConfigUtils.GetDockerCommandPrefix(provider.RemoteUser);

		return new SystemStatusPlan
		{
			ProviderName = provider.ConfigDefinition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyName = provider.KeyName,
			DockerCommandPrefix = dockerPrefix,
			RemoteProjectsRoot = RemoteProjectsRoot,
			StatusScriptHostPath = Path.Combine(config.DapsRootPath, "scripts", StatusScript),
			Verbose = options.Verbose,
		};
	}

	/// <summary>
	/// Builds a summary-only plan against a host that has already been resolved, for callers that
	/// want a reading rather than a report — currently `prod deploy`, which needs the host's docker
	/// platform before it builds an image for it.
	///
	/// Static and provider-free because <see cref="BuildPlan"/> cannot serve that caller:
	/// it resolves through <c>ResolveExplicit</c>, which deliberately refuses to guess between
	/// several configured providers, whereas deploy infers its provider from the selected projects
	/// and so arrives here with the host already decided.
	///
	/// Never verbose. A caller after one value must not pay for `docker stats` and a `du -sb` walk
	/// of every project directory, which is unbounded and grows with the size of the sites.
	/// </summary>
	public static SystemStatusPlan BuildPlanForHost(
		string providerName,
		string dapsRootPath,
		string toolkitContainerName,
		string remoteHost,
		string remoteUser,
		string sshKeyName)
	{
		return new SystemStatusPlan
		{
			ProviderName = providerName,
			DapsRootPath = dapsRootPath,
			ToolkitContainerName = toolkitContainerName,
			RemoteHost = remoteHost,
			RemoteUser = remoteUser,
			SshKeyName = sshKeyName,
			DockerCommandPrefix = ConfigUtils.GetDockerCommandPrefix(remoteUser),
			RemoteProjectsRoot = RemoteProjectsRoot,
			StatusScriptHostPath = Path.Combine(dapsRootPath, "scripts", StatusScript),
			Verbose = false,
		};
	}
}

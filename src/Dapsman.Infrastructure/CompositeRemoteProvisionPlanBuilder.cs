using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// Dispatches prod provision to the right builder based on the resolved provider's type.
/// </summary>
public sealed class CompositeRemoteProvisionPlanBuilder : IRemoteProvisionPlanBuilder
{
	private readonly IHostingProviderResolver _providerResolver;
	private readonly OpenStackRemoteProvisionPlanBuilder _openStackBuilder;
	private readonly GenericVpsRemoteProvisionPlanBuilder _genericVpsBuilder;

	public CompositeRemoteProvisionPlanBuilder(
		IHostingProviderResolver providerResolver,
		OpenStackRemoteProvisionPlanBuilder openStackBuilder,
		GenericVpsRemoteProvisionPlanBuilder genericVpsBuilder)
	{
		_providerResolver = providerResolver;
		_openStackBuilder = openStackBuilder;
		_genericVpsBuilder = genericVpsBuilder;
	}

	public RemoteProvisionPlan BuildRemotePlan(DapsConfig config, RemoteProvisionOptions options)
	{
		var provider = _providerResolver.ResolveExplicit(options.ProviderName);
		return provider.ConfigDefinition switch
		{
			OpenstackProviderDefinition => _openStackBuilder.BuildRemotePlan(config, options),
			GenericVpsProviderDefinition => _genericVpsBuilder.BuildRemotePlan(config, options),
			_ => throw new InvalidOperationException(
				$"Provider '{provider.ConfigDefinition.Name}' has no provisioning support."),
		};
	}
}

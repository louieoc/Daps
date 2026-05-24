using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class OpenStackUnprovisionPlanBuilder : IUnprovisionPlanBuilder
{
	public const string DefaultInstanceName = "daps-prod";

	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _providerResolver;

	public OpenStackUnprovisionPlanBuilder(
		IToolkitResolver toolkitResolver,
		IHostingProviderResolver providerResolver)
	{
		_toolkitResolver = toolkitResolver;
		_providerResolver = providerResolver;
	}

	public UnprovisionPlan BuildPlan(DapsConfig config, UnprovisionOptions options)
	{
		var hostingProvider = _providerResolver.Resolve(options.ProviderName);
		var provider = hostingProvider.ConfigDefinition as OpenstackProviderDefinition
			?? throw new InvalidOperationException("No OpenStack provider configured in daps.yaml.");
		var providerOptions = hostingProvider.Options as OpenStackProviderOptions
			?? throw new InvalidOperationException("Provider options are not OpenStack options.");

		var toolkitDef = _toolkitResolver.Resolve();
		var openRcContainerPath = _toolkitResolver.ToToolkitPath(provider.OpenRcPath);
		var instanceVarsContainerPath = _toolkitResolver.ToToolkitPath(providerOptions.InstanceVarsFilePath);

		return new UnprovisionPlan
		{
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			OpenRcContainerPath = openRcContainerPath,
			InstanceVarsContainerPath = instanceVarsContainerPath,
			DefaultInstanceName = DefaultInstanceName,
			DefaultKeyName = hostingProvider.KeyName,
			SshKeyToolkitPath = $"{toolkitDef.SshPath}/{hostingProvider.KeyName}",
			InstanceVarsFilePath = providerOptions.InstanceVarsFilePath,
		};
	}
}

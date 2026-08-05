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
		// ResolveExplicit, not Resolve: this destroys a VM and there is no project to infer a
		// provider from, so an omitted --provider must be an error rather than silently falling
		// back to the first provider in daps.yaml. Mirrors prod provision.
		var hostingProvider = _providerResolver.ResolveExplicit(options.ProviderName);
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

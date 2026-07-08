using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// This plan is for creating a remote openstack instance and installing docker on it
/// </summary>
public sealed class OpenStackRemoteProvisionPlanBuilder : IRemoteProvisionPlanBuilder
{
	public const string OpenStackCreateInstanceScript = "openstack-create-instance.sh";

	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _providerResolver;

	public OpenStackRemoteProvisionPlanBuilder(
		IToolkitResolver toolkitResolver,
		IHostingProviderResolver providerResolver)
	{
		_toolkitResolver = toolkitResolver;
		_providerResolver = providerResolver;
	}

	public RemoteProvisionPlan BuildRemotePlan(DapsConfig config, RemoteProvisionOptions options)
	{
		var hostingProvider = _providerResolver.ResolveExplicit(options.ProviderName);
		var provider = hostingProvider.ConfigDefinition as OpenstackProviderDefinition
			?? throw new InvalidOperationException("No OpenStack provider configured in daps.yaml.");
		var providerOptions = hostingProvider.Options as OpenStackProviderOptions
			?? throw new InvalidOperationException("Provider options are not OpenStack options.");

		var setVarsHostPath = ConfigUtils.RequireFile(
			options.SetVarsScriptPath is not null
				? ConfigUtils.ResolvePath(config.DapsRootPath, options.SetVarsScriptPath)
				: providerOptions.InstanceVarsFilePath,
			"OpenStack set-vars script not found.");

		var createScriptHostPath = ConfigUtils.RequireFile(
			options.CreateScriptPath is not null
				? ConfigUtils.ResolvePath(config.DapsRootPath, options.CreateScriptPath)
				: Path.Combine(config.DapsRootPath, "scripts", OpenStackCreateInstanceScript),
			"OpenStack create-prod script not found.");

		var toolkitDefinition = _toolkitResolver.Resolve();
		var openRcInContainer = _toolkitResolver.ToToolkitPath(provider.OpenRcPath);
		var setVarsInContainer = _toolkitResolver.ToToolkitPath(setVarsHostPath);
		var createScriptInContainer = _toolkitResolver.ToToolkitPath(createScriptHostPath);
		var defaultKeyName = hostingProvider.KeyName;

		var toolkitCommand = BuildToolkitCommand(
			provider.Name,
			defaultKeyName,
			openRcInContainer,
			setVarsInContainer,
			createScriptInContainer);

		return new RemoteProvisionPlan
		{
			ProviderName = provider.Name,
			DefaultKeyName = defaultKeyName,
			ToolkitContainerName = toolkitDefinition.ContainerName,
			ProviderDetails = new List<KeyValuePair<string, string>>
			{
				new("openrc script", provider.OpenRcPath),
				new("set-vars script", setVarsHostPath),
				new("create script", createScriptHostPath),
			},
			ToolkitCommand = toolkitCommand,
		};
	}

	private static string BuildToolkitCommand(
		string providerName,
		string defaultKeyName,
		string openRcInContainer,
		string setVarsInContainer,
		string createScriptInContainer)
	{
		return string.Join(" && ", new[]
		{
			$"source \"{openRcInContainer}\"",
			$"source \"{setVarsInContainer}\"",
			$"export OS_INSTANCE_VARS_FILE=\"{setVarsInContainer}\"",
			$"if [[ -z \"${{OS_KEY_NAME:-}}\" ]]; then export OS_KEY_NAME=\"{defaultKeyName}\"; fi",
			"mkdir -p ~/.ssh",
			"chmod 700 ~/.ssh",
			"if [[ ! -f \"$HOME/.ssh/${OS_KEY_NAME}\" ]]; then ssh-keygen -t rsa -b 4096 -N \"\" -f \"$HOME/.ssh/${OS_KEY_NAME}\"; fi",
			"if ! openstack keypair show \"$OS_KEY_NAME\" >/dev/null 2>&1; then openstack keypair create --public-key \"$HOME/.ssh/${OS_KEY_NAME}.pub\" \"$OS_KEY_NAME\" >/dev/null; fi",
			$"bash \"{createScriptInContainer}\"",
		});
	}
}

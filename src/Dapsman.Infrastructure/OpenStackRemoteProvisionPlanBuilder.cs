using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// This plan is for creating a remote openstack instance and installing docker on it
/// </summary>
public sealed class OpenStackRemoteProvisionPlanBuilder : IRemoteProvisionPlanBuilder
{
	public const string OpenStackCreateInstanceScript = "openstack-create-instance.sh";
	public const string ConfigureHostScript = "configure-host.sh";

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

		var configureScriptHostPath = ConfigUtils.RequireFile(
			Path.Combine(config.DapsRootPath, "scripts", ConfigureHostScript),
			"Host configuration script not found.");

		var toolkitDefinition = _toolkitResolver.Resolve();
		var openRcInContainer = _toolkitResolver.ToToolkitPath(provider.OpenRcPath);
		var setVarsInContainer = _toolkitResolver.ToToolkitPath(setVarsHostPath);
		var createScriptInContainer = _toolkitResolver.ToToolkitPath(createScriptHostPath);
		var defaultKeyName = hostingProvider.KeyName;

		// scp reads the staged copy, not the repo file: a Windows checkout can leave
		// CRLF endings in the working tree, which break the script once it runs on the
		// remote Linux host. The create script stays a repo path — it runs inside the
		// toolkit, whose bash tolerates CRLF, and it is never copied to the host.
		var stagedConfigureScript = $"{ToolkitRemoteProvisionExecutor.StagingToolkitPath}/{ConfigureHostScript}";

		var toolkitCommand = BuildToolkitCommand(
			provider.Name,
			defaultKeyName,
			openRcInContainer,
			setVarsInContainer,
			createScriptInContainer,
			stagedConfigureScript);

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
				new("configure script", configureScriptHostPath),
			},
			ToolkitCommand = toolkitCommand,
			DapsRootPath = config.DapsRootPath,
			ScriptFilesToStage = [configureScriptHostPath],
		};
	}

	private static string BuildToolkitCommand(
		string providerName,
		string defaultKeyName,
		string openRcInContainer,
		string setVarsInContainer,
		string createScriptInContainer,
		string configureScriptInContainer)
	{
		const string remoteConfigureScriptPath = "/tmp/configure-host.sh";
		const string sshOpts = "-o StrictHostKeyChecking=accept-new";
		const string keyPath = "\"$HOME/.ssh/${OS_KEY_NAME}\"";
		const string sshTarget = "\"${OS_SERVER_USER:-ubuntu}@${OS_SERVER_IP}\"";

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
			// The create script writes OS_SERVER_IP back to the vars file, and on a
			// first provision it was not set when we sourced it above. Re-source to
			// pick up the address of the instance we just created or reused.
			$"source \"{setVarsInContainer}\"",
			// A newly created instance is not accepting SSH yet. Reused instances
			// answer on the first attempt, so this costs nothing when re-provisioning.
			$"echo \"Waiting for SSH on ${{OS_SERVER_IP}}...\" && " +
				"for i in $(seq 1 60); do " +
				$"if ssh -o BatchMode=yes -o ConnectTimeout=5 {sshOpts} -i {keyPath} {sshTarget} true >/dev/null 2>&1; then break; fi; " +
				"sleep 5; " +
				"done",
			$"scp {sshOpts} -i {keyPath} \"{configureScriptInContainer}\" {sshTarget}:{remoteConfigureScriptPath}",
			$"ssh {sshOpts} -i {keyPath} {sshTarget} \"bash {remoteConfigureScriptPath} {ProviderDefinition.AutomaticRebootTime}\"",
		});
	}
}

using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// This plan is for installing docker (and swap) on a pre-existing VPS that Daps did not create.
/// </summary>
public sealed class GenericVpsRemoteProvisionPlanBuilder : IRemoteProvisionPlanBuilder
{
	public const string ProvisionGenericVpsScript = "provision-generic-vps.sh";
	private const string RemoteScriptPath = "/tmp/provision-generic-vps.sh";

	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _providerResolver;

	public GenericVpsRemoteProvisionPlanBuilder(
		IToolkitResolver toolkitResolver,
		IHostingProviderResolver providerResolver)
	{
		_toolkitResolver = toolkitResolver;
		_providerResolver = providerResolver;
	}

	public RemoteProvisionPlan BuildRemotePlan(DapsConfig config, RemoteProvisionOptions options)
	{
		var hostingProvider = _providerResolver.ResolveExplicit(options.ProviderName);
		_ = hostingProvider.ConfigDefinition as GenericVpsProviderDefinition
			?? throw new InvalidOperationException("No generic-vps provider configured in daps.yaml.");

		var toolkitDefinition = _toolkitResolver.Resolve();

		var provisionScriptHostPath = ConfigUtils.RequireFile(
			Path.Combine(config.DapsRootPath, "scripts", ProvisionGenericVpsScript),
			"Generic VPS provision script not found.");
		var provisionScriptInContainer = _toolkitResolver.ToToolkitPath(provisionScriptHostPath);

		var toolkitCommand = BuildToolkitCommand(
			hostingProvider.KeyName,
			hostingProvider.RemoteUser,
			hostingProvider.RemoteHost,
			provisionScriptInContainer,
			options.Upgrade);

		return new RemoteProvisionPlan
		{
			ProviderName = hostingProvider.ConfigDefinition.Name,
			DefaultKeyName = hostingProvider.KeyName,
			ToolkitContainerName = toolkitDefinition.ContainerName,
			ProviderDetails = new List<KeyValuePair<string, string>>
			{
				new("hostname", hostingProvider.RemoteHost),
				new("user", hostingProvider.RemoteUser),
			},
			ToolkitCommand = toolkitCommand,
		};
	}

	private static string BuildToolkitCommand(
		string keyName,
		string remoteUser,
		string remoteHost,
		string provisionScriptInContainer,
		bool upgrade)
	{
		var sshTarget = $"{remoteUser}@{remoteHost}";
		var sshOpts = "-o StrictHostKeyChecking=accept-new";
		var keyPath = $"\"$HOME/.ssh/{keyName}\"";
		var upgradeArg = upgrade ? " --upgrade" : "";

		return string.Join(" && ", new[]
		{
			"mkdir -p ~/.ssh",
			"chmod 700 ~/.ssh",
			$"if [[ ! -f {keyPath} ]]; then ssh-keygen -t rsa -b 4096 -N \"\" -f {keyPath}; fi",
			$"if ! ssh -o BatchMode=yes -o ConnectTimeout=5 {sshOpts} -i {keyPath} {sshTarget} true >/dev/null 2>&1; then " +
				$"ssh -t {sshOpts} {sshTarget} true || true; " +
				$"ssh-copy-id {sshOpts} -i {keyPath}.pub {sshTarget}; " +
				"fi",
			$"scp {sshOpts} -i {keyPath} \"{provisionScriptInContainer}\" {sshTarget}:{RemoteScriptPath}",
			$"ssh {sshOpts} -i {keyPath} {sshTarget} \"bash {RemoteScriptPath} {remoteUser}{upgradeArg}\"",
		});
	}
}

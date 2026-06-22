using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionLocalSyncFromRemotePlanBuilder : ISyncFromRemotePlanBuilder
{
	public const string SyncLocalFromRemote = "sync-remote-to-local.toolkit.sh";

	private readonly string? _providerName;
	private readonly IProjectResolver _projectResolver;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public ConventionLocalSyncFromRemotePlanBuilder(
		string? providerName,
		IToolkitResolver toolkitResolver,
		IProjectResolver projectResolver,
		IHostingProviderResolver hostingResolver)
	{
		_providerName = providerName;
		_toolkitResolver = toolkitResolver;
		_projectResolver = projectResolver;
		_hostingResolver = hostingResolver;
	}

	public PlanResult<SyncFromRemotePlan> BuildPlan(DapsConfig config, SyncFromRemoteOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);

		var syncScriptHostPath = Path.Combine(project.WorkstationScriptsPath, SyncLocalFromRemote);
		if (!File.Exists(syncScriptHostPath))
		{
			return PlanResult<SyncFromRemotePlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no sync script ({SyncLocalFromRemote}); sync-from-prod is not supported for this project.");
		}

		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName, project.Definition.Provider);
		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<SyncFromRemotePlan>.Supported(new SyncFromRemotePlan
		{
			ProjectName = project.Definition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyToolkitPath = $"{toolkitDef.SshPath}/{provider.KeyName}",
			SyncScriptToolkitPath = $"{project.ToolkitScriptsPath}/{SyncLocalFromRemote}",
		});
	}
}

using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionRemoteSyncFromLocalPlanBuilder : ISyncFromLocalPlanBuilder
{
	private const string SyncRemoteFromLocal = "sync-local-to-remote.toolkit.sh";

	private readonly string? _providerName;
	private readonly IProjectResolver _projectResolver;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public ConventionRemoteSyncFromLocalPlanBuilder(
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

	public PlanResult<SyncFromLocalPlan> BuildPlan(DapsConfig config, SyncFromLocalOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);

		var syncScriptHostPath = Path.Combine(project.WorkstationScriptsPath, SyncRemoteFromLocal);
		if (!File.Exists(syncScriptHostPath))
		{
			return PlanResult<SyncFromLocalPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no sync script ({SyncRemoteFromLocal}); sync-from-local is not supported for this project.");
		}

		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName, project.Definition.Provider);
		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<SyncFromLocalPlan>.Supported(new SyncFromLocalPlan
		{
			ProjectName = project.Definition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyToolkitPath = $"{toolkitDef.SshPath}/{provider.KeyName}",
			SyncScriptToolkitPath = $"{project.ToolkitScriptsPath}/{SyncRemoteFromLocal}",
		});
	}
}

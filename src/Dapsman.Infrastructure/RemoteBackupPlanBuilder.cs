using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RemoteBackupPlanBuilder : IRemoteBackupPlanBuilder
{
	public const string BackupRemoteToLocal = "backup-remote.toolkit.sh";

	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public RemoteBackupPlanBuilder(
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

	public PlanResult<RemoteBackupPlan> BuildPlan(DapsConfig config, BackupOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);

		var backupScriptHostPath = Path.Combine(project.WorkstationScriptsPath, BackupRemoteToLocal);
		if (!File.Exists(backupScriptHostPath))
		{
			return PlanResult<RemoteBackupPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no backup script ({BackupRemoteToLocal}); backup is not supported for this project.");
		}
		// todo: why do we have the cli provider twice? _providerName and options.ProviderName
		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName, project.Definition.Provider);
		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<RemoteBackupPlan>.Supported(new RemoteBackupPlan
		{
			ProjectName = project.Definition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyToolkitPath = $"{toolkitDef.SshPath}/{provider.KeyName}",
			BackupScriptToolkitPath = $"{project.ToolkitScriptsPath}/{BackupRemoteToLocal}",
			BackupDestinationToolkitPath = $"{project.ToolkitBackupsPath}/from_prod",
		});
	}
}

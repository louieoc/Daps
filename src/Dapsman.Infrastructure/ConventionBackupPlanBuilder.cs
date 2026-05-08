using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionBackupPlanBuilder : IBackupPlanBuilder
{
	public const string BackupRemoteToLocal = "backup-remote.toolkit.sh";

	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public ConventionBackupPlanBuilder(
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

	public PlanResult<BackupPlan> BuildPlan(DapsConfig config, BackupOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);

		var backupScriptHostPath = Path.Combine(project.WorkstationScriptsPath, BackupRemoteToLocal);
		if (!File.Exists(backupScriptHostPath))
		{
			return PlanResult<BackupPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no backup script ({BackupRemoteToLocal}); backup is not supported for this project.");
		}

		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName);
		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<BackupPlan>.Supported(new BackupPlan
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

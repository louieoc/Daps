using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class LocalBackupPlanBuilder : ILocalBackupPlanBuilder
{
	public const string BackupLocalScript = "backup-local.toolkit.sh";

	private readonly IToolkitResolver _toolkitResolver;

	public LocalBackupPlanBuilder(IToolkitResolver toolkitResolver)
	{
		_toolkitResolver = toolkitResolver;
	}

	public PlanResult<LocalBackupPlan> BuildPlan(DapsProject project, BackupOptions options)
	{
		var backupScriptHostPath = Path.Combine(project.WorkstationScriptsPath, BackupLocalScript);
		if (!File.Exists(backupScriptHostPath))
		{
			return PlanResult<LocalBackupPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no backup script ({BackupLocalScript}); local backup is not supported for this project.");
		}

		if (project.ToolkitPath is null)
		{
			return PlanResult<LocalBackupPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no toolkit bind mount defined (compose_daps_*.dev.yaml); cannot resolve toolkit paths.");
		}

		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<LocalBackupPlan>.Supported(new LocalBackupPlan
		{
			ProjectName = project.Definition.Name,
			ToolkitContainerName = toolkitDef.ContainerName,
			BackupScriptToolkitPath = $"{project.ToolkitScriptsPath}/{BackupLocalScript}",
			BackupDestinationToolkitPath = $"{project.ToolkitBackupsPath}/{BackupSources.FromLocal}",
		});
	}
}

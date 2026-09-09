using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class LocalBackupService
{
	private readonly ILocalBackupPlanBuilder _planBuilder;
	private readonly IBashRunner _bashRunner;

	public LocalBackupService(
		ILocalBackupPlanBuilder planBuilder,
		IBashRunner bashRunner)
	{
		_planBuilder = planBuilder;
		_bashRunner = bashRunner;
	}

	public PlanResult<LocalBackupPlan> CreatePlan(DapsProject project, BackupOptions options)
		=> _planBuilder.BuildPlan(project, options);

	public void Execute(LocalBackupPlan plan)
	{
		_bashRunner.RunScript(plan.BackupScriptToolkitPath, workingDirectory: "/",
			arguments: plan.ScriptArguments, env: plan.EnvVars);
	}
}

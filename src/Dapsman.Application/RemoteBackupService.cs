using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteBackupService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IRemoteBackupPlanBuilder _planBuilder;
	private readonly IBashRunner _bashRunner;

	public RemoteBackupService(
		IDapsConfigLoader configLoader,
		IRemoteBackupPlanBuilder planBuilder,
		IBashRunner bashRunner)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_bashRunner = bashRunner;
	}

	public PlanResult<RemoteBackupPlan> CreatePlan(string dapsYamlPath, BackupOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(RemoteBackupPlan plan)
	{
		_bashRunner.RunScript(plan.BackupScriptToolkitPath, plan.DapsRootPath,
			arguments: plan.ScriptArguments, env: plan.EnvVars);
	}
}

using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class BackupService
{
    private readonly IConfigLoader _configLoader;
    private readonly IBackupPlanBuilder _planBuilder;
    private readonly IBashRunner _bashRunner;

    public BackupService(
        IConfigLoader configLoader,
        IBackupPlanBuilder planBuilder,
        IBashRunner bashRunner)
    {
        _configLoader = configLoader;
        _planBuilder = planBuilder;
        _bashRunner = bashRunner;
    }

    public PlanResult<BackupPlan> CreatePlan(string dapsYamlPath, BackupOptions options)
    {
        var config = _configLoader.Load(dapsYamlPath);
        return _planBuilder.BuildPlan(config, options);
    }

    public void Execute(BackupPlan plan)
    {
        _bashRunner.RunScript(plan.BackupScriptToolkitPath, plan.DapsRootPath,
            arguments: plan.ScriptArguments, env: plan.EnvVars);
    }
}

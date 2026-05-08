using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class SyncFromLocalService
{
    private readonly IConfigLoader _configLoader;
    private readonly ISyncFromLocalPlanBuilder _planBuilder;
    private readonly IBashRunner _bashRunner;

    public SyncFromLocalService(
        IConfigLoader configLoader,
        ISyncFromLocalPlanBuilder planBuilder,
        IBashRunner bashRunner)
    {
        _configLoader = configLoader;
        _planBuilder = planBuilder;
        _bashRunner = bashRunner;
    }

    public PlanResult<SyncFromLocalPlan> CreatePlan(string dapsYamlPath, SyncFromLocalOptions options)
    {
        var config = _configLoader.Load(dapsYamlPath);
        return _planBuilder.BuildPlan(config, options);
    }

    public void Execute(SyncFromLocalPlan plan)
    {
        _bashRunner.RunScript(plan.SyncScriptToolkitPath, plan.DapsRootPath, env: plan.EnvVars);
    }
}

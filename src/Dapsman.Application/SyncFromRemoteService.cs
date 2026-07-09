using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class SyncFromRemoteService
{
    private readonly IDapsConfigLoader _configLoader;
    private readonly ISyncFromRemotePlanBuilder _planBuilder;
    private readonly IBashRunner _bashRunner;

    public SyncFromRemoteService(
        IDapsConfigLoader configLoader,
        ISyncFromRemotePlanBuilder planBuilder,
        IBashRunner bashRunner)
    {
        _configLoader = configLoader;
        _planBuilder = planBuilder;
        _bashRunner = bashRunner;
    }

    public PlanResult<SyncFromRemotePlan> CreatePlan(string dapsYamlPath, SyncFromRemoteOptions options)
    {
        var config = _configLoader.Load(dapsYamlPath);
        return _planBuilder.BuildPlan(config, options);
    }

    public void Execute(SyncFromRemotePlan plan)
    {
        _bashRunner.RunScript(plan.SyncScriptToolkitPath, plan.DapsRootPath, env: plan.EnvVars);
    }
}

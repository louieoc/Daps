using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteProvisionService
{
    private readonly IPrerequisiteChecker _prerequisiteChecker;
    private readonly IDapsConfigLoader _configLoader;
    private readonly IRemoteProvisionPlanBuilder _remotePlanBuilder;
    private readonly IBashRunner _bashRunner;

    public RemoteProvisionService(
        IPrerequisiteChecker prerequisiteChecker,
        IDapsConfigLoader configLoader,
        IRemoteProvisionPlanBuilder remotePlanBuilder,
        IBashRunner bashRunner)
    {
        _prerequisiteChecker = prerequisiteChecker;
        _configLoader = configLoader;
        _remotePlanBuilder = remotePlanBuilder;
        _bashRunner = bashRunner;
    }

    public RemoteProvisionPlan CreatePlan(string dapsYamlPath, RemoteProvisionOptions options)
    {
        _prerequisiteChecker.EnsureLocalBuildPrerequisites();
        var config = _configLoader.Load(dapsYamlPath);
        return _remotePlanBuilder.BuildRemotePlan(config, options);
    }

    public void Execute(RemoteProvisionPlan plan)
    {
        _bashRunner.RunShell(plan.ToolkitCommand, Environment.CurrentDirectory, interactive: true);
    }
}

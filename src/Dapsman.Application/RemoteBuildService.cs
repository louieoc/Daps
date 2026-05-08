using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteBuildService
{
    private readonly IPrerequisiteChecker _prerequisiteChecker;
    private readonly IConfigLoader _configLoader;
    private readonly IRemotePlanBuilder _remotePlanBuilder;
    private readonly IBashRunner _bashRunner;

    public RemoteBuildService(
        IPrerequisiteChecker prerequisiteChecker,
        IConfigLoader configLoader,
        IRemotePlanBuilder remotePlanBuilder,
        IBashRunner bashRunner)
    {
        _prerequisiteChecker = prerequisiteChecker;
        _configLoader = configLoader;
        _remotePlanBuilder = remotePlanBuilder;
        _bashRunner = bashRunner;
    }

    public RemoteBuildPlan CreatePlan(string dapsYamlPath, RemoteBuildOptions options)
    {
        _prerequisiteChecker.EnsureLocalBuildPrerequisites();
        var config = _configLoader.Load(dapsYamlPath);
        return _remotePlanBuilder.BuildRemotePlan(config, options);
    }

    public void Execute(RemoteBuildPlan plan)
    {
        _bashRunner.RunShell(plan.ToolkitCommand, Environment.CurrentDirectory, interactive: true);
    }
}

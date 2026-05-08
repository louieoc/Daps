using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteDeployService
{
	private readonly IPrerequisiteChecker _prerequisiteChecker;
	private readonly IConfigLoader _configLoader;
	private readonly IRemoteDeployPlanBuilder _remoteDeployPlanBuilder;
	private readonly IRemoteDeployExecutor _remoteDeployExecutor;

	public RemoteDeployService(
		IPrerequisiteChecker prerequisiteChecker,
		IConfigLoader configLoader,
		IRemoteDeployPlanBuilder remoteDeployPlanBuilder,
		IRemoteDeployExecutor remoteDeployExecutor)
	{
		_prerequisiteChecker = prerequisiteChecker;
		_configLoader = configLoader;
		_remoteDeployPlanBuilder = remoteDeployPlanBuilder;
		_remoteDeployExecutor = remoteDeployExecutor;
	}

	public RemoteDeployPlan CreatePlan(string dapsYamlPath, RemoteDeployOptions options)
	{
		_prerequisiteChecker.EnsureLocalBuildPrerequisites();
		var config = _configLoader.Load(dapsYamlPath);
		return _remoteDeployPlanBuilder.BuildRemoteDeployPlan(config, options);
	}

	public void ExecuteBuildImageScripts(RemoteDeployPlan plan)
	{
		_remoteDeployExecutor.ExecuteBuildImages(plan);
	}

	public void Execute(RemoteDeployPlan plan)
	{
		_remoteDeployExecutor.Execute(plan);
	}
}

using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteDeployService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IRemoteDeployPlanBuilder _remoteDeployPlanBuilder;
	private readonly IRemoteDeployExecutor _remoteDeployExecutor;

	public RemoteDeployService(
		IDapsConfigLoader configLoader,
		IRemoteDeployPlanBuilder remoteDeployPlanBuilder,
		IRemoteDeployExecutor remoteDeployExecutor)
	{
		_configLoader = configLoader;
		_remoteDeployPlanBuilder = remoteDeployPlanBuilder;
		_remoteDeployExecutor = remoteDeployExecutor;
	}

	public RemoteDeployPlan CreatePlan(string dapsYamlPath, RemoteDeployOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _remoteDeployPlanBuilder.BuildRemoteDeployPlan(config, options);
	}

	public void ExecuteBuildImageScripts(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath, string? targetPlatform)
	{
		_remoteDeployExecutor.ExecuteBuildImages(commands, dapsRootPath, targetPlatform);
	}

	public void Execute(RemoteDeployPlan plan)
	{
		_remoteDeployExecutor.Execute(plan);
	}
}

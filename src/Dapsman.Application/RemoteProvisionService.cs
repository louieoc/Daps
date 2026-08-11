using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteProvisionService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IRemoteProvisionPlanBuilder _remotePlanBuilder;
	private readonly IRemoteProvisionExecutor _executor;

	public RemoteProvisionService(
		IDapsConfigLoader configLoader,
		IRemoteProvisionPlanBuilder remotePlanBuilder,
		IRemoteProvisionExecutor executor)
	{
		_configLoader = configLoader;
		_remotePlanBuilder = remotePlanBuilder;
		_executor = executor;
	}

	public RemoteProvisionPlan CreatePlan(string dapsYamlPath, RemoteProvisionOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _remotePlanBuilder.BuildRemotePlan(config, options);
	}

	public void Execute(RemoteProvisionPlan plan)
	{
		_executor.Execute(plan);
	}
}

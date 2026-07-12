using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteProvisionService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IRemoteProvisionPlanBuilder _remotePlanBuilder;
	private readonly IBashRunner _bashRunner;

	public RemoteProvisionService(
		IDapsConfigLoader configLoader,
		IRemoteProvisionPlanBuilder remotePlanBuilder,
		IBashRunner bashRunner)
	{
		_configLoader = configLoader;
		_remotePlanBuilder = remotePlanBuilder;
		_bashRunner = bashRunner;
	}

	public RemoteProvisionPlan CreatePlan(string dapsYamlPath, RemoteProvisionOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _remotePlanBuilder.BuildRemotePlan(config, options);
	}

	public void Execute(RemoteProvisionPlan plan)
	{
		_bashRunner.RunShell(plan.ToolkitCommand, Environment.CurrentDirectory, interactive: true);
	}
}

using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RemoteTeardownService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IRemoteTeardownPlanBuilder _planBuilder;
	private readonly IRemoteTeardownExecutor _executor;

	public RemoteTeardownService(
		IDapsConfigLoader configLoader,
		IRemoteTeardownPlanBuilder planBuilder,
		IRemoteTeardownExecutor executor)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public RemoteTeardownPlan CreatePlan(string dapsYamlPath, TeardownOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(RemoteTeardownPlan plan)
	{
		_executor.Execute(plan);
	}
}

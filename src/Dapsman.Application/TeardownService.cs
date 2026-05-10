using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class TeardownService
{
	private readonly IConfigLoader _configLoader;
	private readonly ITeardownPlanBuilder _planBuilder;
	private readonly ITeardownExecutor _executor;

	public TeardownService(
		IConfigLoader configLoader,
		ITeardownPlanBuilder planBuilder,
		ITeardownExecutor executor)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public TeardownPlan CreatePlan(string dapsYamlPath, TeardownOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(TeardownPlan plan)
	{
		_executor.Execute(plan);
	}
}

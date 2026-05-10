using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class UnbuildService
{
	private readonly IConfigLoader _configLoader;
	private readonly IUnbuildPlanBuilder _planBuilder;
	private readonly IUnbuildExecutor _executor;

	public UnbuildService(
		IConfigLoader configLoader,
		IUnbuildPlanBuilder planBuilder,
		IUnbuildExecutor executor)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public UnbuildPlan CreatePlan(string dapsYamlPath, UnbuildOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(UnbuildPlan plan)
	{
		_executor.Execute(plan);
	}
}

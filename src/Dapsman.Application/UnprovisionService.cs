using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class UnprovisionService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly IUnprovisionPlanBuilder _planBuilder;
	private readonly IUnprovisionExecutor _executor;

	public UnprovisionService(
		IDapsConfigLoader configLoader,
		IUnprovisionPlanBuilder planBuilder,
		IUnprovisionExecutor executor)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public UnprovisionPlan CreatePlan(string dapsYamlPath, UnprovisionOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(UnprovisionPlan plan)
	{
		_executor.Execute(plan);
	}
}

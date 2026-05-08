using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class OfflineStatusService
{
	private readonly IConfigLoader _configLoader;
	private readonly IOfflineStatusPlanBuilder _planBuilder;
	private readonly IOfflineStatusExecutor _executor;

	public OfflineStatusService(
		IConfigLoader configLoader,
		IOfflineStatusPlanBuilder planBuilder,
		IOfflineStatusExecutor executor)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public PlanResult<OfflineStatusPlan> CreatePlan(string dapsYamlPath, OfflineStatusOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public void Execute(OfflineStatusPlan plan)
	{
		_executor.Execute(plan);
	}
}

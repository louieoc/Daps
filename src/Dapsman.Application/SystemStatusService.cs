using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class SystemStatusService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly ISystemStatusPlanBuilder _planBuilder;
	private readonly ISystemStatusCollector _collector;

	public SystemStatusService(
		IDapsConfigLoader configLoader,
		ISystemStatusPlanBuilder planBuilder,
		ISystemStatusCollector collector)
	{
		_configLoader = configLoader;
		_planBuilder = planBuilder;
		_collector = collector;
	}

	public SystemStatusPlan CreatePlan(string dapsYamlPath, SystemStatusOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _planBuilder.BuildPlan(config, options);
	}

	public SystemStatus Execute(SystemStatusPlan plan)
	{
		return _collector.Collect(plan);
	}
}

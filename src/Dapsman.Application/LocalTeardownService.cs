using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class LocalTeardownService(
	IDapsConfigLoader configLoader,
	ILocalTeardownPlanBuilder planBuilder,
	ILocalTeardownExecutor executor,
	IDapsYamlEditor yamlEditor)
{
	public LocalTeardownPlan CreatePlan(string dapsYamlPath, LocalTeardownOptions options)
	{
		var config = configLoader.Load(dapsYamlPath);
		return planBuilder.BuildPlan(config, options);
	}

	public void Execute(LocalTeardownPlan plan)
	{
		executor.Execute(plan);
		yamlEditor.RemoveProject(plan.DapsYamlPath, plan.ProjectName);
	}
}

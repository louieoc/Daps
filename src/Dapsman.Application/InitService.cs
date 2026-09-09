using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class InitService(
	IInitPlanBuilder planBuilder,
	IDapsYamlEditor yamlEditor)
{
	public InitPlan CreatePlan(InitOptions options)
	{
		return planBuilder.BuildInitPlan(options);
	}

	public void Execute(InitPlan plan)
	{
		// todo: maybe the manager logic should just live in here
		var manager = new TemplateManager(plan, yamlEditor);
		manager.CreateProjectFromTemplate();
	}
}

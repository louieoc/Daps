using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class CaddyRestartService(
	ICaddyRestartPlanBuilder planBuilder,
	IBashRunner bashRunner)
{
	public CaddyRestartPlan CreatePlan(string dapsYamlPath)
	{
		return planBuilder.BuildPlan();
	}

	public void Execute(CaddyRestartPlan plan)
	{
		bashRunner.RunShell(plan.ReloadCommand, plan.WorkingDirectory);
	}
}

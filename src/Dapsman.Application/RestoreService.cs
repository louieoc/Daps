using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class RestoreService
{
	private readonly IRestorePlanBuilder _planBuilder;
	private readonly IRestoreExecutor _executor;

	public RestoreService(
		IRestorePlanBuilder planBuilder,
		IRestoreExecutor executor)
	{
		_planBuilder = planBuilder;
		_executor = executor;
	}

	public PlanResult<RestorePlan> CreatePlan(DapsProject project, RestoreOptions options)
		=> _planBuilder.BuildPlan(project, options);

	public void Execute(RestorePlan plan)
		=> _executor.Execute(plan);
}

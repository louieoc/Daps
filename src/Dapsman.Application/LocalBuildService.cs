using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class LocalBuildService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly ILocalBuildPlanBuilder _composePlanBuilder;
	private readonly ICaddySiteSync _caddySiteSync;
	private readonly IDockerComposeExecutor _composeExecutor;
	private readonly IBashRunner _bashRunner;

	public LocalBuildService(
		IDapsConfigLoader configLoader,
		ILocalBuildPlanBuilder composePlanBuilder,
		ICaddySiteSync caddySiteSync,
		IDockerComposeExecutor composeExecutor,
		IBashRunner bashRunner)
	{
		_configLoader = configLoader;
		_composePlanBuilder = composePlanBuilder;
		_caddySiteSync = caddySiteSync;
		_composeExecutor = composeExecutor;
		_bashRunner = bashRunner;
	}

	public LocalBuildPlan CreatePlan(string dapsYamlPath, LocalBuildOptions options)
	{
		var config = _configLoader.Load(dapsYamlPath);
		return _composePlanBuilder.BuildLocalPlan(options);
	}

	public void SyncCaddySites(LocalBuildPlan plan)
	{
		_caddySiteSync.SyncLocalSites(plan.CaddySync);
	}

	public void ExecuteDapsCompose(LocalBuildPlan plan, LocalBuildOptions options)
	{
		if (plan.DapsComposeFiles.Count == 0)
		{
			return;
		}

		var workingDirectory = Path.GetDirectoryName(plan.DapsComposeFiles[0]) ?? Environment.CurrentDirectory;
		_composeExecutor.RunComposeUp(plan.DapsComposeFiles, options.BuildImages, workingDirectory);
	}

	public void ExecutePrerequisites(LocalProjectComposePlan projectPlan)
	{
		foreach (var script in projectPlan.PrerequisiteScripts)
		{
			_bashRunner.RunScript(script, projectPlan.ProjectPath);
		}
	}

	public void ExecuteProjectCompose(LocalProjectComposePlan projectPlan, LocalBuildOptions options)
	{
		_composeExecutor.RunComposeUp(projectPlan.ComposeFiles, options.BuildImages, projectPlan.ProjectPath);
	}
}

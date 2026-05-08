using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class LocalBuildService
{
	private readonly IPrerequisiteChecker _prerequisiteChecker;
	private readonly IConfigLoader _configLoader;
	private readonly ILocalPlanBuilder _composePlanBuilder;
	private readonly ICaddySiteSync _caddySiteSync;
	private readonly IComposeExecutor _composeExecutor;
	private readonly IBashRunner _bashRunner;

	public LocalBuildService(
		IPrerequisiteChecker prerequisiteChecker,
		IConfigLoader configLoader,
		ILocalPlanBuilder composePlanBuilder,
		ICaddySiteSync caddySiteSync,
		IComposeExecutor composeExecutor,
		IBashRunner bashRunner)
	{
		_prerequisiteChecker = prerequisiteChecker;
		_configLoader = configLoader;
		_composePlanBuilder = composePlanBuilder;
		_caddySiteSync = caddySiteSync;
		_composeExecutor = composeExecutor;
		_bashRunner = bashRunner;
	}

	public LocalBuildPlan CreatePlan(string dapsYamlPath, LocalBuildOptions options)
	{
		_prerequisiteChecker.EnsureLocalBuildPrerequisites();
		var config = _configLoader.Load(dapsYamlPath);
		return _composePlanBuilder.BuildLocalPlan(options);
	}

	public void SyncCaddySites(LocalBuildPlan plan)
	{
		_caddySiteSync.SyncDevSites(plan.CaddySync);
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

	public void ExecutePrerequisites(ProjectComposePlan projectPlan)
	{
		foreach (var script in projectPlan.PrerequisiteScripts)
		{
			_bashRunner.RunScript(script, projectPlan.ProjectPath);
		}
	}

	public void ExecuteProjectCompose(ProjectComposePlan projectPlan, LocalBuildOptions options)
	{
		_composeExecutor.RunComposeUp(projectPlan.ComposeFiles, options.BuildImages, projectPlan.ProjectPath);
	}
}

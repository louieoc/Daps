using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class LocalBuildService
{
	private readonly IDapsConfigLoader _configLoader;
	private readonly ILocalBuildPlanBuilder _composePlanBuilder;
	private readonly ICaddySiteSync _caddySiteSync;
	private readonly IDockerExecutor _dockerExecutor;
	private readonly IBashRunner _bashRunner;

	public LocalBuildService(
		IDapsConfigLoader configLoader,
		ILocalBuildPlanBuilder composePlanBuilder,
		ICaddySiteSync caddySiteSync,
		IDockerExecutor composeExecutor,
		IBashRunner bashRunner)
	{
		_configLoader = configLoader;
		_composePlanBuilder = composePlanBuilder;
		_caddySiteSync = caddySiteSync;
		_dockerExecutor = composeExecutor;
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

	/// <summary>
	/// The Daps and project compose files declare the shared network as external, so it has to exist
	/// before the first compose runs. On a fresh workstation nothing has created it yet.
	/// </summary>
	public void EnsureSharedNetwork(LocalBuildPlan plan, string workingDirectory)
	{
		_dockerExecutor.EnsureNetworkExists(plan.SharedNetworkName, workingDirectory);
	}

	public void ExecuteDapsCompose(LocalBuildPlan plan)
	{
		var workingDirectory = Path.GetDirectoryName(plan.DapsComposeFiles[0]) ?? Environment.CurrentDirectory;
		_dockerExecutor.RunDocker(plan.DapsComposeCommand, workingDirectory);
	}

	public void ExecutePrerequisites(LocalProjectComposePlan projectPlan)
	{
		foreach (var script in projectPlan.PrerequisiteScripts)
		{
			_bashRunner.RunScript(script, projectPlan.ProjectPath);
		}
	}

	public void ExecuteProjectComposeDown(LocalProjectComposePlan projectPlan)
	{
		var command = projectPlan.ComposeDownCommand ?? throw new InvalidOperationException(
			$"No down command was planned for '{projectPlan.ProjectName}'. This plan was not built with --rebuild."
		);

		_dockerExecutor.RunDocker(command, projectPlan.ProjectPath);
	}

	public void ExecuteProjectCompose(LocalProjectComposePlan projectPlan)
	{
		_dockerExecutor.RunDocker(projectPlan.ComposeUpCommand, projectPlan.ProjectPath);
	}
}

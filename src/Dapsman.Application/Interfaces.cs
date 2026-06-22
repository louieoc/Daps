using Dapsman.Domain;

namespace Dapsman.Application;

public interface IConfigLoader
{
	DapsConfig Load(string dapsYamlPath);
}

public interface ILocalPlanBuilder
{
	LocalBuildPlan BuildLocalPlan(LocalBuildOptions options);
}

public interface IPrerequisiteChecker
{
	void EnsureLocalBuildPrerequisites();
}

public interface ICaddySiteSync
{
	void SyncDevSites(CaddySyncPlan plan);
}

public interface IComposeExecutor
{
	void RunComposeUp(IReadOnlyList<string> composeFiles, bool buildImages, string workingDirectory);
}

public interface IRemoteProvisionPlanBuilder
{
	RemoteProvisionPlan BuildRemotePlan(DapsConfig config, RemoteProvisionOptions options);
}

public interface IRemoteDeployPlanBuilder
{
	RemoteDeployPlan BuildRemoteDeployPlan(DapsConfig config, RemoteDeployOptions options);
}

public interface IRemoteDeployExecutor
{
	void ExecuteBuildImages(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath);
	void Execute(RemoteDeployPlan plan);
}

public interface IBashRunner
{
	void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null);
	void RunShell(string shellExpression, string workingDirectory, bool interactive = false);

	/// <summary>Runs a script and returns its stdout. Stderr is not captured (still shown to user).</summary>
	string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null);
}

public interface ISyncFromLocalPlanBuilder
{
	PlanResult<SyncFromLocalPlan> BuildPlan(DapsConfig config, SyncFromLocalOptions options);
}

public interface ISyncFromRemotePlanBuilder
{
	PlanResult<SyncFromRemotePlan> BuildPlan(DapsConfig config, SyncFromRemoteOptions options);
}

public interface IBackupPlanBuilder
{
	PlanResult<BackupPlan> BuildPlan(DapsConfig config, BackupOptions options);
}

public interface IOfflineStatusPlanBuilder
{
	PlanResult<OfflineStatusPlan> BuildPlan(DapsConfig config, OfflineStatusOptions options);
}

public interface IOfflineStatusExecutor
{
	void Execute(OfflineStatusPlan plan);
}

public interface IInitPlanBuilder
{
	InitPlan BuildInitPlan(InitOptions options);
}

public interface ICaddyRestartPlanBuilder
{
	CaddyRestartPlan BuildPlan();
}

public interface ITeardownPlanBuilder
{
	TeardownPlan BuildPlan(DapsConfig config, TeardownOptions options);
}

public interface ITeardownExecutor
{
	void Execute(TeardownPlan plan);
}

public interface IUnprovisionPlanBuilder
{
	UnprovisionPlan BuildPlan(DapsConfig config, UnprovisionOptions options);
}

public interface IUnprovisionExecutor
{
	void Execute(UnprovisionPlan plan);
}

public interface ILocalTeardownPlanBuilder
{
	LocalTeardownPlan BuildPlan(DapsConfig config, LocalTeardownOptions options);
}

public interface ILocalTeardownExecutor
{
	void Execute(LocalTeardownPlan plan);
}

public interface IRestorePointsDiscoverer
{
	IReadOnlyList<RestorePoint> Discover(DapsProject project);
}

public interface IHostPortManager
{
	IReadOnlyList<HostPortBinding> GetBindings(string projectName, IEnumerable<string> devComposeFilePaths);
	IReadOnlyList<HostPortConflict> FindConflicts(IReadOnlyList<HostPortBinding> bindings);
	int FindNextAvailable(int preferredPort, IReadOnlyCollection<int> reservedPorts);
}

public interface IRestorePlanBuilder
{
	PlanResult<RestorePlan> BuildPlan(DapsProject project, RestoreOptions options);
}

public interface IRestoreExecutor
{
	void Execute(RestorePlan plan);
}

public interface IDapsYamlEditor
{
	void AddProject(string yamlPath, string projectName, string relativePath);
	void RemoveProject(string yamlPath, string projectName);
}

public interface IProjectResolver
{
	/// <summary>
	/// Return the named project or the default if none is named. The default applies if only one project is defined.
	/// </summary>
	/// <param name="projectName"></param>
	/// <returns></returns>
	DapsProject Resolve(string? projectName);

	/// <summary>
	/// Return the named projects or all projects is none are named.
	/// </summary>
	/// <param name="projectNames"></param>
	/// <returns></returns>
	IReadOnlyList<DapsProject> Resolve(IReadOnlyList<string>? projectNames);
}

public interface IHostingProviderResolver
{
	HostingProvider Resolve(string? providerName);
	HostingProvider Resolve(string? cliProviderName, string? projectProviderName);

	/// <summary>
	/// For host-level operations not tied to a project. Returns the single active provider when
	/// only one exists; requires an explicit name when multiple active providers are configured.
	/// </summary>
	HostingProvider ResolveExplicit(string? providerName);
}

public interface IContainerResolver
{
	string Resolve();
}

public interface IContainerManager
{
	IReadOnlyList<string> GetContainerNames();
}

public interface IToolkitResolver
{
	ToolkitDefinition Resolve();
	string ToToolkitPath(string hostPath);
}

public interface ICaddyResolver
{
	CaddyDefinition Resolve();
	ProjectCaddyDefinition ResolveForProject(DapsProject project);
}

public interface IDockerResolver
{
	DockerDefinition Resolve();
	ProjectDockerDefinition ResolveForProject(DapsProject project);
}
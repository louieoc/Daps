using Dapsman.Domain;

namespace Dapsman.Application;

public interface IDapsConfigLoader
{
	DapsConfig Load(string dapsYamlPath);
}

public interface ILocalBuildPlanBuilder
{
	LocalBuildPlan BuildLocalPlan(LocalBuildOptions options);
}

public interface ICaddySiteSync
{
	void SyncLocalSites(LocalCaddySyncPlan plan);
}

public interface IDockerExecutor
{
	void RunDocker(string arguments, string workingDirectory);
	void EnsureNetworkExists(string networkName, string workingDirectory);
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
	/// <param name="targetPlatform">
	/// Docker platform the built images must target, e.g. "linux/amd64", as reported by the remote
	/// host. Null leaves the build unpinned, which builds for the workstation's own architecture.
	/// </param>
	void ExecuteBuildImages(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath, string? targetPlatform);
	void Execute(RemoteDeployPlan plan);
}

public interface IRemoteProvisionExecutor
{
	void Execute(RemoteProvisionPlan plan);
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

public interface IRemoteBackupPlanBuilder
{
	PlanResult<RemoteBackupPlan> BuildPlan(DapsConfig config, BackupOptions options);
}

public interface ILocalBackupPlanBuilder
{
	PlanResult<LocalBackupPlan> BuildPlan(DapsProject project, BackupOptions options);
}

public interface IOfflineStatusPlanBuilder
{
	PlanResult<OfflineStatusPlan> BuildPlan(DapsConfig config, OfflineStatusOptions options);
}

public interface IOfflineStatusExecutor
{
	void Execute(OfflineStatusPlan plan);
}

public interface ISystemStatusPlanBuilder
{
	SystemStatusPlan BuildPlan(DapsConfig config, SystemStatusOptions options);
}

public interface ISystemStatusCollector
{
	SystemStatus Collect(SystemStatusPlan plan);
}

public interface IInitPlanBuilder
{
	InitPlan BuildInitPlan(InitOptions options);
}

public interface ICaddyRestartPlanBuilder
{
	CaddyRestartPlan BuildPlan();
}

public interface IRemoteTeardownPlanBuilder
{
	RemoteTeardownPlan BuildPlan(DapsConfig config, TeardownOptions options);
}

public interface IRemoteTeardownExecutor
{
	void Execute(RemoteTeardownPlan plan);
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

	/// <summary>
	/// Resolves a project that daps.yaml lists and whose local folder is present, and returns
	/// null otherwise. For workflows that can work from remote state alone — a project deleted
	/// from daps.yaml still has containers and files on the host that must be removable.
	/// </summary>
	DapsProject? TryResolve(string projectName);
}

public interface IHostingProviderResolver
{
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

public interface ITemplateConfigLoader
{
	TemplateConfig Load(string templateRoot);
}

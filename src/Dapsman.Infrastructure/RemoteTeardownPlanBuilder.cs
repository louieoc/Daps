using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RemoteTeardownPlanBuilder : IRemoteTeardownPlanBuilder
{
	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public RemoteTeardownPlanBuilder(
		string? providerName,
		IToolkitResolver toolkitResolver,
		IProjectResolver projectResolver,
		ICaddyResolver caddyResolver,
		IHostingProviderResolver hostingResolver)
	{
		_providerName = providerName;
		_toolkitResolver = toolkitResolver;
		_projectResolver = projectResolver;
		_caddyResolver = caddyResolver;
		_hostingResolver = hostingResolver;
	}

	public RemoteTeardownPlan BuildPlan(DapsConfig config, TeardownOptions options)
	{
		// Teardown targets a remote host by (provider, project name). A daps.yaml entry is an
		// enhancement — it is what lets us render the "site removed" page — not a precondition:
		// a project deleted from daps.yaml may still have containers and files on the host, and
		// this workflow can still remove them.
		var requestedName = ConfigUtils.RequireSafeProjectName(options.ProjectName);
		var cliProviderName = _providerName ?? options.ProviderName;
		var project = _projectResolver.TryResolve(requestedName);

		// The lookup ignores case but remote paths do not, so a registered project's own spelling
		// wins. Taking the typed one would miss its files and upload a second site file claiming
		// the same domain, which Caddy refuses to load.
		var projectName = project?.Definition.Name ?? requestedName;

		// With no project entry there is no configured provider to fall back on, and guessing is
		// the worst possible failure for a destructive workflow: Resolve(null, null) would take
		// the first non-disabled provider in file order and tear down a host nobody named.
		// ResolveExplicit takes the sole active provider, or demands --provider.
		var provider = project is not null
			? _hostingResolver.Resolve(cliProviderName, project.Definition.Provider)
			: _hostingResolver.ResolveExplicit(cliProviderName);

		var toolkitDef = _toolkitResolver.Resolve();

		return new RemoteTeardownPlan
		{
			ProjectName = projectName,
			DapsRootPath = config.DapsRootPath,
			ProviderName = provider.ConfigDefinition.Name,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyName = provider.KeyName,
			IsRoot = string.Equals(provider.RemoteUser, "root", StringComparison.OrdinalIgnoreCase),
			CaddySiteFileName = $"{projectName}.prod.caddy",
			RemovedCaddyContent = TryGenerateRemovedResponse(project),
			RemoteProjectPath = $"/srv/projects/{projectName}",
		};
	}

	/// <summary>
	/// The 410 page names the domain, which is only readable from the project's local prod caddy
	/// file. Returns null when there is no such file, which tells the executor to delete the
	/// remote site file rather than overwrite it.
	/// </summary>
	private string? TryGenerateRemovedResponse(DapsProject? project)
	{
		if (project is null)
			return null;

		var projectCaddyDef = _caddyResolver.ResolveForProject(project);
		if (string.IsNullOrEmpty(projectCaddyDef.WorkstationProdSiteFile))
			return null;

		var prodCaddyPath = Path.Combine(projectCaddyDef.ProjectSitesPath, projectCaddyDef.WorkstationProdSiteFile);
		if (!File.Exists(prodCaddyPath))
			return null;

		return GenerateRemovedResponse(ReadFirstDomain(prodCaddyPath));
	}

	private static string ReadFirstDomain(string prodCaddyPath)
	{
		foreach (var line in File.ReadLines(prodCaddyPath))
		{
			var trimmed = line.Trim();
			if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
				continue;
			return trimmed.Split(' ', '{')[0].Trim();
		}

		throw new InvalidOperationException($"Could not read domain from Caddy file '{prodCaddyPath}'.");
	}

	private static string GenerateRemovedResponse(string domain) =>
		$$"""
		{{domain}} {
			header Content-Type "text/html; charset=utf-8"
			respond `<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Site Removed</title><style>body{font-family:sans-serif;text-align:center;padding:4rem;color:#333}h1{font-size:2rem;margin-bottom:1rem}p{color:#666}</style></head><body><h1>Site Removed</h1><p>This site has been removed.</p></body></html>` 410
		}
		""";
}

using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionTeardownPlanBuilder : ITeardownPlanBuilder
{
	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public ConventionTeardownPlanBuilder(
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

	public TeardownPlan BuildPlan(DapsConfig config, TeardownOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);
		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName);
		var toolkitDef = _toolkitResolver.Resolve();
		var dockerPrefix = string.Equals(provider.RemoteUser, "root", StringComparison.OrdinalIgnoreCase) ? "docker" : "sudo docker";

		string? caddySiteFileName = null;
		string? removedContent = null;

		var projectCaddyDef = _caddyResolver.ResolveForProject(project);
		if (!string.IsNullOrEmpty(projectCaddyDef.WorkstationProdSiteFile))
		{
			var prodCaddyPath = Path.Combine(projectCaddyDef.ProjectSitesPath, projectCaddyDef.WorkstationProdSiteFile);
			if (File.Exists(prodCaddyPath))
			{
				caddySiteFileName = projectCaddyDef.WorkstationProdSiteFile;
				removedContent = GenerateRemovedResponse(ReadFirstDomain(prodCaddyPath));
			}
		}

		return new TeardownPlan
		{
			ProjectName = project.Definition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyName = provider.KeyName,
			DockerCommandPrefix = dockerPrefix,
			CaddySiteFileName = caddySiteFileName,
			RemovedCaddyContent = removedContent,
			RemoteProjectPath = $"/srv/projects/{project.Definition.Name}",
		};
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

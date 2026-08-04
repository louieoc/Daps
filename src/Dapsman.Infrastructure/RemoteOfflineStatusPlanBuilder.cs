using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RemoteOfflineStatusPlanBuilder : IOfflineStatusPlanBuilder
{
	private readonly bool _offline;
	private readonly string? _providerName;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IProjectResolver _projectResolver;
	private readonly IHostingProviderResolver _hostingResolver;

	public RemoteOfflineStatusPlanBuilder(
		bool offline,
		string? providerName,
		IToolkitResolver toolkitResolver,
		IProjectResolver projectResolver,
		ICaddyResolver caddyResolver,
		IHostingProviderResolver hostingResolver)
	{
		_offline = offline;
		_providerName = providerName;
		_toolkitResolver = toolkitResolver;
		_caddyResolver = caddyResolver;
		_projectResolver = projectResolver;
		_hostingResolver = hostingResolver;
	}

	public PlanResult<OfflineStatusPlan> BuildPlan(DapsConfig config, OfflineStatusOptions options)
	{
		var project = _projectResolver.Resolve(options.ProjectName);
		var projectCaddyDef = _caddyResolver.ResolveForProject(project);

		if (string.IsNullOrEmpty(projectCaddyDef.WorkstationProdSiteFile))
		{
			return PlanResult<OfflineStatusPlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no prod Caddy site file (*.prod.caddy); offline/online is not supported for this project.");
		}

		var prodCaddySitePath = Path.Combine(projectCaddyDef.ProjectSitesPath, projectCaddyDef.WorkstationProdSiteFile);
		if (!File.Exists(prodCaddySitePath))
		{
			return PlanResult<OfflineStatusPlan>.NotSupported(
				$"Project '{project.Definition.Name}' prod Caddy site file not found at '{prodCaddySitePath}'.");
		}

		var provider = _hostingResolver.Resolve(_providerName ?? options.ProviderName);
		var toolkitDef = _toolkitResolver.Resolve();
		var caddySiteContent = _offline ? BuildOfflineContent(projectCaddyDef, prodCaddySitePath) : File.ReadAllText(prodCaddySitePath);
		var dockerPrefix = string.Equals(provider.RemoteUser, "root", StringComparison.OrdinalIgnoreCase) ? "docker" : "sudo docker";

		return PlanResult<OfflineStatusPlan>.Supported(new OfflineStatusPlan
		{
			ProjectName = project.Definition.Name,
			DapsRootPath = config.DapsRootPath,
			ToolkitContainerName = toolkitDef.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyName = provider.KeyName,
			CaddySiteFileName = projectCaddyDef.WorkstationProdSiteFile,
			CaddySiteContent = caddySiteContent,
			DockerCommandPrefix = dockerPrefix,
			IsOffline = _offline,
		});
	}

	private static string BuildOfflineContent(ProjectCaddyDefinition projectCaddyDef, string prodCaddyPath)
	{
		var offlineCaddyPath = Path.Combine(projectCaddyDef.ProjectSitesPath, projectCaddyDef.OfflineCaddyFilename);
		if (File.Exists(offlineCaddyPath))
		{
			return File.ReadAllText(offlineCaddyPath);
		}

		var addresses = ReadSiteAddresses(prodCaddyPath);
		return GenerateOfflineResponse(string.Join(", ", addresses));
	}

	/// <summary>
	/// Collects every site address declared in a prod Caddy site file. A file may declare several
	/// site blocks (e.g. an apex domain plus a www redirect), and a single block may list several
	/// comma-separated addresses — all of them must appear in the offline config, or the missing
	/// domains fall through to Caddy's default handling while the project is offline.
	/// </summary>
	private static List<string> ReadSiteAddresses(string prodCaddyPath)
	{
		var addresses = new List<string>();
		var depth = 0;

		foreach (var line in File.ReadLines(prodCaddyPath))
		{
			var trimmed = line.Trim();
			if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
				continue;

			// A site block opener is a top-level line ending in '{'; everything before the brace
			// is its address list. Directive lines inside a block sit at depth 1 or deeper.
			if (depth == 0 && trimmed.EndsWith('{'))
			{
				var addressList = trimmed[..^1];
				foreach (var address in addressList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					if (!addresses.Contains(address, StringComparer.OrdinalIgnoreCase))
						addresses.Add(address);
				}
			}

			depth += trimmed.Count(c => c == '{') - trimmed.Count(c => c == '}');
			if (depth < 0)
				depth = 0;
		}

		if (addresses.Count == 0)
			throw new InvalidOperationException($"Could not read any site address from Caddy file '{prodCaddyPath}'.");

		return addresses;
	}

	private static string GenerateOfflineResponse(string addresses) =>
		$$"""
		{{addresses}} {
			header Content-Type "text/html; charset=utf-8"
			respond `<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Temporarily Offline</title><style>body{font-family:sans-serif;text-align:center;padding:4rem;color:#333}h1{font-size:2rem;margin-bottom:1rem}p{color:#666}</style></head><body><h1>Temporarily Offline</h1><p>This site is temporarily offline. Please check back soon.</p></body></html>` 503
		}
		""";
}

using Dapsman.Application;
using Dapsman.Domain;
using System.Text.RegularExpressions;

namespace Dapsman.Infrastructure;

public class ProjectResolver : IProjectResolver
{
	private readonly DapsConfig _config;

	public ProjectResolver(DapsConfig config)
	{
		_config = config;
	}

	public DapsProject Resolve(string? projectName)
	{
		var projectDefinition = ResolveProjectDefinition(_config, projectName);
		return GetDapsProject(projectDefinition);
	}

	public IReadOnlyList<DapsProject> Resolve(IReadOnlyList<string>? projectNames)
	{
		var projectDefinitions = SelectProjects(_config.Projects, projectNames);
		var projects = new List<DapsProject>();
		foreach (var p in projectDefinitions)
		{
			projects.Add(GetDapsProject(p));
		}
		return projects;
	}

	private static DapsProject GetDapsProject(ProjectDefinition projectDefinition)
	{
		var toolkitPath = ResolveToolkitPath(projectDefinition);
		var warnings = new List<string>();
		var localPrereqs = DiscoverLocalPrerequisiteScripts(projectDefinition);
		var workstationScriptsPath = Path.Combine(projectDefinition.Path, "_scripts");
		var scriptsToUpload = CollectRemoteScripts(workstationScriptsPath);

		var dp = new DapsProject
		{
			Definition = projectDefinition,
			WorkstationScriptsPath = workstationScriptsPath,
			WorkstationBackupsPath = Path.Combine(projectDefinition.Path, "_backups"),
			ScriptsToUploadToRemote = scriptsToUpload,
			ToolkitPath = toolkitPath,
			LocalPrerequisiteScripts = localPrereqs,
			Warnings = warnings
		};

		return dp;
	}

	private static IReadOnlyList<ProjectDefinition> SelectProjects(
		IReadOnlyList<ProjectDefinition> projects,
		IReadOnlyList<string>? filters)
	{
		if (filters is null || filters.Count == 0)
		{
			return projects;
		}

		var filterSet = new HashSet<string>(filters, StringComparer.OrdinalIgnoreCase);
		return projects.Where(p => filterSet.Contains(p.Name)).ToList();
	}

	private static ProjectDefinition ResolveProjectDefinition(DapsConfig config, string? projectName)
	{
		if (!string.IsNullOrWhiteSpace(projectName))
		{
			return FindProjectInConfig(config, projectName);
		}

		if (config.Projects.Count == 1) return config.Projects[0];
		if (config.Projects.Count > 1)
		{
			var names = string.Join(", ", config.Projects.Select(p => p.Name));
			throw new InvalidOperationException($"Multiple projects configured ({names}). Pass --project <name>.");
		}

		throw new InvalidOperationException("No projects configured in daps.yaml.");
	}

	private static ProjectDefinition FindProjectInConfig(DapsConfig config, string projectName)
	{
		return config.Projects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException($"Project '{projectName}' not found in daps.yaml.");
	}


	private static string? ResolveToolkitPath(ProjectDefinition project)
	{
		var bindMountPaths = ResolveToolkitBindMountPaths(project);
		string? mountTarget = null;

		foreach (var mount in bindMountPaths)
		{
			var trimmed = mount.Target.TrimEnd('/');
			if (trimmed.EndsWith(project.Name, StringComparison.OrdinalIgnoreCase))
			{
				mountTarget = trimmed;
				break;
			}
		}

		return mountTarget;
	}

	private static string? FindDapsComposeProjectExtension(ProjectDefinition project)
	{
		var tokens = BuildProjectTokens(project).ToList();
		var dockerRoot = Path.Combine(project.Path, "_docker");
		var candidates = tokens.Select(t => Path.Combine(dockerRoot, $"compose_daps_{t}.dev.yaml"))
			.Concat(tokens.Select(t => Path.Combine(dockerRoot, $"compose_daps_{t}.yaml")))
			.ToList();

		var composePath = ConfigUtils.FirstExisting(candidates);
		return composePath;
	}

	private static List<(string Source, string Target)> ResolveToolkitBindMountPaths(ProjectDefinition project)
	{
		// checking if the toolkit docker def is mounting anything
		var composePath = FindDapsComposeProjectExtension(project);
		if (composePath is null)
		{
			throw new InvalidOperationException(
				$"Project '{project.Name}' requires compose_daps_<project>.dev.yaml (or shared) to map project folders into toolkit.");
		}

		var paths = new List<(string Source, string Target)>();

		foreach (var rawLine in File.ReadLines(composePath))
		{
			var line = rawLine.Trim();
			if (!line.StartsWith("-", StringComparison.Ordinal))
				continue;

			var match = Regex.Match(line, @"^-\s*([^:]+):(/[^:]+)(?::[^:]+)?$");
			if (!match.Success)
				continue;

			var sourcePath = match.Groups[1].Value.Trim();
			var containerTarget = match.Groups[2].Value.Trim();
			paths.Add((sourcePath, containerTarget));
		}

		return paths;
	}

	private static IReadOnlyList<string> DiscoverLocalPrerequisiteScripts(ProjectDefinition project)
	{
		var scripts = new List<string>();
		var scriptsDir = Path.Combine(project.Path, "_scripts");

		var shared = Path.Combine(scriptsDir, "prerequisites.sh");
		if (File.Exists(shared)) scripts.Add(shared);

		var dev = Path.Combine(scriptsDir, "prerequisites.dev.sh");
		if (File.Exists(dev)) scripts.Add(dev);

		return scripts;
	}

	/// <summary>
	/// Collects scripts from _scripts/ that should be uploaded to the remote server.
	/// Includes *.prod.sh and bare *.sh (no environment segment).
	/// Excludes *.dev.sh and *.toolkit.sh — those are local/workstation-only.
	/// </summary>
	private static List<string> CollectRemoteScripts(string workstationScriptsPath)
	{
		if (!Directory.Exists(workstationScriptsPath))
		{
			return [];
		}

		return Directory.EnumerateFiles(workstationScriptsPath, "*.sh")
			.Where(f =>
			{
				var name = Path.GetFileName(f);
				return !name.EndsWith(".dev.sh", StringComparison.OrdinalIgnoreCase)
					&& !name.EndsWith(".toolkit.sh", StringComparison.OrdinalIgnoreCase);
			})
			.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static IEnumerable<string> BuildProjectTokens(ProjectDefinition project)
	{
		static string Sanitize(string value) =>
			Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", string.Empty);

		var tokens = new List<string>
		{
			project.Name,
			Sanitize(project.Name),
			Path.GetFileName(project.Path),
			Sanitize(Path.GetFileName(project.Path)),
		};

		return tokens
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Distinct(StringComparer.OrdinalIgnoreCase);
	}
}

using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public class DockerResolver : IDockerResolver
{
	private readonly DapsConfig _config;

	public DockerResolver(DapsConfig config)
	{
		_config = config;
	}

	public DockerDefinition Resolve()
	{
		var warnings = new List<string>();
		var devPaths = ResolveDapsComposeSelection(_config, warnings, "dev");
		var prodPaths = ResolveDapsComposeSelection(_config, warnings, "prod");

		return new DockerDefinition
		{
			LocalDapsComposeFilePaths = devPaths,
			RemoteDapsComposeFilePaths = prodPaths
		};
	}

	public ProjectDockerDefinition ResolveForProject(DapsProject project)
	{
		var warnings = new List<string>();
		var projectDockerPath = Path.Combine(project.Definition.Path, "_docker");
		var imageExportsFolder = Path.Combine(projectDockerPath, "image-exports");
		var imageExports = Directory.Exists(imageExportsFolder)
			? Directory.EnumerateFiles(imageExportsFolder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
			: [];

		var dev = "dev";
		var localDapsExtensions = ResolveProjectComposeSelection(project.Definition, "compose_daps", warnings, dev);
		var localComposeFiles = ResolveProjectComposeSelection(project.Definition, "compose", warnings, dev);

		var prod = "prod";
		var prodDapsExtensions = ResolveProjectComposeSelection(project.Definition, "compose_daps", warnings, prod);
		var prodComposeFiles = ResolveProjectComposeSelection(project.Definition, "compose", warnings, prod);

		return new ProjectDockerDefinition
		{
			ProjectDockerPath = projectDockerPath,
			LocalDapsExtensionComposeFiles = localDapsExtensions,
			LocalProjectComposeFiles = localComposeFiles,
			ProdDapsExtensionComposeFiles = prodDapsExtensions,
			ProdProjectComposeFiles = prodComposeFiles,
			ImageExportsFolder = imageExportsFolder,
			ImageExports = imageExports,
			Warnings = warnings
		};
	}

	private static List<string> ResolveDapsComposeSelection(DapsConfig config, List<string> warnings, string environment)
	{
		var supportedEnvs = new[] { "dev", "prod" };
		if (!supportedEnvs.Contains(environment))
		{
			throw new ArgumentOutOfRangeException(nameof(environment), "Only dev and prod are supported.");
		}

		var root = Path.Combine(config.DapsRootPath, "docker"); // Path.Combine(project.Path, "_docker");

		var shared = Path.Combine(root, "compose_daps.yaml");
		var dev = Path.Combine(root, "compose_daps.dev.yaml");
		var prod = Path.Combine(root, "compose_daps.prod.yaml");

		var hasShared = File.Exists(shared);
		var hasDev = File.Exists(dev);
		var hasProd = File.Exists(prod);

		if (!hasShared && !hasDev && !hasProd)
		{
			throw new FileNotFoundException(
				$"Daps has no compose files under '{root}'.");
		}

		var files = new List<string>();
		if (hasShared)
		{
			files.Add(shared!);
		}
		else
		{
			warnings.Add($"No shared Daps compose file ('{shared}') exists.");
		}

		if (hasDev && environment == "dev")
		{
			files.Add(dev!);
		}
		else
		{
			warnings.Add($"No local/dev Daps compose file ('{dev}') exists.");
		}

		if (hasProd && environment == "prod")
		{
			files.Add(prod!);
		}
		else
		{
			warnings.Add($"No prod Daps compose file ('{prod}') exists.");
		}

		return files;
	}

	private static List<string> ResolveProjectComposeSelection(ProjectDefinition project, string prefix, List<string> warnings, string environment)
	{
		var supportedEnvs = new[] { "dev", "prod" };
		if (!supportedEnvs.Contains(environment))
		{
			throw new ArgumentOutOfRangeException(nameof(environment), "Only dev and prod are supported.");
		}

		var tokens = ProjectResolver.BuildProjectTokens(project).ToList();
		var root = Path.Combine(project.Path, "_docker");
		var isDapsLinked = string.Equals(prefix, "compose_daps", StringComparison.OrdinalIgnoreCase);

		var sharedCandidates = tokens.Select(t => Path.Combine(root, $"{prefix}_{t}.yaml")).ToList();
		var devCandidates = tokens.Select(t => Path.Combine(root, $"{prefix}_{t}.dev.yaml")).ToList();
		var prodCandidates = tokens.Select(t => Path.Combine(root, $"{prefix}_{t}.prod.yaml")).ToList();

		var shared = ConfigUtils.FirstExisting(sharedCandidates);
		var dev = ConfigUtils.FirstExisting(devCandidates);
		var prod = ConfigUtils.FirstExisting(prodCandidates);

		var hasShared = shared is not null;
		var hasDev = dev is not null;
		var hasProd = prod is not null;

		if (!isDapsLinked && !hasShared && hasDev != hasProd)
		{
			throw new InvalidOperationException(
				$"Project '{project.Name}' has invalid env overlay set for prefix '{prefix}'. Both .dev.yaml and .prod.yaml are required when no shared base file exists.");
		}

		if (!hasShared && !hasDev && !hasProd)
		{
			throw new FileNotFoundException(
				$"Project '{project.Name}' missing compose files for prefix '{prefix}'. Expected {prefix}_<project>.yaml and optional env overlays {prefix}_<project>.dev.yaml + {prefix}_<project>.prod.yaml under '{root}'.");
		}

		if (isDapsLinked && !hasShared && !hasDev)
		{
			throw new InvalidOperationException(
				$"Project '{project.Name}' has invalid Daps local compose files for prefix '{prefix}'. A local build requires {prefix}_<project>.yaml or {prefix}_<project>.dev.yaml.");
		}

		if (hasShared)
		{
			WarnIfMultipleMatches(sharedCandidates, shared!, warnings, project.Name, prefix, "shared");
		}

		if (hasDev)
		{
			WarnIfMultipleMatches(devCandidates, dev!, warnings, project.Name, prefix, "dev");
		}

		if (hasProd)
		{
			WarnIfMultipleMatches(prodCandidates, prod!, warnings, project.Name, prefix, "prod");
		}

		var files = new List<string>();
		if (hasShared)
		{
			files.Add(shared!);
		}

		if (hasDev && environment == "dev")
		{
			files.Add(dev!);
		}

		if (hasProd && environment == "prod")
		{
			files.Add(prod!);
		}

		return files;
	}

	private static void WarnIfMultipleMatches(
		IReadOnlyList<string> candidates,
		string selected,
		List<string> warnings,
		string projectName,
		string prefix,
		string mode)
	{
		var matches = candidates.Where(File.Exists).ToList();
		if (matches.Count > 1)
		{
			warnings.Add($"Project '{projectName}' has multiple {mode} matches for prefix '{prefix}'. Using '{selected}'.");
		}
	}
}
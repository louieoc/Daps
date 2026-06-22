using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionRemoteDeployPlanBuilder : IRemoteDeployPlanBuilder
{
	private const string BuildDockerImages = "build-docker-images.toolkit.sh";
	private const string PostRemoteDeploy = "post-remote-deploy.toolkit.sh";

	private readonly IHostingProviderResolver _providerResolver;
	private readonly IToolkitResolver _toolkitResolver;
	private readonly IDockerResolver _dockerResolver;
	private readonly ICaddyResolver _caddyResolver;
	private readonly IProjectResolver _projectResolver;

	public ConventionRemoteDeployPlanBuilder(
		IHostingProviderResolver providerResolver,
		IDockerResolver dockerResolver,
		IToolkitResolver toolkitResolver,
		ICaddyResolver caddyResolver,
		IProjectResolver projectResolver)
	{
		_providerResolver = providerResolver;
		_dockerResolver = dockerResolver;
		_toolkitResolver = toolkitResolver;
		_caddyResolver = caddyResolver;
		_projectResolver = projectResolver;
	}

	public RemoteDeployPlan BuildRemoteDeployPlan(DapsConfig config, RemoteDeployOptions options)
	{
		var warnings = new List<string>();
		var selectedProjects = _projectResolver.Resolve(options.ProjectFilters);

		var inferredProjectProvider = InferProjectProvider(selectedProjects, options.ProviderName);
		var provider = _providerResolver.Resolve(options.ProviderName, inferredProjectProvider);
		var toolkitDefinition = _toolkitResolver.Resolve();
		var caddyDefinition = _caddyResolver.Resolve();
		if (config.Projects.Count > 0 && selectedProjects.Count == 0)
		{
			warnings.Add("Project filters did not match configured projects; Daps-only deploy will run.");
		}

		var dapsDockerDef = _dockerResolver.Resolve();
		var buildImageCommands = new List<BuildImageCommandPlan>();
		var caddySiteFilesToUpload = new List<CaddyUploadPlan>();
		var projectPlans = new List<RemoteProjectDeployPlan>();
		var expectedProdCaddyFileNames = caddyDefinition.AllWorkstationProdSiteFileNames;

		foreach (var project in selectedProjects)
		{
			// collect docker compose files and images (resolved first so HasExistingExports is available for build commands)
			var projectDocker = _dockerResolver.ResolveForProject(project);
			var composeSelection = new ProdComposeSelection(projectDocker.ProdProjectComposeFiles!);
			var imageExportsPath = projectDocker.ImageExportsFolder ?? string.Empty;
			var imageExports = projectDocker.ImageExports;
			if (!Directory.Exists(imageExportsPath))
			{
				warnings.Add($"Project '{project.Definition.Name}' is missing image export directory '{imageExportsPath}'.");
			}

			// collect build image commands
			var buildScriptPath = Path.Combine(project.WorkstationScriptsPath, BuildDockerImages);
			if (File.Exists(buildScriptPath))
			{
				buildImageCommands.Add(new BuildImageCommandPlan
				{
					ProjectName = project.Definition.Name,
					ToolkitScriptPath = $"{project.ToolkitScriptsPath}/{BuildDockerImages}",
					HasExistingExports = imageExports.Count > 0,
				});
			}
			else
			{
				warnings.Add($"Project '{project.Definition.Name}' is missing optional image build script '{buildScriptPath}'.");
			}

			// collect prod project caddy files
			var projectCaddyDef = _caddyResolver.ResolveForProject(project);
			if (Directory.Exists(projectCaddyDef.ProjectSitesPath))
			{
				foreach (var sourcePath in projectCaddyDef.WorkstationProdSiteFilePaths)
				{
					caddySiteFilesToUpload.Add(new CaddyUploadPlan
					{
						ProjectName = project.Definition.Name,
						SourcePath = sourcePath,
						DestinationFileName = Path.GetFileName(sourcePath),
					});
				}
			}

			// collect remote scripts
			var remoteScripts = project.ScriptsToUploadToRemote;

			// detect post-deploy hook
			var postDeployScriptWorkstationPath = Path.Combine(project.WorkstationScriptsPath, PostRemoteDeploy);
			var postDeployScriptPath = File.Exists(postDeployScriptWorkstationPath)
				? $"{project.ToolkitScriptsPath}/{PostRemoteDeploy}"
				: null;

			projectPlans.Add(new RemoteProjectDeployPlan
			{
				ProjectName = project.Definition.Name,
				ComposeFilesToUpload = composeSelection.UploadFiles,
				ComposeFileNamesForRemoteRun = composeSelection.UploadFiles.Select(f => Path.GetFileName(f)!).ToList(),
				ImageExportsSourcePath = imageExportsPath,
				ImageExportFilesToUpload = imageExports,
				UploadFiles = ParseProjectUploadManifest(project),
				RemoteScriptFilesToUpload = remoteScripts,
				PostDeployScriptPath = postDeployScriptPath,
			});
		}

		var caddyfileSourcePath = ConfigUtils.RequireFile(caddyDefinition.WorkstationCaddyFilePath, "Required Caddyfile not found.");
		var dapsComposeFiles = dapsDockerDef.RemoteDapsComposeFilePaths;
		var dockerPrefix = string.Equals(provider.RemoteUser, "root", StringComparison.OrdinalIgnoreCase) ? "docker" : "sudo docker";

		return new RemoteDeployPlan
		{
			DapsRootPath = config.DapsRootPath,
			ProviderName = provider.ConfigDefinition.Name,
			ToolkitContainerName = toolkitDefinition.ContainerName,
			RemoteHost = provider.RemoteHost,
			RemoteUser = provider.RemoteUser,
			SshKeyName = provider.KeyName,
			DockerCommandPrefix = dockerPrefix,
			BuildImageCommands = buildImageCommands,
			CaddyfileSourcePath = caddyfileSourcePath,
			CaddySiteFilesToUpload = caddySiteFilesToUpload,
			ExpectedProdCaddyFileNames = expectedProdCaddyFileNames,
			DapsComposeFilesToUpload = dapsComposeFiles,
			ProjectPlans = projectPlans,
			Warnings = warnings,
		};
	}

	private static string? InferProjectProvider(IReadOnlyList<DapsProject> selectedProjects, string? cliProviderName)
	{
		if (cliProviderName is not null)
		{
			return null;
		}

		var projectProviders = selectedProjects
			.Select(p => p.Definition.Provider)
			.Where(p => p is not null)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (projectProviders.Count > 1)
		{
			var groups = selectedProjects
				.Where(p => p.Definition.Provider is not null)
				.GroupBy(p => p.Definition.Provider, StringComparer.OrdinalIgnoreCase)
				.Select(g => $"{g.Key}: {string.Join(", ", g.Select(p => p.Definition.Name))}");
			throw new InvalidOperationException(
				"Projects target different providers. Specify --provider to disambiguate, " +
				"or target only projects that share a provider with --project:\n" +
				string.Join("\n", groups));
		}

		return projectProviders.FirstOrDefault();
	}

	private static IReadOnlyList<ProjectUploadFilePlan> ParseProjectUploadManifest(DapsProject project)
	{
		var manifestPath = Path.Combine(project.WorkstationScriptsPath, "deploy-prod-uploads");
		if (!File.Exists(manifestPath))
		{
			return Array.Empty<ProjectUploadFilePlan>();
		}

		var manifestDirectory = Path.GetDirectoryName(manifestPath)
			?? throw new InvalidOperationException($"Unable to resolve directory for '{manifestPath}'.");
		var uploads = new List<ProjectUploadFilePlan>();
		var lineNumber = 0;
		foreach (var rawLine in File.ReadLines(manifestPath))
		{
			lineNumber++;
			var line = ConfigUtils.StripTrailingComment(rawLine).Trim();
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			var (sourceRaw, destinationRaw) = ParseUploadLine(project.Definition.Name, manifestPath, lineNumber, line);
			var sourcePath = ConfigUtils.ResolvePath(manifestDirectory, sourceRaw);
			if (!File.Exists(sourcePath))
			{
				throw new FileNotFoundException(
					$"Project '{project.Definition.Name}' upload source file not found at line {lineNumber} in '{manifestPath}'.",
					sourcePath);
			}

			var attributes = File.GetAttributes(sourcePath);
			if (attributes.HasFlag(FileAttributes.Directory))
			{
				throw new InvalidOperationException(
					$"Project '{project.Definition.Name}' upload source must be a file (not directory) at line {lineNumber} in '{manifestPath}': '{sourcePath}'.");
			}

			if (!Path.IsPathRooted(destinationRaw) || !destinationRaw.StartsWith("/", StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					$"Project '{project.Definition.Name}' upload destination must be an absolute Linux path at line {lineNumber} in '{manifestPath}': '{destinationRaw}'.");
			}

			uploads.Add(new ProjectUploadFilePlan
			{
				SourcePath = sourcePath,
				RemoteDestinationPath = destinationRaw,
			});
		}

		return uploads;
	}

	private static (string Source, string Destination) ParseUploadLine(
		string projectName,
		string manifestPath,
		int lineNumber,
		string line)
	{
		var arrowIndex = line.IndexOf("->", StringComparison.Ordinal);
		if (arrowIndex >= 0)
		{
			var source = ConfigUtils.Unquote(line[..arrowIndex].Trim());
			var destination = ConfigUtils.Unquote(line[(arrowIndex + 2)..].Trim());
			if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
			{
				throw new InvalidOperationException(
					$"Project '{projectName}' has invalid upload mapping at line {lineNumber} in '{manifestPath}'.");
			}

			return (source, destination);
		}

		var tokens = TokenizeQuotedFields(line);
		if (tokens.Count != 2)
		{
			throw new InvalidOperationException(
				$"Project '{projectName}' has invalid upload mapping at line {lineNumber} in '{manifestPath}'. Use 'source -> destination' or '\"source\" \"destination\"'.");
		}

		return (tokens[0], tokens[1]);
	}

	private static IReadOnlyList<string> TokenizeQuotedFields(string input)
	{
		var tokens = new List<string>();
		var current = new System.Text.StringBuilder();
		var inSingle = false;
		var inDouble = false;

		foreach (var ch in input)
		{
			if (ch == '\'' && !inDouble)
			{
				inSingle = !inSingle;
				continue;
			}

			if (ch == '"' && !inSingle)
			{
				inDouble = !inDouble;
				continue;
			}

			if (char.IsWhiteSpace(ch) && !inSingle && !inDouble)
			{
				if (current.Length > 0)
				{
					tokens.Add(current.ToString());
					current.Clear();
				}

				continue;
			}

			current.Append(ch);
		}

		if (inSingle || inDouble)
		{
			throw new InvalidOperationException("Unbalanced quotes in deploy-prod-uploads manifest.");
		}

		if (current.Length > 0)
		{
			tokens.Add(current.ToString());
		}

		return tokens;
	}

	private sealed record ProdComposeSelection(IReadOnlyList<string> UploadFiles);
}

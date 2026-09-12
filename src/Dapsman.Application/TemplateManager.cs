using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class TemplateManager
{
	private readonly InitPlan _plan;
	private readonly IDapsYamlEditor _yamlEditor;

	private readonly string[] _filesToSkipCopying = new []{ "template.yaml" };
	private readonly string[] _dirsToSkipCopying = new []{ "_secrets" };

	/// <summary>
	/// Copied verbatim, never transformed, for every template — not opt-in via template.yaml.
	/// Scripts take the project name from the DAPS_PROJECT environment variable, so a project's
	/// _scripts stay byte-identical to the template's and can be updated by copying them forward.
	/// </summary>
	private readonly string[] _dirsNeverTransformed = new []{ "_scripts" };

	private string[] _excludedSourceDirs;
	private string[] _dirsToSkipCopyingFull;
	private string[] _dirsNeverTransformedFull;

	public TemplateManager(InitPlan plan, IDapsYamlEditor yamlEditor)
	{
		_plan = plan;
		_yamlEditor = yamlEditor;

		_excludedSourceDirs = _plan.ExcludedFromTemplateTransform.Select(d => Path.Combine(_plan.TemplatePath, d)).ToArray();
		_dirsToSkipCopyingFull = _dirsToSkipCopying.Select(d => Path.Combine(_plan.TemplatePath, d)).ToArray();
		_dirsNeverTransformedFull = _dirsNeverTransformed.Select(d => Path.Combine(_plan.TemplatePath, d)).ToArray();
	}

	public void CreateProjectFromTemplate()
	{
		if (!_plan.Overlay && Directory.Exists(_plan.DestinationPath))
			throw new InvalidOperationException($"Destination already exists: {_plan.DestinationPath}");

		CopyTemplateDirectories();
		CopyAndTransformTemplateFiles();

		_yamlEditor.AddProject(_plan.DapsYamlPath, _plan.ProjectName, _plan.DapsYamlProjectRelativePath);
	}

	private void CopyTemplateDirectories()
	{
		foreach (var sourceDir in Directory.GetDirectories(_plan.TemplatePath, "*", SearchOption.AllDirectories))
		{
			if (_dirsToSkipCopyingFull.Any(d => IsUnder(sourceDir, d)))
			{
				continue;
			}
			var relative = Path.GetRelativePath(_plan.TemplatePath, sourceDir);
			Directory.CreateDirectory(Path.Combine(_plan.DestinationPath, relative));
		}
	}

	private void CopyAndTransformTemplateFiles()
	{
		var caddySitesDir = Path.Combine(_plan.DestinationPath, "_caddy_sites");
		var dockerDir = Path.Combine(_plan.DestinationPath, "_docker");

		foreach (var sourceFile in Directory.GetFiles(_plan.TemplatePath, "*", SearchOption.AllDirectories))
		{
			var fileInfo = new FileInfo(sourceFile);
			if (_filesToSkipCopying.Any(f => f.Equals(fileInfo.Name, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			if (fileInfo.Directory is not null && _dirsToSkipCopyingFull.Any(d => IsUnder(fileInfo.Directory.FullName, d)))
			{
				continue;
			}
			var relative = Path.GetRelativePath(_plan.TemplatePath, sourceFile);
			var targetFile = Path.Combine(_plan.DestinationPath, relative);

			// ensure target filenames have placeholder replaced with project name
			targetFile = ReplacePlaceholderInFilename(targetFile, _plan.PlaceholderText, _plan.ProjectName);

			var targetDirectory = Path.GetDirectoryName(targetFile);
			Directory.CreateDirectory(targetDirectory!);

			if (_plan.Overlay && File.Exists(targetFile))
				continue;

			if (Utils.IsBinary(sourceFile))
			{
				File.Copy(sourceFile, targetFile);
				continue;
			}

			var content = File.ReadAllText(sourceFile);
			
			if (!IsInExcludedDirectory(fileInfo))
			{
				content = ReplacePlaceholderText(content, _plan.ProjectName, _plan.PlaceholderText);
			}

			if (targetDirectory == caddySitesDir)
			{
				if (fileInfo.Name.EndsWith(".prod.caddy"))
				{
					content = ApplyCaddyProdUrl(content, _plan.ProjectName, _plan.ProdUrl);
				}
			}

			if (targetDirectory == dockerDir)
			{
				if (fileInfo.Name.EndsWith(".dev.yaml"))
				{
					content = ApplyDockerDevPortChanges(content, _plan.DevPortAssignments);
				}

				if (fileInfo.Name.EndsWith(".prod.yaml"))
				{
					content = ApplyDockerProdUrl(content, _plan.ProjectName, _plan.ProdUrl);
				}
			}
			
			WriteAllText(targetFile, content);
		}
	}

	private bool IsInExcludedDirectory(FileInfo file)
	{
		if (file.DirectoryName is null) return false;

		return _excludedSourceDirs.Concat(_dirsNeverTransformedFull).Any(d => IsUnder(file.DirectoryName, d));
	}

	public static string ReplacePlaceholderInFilename(string filePath, string? placeholder, string projectName)
	{
		if (placeholder is null) return filePath;

		var info = new FileInfo(filePath);
		var modified = info.Name.Replace(placeholder, projectName);
		var targetFolder = info.Directory?.FullName;
		return targetFolder is null ? modified : Path.Combine(targetFolder, modified);
	}

	public static string ReplacePlaceholderText(string fileContent, string replacementText, string? placeholderText)
	{
		if (placeholderText is null)
			return fileContent;

		var modified = fileContent.Replace(placeholderText, replacementText);
		return modified;
	}

	public static string ApplyDockerDevPortChanges(string fileContent, IReadOnlyList<PortAssignment> devPortChanges)
	{
		var changes = devPortChanges.Where(a => a.AssignedPort != a.OriginalPort).ToList();
		if (changes.Count == 0)
			return fileContent;

		var modified = fileContent;
		foreach (var change in changes)
			modified = modified.Replace(
				$"127.0.0.1:{change.OriginalPort}:",
				$"127.0.0.1:{change.AssignedPort}:");

		return modified;
	}

	public static string ApplyDockerProdUrl(string fileContent, string projectName, string? prodUrl)
	{
		if (prodUrl is null)
			return fileContent;

		var placeholder = $"{projectName}.example.com";
		var httpsPlaceholder = $"https://{placeholder}";
		var httpsUrl = $"https://{prodUrl}";

		if (!fileContent.Contains(httpsPlaceholder))
			return fileContent;

		var modified = fileContent.Replace(httpsPlaceholder, httpsUrl);
		return modified;
	}

	private static string ApplyCaddyProdUrl(string fileContent, string projectName, string? prodUrl)
	{
		if (prodUrl is null)
			return fileContent;

		var placeholder = $"{projectName}.example.com";
		if (!fileContent.Contains(placeholder))
			return fileContent;

		var modified = fileContent.Replace(placeholder, prodUrl);
		return modified;
	}

	private static bool IsUnder(string path, string root)
	{
		// GetFullPath normalizes the separator, so an exclusion written as "wp-content/uploads"
		// in template.yaml still matches a Windows path using backslashes.
		path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
		root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
		return path.Equals(root, StringComparison.OrdinalIgnoreCase)
			|| path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	private static void WriteAllText(string path, string contents)
		=> File.WriteAllText(path, contents.Replace("\r\n", "\n"));
}

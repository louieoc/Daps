using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class InitService(
	IInitPlanBuilder planBuilder,
	IBashRunner bashRunner)
{
	public InitPlan CreatePlan(InitOptions options)
	{
		return planBuilder.BuildInitPlan(options);
	}

	public void Execute(InitPlan plan)
	{
		CopyTemplateDirectory(plan.TemplatePath, plan.DestinationPath);

		if (plan.InitScriptPath is not null)
		{
			var scriptInDestination = Path.Combine(
				plan.DestinationPath,
				Path.GetRelativePath(plan.TemplatePath, plan.InitScriptPath));
			bashRunner.RunScript(scriptInDestination, plan.DestinationPath, plan.ProjectName);
		}

		RegisterInDapsYaml(plan);
	}

	private static void CopyTemplateDirectory(string sourcePath, string destinationPath)
	{
		if (Directory.Exists(destinationPath))
			throw new InvalidOperationException($"Destination already exists: {destinationPath}");

		foreach (var sourceDir in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(sourcePath, sourceDir);
			var targetDir = Path.Combine(destinationPath, relative);
			Directory.CreateDirectory(targetDir);
		}

		foreach (var sourceFile in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(sourcePath, sourceFile);
			var targetFile = Path.Combine(destinationPath, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
			File.Copy(sourceFile, targetFile);
		}
	}

	private static void RegisterInDapsYaml(InitPlan plan)
	{
		var yaml = File.Exists(plan.DapsYamlPath) ? File.ReadAllText(plan.DapsYamlPath) : string.Empty;

		// Guard: if this project name is already registered, don't write a duplicate entry.
		// We check for the YAML key pattern to avoid false matches on path values or comments.
		if (yaml.Contains($"  {plan.ProjectName}:"))
			throw new InvalidOperationException($"Project '{plan.ProjectName}' is already registered in {plan.DapsYamlPath}.");

		// Append the new project entry. We use a simple text append rather than full yaml
		// parsing to avoid a dependency on a yaml library for this one write path.
		var entry = $"\n  {plan.ProjectName}:\n    path: {plan.DapsYamlProjectRelativePath}\n";

		if (yaml.Contains("projects:"))
		{
			File.AppendAllText(plan.DapsYamlPath, entry);
		}
		else
		{
			File.AppendAllText(plan.DapsYamlPath, $"\nprojects:{entry}");
		}
	}
}

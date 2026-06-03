using Dapsman.Domain;

namespace Dapsman.Application;

public sealed class InitService(
	IInitPlanBuilder planBuilder,
	IBashRunner bashRunner,
	IDapsYamlEditor yamlEditor)
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

		yamlEditor.AddProject(plan.DapsYamlPath, plan.ProjectName, plan.DapsYamlProjectRelativePath);
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
}

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

		if (plan.ProdUrl is not null)
			ApplyProdUrl(plan.DestinationPath, plan.ProjectName, plan.ProdUrl);
	}

	private static void ApplyProdUrl(string destinationPath, string projectName, string prodUrl)
	{
		var caddySitesDir = Path.Combine(destinationPath, "_caddy_sites");
		if (!Directory.Exists(caddySitesDir)) return;

		var placeholder = $"{projectName}.example.com";
		foreach (var file in Directory.GetFiles(caddySitesDir, "*.prod.caddy"))
		{
			var content = File.ReadAllText(file);
			if (content.Contains(placeholder))
				File.WriteAllText(file, content.Replace(placeholder, prodUrl));
		}
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

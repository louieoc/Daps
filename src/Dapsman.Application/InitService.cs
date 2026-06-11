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
		CopyTemplateDirectory(plan.TemplatePath, plan.DestinationPath, plan.Overlay);

		if (plan.InitScriptPath is not null)
		{
			var scriptInDestination = Path.Combine(
				plan.DestinationPath,
				Path.GetRelativePath(plan.TemplatePath, plan.InitScriptPath));
			var scriptArgs = plan.Overlay ? $"{plan.ProjectName} --overlay" : plan.ProjectName;
			bashRunner.RunScript(scriptInDestination, plan.DestinationPath, scriptArgs);
		}

		yamlEditor.AddProject(plan.DapsYamlPath, plan.ProjectName, plan.DapsYamlProjectRelativePath);

		if (plan.ProdUrl is not null)
			ApplyProdUrl(plan.DestinationPath, plan.ProjectName, plan.ProdUrl);

		var portChanges = plan.DevPortAssignments.Where(a => a.AssignedPort != a.OriginalPort).ToList();
		if (portChanges.Count > 0)
			ApplyDevPorts(plan.DestinationPath, portChanges);
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

	private static void ApplyDevPorts(string destinationPath, IReadOnlyList<PortAssignment> changes)
	{
		var dockerDir = Path.Combine(destinationPath, "_docker");
		if (!Directory.Exists(dockerDir)) return;

		foreach (var file in Directory.GetFiles(dockerDir, "*.dev.yaml"))
		{
			var content = File.ReadAllText(file);
			var modified = content;
			foreach (var change in changes)
				modified = modified.Replace(
					$"127.0.0.1:{change.OriginalPort}:",
					$"127.0.0.1:{change.AssignedPort}:");
			if (!ReferenceEquals(modified, content))
				File.WriteAllText(file, modified);
		}
	}

	private static void CopyTemplateDirectory(string sourcePath, string destinationPath, bool overlay)
	{
		if (overlay)
		{
			// Destination must already exist; copy only files that don't yet exist there.
			foreach (var sourceDir in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(sourcePath, sourceDir);
				Directory.CreateDirectory(Path.Combine(destinationPath, relative));
			}

			foreach (var sourceFile in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(sourcePath, sourceFile);
				var targetFile = Path.Combine(destinationPath, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				if (!File.Exists(targetFile))
					File.Copy(sourceFile, targetFile);
			}
		}
		else
		{
			if (Directory.Exists(destinationPath))
				throw new InvalidOperationException($"Destination already exists: {destinationPath}");

			foreach (var sourceDir in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(sourcePath, sourceDir);
				Directory.CreateDirectory(Path.Combine(destinationPath, relative));
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
}

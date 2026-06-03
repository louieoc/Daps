using Dapsman.Application;

namespace Dapsman.Infrastructure;

public sealed class DapsYamlEditor : IDapsYamlEditor
{
	public void AddProject(string yamlPath, string projectName, string relativePath)
	{
		var yaml = File.Exists(yamlPath) ? File.ReadAllText(yamlPath) : string.Empty;

		if (yaml.Contains($"  {projectName}:"))
			throw new InvalidOperationException($"Project '{projectName}' is already registered in {yamlPath}.");

		var nl = yaml.Contains("\r\n") ? "\r\n" : "\n";
		var entry = $"{nl}  {projectName}:{nl}    path: {relativePath}{nl}";

		if (yaml.Contains("projects:"))
			File.AppendAllText(yamlPath, entry);
		else
			File.AppendAllText(yamlPath, $"{nl}projects:{entry}");
	}

	public void RemoveProject(string yamlPath, string projectName)
	{
		if (!File.Exists(yamlPath))
			return;

		var lines = File.ReadAllLines(yamlPath);
		var keyLine = $"  {projectName}:";

		var result = new List<string>();
		var found = false;
		var inBlock = false;

		foreach (var line in lines)
		{
			if (line.TrimEnd() == keyLine)
			{
				found = true;
				inBlock = true;
				continue;
			}

			if (inBlock)
			{
				if (line.StartsWith("    "))
					continue;
				inBlock = false;
			}

			result.Add(line);
		}

		if (!found)
			throw new InvalidOperationException($"Project '{projectName}' not found in {yamlPath}.");

		File.WriteAllLines(yamlPath, result);
	}
}

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
		var entry = $"  {projectName}:{nl}    path: {relativePath}{nl}";

		// Trim trailing whitespace before appending. Appending a newline-prefixed entry to a file that
		// already ends in a newline leaves a blank line, and RemoveProject doesn't take it back out, so
		// every init/teardown cycle grew the gap above the newest entry.
		var body = yaml.TrimEnd();
		var prefix = body.Length == 0 ? string.Empty : body + nl;

		if (yaml.Contains("projects:"))
			File.WriteAllText(yamlPath, prefix + entry);
		else
			File.WriteAllText(yamlPath, $"{prefix}projects:{nl}{entry}");
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

		while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
			result.RemoveAt(result.Count - 1);

		File.WriteAllLines(yamlPath, result);
	}
}

using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class DapsYamlConfigLoader : IConfigLoader
{
	public DapsConfig Load(string dapsYamlPath)
	{
		if (!File.Exists(dapsYamlPath))
		{
			throw new FileNotFoundException("Could not find daps.yaml.", dapsYamlPath);
		}

		var fullDapsYamlPath = Path.GetFullPath(dapsYamlPath);
		var dapsRoot = Path.GetDirectoryName(fullDapsYamlPath)
			?? throw new InvalidOperationException("Could not determine Daps root path.");

		var providerDefinitions = new List<ProviderDefinition>();
		var projects = new List<ProjectDefinition>();

		string? section = null;
		string? currentProviderName = null;
		string? currentProviderType = null;

		string? pendingProjectName = null;
		string? pendingProjectPath = null;
		var pendingProjectDisabled = false;

		void FlushPendingProject()
		{
			if (pendingProjectName is not null && pendingProjectPath is not null)
			{
				projects.Add(new ProjectDefinition
				{
					Name = pendingProjectName,
					Path = pendingProjectPath,
					Disabled = pendingProjectDisabled,
				});
			}
			pendingProjectName = null;
			pendingProjectPath = null;
			pendingProjectDisabled = false;
		}

		foreach (var rawLine in File.ReadLines(fullDapsYamlPath))
		{
			var line = ConfigUtils.StripComment(rawLine);
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			var indent = ConfigUtils.CountLeadingSpaces(line);
			var trimmed = line.Trim();

			if (indent == 0)
			{
				FlushPendingProject();
				currentProviderName = null;
				currentProviderType = null;

				if (!trimmed.EndsWith(':'))
				{
					continue;
				}

				section = trimmed.TrimEnd(':').Trim();
				continue;
			}

			if (section == "providers")
			{
				if (indent == 2 && ConfigUtils.TryParseKeyValue(trimmed, out var providerName, out var providerType))
				{
					currentProviderName = providerName;
					currentProviderType = providerType;
					continue;
				}

				if (indent == 4 && currentProviderName is not null && ConfigUtils.TryParseKeyValue(trimmed, out var key, out var value))
				{
					if (currentProviderType == "openstack" && key == "openrc")
					{
						providerDefinitions.Add(new OpenstackProviderDefinition
						{
							Name = currentProviderName,
							OpenRcPath = ConfigUtils.ResolvePath(dapsRoot, value),
						});
					}
				}

				continue;
			}

			if (section == "projects")
			{
				if (indent == 2 && trimmed.EndsWith(':'))
				{
					FlushPendingProject();
					pendingProjectName = trimmed.TrimEnd(':').Trim();
					continue;
				}

				if (indent == 4 && pendingProjectName is not null && ConfigUtils.TryParseKeyValue(trimmed, out var key, out var value))
				{
					if (key == "path")
						pendingProjectPath = ConfigUtils.ResolvePath(dapsRoot, value);
					else if (key == "disabled")
						pendingProjectDisabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
				}
			}
		}

		FlushPendingProject();

		return new DapsConfig
		{
			DapsRootPath = dapsRoot,
			FullYamlPath = fullDapsYamlPath,
			Providers = providerDefinitions,
			Projects = projects,
		};
	}
}

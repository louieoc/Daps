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

		string? pendingProviderName = null;
		string? pendingProviderType = null;
		string? pendingProviderOpenRcPath = null;
		string? pendingProviderHostname = null;
		string? pendingProviderUser = null;
		var pendingProviderDisabled = false;

		void FlushPendingProvider()
		{
			if (pendingProviderName is not null)
			{
				if (pendingProviderType == "openstack" && pendingProviderOpenRcPath is not null)
				{
					providerDefinitions.Add(new OpenstackProviderDefinition
					{
						Name = pendingProviderName,
						OpenRcPath = pendingProviderOpenRcPath,
						Disabled = pendingProviderDisabled,
					});
				}
				else if (pendingProviderType == "generic-vps" && pendingProviderHostname is not null && pendingProviderUser is not null)
				{
					providerDefinitions.Add(new GenericVpsProviderDefinition
					{
						Name = pendingProviderName,
						Hostname = pendingProviderHostname,
						User = pendingProviderUser,
						Disabled = pendingProviderDisabled,
					});
				}
			}
			pendingProviderName = null;
			pendingProviderType = null;
			pendingProviderOpenRcPath = null;
			pendingProviderHostname = null;
			pendingProviderUser = null;
			pendingProviderDisabled = false;
		}

		string? pendingProjectName = null;
		string? pendingProjectPath = null;
		var pendingProjectDisabled = false;
		string? pendingProjectProvider = null;

		void FlushPendingProject()
		{
			if (pendingProjectName is not null && pendingProjectPath is not null)
			{
				projects.Add(new ProjectDefinition
				{
					Name = pendingProjectName,
					Path = pendingProjectPath,
					Disabled = pendingProjectDisabled,
					Provider = pendingProjectProvider,
				});
			}
			pendingProjectName = null;
			pendingProjectPath = null;
			pendingProjectDisabled = false;
			pendingProjectProvider = null;
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
				FlushPendingProvider();
				FlushPendingProject();

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
					FlushPendingProvider();
					pendingProviderName = providerName;
					pendingProviderType = providerType;
					continue;
				}

				if (indent == 4 && pendingProviderName is not null && ConfigUtils.TryParseKeyValue(trimmed, out var key, out var value))
				{
					if (key == "openrc")
						pendingProviderOpenRcPath = ConfigUtils.ResolvePath(dapsRoot, value);
					else if (key == "hostname")
						pendingProviderHostname = value;
					else if (key == "user")
						pendingProviderUser = value;
					else if (key == "disabled")
						pendingProviderDisabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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
					else if (key == "provider")
						pendingProjectProvider = value;
				}
			}
		}

		FlushPendingProvider();
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

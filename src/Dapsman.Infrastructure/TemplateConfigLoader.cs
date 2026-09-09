using System.Text.RegularExpressions;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class TemplateConfigLoader : ITemplateConfigLoader
{
	public TemplateConfig Load(string templateRoot)
	{
		var config = ParseTemplateYaml(templateRoot);
		string? placeholder = config?.PlaceholderText ?? DeriveProjectPlaceholder(templateRoot);

		if (placeholder is null)
		{
			throw new InvalidOperationException("A template placeholder value is required, either in docker files or in template.yaml.");
		}

		return new TemplateConfig
		{
			PlaceholderText = placeholder,
			ExcludedFromTransformation = config?.ExcludedFromTransformation ?? []
		};
	}

	public static TemplateConfig? ParseTemplateYaml(string templateRoot)
	{
		var templateYamlPath = Path.Combine(templateRoot, "template.yaml");
		if (!File.Exists(templateYamlPath))
		{
			return null;
		}

		var fullYamlPath = Path.GetFullPath(templateYamlPath);
		string[]? excludedPaths = null;
		string? placeholderText = null;

		foreach (var rawLine in File.ReadLines(fullYamlPath))
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
				if (!ConfigUtils.TryParseKeyValue(trimmed, out var setting, out var settingValue))
				{
					continue;
				}

				if (setting.Equals("exclude-from-transform", StringComparison.OrdinalIgnoreCase))
				{
					var values = settingValue
						.Replace("[", string.Empty)
						.Replace("]", string.Empty)
						.Split(',')
						.Select(v => v.Trim())
						// Trim first, then drop empties: a trailing comma or a "[ ]" leaves a
						// whitespace-only entry, which would survive a length check and then trim
						// down to "" -- an exclusion matching the template root, i.e. every file.
						.Where(v => v.Length > 0);

					excludedPaths = values.ToArray();
				}

				if (setting.Equals("placeholder", StringComparison.OrdinalIgnoreCase))
				{
					placeholderText = settingValue;
				}
			}
		}

		return new TemplateConfig
		{
			ExcludedFromTransformation = excludedPaths ?? [],
			PlaceholderText = placeholderText
		};
	}

	public static string? DeriveProjectPlaceholder(string templateRoot)
	{
		var dockerDir = Path.Combine(templateRoot, "_docker");
		if (!Directory.Exists(dockerDir)) return null;

		var info = new DirectoryInfo(dockerDir);

		var pattern = @"compose_daps_(.+?)\.dev\.yaml";
		foreach (var file in info.GetFiles())
		{
			var match = Regex.Match(file.Name, pattern);
			if (match.Success)
			{
				var placeholder = match.Groups[1].Value;
				return placeholder;
			}
		}
		return null;
	}
}

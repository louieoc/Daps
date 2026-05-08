using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public class HostingProviderResolver : IHostingProviderResolver
{
	public const string DefaultOpenStackUser = "ubuntu"; // probably this is because I always pick the ubuntu image

	private readonly DapsConfig _config;

	public HostingProviderResolver(DapsConfig config)
	{
		_config = config;
	}

	public HostingProvider Resolve(string? providerName)
	{
		var provider = FindProviderInConfig(_config, providerName);
		var os = provider as OpenstackProviderDefinition;
		if (os is null)
		{
			throw new InvalidOperationException($"Provider {providerName} is not an OpenStack provider. Only OpenStack is supported currently.");
		}

		ConfigUtils.RequireFile(os.OpenRcPath, $"OpenStack RC script not found for provider '{provider.Name}'.");

		var instanceVarsFile = $"openstack_{provider.Name}_instance_vars.sh";
		var instanceVarsPath = Path.Combine(_config.DapsRootPath, "hosting", instanceVarsFile);

		var vars = ParseExportVariables(instanceVarsPath);
		var remoteHost = RequireValue(vars, "OS_SERVER_IP", provider.Name);
		var remoteUser = GetValueOrDefault(vars, "OS_SERVER_USER", DefaultOpenStackUser);
		var keyName = GetValueOrDefault(vars, "OS_KEY_NAME", $"daps-key-{provider.Name}");

		return new HostingProvider
		{
			ConfigDefinition = os,
			RemoteHost = remoteHost,
			RemoteUser = remoteUser,
			KeyName = keyName,
			Options = new OpenStackProviderOptions { InstanceVarsFilePath = instanceVarsPath },
		};
	}

	private static ProviderDefinition FindProviderInConfig(DapsConfig config, string? providerName)
	{
		if (string.IsNullOrWhiteSpace(providerName))
		{
			return config.Providers.FirstOrDefault() ?? throw new InvalidOperationException($"No providers found in daps.yaml.");
		}

		return config.Providers.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException($"Provider '{providerName}' not found in daps.yaml.");
	}

	private static string RequireValue(Dictionary<string, string> vars, string key, string providerName)
	{
		if (!vars.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
			throw new InvalidOperationException($"{key} is not set in the {providerName} variables file.");

		return value;
	}

	private static string GetValueOrDefault(Dictionary<string, string> vars, string key, string fallback) =>
		vars.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

	private static Dictionary<string, string> ParseExportVariables(string scriptPath)
	{
		var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var rawLine in File.ReadLines(scriptPath))
		{
			var line = rawLine.Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || !line.StartsWith("export ")) continue;
			var expression = line["export ".Length..];
			var split = expression.Split('=', 2);
			if (split.Length != 2) continue;
			var key = split[0].Trim();
			var value = ConfigUtils.Unquote(ConfigUtils.StripTrailingComment(split[1]).Trim());
			vars[key] = value;
		}
		return vars;
	}
}

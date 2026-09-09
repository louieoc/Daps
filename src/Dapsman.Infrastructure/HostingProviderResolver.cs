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

	public HostingProvider Resolve(string? cliProviderName, string? projectProviderName)
	{
		var definition = FindProviderInConfig(_config, cliProviderName ?? projectProviderName);
		return ResolveFromDefinition(definition);
	}

	public HostingProvider ResolveExplicit(string? providerName)
	{
		var activeProviders = _config.Providers.Where(p => !p.Disabled).ToList();

		if (string.IsNullOrWhiteSpace(providerName))
		{
			if (activeProviders.Count == 1)
				return ResolveFromDefinition(activeProviders[0]);

			if (activeProviders.Count == 0)
				throw new InvalidOperationException("No providers found in daps.yaml.");

			var names = string.Join(", ", activeProviders.Select(p => p.Name));
			throw new InvalidOperationException(
				$"--provider is required when multiple providers are configured. Available: {names}");
		}

		return Resolve(providerName, null);
	}

	private HostingProvider ResolveFromDefinition(ProviderDefinition provider) => provider switch
	{
		OpenstackProviderDefinition os => ResolveOpenstack(os),
		GenericVpsProviderDefinition vps => ResolveGenericVps(vps),
		_ => throw new InvalidOperationException($"Provider '{provider.Name}' has an unsupported type."),
	};

	private HostingProvider ResolveOpenstack(OpenstackProviderDefinition os)
	{
		ConfigUtils.RequireFile(os.OpenRcPath, $"OpenStack RC script not found for provider '{os.Name}'.");

		var instanceVarsFile = $"openstack_{os.Name}_instance_vars.sh";
		var instanceVarsPath = Path.Combine(_config.DapsRootPath, "hosting", instanceVarsFile);

		var vars = ParseExportVariables(instanceVarsPath);
		var remoteHost = RequireValue(vars, "OS_SERVER_IP", os.Name);
		var remoteUser = GetValueOrDefault(vars, "OS_SERVER_USER", DefaultOpenStackUser);
		var keyName = GetValueOrDefault(vars, "OS_KEY_NAME", $"daps-key-{os.Name}");

		return new HostingProvider
		{
			ConfigDefinition = os,
			RemoteHost = remoteHost,
			RemoteUser = remoteUser,
			KeyName = keyName,
			Options = new OpenStackProviderOptions { InstanceVarsFilePath = instanceVarsPath },
		};
	}

	private static HostingProvider ResolveGenericVps(GenericVpsProviderDefinition vps) => new()
	{
		ConfigDefinition = vps,
		RemoteHost = vps.Hostname,
		RemoteUser = vps.User,
		KeyName = $"daps-key-{vps.Name}",
		Options = new GenericVpsProviderOptions(),
	};

	private static ProviderDefinition FindProviderInConfig(DapsConfig config, string? providerName)
	{
		var activeProviders = config.Providers.Where(p => !p.Disabled).ToList();

		if (string.IsNullOrWhiteSpace(providerName))
			return activeProviders.FirstOrDefault() ?? throw new InvalidOperationException("No providers found in daps.yaml.");

		var named = config.Providers.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException($"Provider '{providerName}' not found in daps.yaml.");

		if (named.Disabled)
			throw new InvalidOperationException($"Provider '{providerName}' is disabled in daps.yaml.");

		return named;
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

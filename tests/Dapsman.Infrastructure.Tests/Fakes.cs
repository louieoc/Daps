using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class FakeContainerManager(IReadOnlyList<string>? names) : IContainerManager
{
	public IReadOnlyList<string> GetContainerNames()
	{
		return names ?? new List<string>
		{
			"daps-toolkit-1",
			"daps-caddy-1"
		};
	}
}

public sealed class FakeHostingProviderResolver(HostingProvider provider) : IHostingProviderResolver
{
	/// <summary>The project provider passed to the two-argument overload, or null if it was never called.</summary>
	public string? ObservedProjectProviderName { get; private set; }

	/// <summary>True when resolution went through the overload that ignores the project's own provider.</summary>
	public bool ResolvedWithoutProjectProvider { get; private set; }

	public HostingProvider Resolve(string? providerName)
	{
		ResolvedWithoutProjectProvider = true;
		return provider;
	}

	public HostingProvider Resolve(string? cliProviderName, string? projectProviderName)
	{
		ObservedProjectProviderName = projectProviderName;
		return provider;
	}

	/// <summary>True when resolution went through the overload that refuses to guess a provider.</summary>
	public bool ResolvedExplicitly { get; private set; }

	public HostingProvider ResolveExplicit(string? providerName)
	{
		ResolvedExplicitly = true;
		return provider;
	}
}

public sealed class FakeHostPortManager : IHostPortManager
{
	public IReadOnlyList<HostPortBinding> GetBindings(string projectName, IEnumerable<string> devComposeFilePaths)
		=> [];

	public IReadOnlyList<HostPortConflict> FindConflicts(IReadOnlyList<HostPortBinding> bindings)
		=> [];

	public int FindNextAvailable(int preferredPort, IReadOnlyCollection<int> reservedPorts)
		=> preferredPort;
}

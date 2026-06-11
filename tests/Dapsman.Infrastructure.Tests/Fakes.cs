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
	public HostingProvider Resolve(string? providerName)
	{
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

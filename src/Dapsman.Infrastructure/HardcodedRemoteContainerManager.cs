using Dapsman.Application;

namespace Dapsman.Infrastructure;

public class HardcodedRemoteContainerManager : IContainerManager
{
	public IReadOnlyList<string> GetContainerNames()
	{
		return new List<string> { "daps-toolkit-1", "daps-caddy-1" };
	}
}
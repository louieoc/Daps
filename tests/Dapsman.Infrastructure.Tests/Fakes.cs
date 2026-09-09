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

/// <summary>
/// Records what was run and replays canned stdout. <paramref name="capturedOutput"/> maps the
/// DAPS_BACKUPS_PATH env var of a CaptureScript call to the stdout it should return, so a single
/// runner can answer differently per source directory.
/// </summary>
public sealed class FakeBashRunner(IReadOnlyDictionary<string, string>? capturedOutput = null) : IBashRunner
{
	public List<(string ScriptPath, string WorkingDirectory, string? Arguments, IReadOnlyDictionary<string, string>? Env)> ScriptRuns { get; } = [];

	public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
		=> ScriptRuns.Add((scriptPath, workingDirectory, arguments, env));

	public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
	{
	}

	public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
	{
		var backupsPath = env?.GetValueOrDefault("DAPS_BACKUPS_PATH") ?? "";
		return capturedOutput?.GetValueOrDefault(backupsPath) ?? "[]";
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

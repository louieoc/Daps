using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public class ToolkitResolver : IToolkitResolver
{
	private readonly DapsConfig _config;
	private readonly IContainerManager _containerManager;

	public ToolkitResolver(DapsConfig config, IContainerManager containerManager)
	{
		_config = config;
		_containerManager = containerManager;
	}

	public ToolkitDefinition Resolve()
	{
		var (containerName, isRunning) = ResolveContainerName();
		var sshPath = $"/root/.ssh";

		return new ToolkitDefinition
		{
			ContainerName = containerName,
			SshPath = sshPath,
			IsRunning = isRunning
		};
	}

	public string ToToolkitPath(string hostPath)
	{
		var dapsRoot = Path.GetFullPath(_config.DapsRootPath);
		var file = Path.GetFullPath(hostPath);

		var relative = Path.GetRelativePath(dapsRoot, file);
		if (relative.StartsWith("..", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Script path '{file}' is outside Daps root '{dapsRoot}'. Cannot map to toolkit container path.");
		}

		var unixRelative = relative.Replace('\\', '/');
		return $"/srv/daps/{unixRelative}";
	}


	// On a fresh workstation no toolkit container exists yet, and workflows that do not need it
	// (init, local build) must still run. So a missing container is not an error here: it resolves
	// to the conventional name with IsRunning false, and the caller decides whether to require it.
	private (string ContainerName, bool IsRunning) ResolveContainerName()
	{
		var expected = $"{_config.DapsComposeProjectName}-toolkit-1";
		var names = _containerManager.GetContainerNames();

		var preferred = names.FirstOrDefault(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase))
			?? names.FirstOrDefault(n => n.EndsWith("toolkit-1", StringComparison.OrdinalIgnoreCase))
			?? names.FirstOrDefault(n => n.Contains("toolkit", StringComparison.OrdinalIgnoreCase));

		return preferred is null ? (expected, false) : (preferred, true);
	}
}

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
		var containerName = ResolveContainerName();
		var sshPath = $"/root/.ssh";

		return new ToolkitDefinition
		{
			ContainerName = containerName,
			SshPath = sshPath
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
				$"Script path '{file}' is outside DAPS root '{dapsRoot}'. Cannot map to toolkit container path.");
		}

		var unixRelative = relative.Replace('\\', '/');
		return $"/srv/daps/{unixRelative}";
	}


	private string ResolveContainerName()
	{
		var names = _containerManager.GetContainerNames();

		var expected = $"{_config.DapsComposeProjectName}-toolkit-1";

		var preferred = names.FirstOrDefault(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase))
			?? names.FirstOrDefault(n => n.EndsWith("toolkit-1", StringComparison.OrdinalIgnoreCase))
			?? names.FirstOrDefault(n => n.Contains("toolkit", StringComparison.OrdinalIgnoreCase));

		if (preferred is null)
		{
			throw new InvalidOperationException("No running toolkit container found. Start local DAPS first (dapsman local build).");
		}

		return preferred;
	}
}

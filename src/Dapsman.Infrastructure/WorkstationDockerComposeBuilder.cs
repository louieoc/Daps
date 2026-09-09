using System.Text;

namespace Dapsman.Infrastructure;

public sealed class WorkstationDockerComposeBuilder
{
	private readonly IReadOnlyList<string> _composeFiles;

	public WorkstationDockerComposeBuilder(IReadOnlyList<string> composeFiles)
	{
		if (composeFiles.Count == 0)
		{
			throw new ArgumentException("At least one compose file is required.", nameof(composeFiles));
		}

		_composeFiles = composeFiles;
	}

	public string BuildDockerComposeUpCommand(bool buildImages)
	{
		var argsBuilder = BuildComposeFileArgs(_composeFiles);

		argsBuilder.Append(" up -d");
		if (buildImages)
		{
			argsBuilder.Append(" --build --renew-anon-volumes");
		}

		return argsBuilder.ToString();
	}

	public string BuildDockerComposeDownCommand(bool removeVolumes)
	{
		var argsBuilder = BuildComposeFileArgs(_composeFiles);

		argsBuilder.Append(" down");
		if (removeVolumes)
		{
			argsBuilder.Append(" -v");
		}

		return argsBuilder.ToString();
	}

	private static StringBuilder BuildComposeFileArgs(IReadOnlyList<string> composeFiles)
	{
		var argsBuilder = new StringBuilder("compose");
		foreach (var file in composeFiles)
		{
			argsBuilder.Append(" -f ");
			argsBuilder.Append('"');
			argsBuilder.Append(file);
			argsBuilder.Append('"');
		}

		return argsBuilder;
	}
}

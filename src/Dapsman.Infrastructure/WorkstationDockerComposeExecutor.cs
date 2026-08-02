using System.Diagnostics;
using System.Text;
using Dapsman.Application;

namespace Dapsman.Infrastructure;

public sealed class WorkstationDockerComposeExecutor : IDockerComposeExecutor
{
	public void RunComposeUp(IReadOnlyList<string> composeFiles, bool buildImages, string workingDirectory)
	{
		if (composeFiles.Count == 0)
		{
			throw new ArgumentException("At least one compose file is required.", nameof(composeFiles));
		}

		RunDocker(BuildDockerComposeCommand(composeFiles, buildImages), workingDirectory);
	}

	public void RunComposeDown(IReadOnlyList<string> composeFiles, bool removeVolumes, string workingDirectory)
	{
		if (composeFiles.Count == 0)
		{
			throw new ArgumentException("At least one compose file is required.", nameof(composeFiles));
		}

		RunDocker(BuildDockerComposeDownCommand(composeFiles, removeVolumes), workingDirectory);
	}

	private static void RunDocker(string arguments, string workingDirectory)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			Arguments = arguments,
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException("Failed to start docker compose process.");
		}

		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			var message = $"docker compose failed with exit code {process.ExitCode}.";
			if (!string.IsNullOrWhiteSpace(stderr))
			{
				message += $"{Environment.NewLine}{stderr.Trim()}";
			}
			else if (!string.IsNullOrWhiteSpace(stdout))
			{
				message += $"{Environment.NewLine}{stdout.Trim()}";
			}

			throw new InvalidOperationException(message);
		}
	}

	public static string BuildDockerComposeCommand(IReadOnlyList<string> composeFiles, bool buildImages)
	{
		var argsBuilder = BuildComposeFileArgs(composeFiles);

		argsBuilder.Append(" up -d");
		if (buildImages)
		{
			argsBuilder.Append(" --build --renew-anon-volumes");
		}

		return argsBuilder.ToString();
	}

	public static string BuildDockerComposeDownCommand(IReadOnlyList<string> composeFiles, bool removeVolumes)
	{
		var argsBuilder = BuildComposeFileArgs(composeFiles);

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

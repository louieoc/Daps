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

		var argsBuilder = BuildDockerComposeCommand(composeFiles, buildImages);

		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			Arguments = argsBuilder.ToString(),
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
		var argsBuilder = new StringBuilder("compose");
		foreach (var file in composeFiles)
		{
			argsBuilder.Append(" -f ");
			argsBuilder.Append('"');
			argsBuilder.Append(file);
			argsBuilder.Append('"');
		}

		argsBuilder.Append(" up -d");
		if (buildImages)
		{
			argsBuilder.Append(" --build --renew-anon-volumes");
		}

		return argsBuilder.ToString();
	}
}

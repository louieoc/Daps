using System.Diagnostics;
using Dapsman.Application;

namespace Dapsman.Infrastructure;

/// <summary>
/// Runs bash inside a running Docker container via `docker exec`.
/// Use interactive: false (default) for non-interactive scripted execution (uses -i).
/// Use interactive: true for commands that require user input such as password prompts (uses -it).
/// </summary>
public sealed class ContainerBashRunner(string containerName) : IBashRunner
{
	public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
	{
		var args = string.IsNullOrEmpty(arguments) ? $"\"{scriptPath}\"" : $"\"{scriptPath}\" {arguments}";
		RunInContainer($"bash {args}", workingDirectory, interactive, env);
	}

	public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
	{
		RunInContainer(shellExpression, workingDirectory, interactive);
	}

	private void RunInContainer(string command, string workingDirectory, bool interactive, IReadOnlyDictionary<string, string>? env = null)
	{
		var ttyFlag = interactive ? "-it" : "-i";
		var envFlags = env is null ? "" : " " + string.Join(" ", env.Select(kvp => $"-e \"{Escape(kvp.Key)}={Escape(kvp.Value)}\""));
		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			Arguments = $"exec {ttyFlag}{envFlags} {containerName} bash -lc \"{Escape(command)}\"",
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = false,
			RedirectStandardError = false,
			CreateNoWindow = false,
		};

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException($"Failed to start docker exec process for container '{containerName}'.");
		}

		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"Container bash process failed with exit code {process.ExitCode}.");
		}
	}

	public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
	{
		var envFlags = env is null ? "" : " " + string.Join(" ", env.Select(kvp => $"-e \"{Escape(kvp.Key)}={Escape(kvp.Value)}\""));
		var command = $"bash \"{Escape(scriptPath)}\"";
		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			Arguments = $"exec -i{envFlags} {containerName} bash -lc \"{Escape(command)}\"",
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = false,
			CreateNoWindow = false,
		};

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException($"Failed to start docker exec process for container '{containerName}'.");
		}

		var output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"Container bash process failed with exit code {process.ExitCode}.");
		}

		return output;
	}

	private static string Escape(string value) =>
		value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

using System.Diagnostics;
using Dapsman.Application;

namespace Dapsman.Infrastructure;

public sealed class WorkstationDockerExecutor : IDockerExecutor
{
	public void EnsureNetworkExists(string networkName, string workingDirectory)
	{
		// A non-zero exit from 'network inspect' means the network does not exist, which is an
		// expected outcome here rather than a failure — so it is inspected, not thrown on.
		if (RunDockerProcess($"network inspect {networkName}", workingDirectory).ExitCode == 0)
		{
			return;
		}

		RunDocker($"network create {networkName}", workingDirectory);
	}

	public void RunDocker(string arguments, string workingDirectory)
	{
		var result = RunDockerProcess(arguments, workingDirectory);
		if (result.ExitCode == 0)
		{
			return;
		}

		var message = $"'docker {arguments}' failed with exit code {result.ExitCode}.";
		if (!string.IsNullOrWhiteSpace(result.StandardError))
		{
			message += $"{Environment.NewLine}{result.StandardError.Trim()}";
		}
		else if (!string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			message += $"{Environment.NewLine}{result.StandardOutput.Trim()}";
		}

		throw new InvalidOperationException(message);
	}

	/// <summary>
	/// Runs docker and returns what it reported. A non-zero exit code is data, not an error — the
	/// caller decides whether it means failure. Only being unable to start docker at all throws.
	/// </summary>
	private static DockerProcessResult RunDockerProcess(string arguments, string workingDirectory)
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

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start docker process.");

		// Both streams must be drained before waiting, or a full pipe buffer deadlocks the process.
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();

		return new DockerProcessResult(process.ExitCode, stdout, stderr);
	}

	private readonly record struct DockerProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

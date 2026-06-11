using System.Text.RegularExpressions;
using Dapsman.Application;

namespace Dapsman.Infrastructure;

public sealed class HostPortManager : IHostPortManager
{
	// Matches "127.0.0.1:<hostPort>:<containerPort>" inside a quoted port binding string.
	private static readonly Regex PortPattern = new(
		@"127\.0\.0\.1:(\d+):(\d+)",
		RegexOptions.Compiled);

	// Ports that FindNextAvailable will never return. Projects may still use these explicitly.
	private static readonly HashSet<int> BuiltInSkipPorts = new(
	[
		3306,  // MySQL
		5432,  // PostgreSQL
		6379,  // Redis
		27017, // MongoDB
		5672,  // AMQP / RabbitMQ
		8161,  // ActiveMQ
		5900,  // VNC
	]);

	public IReadOnlyList<HostPortBinding> GetBindings(string projectName, IEnumerable<string> devComposeFilePaths)
	{
		var bindings = new List<HostPortBinding>();

		foreach (var filePath in devComposeFilePaths)
		{
			if (!File.Exists(filePath)) continue;
			var content = File.ReadAllText(filePath);
			foreach (Match match in PortPattern.Matches(content))
			{
				if (int.TryParse(match.Groups[1].Value, out var hostPort) &&
					int.TryParse(match.Groups[2].Value, out var containerPort))
				{
					bindings.Add(new HostPortBinding(projectName, filePath, hostPort, containerPort));
				}
			}
		}

		return bindings;
	}

	public IReadOnlyList<HostPortConflict> FindConflicts(IReadOnlyList<HostPortBinding> bindings)
	{
		return bindings
			.GroupBy(b => b.HostPort)
			.Where(g => g.Count() > 1)
			.Select(g => new HostPortConflict(g.Key, g.ToList()))
			.ToList();
	}

	public int FindNextAvailable(int preferredPort, IReadOnlyCollection<int> reservedPorts)
	{
		var port = preferredPort;
		while (true)
		{
			if (port >= 1024 && !BuiltInSkipPorts.Contains(port) && !reservedPorts.Contains(port))
				return port;
			port++;
		}
	}
}

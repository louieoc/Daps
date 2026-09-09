using System.Globalization;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

/// <summary>
/// Turns the tab-separated records emitted by scripts/remote-system-status.sh into a
/// <see cref="SystemStatus"/>. This is where every unit conversion and interpretation lives —
/// the remote script only collects.
///
/// Parsing is deliberately forgiving. Unknown record keys are ignored so an older Dapsman can
/// read a newer script's output, malformed records are dropped rather than thrown on, and a
/// record that never arrives leaves its field null so the printer can say "unavailable"
/// instead of the whole command failing over one unreadable value.
/// </summary>
public static class SystemStatusParser
{
	private const long KibiByte = 1024L;

	public static SystemStatus Parse(string output)
	{
		string? hostname = null;
		string? kernelArchitecture = null;
		string? dockerPlatform = null;
		int? cores = null;
		double? load1 = null, load5 = null, load15 = null;
		long? memTotal = null, memAvailable = null, swapTotal = null, swapFree = null;
		long? diskTotal = null, diskUsed = null, diskFree = null;
		double? uptimeSeconds = null;
		bool? rebootRequired = null;

		var containers = new List<ContainerStat>();
		var projects = new List<ProjectDiskUsage>();
		var dockerDisk = new List<DockerDiskUsage>();

		foreach (var line in output.Split('\n'))
		{
			var trimmed = line.TrimEnd('\r');
			if (string.IsNullOrWhiteSpace(trimmed))
				continue;

			var fields = trimmed.Split('\t');
			if (fields.Length < 2)
				continue;

			switch (fields[0])
			{
				case "hostname":
					hostname = fields[1];
					break;
				case "arch":
					kernelArchitecture = NullIfBlank(fields[1]);
					break;
				case "docker_platform":
					dockerPlatform = NullIfBlank(fields[1]);
					break;
				case "cores":
					cores = ParseInt(fields[1]);
					break;
				case "loadavg" when fields.Length >= 4:
					load1 = ParseDouble(fields[1]);
					load5 = ParseDouble(fields[2]);
					load15 = ParseDouble(fields[3]);
					break;
				case "mem_total_kb":
					memTotal = ParseLong(fields[1]) * KibiByte;
					break;
				case "mem_available_kb":
					memAvailable = ParseLong(fields[1]) * KibiByte;
					break;
				case "swap_total_kb":
					swapTotal = ParseLong(fields[1]) * KibiByte;
					break;
				case "swap_free_kb":
					swapFree = ParseLong(fields[1]) * KibiByte;
					break;
				case "disk_total_bytes":
					diskTotal = ParseLong(fields[1]);
					break;
				case "disk_used_bytes":
					diskUsed = ParseLong(fields[1]);
					break;
				case "disk_free_bytes":
					diskFree = ParseLong(fields[1]);
					break;
				case "uptime_seconds":
					uptimeSeconds = ParseDouble(fields[1]);
					break;
				case "reboot_required":
					rebootRequired = fields[1] == "1";
					break;
				case "container" when fields.Length >= 4:
					containers.Add(new ContainerStat(
						fields[1],
						ParsePercent(fields[2]),
						ParseSize(TakeUsedPortion(fields[3]))));
					break;
				case "project" when fields.Length >= 3:
					if (ParseLong(fields[2]) is { } bytes)
						projects.Add(new ProjectDiskUsage(fields[1], bytes));
					break;
				case "docker_disk" when fields.Length >= 4:
					dockerDisk.Add(new DockerDiskUsage(
						fields[1],
						ParseSize(fields[2]),
						ParseSize(fields[3])));
					break;
			}
		}

		return new SystemStatus
		{
			Hostname = hostname,
			KernelArchitecture = kernelArchitecture,
			DockerPlatform = dockerPlatform,
			CpuCores = cores,
			Load1 = load1,
			Load5 = load5,
			Load15 = load15,
			MemoryTotalBytes = memTotal,
			MemoryAvailableBytes = memAvailable,
			SwapTotalBytes = swapTotal,
			SwapFreeBytes = swapFree,
			DiskTotalBytes = diskTotal,
			DiskUsedBytes = diskUsed,
			DiskFreeBytes = diskFree,
			UptimeSeconds = uptimeSeconds,
			RebootRequired = rebootRequired,
			Containers = containers,
			Projects = projects,
			DockerDisk = dockerDisk,
		};
	}

	/// <summary>
	/// The remote emits an empty value rather than dropping the record when a collector returns
	/// nothing, so that an absent reading and a blank one land in the same place: null.
	/// </summary>
	private static string? NullIfBlank(string value)
	{
		var trimmed = value.Trim();
		return trimmed.Length == 0 ? null : trimmed;
	}

	/// <summary>`docker stats` reports memory as "190MiB / 2GiB"; only the first half is usage.</summary>
	private static string TakeUsedPortion(string memUsage)
	{
		var slash = memUsage.IndexOf('/');
		return slash >= 0 ? memUsage[..slash] : memUsage;
	}

	private static double? ParsePercent(string value) => ParseDouble(value.Trim().TrimEnd('%'));

	/// <summary>
	/// Parses the size strings Docker prints. It mixes conventions: `docker stats` uses binary
	/// units (MiB, GiB) while `docker system df` uses decimal ones (MB, GB), and `system df`
	/// appends a percentage to reclaimable ("1.1GB (17%)"). All of it lands here.
	/// </summary>
	private static long? ParseSize(string value)
	{
		var text = value.Trim();

		var paren = text.IndexOf('(');
		if (paren >= 0)
			text = text[..paren].Trim();

		if (text.Length == 0)
			return null;

		var digits = 0;
		while (digits < text.Length && (char.IsAsciiDigit(text[digits]) || text[digits] == '.'))
			digits++;

		if (digits == 0 || ParseDouble(text[..digits]) is not { } number)
			return null;

		var multiplier = text[digits..].Trim().ToUpperInvariant() switch
		{
			"" or "B" => 1L,
			"KB" => 1_000L,
			"MB" => 1_000_000L,
			"GB" => 1_000_000_000L,
			"TB" => 1_000_000_000_000L,
			"KIB" => KibiByte,
			"MIB" => KibiByte * KibiByte,
			"GIB" => KibiByte * KibiByte * KibiByte,
			"TIB" => KibiByte * KibiByte * KibiByte * KibiByte,
			_ => 0L,
		};

		return multiplier == 0 ? null : (long)(number * multiplier);
	}

	// The remote always emits invariant-formatted numbers ("0.31"). Parsing them under a
	// comma-decimal workstation locale would otherwise read that as thirty-one.
	private static int? ParseInt(string value) =>
		int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

	private static long? ParseLong(string value) =>
		long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

	private static double? ParseDouble(string value) =>
		double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
}

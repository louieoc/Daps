namespace Dapsman.Domain;

/// <summary>
/// A single resource reading taken from a remote host. All sizes are bytes; no formatting or
/// unit conversion happens here — the printer decides how a number is shown.
///
/// Every scalar is nullable because the remote script is deliberately fault-tolerant: a host
/// without Docker, or one where a collector failed, returns fewer records rather than an error,
/// and the reading should still print what it does know.
/// </summary>
public sealed class SystemStatus
{
	public string? Hostname { get; init; }

	/// <summary>Kernel architecture as `uname -m` reports it, e.g. "x86_64" or "aarch64".</summary>
	public string? KernelArchitecture { get; init; }

	/// <summary>
	/// The platform the Docker daemon runs images as, e.g. "linux/amd64". This is what the daemon
	/// will actually execute, which can differ from <see cref="KernelArchitecture"/> (a 64-bit
	/// kernel running a 32-bit userland reports aarch64 but runs arm images).
	///
	/// It is what `prod deploy` builds project images against, which is why it is collected in the
	/// summary rather than only under verbose: deploy needs this one field and nothing else the
	/// verbose pass would gather. Null when the host has no Docker.
	/// </summary>
	public string? DockerPlatform { get; init; }

	public int? CpuCores { get; init; }
	public double? Load1 { get; init; }
	public double? Load5 { get; init; }
	public double? Load15 { get; init; }
	public long? MemoryTotalBytes { get; init; }
	public long? MemoryAvailableBytes { get; init; }
	public long? SwapTotalBytes { get; init; }
	public long? SwapFreeBytes { get; init; }
	public long? DiskTotalBytes { get; init; }
	public long? DiskUsedBytes { get; init; }
	public long? DiskFreeBytes { get; init; }
	public double? UptimeSeconds { get; init; }
	public bool? RebootRequired { get; init; }

	public IReadOnlyList<ContainerStat> Containers { get; init; } = [];
	public IReadOnlyList<ProjectDiskUsage> Projects { get; init; } = [];
	public IReadOnlyList<DockerDiskUsage> DockerDisk { get; init; } = [];

	/// <summary>
	/// Derived from MemAvailable, not MemFree. MemFree excludes the page cache, which Linux fills
	/// with reclaimable data on any healthy long-running host — reporting it as "used" would show
	/// a well-behaved server as almost out of memory.
	/// </summary>
	public long? MemoryUsedBytes => MemoryTotalBytes - MemoryAvailableBytes;

	public long? SwapUsedBytes => SwapTotalBytes - SwapFreeBytes;

	/// <summary>1-minute load as a percentage of total core capacity; 100% means fully busy.</summary>
	public double? CpuLoadPercent => CpuCores is > 0 && Load1 is not null
		? Load1.Value / CpuCores.Value * 100.0
		: null;
}

public sealed record ContainerStat(
	string Name,
	double? CpuPercent,
	long? MemoryBytes
);

public sealed record ProjectDiskUsage(
	string Name,
	long Bytes
);

public sealed record DockerDiskUsage(
	string Type,
	long? SizeBytes,
	long? ReclaimableBytes
);

using System.Globalization;

namespace Dapsman.Infrastructure.Tests;

public sealed class SystemStatusParserTests
{
	private const string FullReading = """
		hostname	vps-12345678
		arch	x86_64
		docker_platform	linux/amd64
		cores	4
		loadavg	0.31	0.22	0.19
		mem_total_kb	2038528
		mem_available_kb	815344
		swap_total_kb	1048576
		swap_free_kb	733984
		disk_total_bytes	42949672960
		disk_used_bytes	15139536896
		disk_free_bytes	27810136064
		uptime_seconds	1052345.12
		reboot_required	1
		container	daps-caddy-1	0.20%	22.5MiB / 2GiB
		container	dapster-wp-1	1.42%	190MiB / 2GiB
		docker_disk	Images	6.2GB	1.1GB (17%)
		project	dapster-wp	1932735283
		""";

	[Fact]
	public void Parse_ReadsTheScalarFields()
	{
		var status = SystemStatusParser.Parse(FullReading);

		Assert.Equal("vps-12345678", status.Hostname);
		Assert.Equal("x86_64", status.KernelArchitecture);
		Assert.Equal("linux/amd64", status.DockerPlatform);
		Assert.Equal(4, status.CpuCores);
		Assert.Equal(0.31, status.Load1);
		Assert.Equal(42949672960L, status.DiskTotalBytes);
		Assert.True(status.RebootRequired);
	}

	[Fact]
	public void Parse_ConvertsMeminfoKilobytesToBytes()
	{
		var status = SystemStatusParser.Parse(FullReading);

		// /proc/meminfo counts in kibibytes despite labelling them kB.
		Assert.Equal(2038528L * 1024, status.MemoryTotalBytes);
	}

	[Fact]
	public void Parse_DerivesMemoryUsedFromAvailable_NotFree()
	{
		var status = SystemStatusParser.Parse(FullReading);

		// MemFree would exclude the page cache and report a healthy host as nearly full. The
		// script never sends MemFree, and used must come out of total minus available.
		Assert.Equal((2038528L - 815344L) * 1024, status.MemoryUsedBytes);
	}

	[Fact]
	public void Parse_ReadsDockerStatsMemoryUsage_IgnoringTheLimitAfterTheSlash()
	{
		var status = SystemStatusParser.Parse(FullReading);

		var caddy = status.Containers.Single(c => c.Name == "daps-caddy-1");
		Assert.Equal(0.20, caddy.CpuPercent);
		Assert.Equal((long)(22.5 * 1024 * 1024), caddy.MemoryBytes);
	}

	[Fact]
	public void Parse_ReadsDockerSystemDf_StrippingTheReclaimablePercentage()
	{
		var status = SystemStatusParser.Parse(FullReading);

		// `docker system df` appends a percentage to reclaimable, e.g. "1.1GB (17%)", and uses
		// decimal units where `docker stats` uses binary ones.
		var images = status.DockerDisk.Single();
		Assert.Equal(6_200_000_000L, images.SizeBytes);
		Assert.Equal(1_100_000_000L, images.ReclaimableBytes);
	}

	[Fact]
	public void Parse_UnknownRecordKey_IsIgnored()
	{
		// A newer script may report things this Dapsman does not know about; that must not be
		// the difference between a working command and a crash.
		var status = SystemStatusParser.Parse("cores\t2\nsomething_new\t42\n");

		Assert.Equal(2, status.CpuCores);
	}

	[Fact]
	public void Parse_MissingRecord_LeavesTheFieldNull()
	{
		// A collector that failed on the remote sends nothing rather than a bad value, and the
		// printer needs to be able to tell that apart from a real zero.
		var status = SystemStatusParser.Parse("cores\t2\n");

		Assert.Null(status.DiskTotalBytes);
		Assert.Null(status.RebootRequired);
	}

	[Fact]
	public void Parse_MalformedRecord_IsDroppedRatherThanThrowing()
	{
		var status = SystemStatusParser.Parse("cores\tnot-a-number\ncontainer\tonly-a-name\n\n");

		Assert.Null(status.CpuCores);
		Assert.Empty(status.Containers);
	}

	[Fact]
	public void Parse_UnderACommaDecimalLocale_StillReadsTheLoadAverage()
	{
		var original = Thread.CurrentThread.CurrentCulture;
		try
		{
			// The remote always emits invariant-formatted numbers. Parsing "0.31" under de-DE
			// without an explicit culture would read it as 31.
			Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

			var status = SystemStatusParser.Parse(FullReading);

			Assert.Equal(0.31, status.Load1);
		}
		finally
		{
			Thread.CurrentThread.CurrentCulture = original;
		}
	}

	[Fact]
	public void Parse_ComputesCpuLoadAsAPercentageOfTotalCores()
	{
		var status = SystemStatusParser.Parse(FullReading);

		// 0.31 of 4 cores is under 8% busy; dividing by cores is what makes the number
		// comparable across differently sized VMs.
		Assert.Equal(7.75, status.CpuLoadPercent!.Value, 2);
	}

	[Fact]
	public void Parse_LeavesDockerPlatformNull_WhenTheHostReportedNothingForIt()
	{
		// A host without Docker still emits the record, with an empty value: the script runs
		// `docker version` under `|| true` so one missing tool cannot fail the whole reading.
		var reading = """
			arch	aarch64
			docker_platform	
			""";

		var status = SystemStatusParser.Parse(reading);

		Assert.Equal("aarch64", status.KernelArchitecture);
		Assert.Null(status.DockerPlatform);
	}

	[Fact]
	public void Parse_KeepsDockerPlatformSeparateFromKernelArchitecture()
	{
		// The two can legitimately disagree, and the docker value is the one a build must target,
		// so neither may be derived from or overwritten by the other.
		var reading = """
			arch	aarch64
			docker_platform	linux/arm
			""";

		var status = SystemStatusParser.Parse(reading);

		Assert.Equal("aarch64", status.KernelArchitecture);
		Assert.Equal("linux/arm", status.DockerPlatform);
	}
}

using Dapsman.Application;
using Dapsman.Infrastructure;

namespace Dapsman.Infrastructure.Tests;

public sealed class HostPortManagerTests
{
	private readonly HostPortManager _sut = new();

	// --- GetBindings ---

	[Fact]
	public void GetBindings_ExtractsPortsFromDevComposeFile()
	{
		var file = WriteTempFile("ports:\n  - \"127.0.0.1:8080:80\"\n");
		var bindings = _sut.GetBindings("myproject", [file]);
		Assert.Single(bindings);
		Assert.Equal(8080, bindings[0].HostPort);
		Assert.Equal(80, bindings[0].ContainerPort);
		Assert.Equal("myproject", bindings[0].ProjectName);
	}

	[Fact]
	public void GetBindings_MultipleServicesInOneFile()
	{
		var file = WriteTempFile("""
			services:
			  wordpress:
			    ports:
			      - "127.0.0.1:8080:80"
			  db:
			    ports:
			      - "127.0.0.1:3306:3306"
			  phpmyadmin:
			    ports:
			      - "127.0.0.1:8082:80"
			""");

		var bindings = _sut.GetBindings("mywpsite", [file]);
		Assert.Equal(3, bindings.Count);
		Assert.Contains(bindings, b => b.HostPort == 8080);
		Assert.Contains(bindings, b => b.HostPort == 3306);
		Assert.Contains(bindings, b => b.HostPort == 8082);
	}

	[Fact]
	public void GetBindings_ReservedPortAllowed()
	{
		// Projects may intentionally bind to reserved ports (e.g. 3306 for MySQL dev).
		// GetBindings should return them; reserved ports only gate FindNextAvailable.
		var file = WriteTempFile("ports:\n  - \"127.0.0.1:3306:3306\"\n");
		var bindings = _sut.GetBindings("myproject", [file]);
		Assert.Single(bindings);
		Assert.Equal(3306, bindings[0].HostPort);
	}

	[Fact]
	public void GetBindings_IgnoresNonLocalBindings()
	{
		// Port bindings without 127.0.0.1 are not dev-local bindings; skip them.
		var file = WriteTempFile("ports:\n  - \"8080:80\"\n");
		var bindings = _sut.GetBindings("myproject", [file]);
		Assert.Empty(bindings);
	}

	[Fact]
	public void GetBindings_MissingFileReturnsEmpty()
	{
		var bindings = _sut.GetBindings("myproject", ["/nonexistent/path/file.yaml"]);
		Assert.Empty(bindings);
	}

	// --- FindConflicts ---

	[Fact]
	public void FindConflicts_NoConflicts_ReturnsEmpty()
	{
		var bindings = new List<HostPortBinding>
		{
			new("proj-a", "a.yaml", 8080, 80),
			new("proj-b", "b.yaml", 8081, 80),
		};
		var conflicts = _sut.FindConflicts(bindings);
		Assert.Empty(conflicts);
	}

	[Fact]
	public void FindConflicts_DetectsCollision()
	{
		var bindings = new List<HostPortBinding>
		{
			new("proj-a", "a.yaml", 8080, 80),
			new("proj-b", "b.yaml", 8080, 80),
		};
		var conflicts = _sut.FindConflicts(bindings);
		Assert.Single(conflicts);
		Assert.Equal(8080, conflicts[0].Port);
		Assert.Equal(2, conflicts[0].Bindings.Count);
	}

	// --- FindNextAvailable ---

	[Fact]
	public void FindNextAvailable_ReturnsPreferredIfFree()
	{
		var result = _sut.FindNextAvailable(8083, reservedPorts: []);
		Assert.Equal(8083, result);
	}

	[Fact]
	public void FindNextAvailable_SkipsReservedPorts()
	{
		var result = _sut.FindNextAvailable(8080, reservedPorts: [8080, 8081, 8082]);
		Assert.Equal(8083, result);
	}

	[Fact]
	public void FindNextAvailable_SkipsWellKnownPorts()
	{
		// Ports below 1024 must be skipped even if not in the reserved set
		var result = _sut.FindNextAvailable(80, reservedPorts: []);
		Assert.True(result >= 1024, $"Expected port >= 1024, got {result}");
	}

	[Fact]
	public void FindNextAvailable_SkipsCommonServicePorts()
	{
		// 3306 is a built-in skip port; next available should jump past it
		var result = _sut.FindNextAvailable(3306, reservedPorts: []);
		Assert.NotEqual(3306, result);
		Assert.True(result > 3306);
	}

	// --- Helpers ---

	private static string WriteTempFile(string content)
	{
		var path = Path.GetTempFileName();
		File.WriteAllText(path, content);
		return path;
	}
}

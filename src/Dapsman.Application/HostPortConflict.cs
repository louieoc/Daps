namespace Dapsman.Application;

public sealed record HostPortConflict(
	int Port,
	IReadOnlyList<HostPortBinding> Bindings);

namespace Dapsman.Application;

public sealed class PlanResult<T>
{
	public bool IsSupported { get; init; }
	public T? Plan { get; init; }
	public IReadOnlyList<string> Warnings { get; init; } = [];

	public static PlanResult<T> Supported(T plan, IReadOnlyList<string>? warnings = null) =>
		new() { IsSupported = true, Plan = plan, Warnings = warnings ?? [] };

	public static PlanResult<T> NotSupported(params string[] reasons) =>
		new() { IsSupported = false, Warnings = [..reasons] };
}

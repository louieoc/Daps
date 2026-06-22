namespace Dapsman.Domain;

/// <summary>
/// The hosting provider as parsed from the daps.yaml file
/// </summary>
public abstract class ProviderDefinition
{
	public required string Name { get; init; }

	/// <summary>
	/// When true, the provider is excluded from resolution. Same semantics as Disabled on ProjectDefinition.
	/// </summary>
	public bool Disabled { get; init; }
}

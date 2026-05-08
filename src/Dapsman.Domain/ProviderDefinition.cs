namespace Dapsman.Domain;

/// <summary>
/// The hosting provider as parsed from the daps.yaml file
/// </summary>
public abstract class ProviderDefinition
{
	public required string Name { get; init; }
}

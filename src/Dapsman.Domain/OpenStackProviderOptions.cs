namespace Dapsman.Domain;

public class OpenStackProviderOptions : ProviderOptions
{
	public required string InstanceVarsFilePath { get; init; }
}

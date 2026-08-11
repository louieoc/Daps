namespace Dapsman.Domain;

/// <summary>
/// The hosting provider as parsed from the daps.yaml file
/// </summary>
public abstract class ProviderDefinition
{
	/// <summary>
	/// Local time (24h HH:MM) at which a provisioned host reboots when an unattended
	/// security upgrade requires one. Ubuntu installs security updates unattended but
	/// leaves Automatic-Reboot off, so without this a new kernel is installed and never
	/// booted into. Applied by `prod provision`.
	///
	/// A const for now. It is here rather than in Infrastructure so that a future
	/// `automatic-reboot-time:` key in daps.yaml can override it per provider.
	/// </summary>
	public const string AutomaticRebootTime = "04:00";

	public required string Name { get; init; }

	/// <summary>
	/// When true, the provider is excluded from resolution. Same semantics as Disabled on ProjectDefinition.
	/// </summary>
	public bool Disabled { get; init; }
}

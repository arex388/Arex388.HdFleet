namespace Arex388.HdFleet;

/// <summary>
/// The <see cref="IHdFleetClient"/>'s configuration options.
/// </summary>
public sealed class HdFleetClientOptions {
	//	The SHA-256 fingerprint used by anything that leaves the process —
	//	computed once per options instance, never per request.
	internal string Fingerprint => field ??= KeyFingerprint.Compute(Key);

	/// <summary>
	/// The account's API key.
	/// </summary>
	public required string Key { get; init; }
}

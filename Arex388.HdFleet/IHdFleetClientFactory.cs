namespace Arex388.HdFleet;

/// <summary>
/// HD Fleet API client factory for interacting with multiple accounts.
/// </summary>
public interface IHdFleetClientFactory {
	/// <summary>
	/// Create a client for an account. Clients are cached per API key and effective
	/// <see cref="HdFleetClientOptions.Timeout"/> — repeated calls with the same key and
	/// timeout return the same instance.
	/// </summary>
	/// <param name="options">The client's configuration options.</param>
	/// <returns>The <see cref="IHdFleetClient"/> client.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="HdFleetClientOptions.Timeout"/> is zero, negative, or longer than <see cref="int.MaxValue"/> milliseconds.</exception>
	IHdFleetClient CreateClient(
		HdFleetClientOptions options);
}
